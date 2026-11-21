using Avalonia;
using Avalonia.Media;
using System.Runtime.CompilerServices;

namespace SimdSkia.Avalonia;

internal static class BrushFactory
{
    private sealed class GradientCache {internal Gradient? Value;}
    private static readonly ConditionalWeakTable<IGradientBrush,GradientCache> gradients=new();
    internal static Paint Create(IBrush brush,Rect bounds,bool antialias,CompositeMode composite=CompositeMode.SourceOver)
    {
        byte opacity=Opacity(brush.Opacity);
        if(brush is ISolidColorBrush solid){var color=ConvertTo.Color(solid.Color);return new(color with {A=PixelBuffer.Multiply(color.A,opacity)},composite,antialias);}
        if(brush is IGradientBrush gradient)
        {
            var entry=gradients.GetValue(gradient,static _=>new());
            lock(entry)
            {
                if(entry.Value is null||!entry.Value.Matches(gradient,bounds))entry.Value=new Gradient(gradient,bounds);
                return new(new(255,255,255,opacity),composite,antialias,entry.Value);
            }
        }
        throw new NotSupportedException($"Brush {brush.GetType().FullName} is not implemented (image/visual brushes need a tile pipeline).");
    }
    internal static byte Opacity(double value)
    {if(!double.IsFinite(value)||value<0||value>1)throw new ArgumentOutOfRangeException(nameof(value));return(byte)Math.Round(value*255);}
    private sealed class Gradient:IColorShader,INativeRadialGradient,IComputeGradientShader
    {
        private readonly (double Offset,Rgba32 Color)[] stops;
        private readonly (double Offset,Rgba32 Color)[] originalStops;
        private readonly Rect boundsKey;
        private readonly Matrix transformKey;
        private readonly RelativePoint transformOriginKey;
        private readonly GradientSpreadMethod spread;
        private readonly Matrix inverse;
        private readonly Point origin,center,end;
        private readonly double rx,ry,angle;
        private readonly int kind;
        private LinearGradientDescription? linearDescription;
        private RadialGradientDescription? radialDescription;
        private ComputeGradient? computeDescription;
        public ComputeGradient? ComputeGradient
        {
            get
            {
                // Float32 can choose the wrong side of a discontinuous seam.
                // Keep repeat radii and hard stops on the exact managed sampler.
                if(kind==0||stops.Length==0||kind==1&&spread==GradientSpreadMethod.Repeat||
                    stops[0].Offset<0||stops[^1].Offset>1)return null;
                if(computeDescription is not null)return computeDescription;
                var centerPoint=new PointF((float)center.X,(float)center.Y);
                var transform=ConvertTo.Matrix(inverse);
                if(!float.IsFinite(centerPoint.X)||!float.IsFinite(centerPoint.Y)||!transform.IsFinite||!transform.IsAffine)return null;
                var originPoint=centerPoint;
                PointF radius=default;
                if(kind==1)
                {
                    radius=new((float)rx,(float)ry);
                    if(radius.X<.001f||radius.Y<.001f||!float.IsFinite(radius.X)||!float.IsFinite(radius.Y))return null;
                    originPoint=new((float)origin.X,(float)origin.Y);
                    double x=((double)originPoint.X-centerPoint.X)/radius.X,y=((double)originPoint.Y-centerPoint.Y)/radius.Y;
                    // Allow enough headroom for the float32 dot product before
                    // dividing by the term derived from the focal distance.
                    if(!(x*x+y*y<.999999))return null;
                }
                else if(!(Math.Abs(angle)<1_000_000))return null;
                for(int i=1;i<stops.Length;i++)
                    if((float)stops[i].Offset<=(float)stops[i-1].Offset)return null;
                // Preserve the conventional shader behavior at the angular seam
                // by using the exact CPU sampler for discontinuous conic endpoints.
                if(kind==2&&stops[0].Color!=stops[^1].Color)return null;
                float turns=kind==2?(float)(angle%(2*Math.PI)/(2*Math.PI)):0;
                return computeDescription=new(kind==1?ComputeGradientKind.Radial:ComputeGradientKind.Conic,
                    centerPoint,originPoint,radius,turns,transform,ExtendMode,
                    stops.Select(s=>new LinearColorStop((float)s.Offset,s.Color)).ToArray());
            }
        }
        public RadialGradientDescription? Direct2DGradient
        {
            get
            {
                // Wrapped radial seams can land on a different side of a stop
                // on native Direct2D. Keep the managed repeat contract.
                if(kind!=1||!inverse.IsIdentity||spread==GradientSpreadMethod.Repeat||stops.Length==0||
                    stops[0].Offset<0||stops[^1].Offset>1)return null;
                if(radialDescription is not null)return radialDescription;
                float x=(float)center.X,y=(float)center.Y,dx=(float)(origin.X-center.X),dy=(float)(origin.Y-center.Y);
                float radiusX=(float)rx,radiusY=(float)ry;
                if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(radiusX)||!float.IsFinite(radiusY)||radiusX<=0||radiusY<=0)return null;
                double px=dx/(double)radiusX,py=dy/(double)radiusY;
                if(!(px*px+py*py<1))return null;
                for(int i=1;i<stops.Length;i++)
                    if((float)stops[i].Offset<=(float)stops[i-1].Offset)return null;
                var values=new LinearColorStop[stops.Length];
                for(int i=0;i<values.Length;i++)
                    values[i]=new((float)stops[i].Offset,stops[i].Color);
                return radialDescription=new(new(x,y),new(dx,dy),radiusX,radiusY,values,ExtendMode);
            }
        }
        private GradientExtendMode ExtendMode=>spread switch
        {
            GradientSpreadMethod.Pad=>GradientExtendMode.Clamp,
            GradientSpreadMethod.Repeat=>GradientExtendMode.Repeat,
            GradientSpreadMethod.Reflect=>GradientExtendMode.Mirror,
            _=>throw new NotSupportedException("Unknown gradient spread.")
        };
        public bool TryGetLinearGradient(out LinearGradientDescription? gradient)
        {
            gradient=null;if(kind!=0||stops.Length==0)return false;
            gradient=linearDescription??=new(new((float)origin.X,(float)origin.Y),new((float)end.X,(float)end.Y),
                stops.Select(s=>new LinearColorStop((float)s.Offset,s.Color)),ExtendMode,ConvertTo.Matrix(inverse));
            return true;
        }
        public bool IsHorizontal=>kind==0&&origin.Y==end.Y&&inverse.M21==0;
        public bool IsOpaque=>stops.Length>0&&stops.All(s=>s.Color.A==255);
        internal Gradient(IGradientBrush brush,Rect bounds)
        {
            boundsKey=bounds;transformKey=brush.Transform?.Value??Matrix.Identity;transformOriginKey=brush.TransformOrigin;
            originalStops=brush.GradientStops.Select(s=>(s.Offset,ConvertTo.Color(s.Color))).ToArray();
            stops=originalStops.OrderBy(s=>s.Offset).ToArray();spread=brush.SpreadMethod;
            if(stops.Any(s=>!double.IsFinite(s.Offset)))throw new ArgumentException("Gradient offsets must be finite.");
            Point Position(RelativePoint p)=>p.ToPixels(bounds.Size)+new Vector(bounds.X,bounds.Y);
            var transform=brush.Transform?.Value??Matrix.Identity;var o=Position(brush.TransformOrigin);
            var matrix=Matrix.CreateTranslation(-o.X,-o.Y)*transform*Matrix.CreateTranslation(o.X,o.Y);
            if(!matrix.TryInvert(out inverse))throw new NotSupportedException("Singular brush transforms are not supported.");
            if(brush is ILinearGradientBrush linear){kind=0;origin=Position(linear.StartPoint);end=Position(linear.EndPoint);}
            else if(brush is IRadialGradientBrush radial){kind=1;center=Position(radial.Center);origin=Position(radial.GradientOrigin);rx=radial.RadiusX.ToValue(bounds.Width);ry=radial.RadiusY.ToValue(bounds.Height);}
            else if(brush is IConicGradientBrush conic){kind=2;center=Position(conic.Center);angle=conic.Angle*Math.PI/180;}
            else throw new NotSupportedException("Unknown gradient type.");
        }
        internal bool Matches(IGradientBrush brush,Rect bounds)
        {
            if(bounds!=boundsKey||brush.SpreadMethod!=spread||(brush.Transform?.Value??Matrix.Identity)!=transformKey||brush.TransformOrigin!=transformOriginKey)return false;
            var values=brush.GradientStops;if(values.Count!=originalStops.Length)return false;
            for(int i=0;i<values.Count;i++)if(values[i].Offset!=originalStops[i].Offset||ConvertTo.Color(values[i].Color)!=originalStops[i].Color)return false;
            Point Position(RelativePoint p)=>p.ToPixels(bounds.Size)+new Vector(bounds.X,bounds.Y);
            return brush switch
            {
                ILinearGradientBrush linear=>kind==0&&origin==Position(linear.StartPoint)&&end==Position(linear.EndPoint),
                IRadialGradientBrush radial=>kind==1&&center==Position(radial.Center)&&origin==Position(radial.GradientOrigin)&&rx==radial.RadiusX.ToValue(bounds.Width)&&ry==radial.RadiusY.ToValue(bounds.Height),
                IConicGradientBrush conic=>kind==2&&center==Position(conic.Center)&&angle==conic.Angle*Math.PI/180,
                _=>false
            };
        }
        public Rgba32 Sample(float x,float y)
        {
            if(stops.Length==0)return default;var p=inverse.Transform(new Point(x,y));double t;
            if(kind==0){double dx=end.X-origin.X,dy=end.Y-origin.Y,den=dx*dx+dy*dy;t=den==0?1:((p.X-origin.X)*dx+(p.Y-origin.Y)*dy)/den;}
            else if(kind==2)t=((Math.Atan2(p.Y-center.Y,p.X-center.X)+Math.PI/2-angle)/(2*Math.PI)%1+1)%1;
            else if(rx<=0||ry<=0)t=1;
            else
            {
                double ox=(origin.X-center.X)/rx,oy=(origin.Y-center.Y)/ry,px=(p.X-origin.X)/rx,py=(p.Y-origin.Y)/ry;
                double a=px*px+py*py,b=2*(ox*px+oy*py),c=ox*ox+oy*oy-1,discriminant=b*b-4*a*c;
                double intersection=a==0?double.PositiveInfinity:discriminant<0?0:(-b+Math.Sqrt(discriminant))/(2*a);t=intersection<=0?1:1/intersection;
            }
            t=spread switch {GradientSpreadMethod.Pad=>Math.Clamp(t,0,1),GradientSpreadMethod.Repeat=>t-Math.Floor(t),GradientSpreadMethod.Reflect=>1-Math.Abs((t-2*Math.Floor(t/2))-1),_=>throw new NotSupportedException("Unknown gradient spread.")};
            if(t<=stops[0].Offset)return stops[0].Color;
            for(int i=1;i<stops.Length;i++)if(t<stops[i].Offset)
            {
                double f=(t-stops[i-1].Offset)/(stops[i].Offset-stops[i-1].Offset);var a=stops[i-1].Color;var b=stops[i].Color;
                byte Mix(byte v,byte w)=>(byte)Math.Clamp((int)Math.Round(v+(w-v)*f),0,255);return new(Mix(a.R,b.R),Mix(a.G,b.G),Mix(a.B,b.B),Mix(a.A,b.A));
            }
            return stops[^1].Color;
        }
    }
}
