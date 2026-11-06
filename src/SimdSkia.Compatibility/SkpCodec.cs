// SKP wire constants/layout follow Skia m119 (Google/Android, BSD-3).
// See docs/licenses/Skia-BSD.txt. No Skia library is loaded by this codec.
using System.Text;
using SimdSkia;
using SimdSkia.Imaging;
namespace SkiaSharp;

/// <summary>Bounded native SKP v103 vector/bitmap stream codec. Unsupported
/// flattenable graphs/opcodes fail explicitly, never disappear from a picture.</summary>
internal static partial class SkpCodec
{
    internal const int MaximumBytes=128*1024*1024;
    private const int MaximumItems=1_000_000;
    private const uint ReadTag=0x72656164,FactoryTag=0x66616374,TypefaceTag=0x74706663,ArrayTag=0x61726179,PaintTag=0x706e7420,PathTag=0x70746820,ImageTag=0x696d6167,EofTag=0x656f6620;
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static int Count(BinaryReader r,int maximum=MaximumItems){int n=r.ReadInt32();if(n<0||n>maximum)throw new InvalidDataException("Invalid SKP item count.");return n;}
    private static float Scalar(BinaryReader r){float f=r.ReadSingle();if(!float.IsFinite(f))throw new InvalidDataException("Nonfinite SKP scalar.");return f;}
    private static byte[] Bytes(BinaryReader r,int n){if(n<0||n>MaximumBytes)throw new InvalidDataException("Invalid SKP byte length.");if(r.BaseStream.CanSeek&&n>r.BaseStream.Length-r.BaseStream.Position)throw new EndOfStreamException();var bytes=r.ReadBytes(n);if(bytes.Length!=n)throw new EndOfStreamException();return bytes;}
    private static void Pad(BinaryWriter w,int bytes){for(int i=bytes;i%4!=0;i++)w.Write((byte)0);}
    private static void Padding(BinaryReader r,int bytes){for(int i=bytes;i%4!=0;i++)r.ReadByte();}
    private static void Rect(BinaryWriter w,RectF r){w.Write(r.Left);w.Write(r.Top);w.Write(r.Right);w.Write(r.Bottom);}
    private static SKRect Rect(BinaryReader r)=>new(Scalar(r),Scalar(r),Scalar(r),Scalar(r));
    private static SKBlendMode Blend(CompositeMode mode)=>mode switch{CompositeMode.SourceOver=>SKBlendMode.SrcOver,CompositeMode.Clear=>SKBlendMode.Clear,CompositeMode.Destination=>SKBlendMode.Dst,_=>(SKBlendMode)(int)mode};
    internal static byte[] Write(PictureCommands picture)
    {
        var paints=new List<Paint>();var paths=new List<PathGeometry>();var images=new List<PixelBuffer>();using var ops=new MemoryStream();using var commands=new BinaryWriter(ops,Utf8,true);
        int PaintIndex(Paint p){if(p.Shader is not null||p.ColorFilter is not null||p.ImageFilter is not null||p.MaskFilter is not null||p.Blender is not null)throw new NotSupportedException("SKP flattenable shader/filter/custom-blender graphs are not implemented yet.");int i=paints.IndexOf(p);if(i>=0)return i+1;if(paints.Count==MaximumItems)throw new NotSupportedException("SKP paint budget exceeded.");paints.Add(p);return paints.Count;}
        int PathIndex(PathGeometry path){int i=paths.IndexOf(path);if(i>=0)return i+1;paths.Add(path);return paths.Count;}
        void Op(int code,Action<BinaryWriter>? payload=null)
        {long start=ops.Position;commands.Write(0);payload?.Invoke(commands);long size=ops.Position-start;if(size>0xfffffe||ops.Length>MaximumBytes)throw new NotSupportedException("SKP operation/document size budget exceeded.");long end=ops.Position;ops.Position=start;commands.Write((uint)(code<<24)|(uint)size);ops.Position=end;}
        void Matrix(Matrix3 m)=>Op(71,w=>{foreach(float f in new[]{m.M00,m.M10,0,m.M20,m.M01,m.M11,0,m.M21,0,0,1,0,m.M02,m.M12,0,m.M22})w.Write(f);});
        foreach(var op in picture.Items)
        {
            if(op.Kind is not (PictureCommands.Kind.Restore or PictureCommands.Kind.Save))Matrix(op.Kind==PictureCommands.Kind.Clear?Matrix3.Identity:op.Command.Kind==CommandKind.Clear?op.Matrix:op.Kind==PictureCommands.Kind.Draw?op.Command.Transform:op.Matrix);
            switch(op.Kind)
            {
                case PictureCommands.Kind.Save:Op(30);break;
                case PictureCommands.Kind.Restore:Op(28);break;
                case PictureCommands.Kind.Clip:Op(1,w=>{w.Write(PathIndex(op.Path!));w.Write((int)op.ClipOperation|(op.Antialias?16:0));w.Write(0);});break;
                case PictureCommands.Kind.Layer:
                    var layer=op.Layer;if(layer.Backdrop is not null||layer.Flags!=0)throw new NotSupportedException("SKP backdrop/initialized layers are not implemented.");
                    Op(52,w=>{int flags=(layer.Bounds is null?0:1)|(layer.Paint is null?0:2);w.Write(flags);if(layer.Bounds is {} bounds)Rect(w,bounds.ToCore());if(layer.Paint is {} p)w.Write(PaintIndex(p.ToCore(true)));});break;
                case PictureCommands.Kind.Clear:case PictureCommands.Kind.Paint:Op(13,w=>w.Write(PaintIndex(op.Command.Paint)));break;
                case PictureCommands.Kind.Annotation:
                    Op(53,w=>{var a=op.Annotation!;Rect(w,a.Bounds.ToCore());var key=Utf8.GetBytes(a.Key);w.Write(key.Length);w.Write(key);w.Write((byte)0);Pad(w,key.Length+1);w.Write(a.Value.Length);w.Write(a.Value);Pad(w,a.Value.Length);});break;
                case PictureCommands.Kind.Draw:
                    var c=op.Command;int paint=PaintIndex(c.Paint);
                    if(c.Kind==CommandKind.FillRectangle){Op(21,w=>{w.Write(paint);Rect(w,c.Rectangle);});break;}
                    if(c.Kind==CommandKind.DrawImage){var image=c.Image!;int i=images.IndexOf(image);if(i<0){i=images.Count;images.Add(image);}int id=i;Op(73,w=>{w.Write(paint);w.Write(id);Rect(w,c.ImageSource??new(0,0,image.Width,image.Height));Rect(w,c.Rectangle);w.Write(0);w.Write(c.Sampling==ImageSampling.Mitchell?1:0);if(c.Sampling==ImageSampling.Mitchell){w.Write(1f/3);w.Write(1f/3);}else{w.Write(c.Sampling==ImageSampling.Bilinear?1:0);w.Write(0);}w.Write(0);});break;}
                    PathGeometry geometry;if(c.Kind==CommandKind.FillPath)geometry=c.Path!;else if(c.Kind==CommandKind.DrawGlyphRun){var parts=new List<PathSegment>();foreach(var glyph in c.GlyphRun!.Glyphs)glyph.Outline.AppendTransformedTo(parts,new Matrix3(1,0,glyph.Position.X,0,1,glyph.Position.Y,0,0,1)*c.GlyphRun.FontTransform);geometry=new(parts);}else throw new NotSupportedException($"SKP command {c.Kind} is not implemented.");
                    Op(14,w=>{w.Write(paint);w.Write(PathIndex(geometry));});break;
                default:throw new NotSupportedException("Unknown SKP recording command.");
            }
        }
        using var buffer=new MemoryStream();using(var w=new BinaryWriter(buffer,Utf8,true))
        {
            if(paints.Count>0){w.Write(PaintTag);w.Write(paints.Count);foreach(var p in paints){w.Write(0f);w.Write(4f);var f=p.FloatingColor??new System.Numerics.Vector4(p.Color.R/255f,p.Color.G/255f,p.Color.B/255f,p.Color.A/255f);w.Write(f.X);w.Write(f.Y);w.Write(f.Z);w.Write(f.W);w.Write((uint)((p.Antialias?1:0)|(p.Dither?2:0)|((int)Blend(p.Composite)<<8)));}}
            if(paths.Count>0){w.Write(PathTag);w.Write(paths.Count);w.Write(paths.Count);foreach(var path in paths)WritePath(w,path);}
            if(images.Count>0){w.Write(ImageTag);w.Write(images.Count);foreach(var image in images){var png=PngCodec.Encode(image);w.Write(0);w.Write(png.Length);w.Write(png);Pad(w,png.Length);}}
        }
        using var output=new MemoryStream();using(var w=new BinaryWriter(output,Utf8,true)){w.Write("skiapict"u8);w.Write(103);Rect(w,picture.Cull.ToCore());w.Write((byte)1);w.Write(ReadTag);w.Write((int)ops.Length);ops.Position=0;ops.CopyTo(output);w.Write(FactoryTag);w.Write(4);w.Write(0);w.Write(TypefaceTag);w.Write(0);w.Write(ArrayTag);w.Write((int)buffer.Length);buffer.Position=0;buffer.CopyTo(output);w.Write(EofTag);}
        if(output.Length>MaximumBytes)throw new NotSupportedException("SKP document exceeds 128 MiB.");return output.ToArray();
    }
    private static void WritePath(BinaryWriter w,PathGeometry path)
    {
        var points=new List<PointF>();var weights=new List<float>();var verbs=new List<byte>();
        foreach(var s in path.Segments){verbs.Add(s.Verb switch{PathVerb.Move=>0,PathVerb.Line=>1,PathVerb.Quadratic=>2,PathVerb.Conic=>3,PathVerb.Cubic=>4,PathVerb.Close=>5,_=>throw new NotSupportedException()});if(s.Verb==PathVerb.Close)continue;points.Add(s.A);if(s.Verb is PathVerb.Quadratic or PathVerb.Conic or PathVerb.Cubic)points.Add(s.B);if(s.Verb==PathVerb.Cubic)points.Add(s.C);if(s.Verb==PathVerb.Conic)weights.Add(s.ConicWeight);}
        w.Write(5|((int)path.FillRule<<8));w.Write(points.Count);w.Write(weights.Count);w.Write(verbs.Count);foreach(var p in points){w.Write(p.X);w.Write(p.Y);}foreach(float f in weights)w.Write(f);w.Write(verbs.ToArray());Pad(w,verbs.Count);
    }
}
