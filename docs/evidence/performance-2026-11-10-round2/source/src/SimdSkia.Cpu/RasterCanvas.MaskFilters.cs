namespace SimdSkia.Cpu;
public sealed partial class RasterCanvas
{
    private readonly record struct FilteredMaskKey(PathGeometry Geometry,Matrix3 Matrix,IMaskFilter Filter,bool Antialias,float Tolerance);
    private sealed record FilteredMaskEntry(CoverageMask Mask,long Bytes){internal long Used;}
    private Dictionary<FilteredMaskKey,FilteredMaskEntry>? filteredMasks;
    private long filteredMaskBytes,filteredMaskClock;
    private void DrawMaskFiltered(in DrawingCommand command)
    {
        var filter=command.Paint.MaskFilter!;
        if(command.Kind==CommandKind.DrawImage)throw new NotSupportedException("Mask-filtered images require a sampled image shader; use a shader rectangle until that image pipeline is implemented.");
        PathGeometry geometry;
        if(command.Kind==CommandKind.FillPath)geometry=command.Path??throw new ArgumentException("Path required.");
        else if(command.Kind==CommandKind.FillRectangle)geometry=RectangleGeometry(command.Rectangle);
        else if(command.Kind==CommandKind.DrawGlyphRun)
        {
            var run=command.GlyphRun??throw new ArgumentException("Glyph run required.");var segments=new List<PathSegment>();
            foreach(var glyph in run.Glyphs)glyph.Outline.AppendTransformedTo(segments,new Matrix3(1,0,glyph.Position.X,0,1,glyph.Position.Y,0,0,1)*run.FontTransform);
            geometry=new(segments);
        }
        else throw new NotSupportedException("Mask filters cannot be used with this drawing command.");
        var key=new FilteredMaskKey(geometry,command.Transform,filter,command.Paint.Antialias,command.CurveTolerance);
        filteredMasks??=[];
        if(!filteredMasks.TryGetValue(key,out var cached))
        {
            int margin=filter.Margin(command.Transform);var bounds=geometry.FillRule is FillRule.InverseEvenOdd or FillRule.InverseWinding?new RectF(-margin,-margin,target.Width+margin,target.Height+margin):geometry.Transform(command.Transform).ControlBounds;
            int left=(int)Math.Clamp(Math.Floor(bounds.Left-.5f),-margin,target.Width+margin),top=(int)Math.Clamp(Math.Floor(bounds.Top-.5f),-margin,target.Height+margin);
            int right=(int)Math.Clamp(Math.Ceiling(bounds.Right+.5f),left,target.Width+margin),bottom=(int)Math.Clamp(Math.Ceiling(bounds.Bottom+.5f),top,target.Height+margin);
            if(left==right||top==bottom)return;
            if((long)(right-left)*(bottom-top)>32*1024*1024)throw new NotSupportedException("One mask-filter input exceeds 32 MiB.");
            var shifted=new Matrix3(1,0,-left,0,1,-top,0,0,1)*command.Transform;
            CoverageClip coverage;
            if(command.Kind==CommandKind.FillRectangle&&command.Transform.M01==0&&command.Transform.M10==0&&filter is RasterMaskFilter blur&&blur.TryFilterRectangle(bounds,command.Transform,out var analytic))coverage=analytic!;
            else
            {
                int w=right-left,h=bottom-top;var raster=PathRasterizer.Rasterize(geometry.Flatten(shifted,command.CurveTolerance),geometry.FillRule,w,h,true);var input=new byte[checked(w*h)];
                for(int y=0;y<raster.Height;y++)raster.Data.AsSpan(y*raster.Width,raster.Width).CopyTo(input.AsSpan((y+raster.Top)*w+raster.Left,raster.Width));
                coverage=filter.Filter(new CoverageClip(left,top,w,h,input),command.Transform);
            }
            var data=new byte[checked(coverage.Width*coverage.Height)];for(int y=0;y<coverage.Height;y++)coverage.GetRow(coverage.Top+y).CopyTo(data.AsSpan(y*coverage.Width,coverage.Width));
            var mask=new CoverageMask(coverage.Left,coverage.Top,coverage.Width,coverage.Height,data,[],true);long bytes=data.LongLength+(long)geometry.Segments.Length*28;
            cached=new(mask,bytes);
            if(bytes<=8*1024*1024)
            {
                while(filteredMasks.Count>=64||filteredMaskBytes+bytes>8*1024*1024)
                {FilteredMaskKey victim=default;long oldest=long.MaxValue;foreach(var entry in filteredMasks)if(entry.Value.Used<oldest){victim=entry.Key;oldest=entry.Value.Used;}filteredMaskBytes-=filteredMasks[victim].Bytes;filteredMasks.Remove(victim);}
                filteredMasks.Add(key,cached);filteredMaskBytes+=bytes;
            }
        }
        cached.Used=++filteredMaskClock;
        FillMask(cached.Mask,command with{Paint=command.Paint with{MaskFilter=null}},0,0,true);
    }
}
