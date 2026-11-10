using System.Numerics;
namespace SimdSkia;

/// <summary>Immutable indexed triangles with cached local geometry and interpolants.</summary>
public sealed class TriangleMesh
{
    private readonly PointF[] positions,texCoords;
    private readonly Rgba32[]? colors;
    private readonly int[] indices;
    private readonly PathGeometry?[] paths;
    private readonly Dictionary<(IColorShader?,CompositeMode),IColorShader[]> materials=[];
    public int TriangleCount=>indices.Length/3;
    public TriangleMesh(PointF[] positions,PointF[]? texCoords,Rgba32[]? colors,int[] indices)
    {
        this.positions=(PointF[])positions.Clone();this.texCoords=(PointF[])(texCoords??positions).Clone();this.colors=colors is null?null:(Rgba32[])colors.Clone();this.indices=(int[])indices.Clone();paths=new PathGeometry?[TriangleCount];
    }
    public PathGeometry Geometry(int index)
    {
        lock(paths){if(paths[index] is {} known)return known;int at=index*3;var geometry=new PathGeometry([new(PathVerb.Move,positions[indices[at]]),new(PathVerb.Line,positions[indices[at+1]]),new(PathVerb.Line,positions[indices[at+2]]),new(PathVerb.Close,default)]);if(TriangleCount<=4096)paths[index]=geometry;return geometry;}
    }
    public bool TryConstant(int triangle,IColorShader? material,out Rgba32 color)
    {int at=triangle*3;if(material is null&&colors is not null&&colors[indices[at]]==colors[indices[at+1]]&&colors[indices[at]]==colors[indices[at+2]]){color=colors[indices[at]];return true;}color=default;return false;}
    public IColorShader[] Bind(IColorShader? material,CompositeMode vertexBlend)
    {
        lock(materials)
        {
            var key=(material,vertexBlend);if(materials.TryGetValue(key,out var found))return found;var shaders=new IColorShader[TriangleCount];
            for(int i=0;i<shaders.Length;i++){int at=i*3,a=indices[at],b=indices[at+1],c=indices[at+2];shaders[i]=new TriangleShader(positions[a],positions[b],positions[c],texCoords[a],texCoords[b],texCoords[c],colors?[a],colors?[b],colors?[c],material,vertexBlend);}
            if(materials.Count>=8)materials.Clear();if(TriangleCount<=4096)materials.Add(key,shaders);return shaders;
        }
    }
    private sealed class TriangleShader : IPremultipliedColorShader,IPremultipliedBatchShader
    {
        private readonly PointF a,ta,textureB,textureC;
        private readonly float ux,uy,vx,vy;
        private readonly Vector4 ca,colorB,colorC;
        private readonly bool colored;
        private readonly IColorShader? material;
        private readonly CompositeMode mode;
        private IColorShader? managed;
        internal TriangleShader(PointF a,PointF b,PointF c,PointF ta,PointF tb,PointF tc,Rgba32? ca,Rgba32? cb,Rgba32? cc,IColorShader? material,CompositeMode mode)
        {
            this.a=a;this.ta=ta;textureB=new(tb.X-ta.X,tb.Y-ta.Y);textureC=new(tc.X-ta.X,tc.Y-ta.Y);this.material=material;this.mode=mode;colored=ca is not null;
            double bx=(double)b.X-a.X,by=(double)b.Y-a.Y,cx=(double)c.X-a.X,cy=(double)c.Y-a.Y,det=bx*cy-by*cx;
            if(det!=0){ux=(float)(cy/det);uy=(float)(-cx/det);vx=(float)(-by/det);vy=(float)(bx/det);}
            static Vector4 Pma(Rgba32? value){if(value is not {} color)return default;float alpha=color.A/255f;return new(color.R/255f*alpha,color.G/255f*alpha,color.B/255f*alpha,alpha);}
            this.ca=Pma(ca);colorB=Pma(cb)-this.ca;colorC=Pma(cc)-this.ca;
        }
        public IColorShader ForDestination(bool taggedSrgb)
        {
            if(!taggedSrgb||material is null)return this;var resolved=material.ForDestination(true);if(ReferenceEquals(resolved,material))return this;
            return managed??=new Resolved(this,resolved);
        }
        private Vector4 Evaluate(float x,float y,IColorShader? shader)
        {
            float px=x-a.X,py=y-a.Y,u=px*ux+py*uy,v=px*vx+py*vy;
            var vertex=ca+colorB*u+colorC*v;if(shader is null)return vertex;
            float tx=ta.X+textureB.X*u+textureC.X*v,ty=ta.Y+textureB.Y*u+textureC.Y*v;
            Vector4 value;if(shader is IPremultipliedColorShader pm)value=pm.SamplePremultiplied(tx,ty);else{var c=shader.Sample(tx,ty);float alpha=c.A/255f;value=new(c.R/255f*alpha,c.G/255f*alpha,c.B/255f*alpha,alpha);}
            return colored?ColorBlend.ComposePremultiplied(value,vertex,mode):value;
        }
        public Vector4 SamplePremultiplied(float x,float y)=>Evaluate(x,y,material);
        void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)=>SampleBatch(samples,material);
        private void SampleBatch(Span<Vector4> samples,IColorShader? shader)
        {
            if(samples.Length>128)
            {for(int i=0;i<samples.Length;i++)samples[i]=Evaluate(samples[i].X,samples[i].Y,shader);return;}
            Span<Vector4> vertices=stackalloc Vector4[samples.Length];
            for(int i=0;i<samples.Length;i++)
            {
                float px=samples[i].X-a.X,py=samples[i].Y-a.Y,u=px*ux+py*uy,v=px*vx+py*vy;
                var vertex=ca+colorB*u+colorC*v;
                if(shader is null){samples[i]=vertex;continue;}
                vertices[i]=vertex;
                samples[i]=new(ta.X+textureB.X*u+textureC.X*v,ta.Y+textureB.Y*u+textureC.Y*v,0,0);
            }
            if(shader is null)return;
            if(shader is IPremultipliedBatchShader batch)batch.SampleBatch(samples);
            else if(shader is IPremultipliedColorShader premultiplied)
                for(int i=0;i<samples.Length;i++)samples[i]=premultiplied.SamplePremultiplied(samples[i].X,samples[i].Y);
            else for(int i=0;i<samples.Length;i++)
            {var color=shader.Sample(samples[i].X,samples[i].Y);float alpha=color.A/255f;samples[i]=new(color.R/255f*alpha,color.G/255f*alpha,color.B/255f*alpha,alpha);}
            if(colored)for(int i=0;i<samples.Length;i++)samples[i]=ColorBlend.ComposePremultiplied(samples[i],vertices[i],mode);
        }
        public Rgba32 Sample(float x,float y)=>Straight(SamplePremultiplied(x,y));
        private static Rgba32 Straight(Vector4 p)=>p.W>0?new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W)):default;
        private sealed class Resolved(TriangleShader source,IColorShader material) : IPremultipliedColorShader,IPremultipliedBatchShader
        {public Vector4 SamplePremultiplied(float x,float y)=>source.Evaluate(x,y,material);public Rgba32 Sample(float x,float y)=>Straight(SamplePremultiplied(x,y));void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)=>source.SampleBatch(samples,material);}
    }
}
