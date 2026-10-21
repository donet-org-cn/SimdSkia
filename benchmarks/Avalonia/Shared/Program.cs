if(args.Length==3&&args[0]=="--make-labels")
{SimdSkia.Benchmarks.Workload.MakeLabels(args[1],args[2]);return 0;}
if(args.Length==5&&args[0]=="--probe")
{
    using(var b=new SkiaSharp.SKBitmap(1,1))using(var c=new SkiaSharp.SKCanvas(b))c.Clear();
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    using var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();long initialWorkingSet=process.WorkingSet64;
    long allocated=GC.GetAllocatedBytesForCurrentThread();long begin=System.Diagnostics.Stopwatch.GetTimestamp();
    using var workload=new SimdSkia.Benchmarks.Workload(args[1],1024,768,args[3],args[2]);
    double construction=System.Diagnostics.Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
    var frames=new double[9];for(int i=0;i<frames.Length;i++){begin=System.Diagnostics.Stopwatch.GetTimestamp();workload.Render(i);frames[i]=System.Diagnostics.Stopwatch.GetElapsedTime(begin).TotalMilliseconds;}
    long totalAllocated=GC.GetAllocatedBytesForCurrentThread()-allocated;process.Refresh();
    var result=new{Scene=args[1],ConstructionMilliseconds=construction,FrameMilliseconds=frames,ConstructionAndNineFramesAllocatedBytes=totalAllocated,InitialWorkingSetBytes=initialWorkingSet,FinalWorkingSetBytes=process.WorkingSet64,PrivateBytes=process.PrivateMemorySize64};
    System.IO.File.WriteAllText(args[4],System.Text.Json.JsonSerializer.Serialize(result,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return 0;
}
return SimdSkia.Benchmarks.Worker.Run(args);
