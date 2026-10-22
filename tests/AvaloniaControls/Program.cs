using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SimdSkia.ControlValidation;
#if MANAGED
using SimdSkia.Avalonia;
#endif

string Value(string key,string fallback){int index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:fallback;}
string font=Path.GetFullPath(Value("--font","baseline/fonts/ZCOOLXiaoWei-Regular.ttf"));
string output=Path.GetFullPath(Value("--output","artifacts/avalonia-controls/test"));Directory.CreateDirectory(output);
int frames=int.Parse(Value("--frames","60"),System.Globalization.CultureInfo.InvariantCulture);
double scale=double.Parse(Value("--scale","1"),System.Globalization.CultureInfo.InvariantCulture);
string scenario=Value("--scene","controls");
if(scenario is not ("controls" or "scroll"))throw new ArgumentException("Unknown benchmark scene: "+scenario);
if(frames<=0||!double.IsFinite(scale)||scale<=0||scale>4)throw new ArgumentOutOfRangeException(nameof(frames),"Frames must be positive and scale in (0,4].");
bool nativeDefaultOpacity=args.Contains("--native-default-opacity");
string systemFamily=Value("--system-font-family","");
if(systemFamily.Length!=0&&!args.Contains("--bench"))throw new ArgumentException("System font mode is a benchmark; fixture validation must keep its fixed font.");
var builder=AppBuilder.Configure<TestApp>().UseStandardRuntimePlatformSubsystem().UseManagedHeadless();
#if MANAGED
const string engine="SimdSkia";
if(systemFamily.Length==0)builder.UseSimdSkia(o=>o.FontFiles.Add(font));else builder.UseSimdSkia();
if(nativeDefaultOpacity)throw new ArgumentException("Native default opacity measurements must use the isolated reference process.");
#else
const string engine="Avalonia.Skia";builder.UseSkia().UseHarfBuzz();
#endif
builder.SetupWithoutStarting();
if(args.Contains("--tile-cache-probe")){TileCacheProbe.Run(output);return;}
FontFamily selectedFamily;
if(systemFamily.Length==0)
{
    var collection=new EmbeddedFontCollection(new Uri("fonts:Validation"),new Uri(font));
    if(collection.Count==0)throw new InvalidOperationException("Font file was not registered.");
    global::Avalonia.Media.FontManager.Current.AddFontCollection(collection);selectedFamily=collection[0];
}
else
{
    var manager=AvaloniaLocator.Current.GetRequiredService<IFontManagerImpl>();
    if(!manager.GetInstalledFontFamilyNames().Contains(systemFamily,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Requested installed family is missing.");
    selectedFamily=new FontFamily(systemFamily);
}
if(args.Contains("--text-probe")){TextProbe.Run(output,selectedFamily);return;}
if(args.Contains("--clip-probe")){TextProbe.Clips(output);return;}
if(args.Contains("--feature-probe")){FeatureProbe.Run(output);return;}
if(args.Contains("--dump-shaping"))
{
    var shaper=AvaloniaLocator.Current.GetRequiredService<ITextShaperImpl>();var face=new Typeface(selectedFamily).GlyphTypeface;
    var snapshots=new List<object>();
    foreach(double size in new[]{12d,18,27})foreach(string text in new[]{"AVATAR To WA","office fi fl","SimdSkia 托管渲染 / Managed Avalonia","中文界面 | Avalonia rendering | SimdSkia CPU 123.45","中文，测试。English!"})
    {
        using var shaped=shaper.ShapeText(text.AsMemory(),new(face,size));snapshots.Add(new{text,size,glyphs=shaped.Select(g=>new{g.GlyphIndex,g.GlyphCluster,g.GlyphAdvance,x=g.GlyphOffset.X,y=g.GlyphOffset.Y}).ToArray()});
    }
    File.WriteAllText(Path.Combine(output,"shaping.json"),JsonSerializer.Serialize(snapshots,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Wrote {snapshots.Count} real {engine} shaping snapshots.");return;
}
var scene=scenario=="controls"?ControlScene.Create(fullOpacity:!nativeDefaultOpacity):ControlScene.CreateList();
TextElement.SetFontFamily(scene,selectedFamily);
var host=new Window{Content=scene,Width=1000,Height=720};host.Show();Dispatcher.UIThread.RunJobs();ControlScene.Prepare(scene);
int controls=scene.GetVisualDescendants().OfType<Button>().Count();
if(scenario=="controls"&&(controls<1||!scene.GetVisualDescendants().OfType<TextBox>().Any(t=>t.GetVisualChildren().Any())))throw new InvalidOperationException("Real control templates were not applied.");
using var bitmap=new RenderTargetBitmap(new PixelSize((int)(1000*scale),(int)(720*scale)),new Vector(96*scale,96*scale));
var first=Stopwatch.StartNew();bitmap.Render(scene);first.Stop();
var warm=Stopwatch.StartNew();do{bitmap.Render(scene);}while(warm.Elapsed.TotalSeconds<2.5);
#if MANAGED
if(!args.Contains("--bench"))Validation.Run(host,scene,bitmap,font,output);
if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name is "SkiaSharp" or "HarfBuzzSharp" or "Avalonia.Skia"))throw new InvalidOperationException("Native reference renderer/font backend loaded into managed process.");
#endif
var frameTicks=new long[frames];GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
var process=Process.GetCurrentProcess();process.Refresh();long wsBefore=process.WorkingSet64,privateBefore=process.PrivateMemorySize64,allocated=GC.GetTotalAllocatedBytes(true);double cpuBefore=process.TotalProcessorTime.TotalMilliseconds;
int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
var sw=Stopwatch.StartNew();
for(int i=0;i<frames;i++)
{
    long started=Stopwatch.GetTimestamp();
    if(scenario=="scroll")scene.RenderTransform=new TranslateTransform(0,-(i%17));
    bitmap.Render(scene);
    frameTicks[i]=Stopwatch.GetTimestamp()-started;
}
sw.Stop();double cpu=process.TotalProcessorTime.TotalMilliseconds-cpuBefore;long allocation=GC.GetTotalAllocatedBytes(true)-allocated;process.Refresh();
gc0=GC.CollectionCount(0)-gc0;gc1=GC.CollectionCount(1)-gc1;gc2=GC.CollectionCount(2)-gc2;
long ws=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,peak=process.PeakWorkingSet64,heap=GC.GetTotalMemory(false);long retainedHeap=GC.GetTotalMemory(true);
Array.Sort(frameTicks);double Percentile(double p)=>frameTicks[Math.Clamp((int)Math.Ceiling(p*frames)-1,0,frames-1)]*1000d/Stopwatch.Frequency;
var result=new{engine,scenario,frames,scale,opacityMode=nativeDefaultOpacity?"native default per draw":"full group",width=bitmap.PixelSize.Width,height=bitmap.PixelSize.Height,millisecondsPerFrame=sw.Elapsed.TotalMilliseconds/frames,framesPerSecond=frames/sw.Elapsed.TotalSeconds,p50Ms=Percentile(.5),p95Ms=Percentile(.95),p99Ms=Percentile(.99),maximumFrameMs=Percentile(1),gc0,gc1,gc2,cpuMillisecondsPerFrame=cpu/frames,cpuPercentOneCore=100*cpu/sw.Elapsed.TotalMilliseconds,allocatedBytesPerFrame=(double)allocation/frames,workingSetMiB=ws/1048576d,privateMiB=privateBytes/1048576d,peakProcessWorkingSetMiB=peak/1048576d,workingSetBeforeMiB=wsBefore/1048576d,privateBeforeMiB=privateBefore/1048576d,managedHeapUsedMiB=heap/1048576d,managedRetainedAfterGcMiB=retainedHeap/1048576d,firstRenderMs=first.Elapsed.TotalMilliseconds,fontMode=systemFamily.Length==0?"fixed file":"system",fontFamily=selectedFamily.Name,fontSha256=systemFamily.Length==0?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(font))):null,runtime=Environment.Version.ToString(),processors=Environment.ProcessorCount,os=RuntimeInformation.OSDescription};
var json=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(Path.Combine(output,"metrics.json"),json);Console.WriteLine(json);
// Audit the actual selected outline/metrics tables after timing, including
// system TTC faces. Native and managed workers must use the same font data.
var fontAudit=new Dictionary<string,string>();
var auditFace=new Typeface(selectedFamily).GlyphTypeface.PlatformTypeface;
foreach(string tag in new[]{"glyf","cmap","head","hmtx","OS/2"})
    if(auditFace.TryGetTable(new OpenTypeTag((uint)tag[0]<<24|(uint)tag[1]<<16|(uint)tag[2]<<8|tag[3]),out var table))fontAudit[tag]=Convert.ToHexString(SHA256.HashData(table.Span));
