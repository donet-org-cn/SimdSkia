param([string]$Run='artifacts/velashell-compat-2026-10-27')
$ErrorActionPreference='Stop'
$simdRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runRoot=[IO.Path]::GetFullPath((Join-Path $simdRoot $Run))
$evidenceRoot=Join-Path $simdRoot 'docs/evidence/velashell-2026-10-27'
New-Item -ItemType Directory -Path $evidenceRoot -Force|Out-Null
Add-Type -TypeDefinition @'
using System;
public sealed class VelaPixelDifference {
    public int Maximum {get;set;}
    public double Rmse {get;set;}
    public long DifferentPixels {get;set;}
    public bool Passed {get;set;}
    public static VelaPixelDifference Compare(byte[] a,byte[] b) {
        if(a.Length!=b.Length||a.Length%4!=0)throw new ArgumentException("Pixel sizes differ");
        int max=0;double sum=0;long different=0;
        for(int i=0;i<a.Length;i+=4){bool changed=false;for(int c=0;c<4;c++){int e=Math.Abs(a[i+c]-b[i+c]);sum+=(double)e*e;if(e>max)max=e;if(e!=0)changed=true;}if(changed)different++;}
        double rmse=Math.Sqrt(sum/a.Length);
        return new VelaPixelDifference {Maximum=max,Rmse=rmse,DifferentPixels=different,Passed=max<=32&&rmse<=2};
    }
}
'@
function Read-Result([string]$name){Get-Content -LiteralPath (Join-Path $runRoot "$name/result.json") -Raw -Encoding UTF8|ConvertFrom-Json}
$original=Read-Result 'original-final'
$backends=@{}
$quality=[Collections.Generic.List[object]]::new()
foreach($name in @('cpu-final','direct2d-final','vulkan-final')){
    $actual=Read-Result $name
    $backends[$name]=$actual
    foreach($scene in $actual.scenes){
        if(!$scene.passed){continue}
        $a=Join-Path $runRoot ('original-final/'+$scene.scene+'.bgra')
        $b=Join-Path $runRoot ($name+'/'+$scene.scene+'.bgra')
        if(!(Test-Path -LiteralPath $a)){continue}
        try{$difference=[VelaPixelDifference]::Compare([IO.File]::ReadAllBytes($a),[IO.File]::ReadAllBytes($b));$quality.Add([pscustomobject]@{backend=$name;scene=$scene.scene;maxChannelError=$difference.Maximum;rmse=$difference.Rmse;differentPixels=$difference.DifferentPixels;passed=$difference.Passed})}
        catch{$quality.Add([pscustomobject]@{backend=$name;scene=$scene.scene;passed=$false;error=$_.Exception.Message})}
    }
}
$tests=[Collections.Generic.List[object]]::new()
foreach($trx in Get-ChildItem -LiteralPath $runRoot -Recurse -Filter '*.trx'|Where-Object {$_.FullName -notmatch '\\VelaShell\\'}){
    [xml]$doc=Get-Content -LiteralPath $trx.FullName -Raw -Encoding UTF8
    $c=$doc.TestRun.ResultSummary.Counters
    $failures=@($doc.TestRun.Results.UnitTestResult|Where-Object {$_.outcome -ne 'Passed'}|ForEach-Object {[pscustomobject]@{name=[string]$_.testName;outcome=[string]$_.outcome;duration=[string]$_.duration;message=[string]$_.Output.ErrorInfo.Message;stack=[string]$_.Output.ErrorInfo.StackTrace}})
    $tests.Add([pscustomobject]@{file=$trx.FullName.Substring($runRoot.Length+1);outcome=[string]$doc.TestRun.ResultSummary.outcome;total=[int]$c.total;executed=[int]$c.executed;passed=[int]$c.passed;failed=[int]$c.failed;notExecuted=[int]$c.notExecuted;failures=$failures;sha256=(Get-FileHash -LiteralPath $trx.FullName -Algorithm SHA256).Hash})
    Copy-Item -LiteralPath $trx.FullName -Destination (Join-Path $evidenceRoot ($trx.Directory.Parent.Name+'-'+$trx.Directory.Name+'-'+$trx.Name))
}
foreach($file in Get-ChildItem -LiteralPath $runRoot -File|Where-Object {$_.Name -match '^(views-|terminal-|stack-|unoptimized-abort|final-build|final-regression|system-font-regression)'}){
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $evidenceRoot $file.Name)
}
foreach($name in @('original-final','cpu-fixed','cpu-final','direct2d','direct2d-final','vulkan','vulkan-final','bad-host','combining')){
    foreach($file in @('result.json','scenes.json','process.json','failures.log','stderr.log','avalonia.log','current-scene.txt')){
        $path=Join-Path $runRoot ($name+'/'+$file)
        if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $evidenceRoot ($name+'-'+$file))}
    }
}
foreach($pair in @(@('original-final','main'),@('cpu-final','main'),@('original-final','settings-14'),@('cpu-final','settings-14'),@('original-final','settings-01'),@('cpu-final','settings-01'),@('original-final','terminal-cjk'),@('cpu-final','terminal-cjk'))){
    $path=Join-Path $runRoot ($pair[0]+'/'+$pair[1]+'.png')
    if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $evidenceRoot ($pair[0]+'-'+$pair[1]+'.png'))}
}
$repo=Join-Path $runRoot 'VelaShell'
$head=& git -c safe.directory=$repo -C $repo rev-parse HEAD
$patch=& git -c safe.directory=$repo -C $repo diff
[IO.File]::WriteAllText((Join-Path $evidenceRoot 'isolated-test.patch'),($patch -join "`n"),[Text.UTF8Encoding]::new($false))
$sources=@{}
foreach($file in @('src/SimdSkia.Avalonia/Fonts.cs','src/SimdSkia.Avalonia/DrawingContext.cs','src/SimdSkia.Cpu/RasterCanvas.Sampling.cs','tools/VelaShellProbe/Probe.cs')){
    $sources[$file]=(Get-FileHash -LiteralPath (Join-Path $simdRoot $file) -Algorithm SHA256).Hash
}
$report=[pscustomobject]@{date='2026-10-27';upstream='https://github.com/joesdu/VelaShell';upstreamCommit=$head;configuration='Debug/net11.0';sdk='11.0.100-rc.1.26425.128';runtime=$original.runtime;avalonia='12.1.4';original=$original;backends=$backends;qualityScope='CPU RenderTargetBitmap snapshots under each application configuration; not GPU presented-frame quality or throughput benchmarks';threshold=[pscustomobject]@{maxChannelError=32;rmse=2};quality=$quality.ToArray();tests=$tests.ToArray();sourceHashes=$sources}
$report|ConvertTo-Json -Depth 30|Set-Content -LiteralPath (Join-Path $evidenceRoot 'results.json') -Encoding UTF8
$tests|Format-Table file,total,passed,failed,notExecuted
$quality|Group-Object backend|ForEach-Object {Write-Output ($_.Name+': quality '+@($_.Group|Where-Object passed).Count+'/'+$_.Count)}
Write-Output ('Evidence: '+$evidenceRoot)
