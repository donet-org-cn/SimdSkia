// Adaptive Coons tessellation/forward differences derived from SkPatchUtils,
// Google BSD-3. See licenses/Skia-BSD.txt. No native drawing or code generation.
using System.Numerics;
namespace SimdSkia;
public static class CoonsPatchMesh
{
    public static (int X,int Y) LevelOfDetail(ReadOnlySpan<PointF> points,Matrix3 matrix)
    {
        static float Length(ReadOnlySpan<PointF> points,Matrix3 matrix,int a,int b,int c,int d)
        {
            var p=matrix.Map(points[a].X,points[a].Y);var q=matrix.Map(points[b].X,points[b].Y);var r=matrix.Map(points[c].X,points[c].Y);var s=matrix.Map(points[d].X,points[d].Y);
            static float Distance((float X,float Y) a,(float X,float Y) b){float dx=b.X-a.X,dy=b.Y-a.Y;return MathF.Sqrt(dx*dx+dy*dy);}return Distance(p,q)+Distance(q,r)+Distance(r,s);
        }
        float x=MathF.Max(Length(points,matrix,0,1,2,3),Length(points,matrix,9,8,7,6))/10,y=MathF.Max(Length(points,matrix,0,11,10,9),Length(points,matrix,3,4,5,6))/10;
        if(!float.IsFinite(x)||!float.IsFinite(y)||x>int.MaxValue||y>int.MaxValue)throw new NotSupportedException("Patch level of detail exceeds finite bounds.");
        int nx=Math.Max(8,(int)x),ny=Math.Max(8,(int)y);
        if((long)(nx+1)*(ny+1)>10000||nx>200||ny>200){float total=(float)nx+ny;nx=Math.Max(1,(int)MathF.Floor(nx/total*200));ny=Math.Max(1,(int)MathF.Floor(ny/total*200));}
        return(nx,ny);
    }
    private struct Evaluator
    {
        private readonly PointF a,b,c,d;
        private PointF value,first,second,third;
        internal Evaluator(PointF p0,PointF p1,PointF p2,PointF p3)
        {a=p3+p1*3-p2*3-p0;b=p2*3-p1*6+p0*3;c=(p1-p0)*3;d=p0;value=first=second=third=default;}
        internal void Start(int count){float h=1f/count,h2=h*h,h3=h2*h;third=a*(6*h3);second=third+b*(2*h2);first=a*h3+b*h2+c*h;value=d;}
        internal PointF Next(){var result=value;value+=first;first+=second;second+=third;return result;}
    }
    private static PointF Bilerp(float u,float v,PointF tl,PointF tr,PointF bl,PointF br)=>(tl*(1-u)+tr*u)*(1-v)+(bl*(1-u)+br*u)*v;
    private static Vector4 Bilerp(float u,float v,Vector4 tl,Vector4 tr,Vector4 bl,Vector4 br)=>(tl*(1-u)+tr*u)*(1-v)+(bl*(1-u)+br*u)*v;
    public static TriangleMesh Build(PointF[] points,Rgba32[]? colors,PointF[]? texCoords,int nx,int ny)
    {
        var top=new Evaluator(points[0],points[1],points[2],points[3]);var bottom=new Evaluator(points[9],points[8],points[7],points[6]);var left=new Evaluator(points[0],points[11],points[10],points[9]);var right=new Evaluator(points[3],points[4],points[5],points[6]);top.Start(nx);bottom.Start(nx);
        int count=checked((nx+1)*(ny+1)),stride=ny+1;var vertices=new PointF[count];var tex=texCoords is null?null:new PointF[count];var rgba=colors is null?null:new Rgba32[count];var indices=new int[checked(nx*ny*6)];
        Span<Vector4> corner=stackalloc Vector4[4];if(colors is not null)for(int i=0;i<4;i++){float a=colors[i].A*(1f/255);corner[i]=new(colors[i].R*(1f/255)*a,colors[i].G*(1f/255)*a,colors[i].B*(1f/255)*a,a);}
        float u=0;
        for(int x=0;x<=nx;x++)
        {
            var t=top.Next();var b=bottom.Next();left.Start(ny);right.Start(ny);float v=0;
            for(int y=0;y<=ny;y++)
            {
                var l=left.Next();var r=right.Next();int at=x*stride+y;var s0=t*(1-v)+b*v;var s1=l*(1-u)+r*u;var s2=Bilerp(u,v,points[0],points[3],points[9],points[6]);vertices[at]=s0+s1-s2;
                if(tex is not null)tex[at]=Bilerp(u,v,texCoords![0],texCoords[1],texCoords[3],texCoords[2]);
                if(rgba is not null){var p=Bilerp(u,v,corner[0],corner[1],corner[3],corner[2]);rgba[at]=p.W>0?new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W)):default;}
                if(x<nx&&y<ny){int k=(x*ny+y)*6;indices[k]=at;indices[k+1]=at+1;indices[k+2]=at+stride+1;indices[k+3]=at;indices[k+4]=at+stride+1;indices[k+5]=at+stride;}
                v=Math.Clamp(v+1f/ny,0,1);
            }
            u=Math.Clamp(u+1f/nx,0,1);
        }
        return new(vertices,tex,rgba,indices);
    }
}
