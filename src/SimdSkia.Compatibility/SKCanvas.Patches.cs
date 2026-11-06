using SimdSkia;
namespace SkiaSharp;
public partial class SKCanvas
{
    private sealed record PatchBatch(SKPoint[] Cubics,SKColor[]? Colors,SKPoint[]? TexCoords,int X,int Y,SKVertices Vertices,long Bytes);
    private List<PatchBatch>? patchBatches;
    private long patchBytes;
    private void ClearPatchCache(){if(patchBatches is not null)foreach(var batch in patchBatches)batch.Vertices.Dispose();patchBatches?.Clear();patchBytes=0;}
    public void DrawPatch(SKPoint[] cubics,SKColor[]? colors,SKPoint[]? texCoords,SKPaint paint)=>DrawPatch(cubics,colors,texCoords,SKBlendMode.Modulate,paint);
    public void DrawPatch(SKPoint[] cubics,SKColor[]? colors,SKPoint[]? texCoords,SKBlendMode mode,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(cubics);ArgumentNullException.ThrowIfNull(paint);paint.ThrowIfDisposed();
        if(cubics.Length!=12||colors is not null&&colors.Length!=4||texCoords is not null&&texCoords.Length!=4)throw new ArgumentException("A Coons patch requires 12 cubic controls and four corner attributes.");
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));foreach(var p in cubics)if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))throw new ArgumentException("Patch coordinates must be finite.",nameof(cubics));if(texCoords is not null)foreach(var p in texCoords)if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))throw new ArgumentException("Patch texture coordinates must be finite.",nameof(texCoords));
        Span<PointF> points=stackalloc PointF[12];for(int i=0;i<12;i++)points[i]=new(cubics[i].X,cubics[i].Y);var lod=CoonsPatchMesh.LevelOfDetail(points,matrix);patchBatches??=[];PatchBatch? selected=null;
        foreach(var batch in patchBatches)
        {if(batch.X!=lod.X||batch.Y!=lod.Y||!batch.Cubics.AsSpan().SequenceEqual(cubics)||(batch.Colors is null)!=(colors is null)||(batch.TexCoords is null)!=(texCoords is null))continue;if(colors is not null&&!batch.Colors!.AsSpan().SequenceEqual(colors)||texCoords is not null&&!batch.TexCoords!.AsSpan().SequenceEqual(texCoords))continue;selected=batch;break;}
        bool cached=selected is not null;
        if(selected is null)
        {
            var mesh=CoonsPatchMesh.Build(points.ToArray(),colors?.Select(c=>c.ToCore()).ToArray(),texCoords?.Select(p=>new PointF(p.X,p.Y)).ToArray(),lod.X,lod.Y);var vertices=new SKVertices(mesh,colors is not null);long size=checked((long)mesh.TriangleCount*1200);
            selected=new((SKPoint[])cubics.Clone(),colors is null?null:(SKColor[])colors.Clone(),texCoords is null?null:(SKPoint[])texCoords.Clone(),lod.X,lod.Y,vertices,size);
            if(size<=8*1024*1024){while(patchBatches.Count>=8||patchBytes+size>8*1024*1024){var old=patchBatches[0];patchBatches.RemoveAt(0);patchBytes-=old.Bytes;old.Vertices.Dispose();}patchBatches.Add(selected);patchBytes+=size;cached=true;}
        }
        try{DrawVertices(selected.Vertices,mode,paint);}finally{if(!cached)selected.Vertices.Dispose();}
    }
}
