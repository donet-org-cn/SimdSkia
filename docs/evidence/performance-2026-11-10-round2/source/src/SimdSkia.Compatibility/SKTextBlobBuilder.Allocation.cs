// Public forwarding contracts from SkiaSharp v3.119.4 (MIT).
namespace SkiaSharp;
public partial class SKTextBlobBuilder
{
    public SKRunBuffer AllocateRun(SKFont font, int count, float x, float y, SKRect? bounds = null)=>(SKRunBuffer)AllocateRawRun(font,count,x,y,bounds).Run;
    public SKRawRunBuffer<float> AllocateRawRun(SKFont font, int count, float x, float y, SKRect? bounds = null)=>Allocate<float>(font,count,0,new SKPoint(x,y),0,false,bounds);
    public SKTextRunBuffer AllocateTextRun(SKFont font, int count, float x, float y, int textByteCount, SKRect? bounds = null)=>(SKTextRunBuffer)AllocateRawTextRun(font,count,x,y,textByteCount,bounds).Run;
    public SKRawRunBuffer<float> AllocateRawTextRun(SKFont font, int count, float x, float y, int textByteCount, SKRect? bounds = null)=>Allocate<float>(font,count,0,new SKPoint(x,y),textByteCount,true,bounds);
    public SKHorizontalRunBuffer AllocateHorizontalRun(SKFont font, int count, float y, SKRect? bounds = null)=>(SKHorizontalRunBuffer)AllocateRawHorizontalRun(font,count,y,bounds).Run;
    public SKRawRunBuffer<float> AllocateRawHorizontalRun(SKFont font, int count, float y, SKRect? bounds = null)=>Allocate<float>(font,count,1,new SKPoint(0,y),0,false,bounds);
    public SKHorizontalTextRunBuffer AllocateHorizontalTextRun(SKFont font, int count, float y, int textByteCount, SKRect? bounds = null)=>(SKHorizontalTextRunBuffer)AllocateRawHorizontalTextRun(font,count,y,textByteCount,bounds).Run;
    public SKRawRunBuffer<float> AllocateRawHorizontalTextRun(SKFont font, int count, float y, int textByteCount, SKRect? bounds = null)=>Allocate<float>(font,count,1,new SKPoint(0,y),textByteCount,true,bounds);
    public SKPositionedRunBuffer AllocatePositionedRun(SKFont font, int count, SKRect? bounds = null)=>(SKPositionedRunBuffer)AllocateRawPositionedRun(font,count,bounds).Run;
    public SKRawRunBuffer<SKPoint> AllocateRawPositionedRun(SKFont font, int count, SKRect? bounds = null)=>Allocate<SKPoint>(font,count,2,default,0,false,bounds);
    public SKPositionedTextRunBuffer AllocatePositionedTextRun(SKFont font, int count, int textByteCount, SKRect? bounds = null)=>(SKPositionedTextRunBuffer)AllocateRawPositionedTextRun(font,count,textByteCount,bounds).Run;
    public SKRawRunBuffer<SKPoint> AllocateRawPositionedTextRun(SKFont font, int count, int textByteCount, SKRect? bounds = null)=>Allocate<SKPoint>(font,count,2,default,textByteCount,true,bounds);
    public SKRotationScaleRunBuffer AllocateRotationScaleRun(SKFont font, int count, SKRect? bounds = null)=>(SKRotationScaleRunBuffer)AllocateRawRotationScaleRun(font,count,bounds).Run;
    public SKRawRunBuffer<SKRotationScaleMatrix> AllocateRawRotationScaleRun(SKFont font, int count, SKRect? bounds = null)=>Allocate<SKRotationScaleMatrix>(font,count,3,default,0,false,bounds);
    public SKRotationScaleTextRunBuffer AllocateRotationScaleTextRun(SKFont font, int count, int textByteCount, SKRect? bounds = null)=>(SKRotationScaleTextRunBuffer)AllocateRawRotationScaleTextRun(font,count,textByteCount,bounds).Run;
    public SKRawRunBuffer<SKRotationScaleMatrix> AllocateRawRotationScaleTextRun(SKFont font, int count, int textByteCount, SKRect? bounds = null)=>Allocate<SKRotationScaleMatrix>(font,count,3,default,textByteCount,true,bounds);
}
