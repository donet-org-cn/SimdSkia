namespace SimdSkia.Benchmarks;

public sealed record BenchmarkSettings(string Engine,string Output,string FixtureDirectory,string FontPath,int Fork,int Samples,int SampleMilliseconds,int WarmupMilliseconds,int Seed,string Sizes,string Scenes);
public sealed record RuntimeDetails(string Framework,string OS,string Architecture,string Cpu,int LogicalProcessors,bool ServerGc,string AssemblyIdentity,string AssemblySha256,string FontSha256,string ExplicitSimd,string? TieredCompilation,string? ReadyToRun,long StopwatchFrequency);
public sealed record MemorySnapshot(long ManagedLiveBytes,long ManagedHeapBytes,long PrivateBytes,long WorkingSetBytes,long ProcessPeakWorkingSetBytes);
public sealed record SampleResult(int Operations,double TotalMilliseconds,long AllocatedBytes,int Gen0,int Gen1,int Gen2,MemorySnapshot Memory,double CpuUtilizationPercent,double? CpuMilliseconds=null);
public sealed record WorkloadResult(string Scene,int Width,int Height,string PixelFormat,string AlphaType,int OperationsPerFrame,int AllowedPixelError,bool CompressionComparable,int IterationsPerSample,double SetupMilliseconds,MemorySnapshot BeforeSetup,MemorySnapshot WarmResident,MemorySnapshot AfterGc,SampleResult[] Samples,string LatencyFile,string PixelsFile,long EncodedBytes,long InputEncodedBytes,string InputSha256);
public sealed record WorkerResult(BenchmarkSettings Settings,RuntimeDetails Runtime,WorkloadResult[] Workloads);