File.WriteAllText(Path.Combine(output,"font-tables.json"),JsonSerializer.Serialize(fontAudit));
// Diagnostic runs pause only after measurements and a full collection. This lets
// heap tools inspect retained renderer state without entering benchmark timings.
if(args.Contains("--memory-wait"))
{
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    File.WriteAllText(Path.Combine(output,"diagnostic-ready.txt"),Environment.ProcessId.ToString());
    var wait=Stopwatch.StartNew();
    while(!File.Exists(Path.Combine(output,"diagnostic-continue.txt"))&&wait.Elapsed.TotalMinutes<5)Thread.Sleep(100);
}
using(var stream=File.Create(Path.Combine(output,"scene.png")))bitmap.Save(stream,PngBitmapEncoderOptions.Default);
var pixels=new byte[checked(bitmap.PixelSize.Width*bitmap.PixelSize.Height*4)];var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);
try{bitmap.CopyPixels(new PixelRect(0,0,bitmap.PixelSize.Width,bitmap.PixelSize.Height),pin.AddrOfPinnedObject(),pixels.Length,bitmap.PixelSize.Width*4);}finally{pin.Free();}
if(bitmap.Format==PixelFormats.Bgra8888)for(int i=0;i<pixels.Length;i+=4)(pixels[i],pixels[i+2])=(pixels[i+2],pixels[i]);
File.WriteAllBytes(Path.Combine(output,"scene.rgba"),pixels);
host.Close();
#if MANAGED
if(Environment.GetEnvironmentVariable("SIMD_SKIA_PROFILE")=="1")
{
    var timings=System.Reflection.Assembly.Load("SimdSkia.Avalonia").GetType("SimdSkia.Avalonia.SimdSkiaDiagnostics")?.GetMethod("GetDrawingTimings")?.Invoke(null,null);
    Console.WriteLine("PROFILE "+JsonSerializer.Serialize(timings,new JsonSerializerOptions{IncludeFields=true}));
}
#endif

sealed class TestApp:Application {public override void Initialize()=>Styles.Add(new FluentTheme());}
