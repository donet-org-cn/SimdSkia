using Avalonia;
using Avalonia.Media;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

internal sealed partial class DrawingContext
{
    private readonly record struct ShadowKey(PathGeometry Path, Matrix3 Matrix, Matrix3 OffsetMatrix,
        double Blur, double Spread, bool Inset, Color Color, PixelFormat Format, int TargetWidth, int TargetHeight);
    private readonly record struct ShadowStrip(RectF Bounds, PixelBuffer Pixels);
    private sealed record ShadowImage(List<ShadowStrip> Strips, int ByteCount) : IDisposable
    {
        public void Dispose() { foreach (var strip in Strips) strip.Pixels.Dispose(); }
    }
    private const int ShadowBudget = 8 * 1024 * 1024;
    private static readonly object shadowGate = new();
    private static readonly Dictionary<ShadowKey, ShadowImage> shadows = [];
    private static readonly HashSet<ShadowKey> recentShadows = [];
    private static long shadowBytes;
    private static readonly DropShadowBlurCache effectShadows = new();

    private readonly record struct FilterSpec(float SigmaX,float SigmaY,float OffsetX,float OffsetY,Rgba32 Color,bool Shadow)
    {
        internal RectF Expand(RectF r)
        {
            float x=MathF.Ceiling(SigmaX*3)+1,y=MathF.Ceiling(SigmaY*3)+1;
            return new(Math.Min(r.Left,r.Left+OffsetX-x),Math.Min(r.Top,r.Top+OffsetY-y),Math.Max(r.Right,r.Right+OffsetX+x),Math.Max(r.Bottom,r.Bottom+OffsetY+y));
        }
        internal PixelBuffer Apply(PixelBuffer source,RectF bounds)
        {
            var blurBounds=new RectF(bounds.Left-MathF.Ceiling(SigmaX*3)-1,bounds.Top-MathF.Ceiling(SigmaY*3)-1,bounds.Right+MathF.Ceiling(SigmaX*3)+1,bounds.Bottom+MathF.Ceiling(SigmaY*3)+1);
            return Shadow?ManagedFilters.DropShadow(source,SigmaX,SigmaY,OffsetX,OffsetY,Color,blurBounds,effectShadows):ManagedFilters.Blur(source,SigmaX,SigmaY,blurBounds);
        }
    }
    // This factor belongs to Avalonia 12.1.5's radius contract (not CSS blur).
    private static float Sigma(double radius)
    {if(!double.IsFinite(radius)||radius<0)throw new ArgumentOutOfRangeException(nameof(radius));return radius==0?0:.288675f*(float)radius+.5f;}
    private static (float X,float Y) DeviceSigma(double radius,Matrix3 matrix)
    {
        float sigma=Sigma(radius),x=sigma*MathF.Sqrt(matrix.M00*matrix.M00+matrix.M01*matrix.M01),y=sigma*MathF.Sqrt(matrix.M10*matrix.M10+matrix.M11*matrix.M11);
        if(!float.IsFinite(x)||!float.IsFinite(y)||x>256||y>256)throw new NotSupportedException("Device Gaussian sigma exceeds the 256 pixel limit.");
        if(MathF.Abs(matrix.M00*matrix.M10+matrix.M01*matrix.M11)>1e-5f&&sigma>0)throw new NotSupportedException("Gaussian effects under correlated shear transforms are not implemented.");
        return(x,y);
    }
    public void PushEffect(Rect? bounds,IEffect effect)
    {
        Check();ArgumentNullException.ThrowIfNull(effect);FilterSpec filter;var matrix=DeviceMatrix;
        if(effect is IBlurEffect blur){var s=DeviceSigma(blur.Radius,matrix);filter=new(s.X,s.Y,0,0,default,false);}
        else if(effect is IDropShadowEffect shadow)
        {
            var s=DeviceSigma(shadow.BlurRadius,matrix);BrushFactory.Opacity(shadow.Opacity);
            if(!double.IsFinite(shadow.OffsetX)||!double.IsFinite(shadow.OffsetY))throw new ArgumentException("Shadow offset must be finite.");
            float x=(float)(matrix.M00*shadow.OffsetX+matrix.M01*shadow.OffsetY),y=(float)(matrix.M10*shadow.OffsetX+matrix.M11*shadow.OffsetY);
            var color=ConvertTo.Color(shadow.Color);filter=new(s.X,s.Y,x,y,color with{A=(byte)Math.Floor(color.A*shadow.Opacity)},true);
        }
        else throw new NotSupportedException($"Effect {effect.GetType().Name} is not implemented.");
        BeginGroup(GroupKind.Effect,255,null,bounds??default,filter);
    }
    public void PopEffect()=>EndGroup(GroupKind.Effect);
    private void ValidateShadows(BoxShadows shadows)
    {
        foreach(var shadow in shadows)
        {
            if(!double.IsFinite(shadow.OffsetX)||!double.IsFinite(shadow.OffsetY)||!double.IsFinite(shadow.Spread))throw new ArgumentException("Box shadow offsets/spread must be finite.");
            DeviceSigma(shadow.Blur,DeviceMatrix);
        }
    }
    private void DrawShadowedRectangle(IBrush? brush,IPen? pen,RoundedRect rect,BoxShadows shadows)
    {
        // Validate all source paints before the first outer shadow touches pixels.
        using var fillValidation=brush is not null?CreatePaint(brush,rect.Rect,Antialias):default;
        using var strokeValidation=pen?.Brush is not null?CreatePaint(pen.Brush,rect.Rect,Antialias):default;
        foreach(var shadow in shadows)if(!shadow.IsInset&&shadow.Color.A>0)DrawBoxShadow(rect,shadow);
        DrawRectangle(brush,null,rect);
        foreach(var shadow in shadows)if(shadow.IsInset&&shadow.Color.A>0)DrawBoxShadow(rect,shadow);
        if(pen?.Brush is not null&&pen.Thickness>0)DrawGeometry(null,pen,Shape(rect));
    }
    private void DrawBoxShadow(RoundedRect rect,BoxShadow shadow)
    {
        var m=DeviceMatrix;var sigma=DeviceSigma(shadow.Blur,m);int padX=(int)MathF.Ceiling(sigma.X*3)+2,padY=(int)MathF.Ceiling(sigma.Y*3)+2;
        var shadowMatrix=ApplyOrigin(transform*Matrix.CreateTranslation(shadow.OffsetX,shadow.OffsetY)*dpiTransform);
        var basePath = Shape(rect).Path;
        var key = new ShadowKey(basePath, m, shadowMatrix, shadow.Blur, shadow.Spread, shadow.IsInset,
            shadow.Color, target.Format, target.Width, target.Height);
        ShadowImage? previous;
        lock (shadowGate)
            shadows.TryGetValue(key, out previous);
        if (previous is not null)
        {
            DrawShadowImage(previous, immutable: true);
            return;
        }

        var casting=shadow.IsInset?rect.Deflate(shadow.Spread,shadow.Spread):rect.Inflate(shadow.Spread,shadow.Spread);
        var path=Shape(casting).Path;var b=(shadow.IsInset?basePath:path).GetTightBounds(shadow.IsInset?m:shadowMatrix);
        int left=(int)Math.Clamp(MathF.Floor(b.Left)-padX,-padX,target.Width+padX),top=(int)Math.Clamp(MathF.Floor(b.Top)-padY,-padY,target.Height+padY);
        int right=(int)Math.Clamp(MathF.Ceiling(b.Right)+padX,-padX,target.Width+padX),bottom=(int)Math.Clamp(MathF.Ceiling(b.Bottom)+padY,-padY,target.Height+padY);
        if(left>=right||top>=bottom)return;
        int width = right - left, height = bottom - top;
        var local = shadowMatrix with { M02 = shadowMatrix.M02-left, M12 = shadowMatrix.M12-top };
        var cast = RasterCanvas.RasterizeClip(path, local, width, height, true);
        CoverageClip? box = null;
        int boxLeft = 0, boxRight = 0, boxTop, boxBottom;
        if (!rect.IsRounded && m.M01 == 0 && m.M10 == 0)
        {
            var bounds = basePath.GetTightBounds(m);
            boxLeft = (int)MathF.Floor(bounds.Left + .5f);
            boxRight = (int)MathF.Floor(bounds.Right + .5f);
            boxTop = Math.Clamp((int)MathF.Floor(bounds.Top + .5f), top, bottom);
            boxBottom = Math.Clamp((int)MathF.Floor(bounds.Bottom + .5f), top, bottom);
        }
        else
        {
            box = CreateCoverageClip(basePath,m,true);
            boxTop = box.Top + box.RepeatStart;
            boxBottom = boxTop + box.ExtraRows + (box.Height == 0 ? 0 : 1);
        }

        // A vertical Gaussian only reads radius rows on each side. Between
        // those margins identical input rows give byte-identical output rows,
        // provided the box coverage applied after the blur is also constant.
        int radius = (int)Math.Ceiling(sigma.Y * 3);
        int band = Math.Max(cast.Top + cast.RepeatStart + radius, boxTop - top);
        int last = Math.Min(cast.Top + cast.RepeatStart + cast.ExtraRows - radius, boxBottom - 1 - top);
        int extraRows = last - band;
        if (extraRows < Math.Max(8, height / 4)) extraRows = 0;
        int storedHeight = height - extraRows;
        var format = target.Format == PixelFormat.Alpha8 ? PixelFormat.Bgra8888 : target.Format;
        int byteCount = checked(width * storedHeight * 4);
        bool remember = false;
        if (byteCount <= ShadowBudget / 2)
        {
            lock (shadowGate)
            {
                // Animated shadows continue using pooled scratch storage.
                remember = recentShadows.Remove(key);
                if (!remember)
                {
                    if (recentShadows.Count >= 16) recentShadows.Clear();
                    recentShadows.Add(key);
                }
            }
        }
        using var mask=PixelBuffer.Rent(width,storedHeight,PixelFormat.Alpha8,AlphaFormat.Premultiplied);
        for (int y = 0; y < storedHeight; y++)
        {
            var source = cast.GetRow(y > band ? y + extraRows : y);
            var row = mask.GetRowSpan(y);
            if (!source.IsEmpty) source.CopyTo(row.Slice(cast.Left));
            if (shadow.IsInset)
                for (int x = 0; x < row.Length; x++) row[x] = (byte)~row[x];
        }
        using var blurred = ManagedFilters.Blur(mask, sigma.X, sigma.Y);
        var strips = new List<ShadowStrip>(extraRows == 0 ? 1 : 3);
        var image = new ShadowImage(strips, byteCount);
        bool cached = false;
        try
        {
            if (extraRows == 0)
                AddStrip(0, height, 0, height);
            else
            {
                AddStrip(0, band, 0, band);
                AddStrip(band, 1, band, 1 + extraRows);
                AddStrip(band + 1, storedHeight - band - 1, band + 1 + extraRows, storedHeight - band - 1);
            }
            if (remember)
            {
                image = RememberShadow(key, image);
                cached = true;
            }
            DrawShadowImage(image, cached);
        }
        finally { if (!cached) image.Dispose(); }

        void AddStrip(int sourceY, int rows, int destY, int destHeight)
        {
            if (rows == 0) return;
            var pixels = remember ? new PixelBuffer(width, rows, format, AlphaFormat.Premultiplied) : PixelBuffer.Rent(width, rows, format);
            strips.Add(new(new(left, top + destY, right, top + destY + destHeight), pixels));
            var color = ConvertTo.Color(shadow.Color);
            int ri = format == PixelFormat.Rgba8888 ? 0 : 2;
            for (int y = 0; y < rows; y++)
            {
                var coverage = blurred.GetRowSpan(sourceY + y);
                var row = pixels.GetRowSpan(y);
                int deviceY = top + destY + y;
                bool inBoxRow = deviceY >= boxTop && deviceY < boxBottom;
                for (int x = 0; x < width; x++)
                {
                    int deviceX = left + x;
                    byte clipAlpha = box is not null ? box.At(deviceX,deviceY) :
                        inBoxRow && deviceX >= boxLeft && deviceX < boxRight ? (byte)255 : (byte)0;
                    if (!shadow.IsInset) clipAlpha = (byte)(255 - clipAlpha);
                    byte a = PixelBuffer.Multiply(PixelBuffer.Multiply(coverage[x],color.A),clipAlpha);
                    row[x*4+ri]=PixelBuffer.Multiply(color.R,a);row[x*4+1]=PixelBuffer.Multiply(color.G,a);row[x*4+2-ri]=PixelBuffer.Multiply(color.B,a);row[x*4+3]=a;
                }
            }
        }
    }

    private static ShadowImage RememberShadow(ShadowKey key, ShadowImage image)
    {
        lock (shadowGate)
        {
            if (shadows.TryGetValue(key, out var previous))
            {
                image.Dispose();
                return previous;
            }
            int bytes = image.ByteCount;
            while (shadows.Count >= 16 || shadowBytes + bytes > ShadowBudget)
            {
                var oldest = shadows.First();
                shadows.Remove(oldest.Key);
                shadowBytes -= oldest.Value.ByteCount;
            }
            // Owned arrays remain valid for a draw already using an evicted image.
            shadows.Add(key, image);
            shadowBytes += bytes;
            return image;
        }
    }

    private void DrawShadowImage(ShadowImage image, bool immutable)
    {
        foreach (var strip in image.Strips)
        {
            var pixels = strip.Pixels;
            Execute(new(CommandKind.DrawImage,strip.Bounds,Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
            {Image=pixels,ImageIsImmutable=immutable,ImageContentVersion=1,ImageSource=new(0,0,pixels.Width,pixels.Height),Sampling=ImageSampling.Nearest});
        }
    }
}
