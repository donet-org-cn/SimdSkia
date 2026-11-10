using SimdSkia;
using System.Globalization;
using System.Text;

namespace SkiaSharp;

public partial class SKPath
{
    public void ArcTo(SKPoint r,float xAxisRotate,SKPathArcSize largeArc,SKPathDirection sweep,SKPoint xy)=>ArcTo(r.X,r.Y,xAxisRotate,largeArc,sweep,xy.X,xy.Y);
    public void RArcTo(SKPoint r,float xAxisRotate,SKPathArcSize largeArc,SKPathDirection sweep,SKPoint xy)=>RArcTo(r.X,r.Y,xAxisRotate,largeArc,sweep,xy.X,xy.Y);
    public void RArcTo(float rx,float ry,float xAxisRotate,SKPathArcSize largeArc,SKPathDirection sweep,float x,float y)
    {ThrowIfDisposed();var p=CurrentPoint();ArcTo(rx,ry,xAxisRotate,largeArc,sweep,p.X+x,p.Y+y);}
    public void ArcTo(float rx,float ry,float xAxisRotate,SKPathArcSize largeArc,SKPathDirection sweep,float x,float y)
    {
        ThrowIfDisposed();if(!float.IsFinite(rx)||!float.IsFinite(ry)||!float.IsFinite(xAxisRotate)||!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentException("Arc coordinates must be finite.");
        if(!Enum.IsDefined(largeArc)||!Enum.IsDefined(sweep))throw new ArgumentOutOfRangeException(nameof(largeArc));
        // Same single-precision endpoint conversion as Skia m119 (SkPath.cpp, BSD-3).
        // In particular, radii correction rounding affects the conic weight, so a double
        // precision SVG formula is visually close but is not the baseline geometry.
        var start=CurrentPoint();rx=MathF.Abs(rx);ry=MathF.Abs(ry);
        if(rx==0||ry==0||start==new PointF(x,y)){LineTo(x,y);return;}
        float radians=xAxisRotate*(MathF.PI/180),c=MathF.Cos(radians),s=MathF.Sin(radians);
        if(MathF.Abs(c)<1f/4096)c=0;if(MathF.Abs(s)<1f/4096)s=0;
        float dx=(start.X-x)*.5f,dy=(start.Y-y)*.5f;
        float mx=c*dx+s*dy,my=-s*dx+c*dy;
        float scale=mx*mx/(rx*rx)+my*my/(ry*ry);
        if(scale>1){scale=MathF.Sqrt(scale);rx*=scale;ry*=scale;}
        float ix=1/rx,iy=1/ry;
        PointF Unit(float px,float py)=>new((ix*c)*px+(ix*s)*py,(-iy*s)*px+(iy*c)*py);
        var u=Unit(start.X,start.Y);var v=Unit(x,y);
        dx=v.X-u.X;dy=v.Y-u.Y;float d=dx*dx+dy*dy;
        float factor=MathF.Sqrt(MathF.Max(1/d-.25f,0));
        if((sweep==SKPathDirection.CounterClockwise)!=(largeArc==SKPathArcSize.Large))factor=-factor;
        dx*=factor;dy*=factor;
        float cx=(u.X+v.X)*.5f-dy,cy=(u.Y+v.Y)*.5f+dx;
        float first=MathF.Atan2(u.Y-cy,u.X-cx),last=MathF.Atan2(v.Y-cy,v.X-cx),delta=last-first;
        if(delta<0&&sweep==SKPathDirection.Clockwise)delta+=MathF.PI*2;
        else if(delta>0&&sweep!=SKPathDirection.Clockwise)delta-=MathF.PI*2;
        if(MathF.Abs(delta)<MathF.PI/1000000){LineTo(x,y);return;}
        int count=(int)MathF.Ceiling(MathF.Abs(delta/(2*MathF.PI/3)));
        float step=delta/count,t=MathF.Tan(.5f*step),weight=MathF.Sqrt(.5f+MathF.Cos(step)*.5f);
        if(!float.IsFinite(t))throw new ArgumentException("Arc exceeds finite geometry range.");
        bool integer=MathF.Abs(MathF.PI/2-MathF.Abs(step))<1f/4096&&rx==MathF.Floor(rx)&&ry==MathF.Floor(ry)&&x==MathF.Floor(x)&&y==MathF.Floor(y);
        PointF Map(float px,float py)
        {
            float a=(c*rx)*px+(-s*ry)*py,b=(s*rx)*px+(c*ry)*py;
            return integer?new(MathF.Floor(a+.5f),MathF.Floor(b+.5f)):new(a,b);
        }
        Span<PathSegment> curves=stackalloc PathSegment[4];
        for(int i=0;i<count;i++)
        {
            float end=first+step,sn=MathF.Sin(end),cs=MathF.Cos(end);
            if(MathF.Abs(sn)<1f/4096)sn=0;if(MathF.Abs(cs)<1f/4096)cs=0;
            var point=Map(cs+cx,sn+cy);var control=Map(cs+cx+t*sn,sn+cy-t*cs);
            if(i==count-1)point=new(x,y);
            if(!float.IsFinite(control.X)||!float.IsFinite(control.Y)||!float.IsFinite(point.X)||!float.IsFinite(point.Y))throw new ArgumentException("Arc exceeds finite geometry range.");
            curves[i]=PathSegment.Conic(control,point,weight);first=end;
        }
        for(int i=0;i<count;i++)Add(curves[i]);
    }

    public static SKPath ParseSvgPathData(string svgPath)
    {
        ArgumentNullException.ThrowIfNull(svgPath);var path=new SKPath();
        try{var parser=new SvgParser(svgPath);parser.Parse(path);return path;}catch(Exception e)when(e is FormatException or ArgumentException or OverflowException){path.Dispose();return null!;}
    }
    private ref struct SvgParser(ReadOnlySpan<char> text)
    {
        private readonly ReadOnlySpan<char> input=text;
        private int at;
        private void Space(){while(at<input.Length&&(char.IsWhiteSpace(input[at])||input[at]==','))at++;}
        private float Number()
        {
            Space();int first=at;if(at<input.Length&&input[at] is '+' or '-')at++;
            while(at<input.Length&&char.IsAsciiDigit(input[at]))at++;
            if(at<input.Length&&input[at]=='.'){at++;while(at<input.Length&&char.IsAsciiDigit(input[at]))at++;}
            if(at<input.Length&&input[at] is 'e' or 'E'){at++;if(at<input.Length&&input[at] is '+' or '-')at++;while(at<input.Length&&char.IsAsciiDigit(input[at]))at++;}
            if(at==first||!float.TryParse(input[first..at],NumberStyles.Float,CultureInfo.InvariantCulture,out var result)||!float.IsFinite(result))throw new FormatException("Invalid SVG number.");return result;
        }
        private bool Flag(){Space();if(at==input.Length||input[at] is not ('0' or '1'))throw new FormatException("Invalid arc flag.");return input[at++]=='1';}
        private SKPoint ReadPoint(bool relative,float x,float y){float a=Number(),b=Number();return relative?new(a+x,b+y):new(a,b);}
        internal void Parse(SKPath path)
        {
            char command='\0',previous='\0';SKPoint control=default;
            while(true)
            {
                Space();if(at==input.Length)return;if(char.IsAsciiLetter(input[at]))command=input[at++];else if(command=='\0')throw new FormatException("Missing SVG command.");
                bool relative=char.IsLower(command);char op=char.ToUpperInvariant(command);var p=path.CurrentPoint();float x=p.X,y=p.Y;
                switch(op)
                {
                    case 'M':path.MoveTo(ReadPoint(relative,x,y));command=relative?'l':'L';break;
                    case 'L':path.LineTo(ReadPoint(relative,x,y));break;
                    case 'H':path.LineTo(Number()+(relative?x:0),y);break;
                    case 'V':path.LineTo(x,Number()+(relative?y:0));break;
                    case 'C':{var a=ReadPoint(relative,x,y);var b=ReadPoint(relative,x,y);var end=ReadPoint(relative,x,y);path.CubicTo(a,b,end);control=b;break;}
                    case 'S':{var a=previous is 'C' or 'S'?new SKPoint(2*x-control.X,2*y-control.Y):new SKPoint(x,y);var b=ReadPoint(relative,x,y);var end=ReadPoint(relative,x,y);path.CubicTo(a,b,end);control=b;break;}
                    case 'Q':{var a=ReadPoint(relative,x,y);var end=ReadPoint(relative,x,y);path.QuadTo(a,end);control=a;break;}
                    case 'T':{var a=previous is 'Q' or 'T'?new SKPoint(2*x-control.X,2*y-control.Y):new SKPoint(x,y);path.QuadTo(a,ReadPoint(relative,x,y));control=a;break;}
                    case 'A':{float rx=Number(),ry=Number(),angle=Number();bool large=Flag(),clockwise=Flag();var end=ReadPoint(relative,x,y);path.ArcTo(rx,ry,angle,large?SKPathArcSize.Large:SKPathArcSize.Small,clockwise?SKPathDirection.Clockwise:SKPathDirection.CounterClockwise,end.X,end.Y);break;}
                    case 'Z':path.Close();command='\0';break;
                    default:throw new FormatException("Unknown SVG command.");
                }
                previous=op;
            }
        }
    }
    public string ToSvgPathData()
    {
        ThrowIfDisposed();var text=new StringBuilder();PointF current=default;
        void Point(PointF p){text.Append(p.X.ToString("G9",CultureInfo.InvariantCulture));text.Append(' ');text.Append(p.Y.ToString("G9",CultureInfo.InvariantCulture));}
        foreach(var s in segments)
        {
            switch(s.Verb)
            {
                case PathVerb.Move:text.Append('M');Point(s.A);current=s.A;break;
                case PathVerb.Line:text.Append('L');Point(s.A);current=s.A;break;
                case PathVerb.Quadratic:text.Append('Q');Point(s.A);text.Append(' ');Point(s.B);current=s.B;break;
                case PathVerb.Cubic:text.Append('C');Point(s.A);text.Append(' ');Point(s.B);text.Append(' ');Point(s.C);current=s.C;break;
                case PathVerb.Conic:
                    var quads=ConvertConicToQuads(new(current.X,current.Y),new(s.A.X,s.A.Y),new(s.B.X,s.B.Y),s.ConicWeight,4);
                    for(int i=1;i+1<quads.Length;i+=2){text.Append('Q');Point(new(quads[i].X,quads[i].Y));text.Append(' ');Point(new(quads[i+1].X,quads[i+1].Y));}current=s.B;break;
                case PathVerb.Close:text.Append('Z');break;
            }
        }
        return text.ToString();
    }
}
