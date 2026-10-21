using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using SimdSkia.Benchmarks;

if(args.Length!=2){Console.Error.WriteLine("Report <run-directory> <markdown-path>");return 1;}
string root=Path.GetFullPath(args[0]);string markdownPath=Path.GetFullPath(args[1]);
using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"run.json")));
string date=manifest.RootElement.GetProperty("Date").GetString()??throw new InvalidDataException("Missing run date.");
var workers=Directory.GetFiles(root,"results.json",SearchOption.AllDirectories).Select(path=>(Directory:Path.GetDirectoryName(path)!,Result:JsonSerializer.Deserialize<WorkerResult>(File.ReadAllText(path))??throw new InvalidDataException(path))).ToArray();
if(workers.Length<2)throw new InvalidDataException("Need original/simd GPU workers.");
string[] engines=["original","simd"];
var first=workers[0].Result;
foreach(var worker in workers)
{
    var info=worker.Result.Runtime;
    if(info.Framework!=first.Runtime.Framework||info.OS!=first.Runtime.OS||info.Architecture!=first.Runtime.Architecture||info.Cpu!=first.Runtime.Cpu||info.FontSha256!=first.Runtime.FontSha256||info.ServerGc!=first.Runtime.ServerGc||info.TieredCompilation!=first.Runtime.TieredCompilation||info.ReadyToRun!=first.Runtime.ReadyToRun)throw new InvalidDataException("Worker runtime/font/settings mismatch.");
}
string[] sceneOrder=["clear","rectangles","blend","evenodd","stroke","rect-aa","gradient","image","curve-aa","chinese","curve-changing","chinese-changing"];
var keys=first.Workloads.Select(w=>(w.Scene,w.Width,w.Height)).OrderBy(k=>k.Width).ThenBy(k=>k.Height).ThenBy(k=>Array.IndexOf(sceneOrder,k.Scene)).ToArray();
var rows=new List<Row>();
foreach(var key in keys)
{
    var items=workers.Select(w=>(w.Directory,w.Result.Settings.Engine,w.Result.Settings.Fork,w.Result.Runtime,Workload:w.Result.Workloads.Single(x=>x.Scene==key.Scene&&x.Width==key.Width&&x.Height==key.Height))).ToArray();
    var reference=items.First(x=>x.Engine=="original");var expected=File.ReadAllBytes(Path.Combine(reference.Directory,reference.Workload.PixelsFile));
    foreach(string engine in engines)
    {
        var selected=items.Where(i=>i.Engine==engine).ToArray();if(selected.Length==0)throw new InvalidDataException("Missing engine: "+engine);
        var ticks=new List<double>();var means=new List<double>();long allocations=0,operations=0;double cpuMilliseconds=0,wallMilliseconds=0;int gen0=0,gen1=0,gen2=0,maxError=0;long errors=0,channels=0,failedChannels=0;
        foreach(var item in selected)
        {
            byte[] actual=File.ReadAllBytes(Path.Combine(item.Directory,item.Workload.PixelsFile));if(actual.Length!=expected.Length)throw new InvalidDataException("Pixel payload size mismatch.");
            for(int i=0;i<actual.Length;i++){int e=Math.Abs(actual[i]-expected[i]);maxError=Math.Max(maxError,e);errors+=e;channels++;if(e>item.Workload.AllowedPixelError)failedChannels++;}
            var payload=File.ReadAllBytes(Path.Combine(item.Directory,item.Workload.LatencyFile));if(payload.Length%8!=0)throw new InvalidDataException("Invalid tick payload.");
            foreach(long tick in MemoryMarshal.Cast<byte,long>(payload.AsSpan()))ticks.Add(tick*1000.0/item.Runtime.StopwatchFrequency);
            if(payload.Length/8!=item.Workload.Samples.Sum(s=>s.Operations))throw new InvalidDataException("Frame count and latency payload differ.");
            foreach(var sample in item.Workload.Samples){cpuMilliseconds+=sample.CpuMilliseconds??sample.TotalMilliseconds*sample.CpuUtilizationPercent/100;wallMilliseconds+=sample.TotalMilliseconds;means.Add(sample.TotalMilliseconds/sample.Operations);operations+=sample.Operations;allocations+=sample.AllocatedBytes;gen0+=sample.Gen0;gen1+=sample.Gen1;gen2+=sample.Gen2;}
        }
        double median=Quantile(means,.5),cv=StdDev(means)/means.Average();bool pixelPass=failedChannels==0;
        rows.Add(new(key.Scene,key.Width,key.Height,engine,selected.Length,means.Count,operations,median,Quantile(means,.25),Quantile(means,.75),Quantile(ticks,.5),Quantile(ticks,.95),1000/median,allocations/(double)operations,gen0,gen1,gen2,
            100*cpuMilliseconds/wallMilliseconds,cpuMilliseconds/operations,100*cpuMilliseconds/wallMilliseconds/first.Runtime.LogicalProcessors,
            Quantile(selected.Select(x=>(double)x.Workload.WarmResident.PrivateBytes),.5),Quantile(selected.Select(x=>(double)x.Workload.WarmResident.WorkingSetBytes),.5),Quantile(selected.Select(x=>(double)x.Workload.AfterGc.ManagedLiveBytes),.5),Quantile(selected.Select(x=>(double)(x.Workload.WarmResident.PrivateBytes-x.Workload.BeforeSetup.PrivateBytes)),.5),
            selected.Max(x=>x.Workload.Samples.Max(s=>s.Memory.ProcessPeakWorkingSetBytes)),cv,maxError,channels==0?0:(double)errors/channels,selected[0].Workload.AllowedPixelError,pixelPass));
    }
}
double Quantile(IEnumerable<double> values,double q){var sorted=values.Order().ToArray();if(sorted.Length==0)throw new InvalidDataException("Empty samples.");double index=(sorted.Length-1)*q;int lo=(int)index,hi=(int)Math.Ceiling(index);return sorted[lo]+(sorted[hi]-sorted[lo])*(index-lo);}
double StdDev(IEnumerable<double> values){var a=values.ToArray();double mean=a.Average();return Math.Sqrt(a.Sum(x=>(x-mean)*(x-mean))/Math.Max(1,a.Length-1));}
string Number(double value,string format="F3")=>value.ToString(format,CultureInfo.InvariantCulture);
Row Get(string scene,int width,int height,string engine)=>rows.Single(r=>r.Scene==scene&&r.Width==width&&r.Height==height&&r.Engine==engine);
var output=new{Date=date,Scope="Vulkan GPU round-trip (draw + submit + fence + readback)",BaselinePackage="SkiaSharp 3.119.4",Runtime=first.Runtime,Workers=workers.Select(w=>new{w.Result.Settings,w.Result.Runtime,EngineOptions=w.Result.Runtime.ExplicitSimd}),Rows=rows,Limitations=new[]{"Per-frame managed allocations only; native/driver allocations not counted.","Process memory includes runtime, Vulkan driver mappings and earlier workloads.","P95 is individual frame latency; table time is median of batch means.","CPU utilization is process TotalProcessorTime over wall clock, normalized to one logical core.","Each frame includes full GPU round-trip: command record, submit, fence wait and host readback copy.","Fixed scenes reuse texture/caches across frames; -changing scenes force coverage misses.","Quality gate compares against the original's GPU output as ground truth."}};
File.WriteAllText(Path.Combine(root,"summary.json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
var csv=new StringBuilder("scene,width,height,engine,forks,samples,operations,median_ms,p50_ms,p95_ms,ops_per_second,managed_bytes_per_op,gen0,gen1,gen2,cpu_percent,cpu_ms_per_op,machine_cpu_percent,private_mib,working_set_mib,retained_managed_mib,setup_private_delta_mib,process_peak_working_set_mib,cv,max_pixel_error,mean_pixel_error,allowed_error,pixel_pass\n");
foreach(var row in rows)csv.AppendLine(string.Join(',',row.Scene,row.Width,row.Height,row.Engine,row.Forks,row.Samples,row.Operations,Number(row.MedianMs,"F6"),Number(row.P50Ms,"F6"),Number(row.P95Ms,"F6"),Number(row.OpsPerSecond),Number(row.ManagedBytesPerOp),row.Gen0,row.Gen1,row.Gen2,Number(row.CpuPercent,"F1"),Number(row.CpuMsPerOp,"F6"),Number(row.MachineCpuPercent,"F2"),Number(row.PrivateBytes/1048576),Number(row.WorkingSetBytes/1048576),Number(row.RetainedManagedBytes/1048576),Number(row.SetupPrivateDelta/1048576),Number(row.PeakWorkingSetBytes/1048576.0),Number(row.Cv),row.MaxPixelError,Number(row.MeanPixelError),row.AllowedPixelError,row.PixelPass));
File.WriteAllText(Path.Combine(root,"results.csv"),csv.ToString());
var originalDescriptions=workers.Where(w=>w.Result.Settings.Engine=="original").Select(w=>w.Result.Runtime.ExplicitSimd).Distinct().ToArray();
var simdDescriptions=workers.Where(w=>w.Result.Settings.Engine=="simd").Select(w=>w.Result.Runtime.ExplicitSimd).Distinct().ToArray();
var text=new StringBuilder($"# 原版与 SimdSkia GPU(Vulkan)性能对比：{date}\n\n");
text.AppendLine($"实际执行：{first.Runtime.Cpu}；{first.Runtime.OS}；{first.Runtime.Framework} / {first.Runtime.Architecture}，逻辑 CPU {first.Runtime.LogicalProcessors}。原版包 SkiaSharp 3.119.4。");
text.AppendLine($"原版路径：{originalDescriptions.FirstOrDefault() ?? "Native Skia Vulkan/GRContext"}；SimdSkia 路径：{simdDescriptions.FirstOrDefault() ?? "SimdSkia Vulkan compute"}。");
text.AppendLine($"每种实现 {workers.Count(w=>w.Result.Settings.Engine=="original")} 个独立进程；每项每进程 {first.Settings.Samples} 批，目标每批 {first.Settings.SampleMilliseconds} ms，预热至少 {first.Settings.WarmupMilliseconds} ms 且 3 帧，再校准。进程顺序/场景顺序打乱，串行执行。TieredCompilation={first.Runtime.TieredCompilation}。");
var largest=keys.OrderByDescending(k=>(long)k.Width*k.Height).First();
text.AppendLine($"主要结论：以 {largest.Width}×{largest.Height} 为例，以下只列通过画质门槛的场景。\n");
foreach(string scene in sceneOrder)
{
    var candidate=rows.SingleOrDefault(r=>r.Scene==scene&&r.Width==largest.Width&&r.Height==largest.Height&&r.Engine=="simd");
    if(candidate is not null&&candidate.PixelPass){var original=Get(scene,largest.Width,largest.Height,"original");text.AppendLine($"- {scene}：原版 {Number(original.MedianMs)} ms，SimdSkia {Number(candidate.MedianMs)} ms，为原版耗时的 {Number(candidate.MedianMs/original.MedianMs,"F2")} 倍。");}
}
text.AppendLine();
text.AppendLine("这次只比较同机 Vulkan GPU round-trip（绘制全部命令 + 提交 + 等待 fence + 回读主机）。未与 CPU 光栅结果混用；CPU 结果见 PERFORMANCE.md。\n");
string relative=Path.GetRelativePath(Path.GetDirectoryName(markdownPath)!,root).Replace('\\','/');
text.AppendLine($"原始数据：[JSON]({relative}/summary.json)、[CSV]({relative}/results.csv)、[配置]({relative}/run.json)。工作进程数据、逐帧 ticks、像素和日志保留在同目录。\n");
foreach(var size in keys.Select(k=>(k.Width,k.Height)).Distinct())
{
    text.AppendLine($"## {size.Width}×{size.Height}：时间与质量\n");
    text.AppendLine("表内 ms 为多进程所有批均值的中位数；‘慢倍数’=SimdSkia / 原版，低于 1 表示更快。质量未过不给性能胜负结论。\n");
    text.AppendLine("| 场景 | 原版 ms | SimdSkia ms | 慢倍数 | SimdSkia P95 ms | 质量 |\n| --- | ---: | ---: | ---: | ---: | --- |");
    foreach(var key in keys.Where(k=>k.Width==size.Width&&k.Height==size.Height))
    {
        var original=Get(key.Scene,key.Width,key.Height,"original");var simd=Get(key.Scene,key.Width,key.Height,"simd");
        string quality=!simd.PixelPass?$"诊断：max {simd.MaxPixelError}>{simd.AllowedPixelError}":$"通过 max={simd.MaxPixelError}";
        text.AppendLine($"| {key.Scene} | {Number(original.MedianMs)} | {Number(simd.MedianMs)} | {(simd.PixelPass?Number(simd.MedianMs/original.MedianMs,"F2")+"×":"—")} | {Number(simd.P95Ms)} | {quality} |");
    }
    text.AppendLine();text.AppendLine($"![GPU latency chart]({relative}/latency-{size.Width}x{size.Height}.svg)\n");
    text.AppendLine("### 托管分配、进程内存、CPU 使用率\n\n下表内存绝对值含 runtime、Vulkan 驱动映射与先前场景，不等于显存/渲染缓冲用量。CPU ms/帧为各批进程总处理器时间累计/总帧数；CPU% 按累计 CPU 时间/累计墙钟加权，单逻辑核归一（100% = 一核满载）。JSON/CSV 同时提供除以逻辑核数的机器占用。满速占用不等于固定帧率应用占用，Windows 处理器时间有计数粒度，短批内 0 不代表不消耗 CPU。\n\n| 场景 | 原 B/op | Simd B/op | 原 CPU ms/帧 | Simd CPU ms/帧 | 原 CPU% 单核 | Simd CPU% 单核 | 原 private MiB | Simd private MiB | 原 working set MiB | Simd working set MiB |\n| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
    foreach(var key in keys.Where(k=>k.Width==size.Width&&k.Height==size.Height))
    {
        var r=Get(key.Scene,key.Width,key.Height,"simd");var o=Get(key.Scene,key.Width,key.Height,"original");
        text.AppendLine($"| {key.Scene} | {Number(o.ManagedBytesPerOp,"F0")} | {Number(r.ManagedBytesPerOp,"F0")} | {Number(o.CpuMsPerOp)} | {Number(r.CpuMsPerOp)} | {Number(o.CpuPercent,"F1")} | {Number(r.CpuPercent,"F1")} | {Number(o.PrivateBytes/1048576,"F1")} | {Number(r.PrivateBytes/1048576,"F1")} | {Number(o.WorkingSetBytes/1048576,"F1")} | {Number(r.WorkingSetBytes/1048576,"F1")} |");
    }
    text.AppendLine("\n完整 op/s、GC 次数、托管 retained、进程历史峰值、IQR 和 CV 在 CSV/JSON。\n");
    text.AppendLine();
    WriteChart(size.Width,size.Height);
}
text.AppendLine("## 解读边界\n\n- 每个‘op’是一整帧 GPU round-trip：绘制全部命令 + flush 提交 + fence 等待 + 主机回读拷贝。rectangles/blend 每帧 65 条命令（1 clear+64 draw），chinese 含 4 行文字，其余 1 项。几何/字体/纹理准备不在计时内；每帧的命令录制、提交、等待与回读在计时内。\n- 双方目标像素格式 RGBA8888/Premul，同一 GPU 上各自独立初始化 Vulkan（原版用 graphics queue + GRContext 光栅后端；SimdSkia 用 compute queue + 自研 compute shader 管线）。\n- 固定场景允许正常复用纹理/缓存（SimdSkia 纹理缓存 ≤32 个/64MiB LRU）；curve-changing/chinese-changing 场景每帧移动 1/16 像素、97 个位置循环，强制覆盖率/文字缓存持续未命中。变化场景像素 gate 检查 frame=0。\n- 质量门槛以原版 GPU 输出为基准逐通道比较；AA/字体必须检查本轮相同尺寸的像素 gate；失败的时间保留为诊断数据。未提高容差，也未降低画质。\n- B/op 是线程托管分配，不含 native malloc 与显存。private/working set 是整个进程，包含 runtime、Vulkan 驱动映射、native/字体缓存和先前场景，不能当显存或纯渲染缓冲内存公平排名；peak working set 是进程历史峰值。\n- P95 是实际单帧延迟，包含 GC/调度/驱动抖动；耗时列是批均值中位数，两者定义不同。\n- 帧时间由 CPU 计时（Stopwatch）测得，包含每帧回读同步；驱动内部排队可能引入噪声。样本不控制 OS power plan/Turbo/其他用户进程，不给所有硬件的优势承诺。\n");
var measured=rows.Where(r=>r.Engine=="simd").ToArray();text.AppendLine($"\nGPU pixel acceptance: {measured.Count(r=>r.PixelPass)}/{measured.Length}. Failed rows remain diagnostic; no threshold was relaxed.");
Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);File.WriteAllText(markdownPath,text.ToString());Console.WriteLine($"Report: {markdownPath}; {rows.Count} engine/workload rows.");return measured.All(r=>r.PixelPass)?0:2;

void WriteChart(int width,int height)
{
    var scenes=keys.Where(k=>k.Width==width&&k.Height==height).Select(k=>k.Scene).ToArray();int h=100+scenes.Length*76;
    var svg=new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1100\" height=\"{h}\" viewBox=\"0 0 1100 {h}\"><rect width=\"1100\" height=\"{h}\" fill=\"white\"/><g font-family=\"Arial,sans-serif\" font-size=\"12\" fill=\"#243247\"><text x=\"20\" y=\"28\" font-size=\"20\">GPU {width}x{height}: median batch mean (log ms; lower is better)</text>");
    double minimum=Math.Floor(Math.Log10(rows.Where(r=>r.Width==width&&r.Height==height).Min(r=>r.MedianMs)))-.2,maximum=Math.Ceiling(Math.Log10(rows.Where(r=>r.Width==width&&r.Height==height).Max(r=>r.MedianMs)))+.2;
    double X(double value)=>220+(Math.Log10(value)-minimum)/(maximum-minimum)*680;
    for(int exponent=(int)Math.Ceiling(minimum);exponent<=maximum;exponent++){double x=X(Math.Pow(10,exponent));svg.Append($"<path d=\"M {Number(x)} 70 V {h-20}\" stroke=\"#e6ebf2\"/><text x=\"{Number(x)}\" y=\"58\">{Number(Math.Pow(10,exponent),"G4")} ms</text>");}
    string[] colors=["#4263eb","#f59f00"];
    for(int i=0;i<scenes.Length;i++)
    {
        int y=95+i*76;var simd=Get(scenes[i],width,height,"simd");svg.Append($"<text x=\"20\" y=\"{y+18}\">{SecurityElement.Escape(scenes[i])}{(simd.PixelPass?"":" (diagnostic)")}</text>");
        for(int e=0;e<2;e++){var row=Get(scenes[i],width,height,engines[e]);double right=X(row.MedianMs);svg.Append($"<rect x=\"220\" y=\"{y+e*17}\" width=\"{Number(Math.Max(1,right-220))}\" height=\"12\" fill=\"{colors[e]}\"/><text x=\"{Number(right+6)}\" y=\"{y+e*17+10}\">{engines[e]} {Number(row.MedianMs)} ms</text>");}
    }
    svg.Append("</g></svg>");File.WriteAllText(Path.Combine(root,$"latency-{width}x{height}.svg"),svg.ToString());
}

sealed record Row(string Scene,int Width,int Height,string Engine,int Forks,int Samples,long Operations,double MedianMs,double Q1Ms,double Q3Ms,double P50Ms,double P95Ms,double OpsPerSecond,double ManagedBytesPerOp,int Gen0,int Gen1,int Gen2,double CpuPercent,double CpuMsPerOp,double MachineCpuPercent,double PrivateBytes,double WorkingSetBytes,double RetainedManagedBytes,double SetupPrivateDelta,long PeakWorkingSetBytes,double Cv,int MaxPixelError,double MeanPixelError,int AllowedPixelError,bool PixelPass);
