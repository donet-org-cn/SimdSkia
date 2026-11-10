using SimdSkia;
namespace SkiaSharp;
internal static partial class SkpCodec
{
    internal static SKPicture Read(Stream input)
    {
        using var reader=new BinaryReader(input,Utf8,true);if(!Bytes(reader,8).AsSpan().SequenceEqual("skiapict"u8))throw new InvalidDataException("Not an SKP document.");int version=reader.ReadInt32();if(version<82||version>103)return null!;if(version!=103)throw new NotSupportedException($"SKP version {version} is not implemented; the fixed baseline writes version 103.");var cull=Rect(reader);int trailing=reader.ReadByte();if(trailing==0)return null!;if(trailing!=1)throw new NotSupportedException("Custom-procedure SKP encodings are not implemented.");
        byte[]? ops=null;var paints=new List<SKPaint>();var paths=new List<SKPath>();var images=new List<SKImage>();long total=29;
        try
        {
            for(int chunks=0;chunks<MaximumItems;chunks++)
            {
                uint tag=reader.ReadUInt32();total+=4;if(tag==EofTag){if(ops is null)throw new InvalidDataException("Missing SKP operations.");return Replay(cull,ops,paints,paths,images);}
                int n=Count(reader,MaximumBytes);total+=4;if(tag==ReadTag||tag==FactoryTag||tag==ArrayTag){total+=n;if(total>MaximumBytes)throw new NotSupportedException("SKP document exceeds the 128 MiB budget.");}
                switch(tag)
                {
                    case ReadTag:if(ops is not null)throw new InvalidDataException("Duplicate SKP operation section.");ops=Bytes(reader,n);break;
                    case FactoryTag:
                        using(var f=new BinaryReader(new MemoryStream(Bytes(reader,n)),Utf8)){int count=Count(f);if(count!=0)throw new NotSupportedException("SKP flattenable factory graphs are not implemented.");if(f.BaseStream.Position!=n)throw new InvalidDataException("Invalid SKP factory section.");}break;
                    case TypefaceTag:if(n!=0)throw new NotSupportedException("SKP embedded typeface/text-blob tables are not implemented; serialize text as outlines.");break;
                    case ArrayTag:ReadArrays(Bytes(reader,n),paints,paths,images);break;
                    default:throw new NotSupportedException($"SKP section 0x{tag:X8} is not implemented.");
                }
            }
            throw new InvalidDataException("SKP chunk budget exceeded.");
        }
        finally{foreach(var p in paints)p.Dispose();foreach(var p in paths)p.Dispose();foreach(var p in images)p.Dispose();}
    }
    private static void ReadArrays(byte[] bytes,List<SKPaint> paints,List<SKPath> paths,List<SKImage> images)
    {
        using var r=new BinaryReader(new MemoryStream(bytes),Utf8);
        while(r.BaseStream.Position<bytes.Length)
        {
            uint tag=r.ReadUInt32();int n=Count(r);
            switch(tag)
            {
                case PaintTag:
                    if(paints.Count!=0)throw new InvalidDataException("Duplicate paint section.");
                    for(int i=0;i<n;i++)
                    {
                        float width=Scalar(r),miter=Scalar(r);var color=new SKColorF(Scalar(r),Scalar(r),Scalar(r),Scalar(r));uint packed=r.ReadUInt32();if((packed>>24)!=0)throw new NotSupportedException("SKP paint flattenable graphs are not implemented.");var mode=(SKBlendMode)((packed>>8)&255);var cap=(SKStrokeCap)((packed>>16)&3);var join=(SKStrokeJoin)((packed>>18)&3);var style=(SKPaintStyle)((packed>>20)&3);if(width<0||miter<0||!Enum.IsDefined(mode)||!Enum.IsDefined(cap)||!Enum.IsDefined(join)||!Enum.IsDefined(style))throw new InvalidDataException("Invalid SKP paint.");
                        paints.Add(new(){StrokeWidth=width,StrokeMiter=miter,ColorF=color,BlendMode=mode,StrokeCap=cap,StrokeJoin=join,Style=style,IsAntialias=(packed&1)!=0,IsDither=(packed&2)!=0});
                    }break;
                case PathTag:if(paths.Count!=0||Count(r)!=n)throw new InvalidDataException("Invalid SKP path section.");for(int i=0;i<n;i++)paths.Add(ReadPath(r));break;
                case ImageTag:
                    if(images.Count!=0)throw new InvalidDataException("Duplicate image section.");
                    for(int i=0;i<n;i++){uint flags=r.ReadUInt32();if(flags!=0)throw new NotSupportedException("SKP image subset/mipmap/unpremultiplied flags are not implemented.");int length=Count(r,MaximumBytes);var encoded=Bytes(r,length);Padding(r,length);using var data=SKData.CreateCopy(encoded);var image=SKImage.FromEncodedData(data);if(image is null)throw new InvalidDataException("Invalid SKP embedded image.");images.Add(image);}break;
                case 0x626c6f62:case 0x736c7567:case 0x76657274:
                    if(n!=0)throw new NotSupportedException($"Nonempty SKP text/slug/vertex table 0x{tag:X8} is not implemented.");break;
                default:throw new NotSupportedException($"SKP resource section 0x{tag:X8} is not implemented.");
            }
        }
    }
    private static SKPath ReadPath(BinaryReader r)
    {
        uint packed=r.ReadUInt32();int version=(int)(packed&255);if(version is not (4 or 5))throw new NotSupportedException($"SKP path version {version} is not implemented.");var fill=(SKPathFillType)((packed>>8)&3);int kind=(int)(packed>>28);
        if(kind==1){using var rr=ReadRoundRect(r);uint start=r.ReadUInt32();if(start>7)throw new InvalidDataException("Invalid rounded path start index.");int direction=(int)((packed>>26)&3);if(direction>1)throw new InvalidDataException("Invalid rounded path direction.");var result=new SKPath{FillType=fill};try{result.AddRoundRect(rr,(SKPathDirection)direction,start);return result;}catch{result.Dispose();throw;}}
        if(kind!=0)throw new NotSupportedException("Unknown SKP path encoding.");int pn=Count(r,MaximumItems*3),wn=Count(r),vn=Count(r);long needed=(long)pn*8+(long)wn*4+((long)vn+3&~3L);if(needed>r.BaseStream.Length-r.BaseStream.Position)throw new EndOfStreamException();
        var points=new SKPoint[pn];for(int i=0;i<pn;i++)points[i]=new(Scalar(r),Scalar(r));var weights=new float[wn];for(int i=0;i<wn;i++){weights[i]=Scalar(r);if(weights[i]<0)throw new InvalidDataException("Negative conic weight.");}var verbs=Bytes(r,vn);Padding(r,vn);if(version==4)Array.Reverse(verbs);int pi=0,wi=0;var path=new SKPath{FillType=fill};
        SKPoint Point(){if(pi>=pn)throw new InvalidDataException("SKP path point count mismatch.");return points[pi++];}
        try
        {
            foreach(byte verb in verbs)switch(verb){case 0:path.MoveTo(Point());break;case 1:path.LineTo(Point());break;case 2:path.QuadTo(Point(),Point());break;case 3:if(wi>=wn)throw new InvalidDataException("SKP conic count mismatch.");path.ConicTo(Point(),Point(),weights[wi++]);break;case 4:path.CubicTo(Point(),Point(),Point());break;case 5:path.Close();break;default:throw new InvalidDataException("Unknown SKP path verb.");}
            if(pi!=pn||wi!=wn)throw new InvalidDataException("SKP path payload count mismatch.");return path;
        }
        catch{path.Dispose();throw;}
    }
    private static SKRoundRect ReadRoundRect(BinaryReader r)
    {var rect=Rect(r);SKPoint[] radii=new SKPoint[4];for(int i=0;i<4;i++)radii[i]=new(Scalar(r),Scalar(r));var rr=new SKRoundRect();rr.SetRectRadii(rect,radii);return rr;}
    private static SKMatrix Matrix(BinaryReader r)=>new(Scalar(r),Scalar(r),Scalar(r),Scalar(r),Scalar(r),Scalar(r),Scalar(r),Scalar(r),Scalar(r));
    private static SKMatrix Matrix44(BinaryReader r)
    {float[] f=new float[16];for(int i=0;i<16;i++)f[i]=Scalar(r);if(f[2]!=0||f[6]!=0||f[8]!=0||f[9]!=0||f[10]!=1||f[11]!=0||f[14]!=0)throw new NotSupportedException("SKP 3D transforms are not implemented.");return new(f[0],f[4],f[12],f[1],f[5],f[13],f[3],f[7],f[15]);}
    private static SKSamplingOptions Sampling(BinaryReader r)
    {int aniso=Count(r);if(aniso!=0)throw new NotSupportedException("SKP anisotropic image sampling is not implemented.");int cubic=r.ReadInt32();if(cubic==1)return new(new SKCubicResampler(Scalar(r),Scalar(r)));if(cubic!=0)throw new InvalidDataException("Invalid SKP sampling.");var filter=(SKFilterMode)r.ReadInt32();var mip=(SKMipmapMode)r.ReadInt32();if(!Enum.IsDefined(filter)||!Enum.IsDefined(mip))throw new InvalidDataException("Invalid SKP sampling enum.");return new(filter,mip);}
    private static SKPicture Replay(SKRect cull,byte[] bytes,List<SKPaint> paints,List<SKPath> paths,List<SKImage> images)
    {
        using var recorder=new SKPictureRecorder();var canvas=recorder.BeginRecording(cull);using var r=new BinaryReader(new MemoryStream(bytes),Utf8);int count=0;
        SKPaint? Paint(bool required){int i=r.ReadInt32();if(i==0&&!required)return null;if(i<=0||i>paints.Count)throw new InvalidDataException("Invalid SKP paint index.");return paints[i-1];}
        SKPath Path(){int i=r.ReadInt32();if(i<=0||i>paths.Count)throw new InvalidDataException("Invalid SKP path index.");return paths[i-1];}
        SKImage Image(){int i=r.ReadInt32();if(i<0||i>=images.Count)throw new InvalidDataException("Invalid SKP image index.");return images[i];}
        while(r.BaseStream.Position<bytes.Length)
        {
            if(++count>MaximumItems)throw new NotSupportedException("SKP operation budget exceeded.");long start=r.BaseStream.Position;uint bits=r.ReadUInt32();int code=(int)(bits>>24),size=(int)(bits&0xffffff);if(size==0xffffff)size=Count(r,MaximumBytes);if(size<4||size%4!=0||size>bytes.Length-start)throw new InvalidDataException("Invalid SKP operation extent.");long end=start+size;
            switch(code)
            {
                case 30:canvas.Save();break;case 28:if(canvas.SaveCount<=1)throw new InvalidDataException("Unbalanced SKP restore.");canvas.Restore();break;
                case 33:canvas.SetMatrix(Matrix(r));break;case 71:canvas.SetMatrix(Matrix44(r));break;case 5:{var m=Matrix(r);canvas.Concat(in m);break;}case 68:{var m=Matrix44(r);canvas.Concat(in m);break;}
                case 29:canvas.RotateDegrees(Scalar(r));break;case 32:canvas.Scale(Scalar(r),Scalar(r));break;case 34:canvas.Skew(Scalar(r),Scalar(r));break;case 35:canvas.Translate(Scalar(r),Scalar(r));break;
                case 1:{var path=Path();int clip=r.ReadInt32();r.ReadInt32();if((clip&~17)!=0)throw new InvalidDataException("Invalid SKP clip parameters.");canvas.ClipPath(path,(SKClipOperation)(clip&1),(clip&16)!=0);break;}
                case 3:{var rect=Rect(r);int clip=r.ReadInt32();r.ReadInt32();if((clip&~17)!=0)throw new InvalidDataException("Invalid SKP clip parameters.");canvas.ClipRect(rect,(SKClipOperation)(clip&1),(clip&16)!=0);break;}
                case 4:{using var rr=ReadRoundRect(r);int clip=r.ReadInt32();r.ReadInt32();if((clip&~17)!=0)throw new InvalidDataException("Invalid SKP clip parameters.");canvas.ClipRoundRect(rr,(SKClipOperation)(clip&1),(clip&16)!=0);break;}
                case 13:canvas.DrawPaint(Paint(true)!);break;case 21:{var p=Paint(true);canvas.DrawRect(Rect(r),p!);break;}case 12:{var p=Paint(true);canvas.DrawOval(Rect(r),p!);break;}case 14:{var p=Paint(true);canvas.DrawPath(Path(),p!);break;}case 22:{var p=Paint(true);using var rr=ReadRoundRect(r);canvas.DrawRoundRect(rr,p!);break;}
                case 52:{int flags=r.ReadInt32();if((flags&~3)!=0)throw new NotSupportedException("SKP nontrivial save-layer records are not implemented.");SKRect? bounds=(flags&1)!=0?Rect(r):null;var paint=(flags&2)!=0?Paint(true):null;if(bounds is {} b)canvas.SaveLayer(b,paint!);else canvas.SaveLayer(paint);break;}
                case 72:{var p=Paint(false);var im=Image();float x=Scalar(r),y=Scalar(r);canvas.DrawImage(im,x,y,Sampling(r),p);break;}
                case 73:{var p=Paint(false);var im=Image();var src=Rect(r);var dst=Rect(r);var sampling=Sampling(r);int constraint=r.ReadInt32();if((uint)constraint>1)throw new InvalidDataException("Invalid SKP source constraint.");canvas.DrawImage(im,src,dst,sampling,p);break;}
                case 53:{var rect=Rect(r);int n=Count(r,MaximumBytes);var key=Bytes(r,n);if(r.ReadByte()!=0)throw new InvalidDataException("Invalid SKP annotation terminator.");Padding(r,n+1);int length=Count(r,MaximumBytes);using var data=SKData.CreateCopy(Bytes(r,length));Padding(r,length);canvas.DrawAnnotation(rect,Utf8.GetString(key),data);break;}
                case 36:r.BaseStream.Position=end;break;case 63:canvas.Flush();break;
                default:throw new NotSupportedException($"SKP drawing opcode {code} is not implemented.");
            }
            if(r.BaseStream.Position!=end)throw new InvalidDataException("SKP operation payload length mismatch.");
        }
        return recorder.EndRecording();
    }
}
