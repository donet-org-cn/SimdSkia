using SkiaSharp;
using System.Buffers.Binary;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);int checks=0,unsupported=0,valid=0;var random=new Random(3181194);void Check(bool b){checks++;if(!b)throw new Exception("SKP validation "+checks);}
using var rec=new SKPictureRecorder();using(var p=new SKPaint{Color=SKColors.Red})rec.BeginRecording(new(0,0,16,16)).DrawRect(2,3,10,9,p);using var pic=rec.EndRecording();using var data=pic.Serialize();var good=data.ToArray();
for(int n=0;n<good.Length;n++){using var read=SKPicture.Deserialize(good.AsSpan(0,n));Check(read is null);}
for(int i=0;i<3000;i++)
{
 var bytes=i%2==0?good.ToArray():new byte[random.Next(512)];if(i%2!=0)random.NextBytes(bytes);else for(int k=0;k<random.Next(1,5);k++)bytes[random.Next(bytes.Length)]=(byte)random.Next(256);
 try{using var read=SKPicture.Deserialize(bytes);if(read is not null){valid++;using var b=new SKBitmap(16,16);using var c=new SKCanvas(b);c.DrawPicture(read);}checks++;}catch(NotSupportedException){unsupported++;checks++;}
}
using(var g=new MemoryStream()){pic.Serialize(g);long expected=g.Length;g.WriteByte(42);g.Position=0;using var read=SKPicture.Deserialize(g);Check(read is not null);Check(g.Position==expected);Check(g.ReadByte()==42);}
using(var s=new ShortReads(good)){using var read=SKPicture.Deserialize(s);Check(read is not null);Check(s.CanRead);}
var huge=good.ToArray();BinaryPrimitives.WriteInt32LittleEndian(huge.AsSpan(33),int.MaxValue);using(var read=SKPicture.Deserialize(huge))Check(read is null);
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks,unsupported,valid,seed=3181194}));Console.WriteLine($"PASS {checks} SKP malformed/short-read/stream-boundary checks ({unsupported} explicit unsupported mutations)");
sealed class ShortReads(byte[] bytes):MemoryStream(bytes,false)
{public override int Read(Span<byte> b)=>base.Read(b[..Math.Min(b.Length,3)]);public override int Read(byte[] b,int offset,int count)=>base.Read(b,offset,Math.Min(count,3));}
