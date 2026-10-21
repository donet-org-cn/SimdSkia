param(
    [string]$Output='artifacts/ocr/current',
    [int]$Forks=3,
    [int]$Samples=5,
    [int]$SampleMilliseconds=100
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
$savedTier=$env:DOTNET_TieredCompilation;$savedR2R=$env:DOTNET_ReadyToRun;$savedServer=$env:DOTNET_gcServer
try {
    foreach($engine in @('Reference','Managed')){dotnet build ('benchmarks/Ocr/'+$engine) -c Release --nologo;if($LASTEXITCODE -ne 0){throw 'Worker build failed.'}}
    $outputPath=[IO.Path]::GetFullPath((Join-Path $workspacePath $Output))
    if(Test-Path -LiteralPath (Join-Path $outputPath 'run.json')){throw 'Output contains earlier evidence. Select a new directory.'}
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    $fixturesPath=Join-Path $outputPath 'fixtures'
    $env:DOTNET_TieredCompilation='0';$env:DOTNET_ReadyToRun='0';$env:DOTNET_gcServer='0'
    $referencePath='benchmarks/Ocr/Reference/bin/Release/net10.0/SimdSkia.Ocr.Reference.dll'
    $managedPath='benchmarks/Ocr/Managed/bin/Release/net10.0/SimdSkia.Ocr.Managed.dll'
    dotnet $referencePath --make-fixtures --fixtures $fixturesPath --out (Join-Path $outputPath 'setup')
    if($LASTEXITCODE -ne 0){throw 'Fixture generation failed.'}
    [xml]$target=Get-Content -LiteralPath benchmarks/Ocr/Target.local.props -Raw -Encoding UTF8
    $qcxtPath=[string]$target.Project.PropertyGroup.QcxtRoot
    $targetHashes=[ordered]@{};foreach($p in @(rg --files (Join-Path $qcxtPath 'src/Qcxt.OCR') (Join-Path $qcxtPath 'src/Qcxt.OCR.SkiaSharp') -g '*.cs' -g '*.csproj' -g '*.onnx' -g '*.txt' -g '!**/obj/**' -g '!**/bin/**')){$targetHashes[$p]=(Get-FileHash -LiteralPath $p).Hash}
    $jobs=@();$random=[Random]::new(20261012)
    for($fork=0;$fork -lt $Forks;$fork++){
        $group=@();foreach($engine in @('original','simd')){
            foreach($size in @(500,1024,2048)){$group+=@{Engine=$engine;Mode='decode';Size=$size;Fork=$fork}}
            foreach($mode in @('raw-ocr','bitmap-ocr','bytes-ocr')){$group+=@{Engine=$engine;Mode=$mode;Size=500;Fork=$fork}}
        }
        for($i=$group.Count-1;$i -gt 0;$i--){$j=$random.Next($i+1);$temp=$group[$i];$group[$i]=$group[$j];$group[$j]=$temp}
        $jobs+=$group
    }
    $productHashes=[ordered]@{};foreach($p in @(rg --files src -g '*.cs' -g '*.csproj' -g '*.comp' -g '*.spv' -g '!**/obj/**' -g '!**/bin/**')){$productHashes[$p]=(Get-FileHash -LiteralPath $p).Hash}
    $manifest=[ordered]@{Date=(Get-Date -Format 'yyyy-MM-dd');StartedUtc=[DateTimeOffset]::UtcNow.ToString('o');ProductSourceSha256=$productHashes;Baseline='SkiaSharp 4.153.1 (actual target module)';QcxtRoot=$qcxtPath;TargetSourcesSha256=$targetHashes;Forks=$Forks;Samples=$Samples;SampleMilliseconds=$SampleMilliseconds;OcrOptions='current defaults: CPU, automatic thread budgets';JobOrder=$jobs;Complete=$false}
    $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $outputPath 'run.json') -Encoding UTF8
    foreach($job in $jobs){
        $program=if($job.Engine -eq 'original'){$referencePath}else{$managedPath}
        $name=$job.Engine+'-'+$job.Mode+'-'+$job.Size+'-'+$job.Fork
        dotnet $program --mode $job.Mode --size $job.Size --fork $job.Fork --out (Join-Path $outputPath $name) --fixtures $fixturesPath --samples $Samples --sample-ms $SampleMilliseconds 2>&1 | Tee-Object -FilePath (Join-Path $outputPath ($name+'.log'))
        if($LASTEXITCODE -ne 0){throw ('OCR worker failed: '+$name)}
    }
    foreach($engine in @('original','simd')){
        $program=if($engine -eq 'original'){$referencePath}else{$managedPath}
        dotnet $program --mode formats --out (Join-Path $outputPath ($engine+'-formats')) --fixtures $fixturesPath
        if($LASTEXITCODE -ne 0){throw 'Format probe failed.'}
    }
    foreach($p in $targetHashes.Keys){if((Get-FileHash -LiteralPath $p).Hash -ne $targetHashes[$p]){throw ('Target changed during measurement: '+$p)}}
    foreach($p in $productHashes.Keys){if((Get-FileHash -LiteralPath $p).Hash -ne $productHashes[$p]){throw ('Product changed during measurement: '+$p)}}
    $manifest.Complete=$true
    $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $outputPath 'run.json') -Encoding UTF8
}
finally{$env:DOTNET_TieredCompilation=$savedTier;$env:DOTNET_ReadyToRun=$savedR2R;$env:DOTNET_gcServer=$savedServer;Pop-Location}
