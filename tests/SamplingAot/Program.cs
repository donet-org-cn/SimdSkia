using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

if(args.Contains("--inspect")){Console.WriteLine("READY");Console.ReadLine();}
Console.WriteLine($"NativeAot={!RuntimeFeature.IsDynamicCodeSupported}; AVX2={Avx2.IsSupported}; SSE41={Sse41.IsSupported}");
Console.WriteLine($"PASS {SamplingValidation.Run()} sampling checks.");
