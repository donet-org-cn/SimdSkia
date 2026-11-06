namespace SkiaSharp;
public partial class SKCanvas
{
    private sealed class AtlasBatch(uint imageId,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKColor[]? colors,SKSamplingOptions sampling,SKVertices vertices,SKShader shader,long bytes) : IDisposable
    {
        internal readonly uint ImageId=imageId;
        internal readonly SKRect[] Sprites=sprites;
        internal readonly SKRotationScaleMatrix[] Transforms=transforms;
        internal readonly SKColor[]? Colors=colors;
        internal readonly SKSamplingOptions Sampling=sampling;
        internal readonly SKVertices Vertices=vertices;
        internal readonly SKShader Shader=shader;
        internal readonly long Bytes=bytes;
        public void Dispose(){try{Vertices.Dispose();}finally{Shader.Dispose();}}
    }
    private List<AtlasBatch>? atlasBatches;
    private long atlasBytes;
    private void ClearAtlasCache(){if(atlasBatches is not null)foreach(var batch in atlasBatches)batch.Dispose();atlasBatches?.Clear();atlasBytes=0;}
    public void DrawAtlas(SKImage atlas,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKPaint? paint=null)=>Atlas(atlas,sprites,transforms,null,SKBlendMode.Dst,paint?.LegacySampling??SKSamplingOptions.Default,null,paint);
    public void DrawAtlas(SKImage atlas,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKSamplingOptions sampling,SKPaint? paint=null)=>Atlas(atlas,sprites,transforms,null,SKBlendMode.Dst,sampling,null,paint);
    public void DrawAtlas(SKImage atlas,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKColor[] colors,SKBlendMode mode,SKPaint? paint=null)=>Atlas(atlas,sprites,transforms,colors,mode,paint?.LegacySampling??SKSamplingOptions.Default,null,paint);
    public void DrawAtlas(SKImage atlas,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKColor[] colors,SKBlendMode mode,SKSamplingOptions sampling,SKPaint? paint=null)=>Atlas(atlas,sprites,transforms,colors,mode,sampling,null,paint);
    public void DrawAtlas(SKImage atlas,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKColor[] colors,SKBlendMode mode,SKRect cullRect,SKPaint? paint=null)=>Atlas(atlas,sprites,transforms,colors,mode,paint?.LegacySampling??SKSamplingOptions.Default,cullRect,paint);
    public void DrawAtlas(SKImage atlas,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKColor[] colors,SKBlendMode mode,SKSamplingOptions sampling,SKRect cullRect,SKPaint? paint=null)=>Atlas(atlas,sprites,transforms,colors,mode,sampling,cullRect,paint);
    private void Atlas(SKImage atlas,SKRect[] sprites,SKRotationScaleMatrix[] transforms,SKColor[]? colors,SKBlendMode mode,SKSamplingOptions sampling,SKRect? cull,SKPaint? paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(atlas);ArgumentNullException.ThrowIfNull(sprites);ArgumentNullException.ThrowIfNull(transforms);atlas.ThrowIfDisposed();paint?.ThrowIfDisposed();
        if(sprites.Length!=transforms.Length||colors is not null&&colors.Length!=sprites.Length)throw new ArgumentException("Atlas sprite, transform and color counts must match.");
        if(sprites.Length>16384)throw new NotSupportedException("An atlas batch is limited to 16384 sprites; split larger submissions.");
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        if(cull is {} bounds){if(!bounds.ToCore().IsFinite)throw new ArgumentException("Cull bounds must be finite.",nameof(cull));if(QuickReject(bounds))return;}
        if(sprites.Length==0)return;
        AtlasBatch? selected=null;atlasBatches??=[];
        foreach(var entry in atlasBatches)
        {
            if(entry.ImageId!=atlas.UniqueId||entry.Sampling!=sampling||!entry.Sprites.AsSpan().SequenceEqual(sprites)||!entry.Transforms.AsSpan().SequenceEqual(transforms))continue;
            if((entry.Colors is null)!=(colors is null)||colors is not null&&!entry.Colors!.AsSpan().SequenceEqual(colors))continue;selected=entry;break;
        }
        bool cached=selected is not null;
        if(selected is null)
        {
            var positions=new SKPoint[sprites.Length*4];var texs=new SKPoint[positions.Length];var tint=colors is null?null:new SKColor[positions.Length];var indices=new ushort[sprites.Length*6];
            for(int i=0;i<sprites.Length;i++)
            {
                var rect=sprites[i];var transform=transforms[i].ToMatrix();if(!rect.ToCore().IsFinite||!transform.ToCore().IsFinite)throw new ArgumentException("Atlas coordinates must be finite.");
                int v=i*4,at=i*6;float width=Math.Max(0,rect.Width),height=Math.Max(0,rect.Height);positions[v]=transform.MapPoint(0,0);positions[v+1]=transform.MapPoint(width,0);positions[v+2]=transform.MapPoint(width,height);positions[v+3]=transform.MapPoint(0,height);
                texs[v]=new(rect.Left,rect.Top);texs[v+1]=new(rect.Right,rect.Top);texs[v+2]=new(rect.Right,rect.Bottom);texs[v+3]=new(rect.Left,rect.Bottom);if(tint is not null)tint.AsSpan(v,4).Fill(colors![i]);
                indices[at]=(ushort)v;indices[at+1]=(ushort)(v+1);indices[at+2]=(ushort)(v+2);indices[at+3]=(ushort)v;indices[at+4]=(ushort)(v+2);indices[at+5]=(ushort)(v+3);
            }
            var shader=atlas.ToShader(SKShaderTileMode.Clamp,SKShaderTileMode.Clamp,sampling);
            SKVertices? vertices=null;
            try
            {
                vertices=SKVertices.CreateCopy(SKVertexMode.Triangles,positions,texs,tint,indices);long size=(long)sprites.Length*1200+atlas.Buffer.ByteCount;
                selected=new(atlas.UniqueId,(SKRect[])sprites.Clone(),(SKRotationScaleMatrix[])transforms.Clone(),colors is null?null:(SKColor[])colors.Clone(),sampling,vertices,shader,size);
                if(size<=8*1024*1024){while(atlasBatches.Count>=8||atlasBytes+size>8*1024*1024){var old=atlasBatches[0];atlasBatches.RemoveAt(0);atlasBytes-=old.Bytes;old.Dispose();}atlasBatches.Add(selected);atlasBytes+=size;cached=true;}
            }
            catch{vertices?.Dispose();shader.Dispose();throw;}
        }
        try{using var drawing=paint?.Clone()??new SKPaint();drawing.Shader=selected.Shader;DrawVertices(selected.Vertices,mode,drawing);}finally{if(!cached)selected.Dispose();}
    }
}
