param(
    [ValidateSet('before','after')][string]$Phase='after',
    [string]$Output='artifacts/avalonia/performance-2026-10-21',
    [string]$Font='baseline/fonts/ZenOldMincho-Regular.ttf',
    [int]$Forks=3,[int]$Samples=5,[int]$SampleMilliseconds=100,
    [string]$Sizes='1024x768'
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
$savedTier=$env:DOTNET_TieredCompilation;$savedR2R=$env:DOTNET_ReadyToRun;$savedServer=$env:DOTNET_gcServer
try {
    $root=[IO.Path]::GetFullPath((Join-Path $workspacePath $Output));New-Item -ItemType Directory -Force -Path $root|Out-Null
    $manifestFile=Join-Path $root ($Phase+'-run.json');if(Test-Path -LiteralPath $manifestFile){throw 'Evidence already exists; use a new output path.'}
    foreach($project in @('benchmarks/Avalonia/Managed','benchmarks/Avalonia/Reference')){dotnet build $project -c Release --nologo;if($LASTEXITCODE -ne 0){throw 'Build failed.'}}
    $fontPath=(Resolve-Path -LiteralPath $Font).Path;$fixturePath=Join-Path $root 'fixtures'
    $referencePath=Join-Path $workspacePath 'benchmarks/Avalonia/Reference/bin/Release/net8.0/SimdSkia.AvaloniaBench.Reference.dll'
    $managedPath=Join-Path $workspacePath 'benchmarks/Avalonia/Managed/bin/Release/net8.0/SimdSkia.AvaloniaBench.Managed.dll'
    $env:DOTNET_TieredCompilation='0';$env:DOTNET_ReadyToRun='0';$env:DOTNET_gcServer='0'
    if(-not(Test-Path -LiteralPath (Join-Path $fixturePath 'labels.json'))){dotnet $referencePath --make-labels $fixturePath $fontPath;if($LASTEXITCODE -ne 0){throw 'Label fixture failed.'}}
    if($Phase -eq 'before'){
        $frozen=Join-Path $root 'before-worker';Copy-Item -LiteralPath (Split-Path $managedPath) -Destination $frozen -Recurse
        $managedPath=Join-Path $frozen 'SimdSkia.AvaloniaBench.Managed.dll'
        Copy-Item -LiteralPath (Join-Path $workspacePath 'src') -Destination (Join-Path $root 'before-source') -Recurse
    }
    $hashes=[ordered]@{};foreach($f in @(rg --files src -g '*.cs' -g '*.csproj' -g '*.comp' -g '*.spv' -g '!**/obj/**' -g '!**/bin/**')){$hashes[$f]=(Get-FileHash -LiteralPath $f).Hash}
    $jobs=@();$rng=[Random]::new(20261021)
    for($fork=0;$fork -lt $Forks;$fork++){
        $order=if($Phase -eq 'before'){@('before')}else{@('original','simd','scalar')}
        for($i=$order.Count-1;$i -gt 0;$i--){$j=$rng.Next($i+1);$t=$order[$i];$order[$i]=$order[$j];$order[$j]=$t}
        foreach($engine in $order){$jobs+=@{Engine=$engine;Fork=$fork}}
    }
    $manifest=[ordered]@{Date=(Get-Date -Format 'yyyy-MM-dd');Phase=$Phase;Avalonia='12.1.4';SkiaSharp='3.119.4';Backend='CPU raster SK call replay; fresh canvas per frame, not a full Avalonia app';Font=$fontPath;FontSha256=(Get-FileHash $fontPath).Hash;FixtureSha256=(Get-FileHash (Join-Path $fixturePath 'labels.json')).Hash;Forks=$Forks;Samples=$Samples;TargetSampleMilliseconds=$SampleMilliseconds;Sizes=$Sizes;TieredCompilation=0;ReadyToRun=0;ServerGc=0;Jobs=$jobs;SourceSha256=$hashes;Complete=$false}
    $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $manifestFile -Encoding UTF8
    foreach($job in $jobs){
        $program=if($job.Engine -eq 'original'){$referencePath}else{$managedPath};$engineArg=if($job.Engine -eq 'before'){'simd'}else{$job.Engine}
        dotnet $program --engine $engineArg --fork $job.Fork --out (Join-Path $root ($job.Engine+'-'+$job.Fork)) --fixtures $fixturePath --font $fontPath --sizes $Sizes --samples $Samples --sample-ms $SampleMilliseconds --warmup-ms 150 2>&1 | Tee-Object -FilePath (Join-Path $root ($job.Engine+'-'+$job.Fork+'.log'))
        if($LASTEXITCODE -ne 0){throw 'UI benchmark failed.'}
    }
    if($Phase -eq 'after'){
        foreach($engine in @('original','simd')){
            $program=if($engine -eq 'original'){$referencePath}else{$managedPath}
            foreach($scene in @('ui-labels','ui-scroll','ui-text-rebuild','ui-opaque-images')){
                dotnet $program --probe $scene $fontPath $fixturePath (Join-Path $root ($engine+'-startup-'+$scene+'.json'))
                if($LASTEXITCODE -ne 0){throw 'Startup probe failed.'}
            }
        }
    }
    foreach($f in $hashes.Keys){if((Get-FileHash -LiteralPath $f).Hash -ne $hashes[$f]){throw ('Source changed during run: '+$f)}}
    $manifest.Complete=$true;$manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $manifestFile -Encoding UTF8
}
finally{$env:DOTNET_TieredCompilation=$savedTier;$env:DOTNET_ReadyToRun=$savedR2R;$env:DOTNET_gcServer=$savedServer;Pop-Location}
