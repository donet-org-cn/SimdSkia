using Avalonia;
using Avalonia.Media;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

internal sealed partial class DrawingContext
{
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
            return Shadow?ManagedFilters.DropShadow(source,SigmaX,SigmaY,OffsetX,OffsetY,Color,blurBounds):ManagedFilters.Blur(source,SigmaX,SigmaY,blurBounds);
        }
    }
    // This factor belongs to Avalonia 12.1.4's radius contract (not CSS blur).
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
        BeginGroup("effect",255,null,bounds??default,filter);
    }
    public void PopEffect()=>EndGroup("effect");
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
        var casting=shadow.IsInset?rect.Deflate(shadow.Spread,shadow.Spread):rect.Inflate(shadow.Spread,shadow.Spread);
        var shadowMatrix=ApplyOrigin(transform*Matrix.CreateTranslation(shadow.OffsetX,shadow.OffsetY)*dpiTransform);
        var geometry=Shape(casting);var baseGeometry=Shape(rect);var b=(shadow.IsInset?baseGeometry.Path:geometry.Path).GetTightBounds(shadow.IsInset?m:shadowMatrix);
        int left=(int)Math.Clamp(MathF.Floor(b.Left)-padX,-padX,target.Width+padX),top=(int)Math.Clamp(MathF.Floor(b.Top)-padY,-padY,target.Height+padY);
        int right=(int)Math.Clamp(MathF.Ceiling(b.Right)+padX,-padX,target.Width+padX),bottom=(int)Math.Clamp(MathF.Ceiling(b.Bottom)+padY,-padY,target.Height+padY);
        if(left>=right||top>=bottom)return;
        using var mask=PixelBuffer.Rent(right-left,bottom-top,target.Format,AlphaFormat.Premultiplied);
        using(var renderer=new RasterCanvas(mask))
        {
            if(shadow.IsInset)renderer.Execute(DrawingCommand.Clear(new(255,255,255,255)));
            var local=shadowMatrix with{M02=shadowMatrix.M02-left,M12=shadowMatrix.M12-top};
            renderer.Execute(new(CommandKind.FillPath,default,local,new(new(255,255,255,255),shadow.IsInset?CompositeMode.Clear:CompositeMode.SourceOver,true)){Path=geometry.Path,CurveTolerance=.015625f});
        }
        using var blurred=ManagedFilters.Blur(mask,sigma.X,sigma.Y);
        CoverageClip boxClip;
        if(!rect.IsRounded&&m.M01==0&&m.M10==0)
        {
            var bounds=baseGeometry.Path.GetTightBounds(m);int x0=(int)MathF.Floor(bounds.Left+.5f),y0=(int)MathF.Floor(bounds.Top+.5f),x1=(int)MathF.Floor(bounds.Right+.5f),y1=(int)MathF.Floor(bounds.Bottom+.5f);
            x0=Math.Clamp(x0,left,right);y0=Math.Clamp(y0,top,bottom);x1=Math.Clamp(x1,x0,right);y1=Math.Clamp(y1,y0,bottom);
            var coverage=new byte[(x1-x0)*(y1-y0)];coverage.AsSpan().Fill(255);boxClip=new(x0,y0,x1-x0,y1-y0,coverage);
        }
        else boxClip=canvas.CreateClip(baseGeometry.Path,m,true);
        var color=ConvertTo.Color(shadow.Color);int ri=target.Format==PixelFormat.Rgba8888?0:2;
        for(int y=0;y<blurred.Height;y++)
        {
            var row=blurred.GetRowSpan(y);
            for(int x=0;x<blurred.Width;x++)
            {
                byte clipAlpha=boxClip.At(left+x,top+y);if(!shadow.IsInset)clipAlpha=(byte)(255-clipAlpha);
                byte a=PixelBuffer.Multiply(PixelBuffer.Multiply(row[x*4+3],color.A),clipAlpha);
                row[x*4+ri]=PixelBuffer.Multiply(color.R,a);row[x*4+1]=PixelBuffer.Multiply(color.G,a);row[x*4+2-ri]=PixelBuffer.Multiply(color.B,a);row[x*4+3]=a;
            }
        }
        Execute(new(CommandKind.DrawImage,new(left,top,right,bottom),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
        {Image=blurred,ImageSource=new(0,0,blurred.Width,blurred.Height),Sampling=ImageSampling.Nearest});
    }
}
