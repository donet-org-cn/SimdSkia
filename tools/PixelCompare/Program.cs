using System.Text;
using System.Text.Json;

if (args.Length!=3) { Console.Error.WriteLine("PixelCompare <reference-directory> <managed-directory> <report-directory>"); return 1; }
Directory.CreateDirectory(args[2]);
var results=new List<object>();
bool passed=true;
string[] referenceFiles=Directory.GetFiles(args[0],"*.rgba").Order(StringComparer.Ordinal).ToArray();
if (referenceFiles.Length==0) throw new InvalidDataException("No reference scenes found.");
string[] candidateNames=Directory.GetFiles(args[1],"*.rgba").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
if (!referenceFiles.Select(Path.GetFileName).SequenceEqual(candidateNames)) throw new InvalidDataException("Scene lists differ.");
foreach (string file in referenceFiles)
{
    string name=Path.GetFileNameWithoutExtension(file);
    var reference=File.ReadAllBytes(file); var candidate=File.ReadAllBytes(Path.Combine(args[1],name+".rgba"));
    using var metadata=JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(file,".json")));
    int width=metadata.RootElement.GetProperty("Width").GetInt32(),height=metadata.RootElement.GetProperty("Height").GetInt32();
    if (reference.Length!=candidate.Length || reference.Length!=checked(width*height*4)) throw new InvalidDataException(name+": invalid pixel payload dimensions.");
    // Integer geometry must match exactly. Alpha blending allows one code value of fixed-point rounding error.
    int threshold=name.StartsWith("blend-",StringComparison.Ordinal) || name.StartsWith("source-",StringComparison.Ordinal)
        || name.StartsWith("clear-then-blend-",StringComparison.Ordinal) ? 1 : 0;
    if(name.StartsWith("gradient-",StringComparison.Ordinal))threshold=1;
    if(name.StartsWith("rect-aa-",StringComparison.Ordinal))threshold=2;
    if(name.StartsWith("path-aa-",StringComparison.Ordinal)||name.StartsWith("chinese-",StringComparison.Ordinal))threshold=32;
    int max=0,differentPixels=0,failedChannels=0; long total=0;
    var diff=new byte[width*height*3];
    for (int p=0;p<width*height;p++)
    {
        bool different=false;
        for (int c=0;c<4;c++)
        {
            int error=Math.Abs(reference[p*4+c]-candidate[p*4+c]);
            max=Math.Max(max,error); total+=error; different|=error>0;
            if (error>threshold) failedChannels++;
            if (c<3) diff[p*3+c]=(byte)Math.Min(255,error*32);
            else if (error>0) for (int k=0;k<3;k++) diff[p*3+k]=(byte)Math.Max(diff[p*3+k],Math.Min(255,error*32));
        }
        if (different) differentPixels++;
    }
    bool ok=failedChannels==0; passed&=ok;
    results.Add(new { Scene=name,MaxChannelError=max,MeanAbsoluteError=(double)total/reference.Length,DifferentPixels=differentPixels,Threshold=threshold,FailedChannels=failedChannels,Passed=ok });
    using var ppm=File.Create(Path.Combine(args[2],name+"-diff.ppm"));
    ppm.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n")); ppm.Write(diff);
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}: max={max}, changedPixels={differentPixels}, threshold={threshold}");
}
var referenceBehavior=JsonSerializer.Deserialize<SortedDictionary<string,string>>(File.ReadAllText(Path.Combine(args[0],"behavior.json")))!;
var candidateBehavior=JsonSerializer.Deserialize<SortedDictionary<string,string>>(File.ReadAllText(Path.Combine(args[1],"behavior.json")))!;
var behaviorDifferences=referenceBehavior.Keys.Union(candidateBehavior.Keys).Order(StringComparer.Ordinal)
    .Where(k=>!referenceBehavior.TryGetValue(k,out var r) || !candidateBehavior.TryGetValue(k,out var c) || r!=c)
    .Select(k=>new { Probe=k,Reference=referenceBehavior.GetValueOrDefault(k),Candidate=candidateBehavior.GetValueOrDefault(k) }).ToArray();
passed &= behaviorDifferences.Length==0;
Console.WriteLine($"{(behaviorDifferences.Length==0 ? "PASS" : "FAIL")}: {referenceBehavior.Count} baseline behavior probes; {behaviorDifferences.Length} differences.");
File.WriteAllText(Path.Combine(args[2],"results.json"),JsonSerializer.Serialize(new { Backend="CPU raster, including 16x16 antialiasing",Passed=passed,Scenes=results,BehaviorProbes=referenceBehavior.Count,BehaviorDifferences=behaviorDifferences },new JsonSerializerOptions { WriteIndented=true }));
return passed ? 0 : 2;
