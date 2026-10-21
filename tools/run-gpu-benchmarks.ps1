param(
    [string]$Output='artifacts/performance-gpu/latest',
    [string]$Font='baseline/fonts/ZenOldMincho-Regular.ttf',
    [int]$Forks=3,
    [int]$Samples=7,
    [int]$SampleMilliseconds=60,
    [int]$WarmupMilliseconds=100,
    [string]$Sizes='256x256,1024x768',
    [string]$Scenes='',
    [string]$Report='docs/PERFORMANCE-GPU.md',
    [string]$ManagedDirectory=''
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
$savedTier=$env:DOTNET_TieredCompilation
$savedR2R=$env:DOTNET_ReadyToRun
$savedServer=$env:DOTNET_gcServer
try {
    if($Forks -lt 1 -or $Forks -gt 20){throw 'Fork count outside 1..20.'}
    if(-not(Test-Path -LiteralPath $Font)){throw 'Missing real font. Run tools/fetch-test-font.ps1 or supply -Font.'}
    if(-not(Test-Path -LiteralPath baseline/skiasharp.3.119.4/lib/net6.0/SkiaSharp.dll)){throw 'Run tools/fetch-baseline.ps1 -IncludeWindowsReferenceRuntime.'}
    if(-not(Test-Path -LiteralPath baseline/skiasharp.vulkan.sharpvk.3.119.4/lib/net6.0/SkiaSharp.Vulkan.SharpVk.dll)){throw 'Run tools/fetch-baseline.ps1 -IncludeVulkanReferenceRuntime.'}
    foreach($project in @('benchmarks/GpuReference','benchmarks/GpuManaged','benchmarks/GpuReport')){dotnet build $project -c Release --nologo;if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}}
    $outputPath=[IO.Path]::GetFullPath((Join-Path $workspacePath $Output))
    if(Test-Path -LiteralPath (Join-Path $outputPath 'run.json')){throw 'Output contains an earlier run. Supply a new -Output directory; existing evidence is preserved.'}
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    $fontPath=(Resolve-Path -LiteralPath $Font).Path
    $managedPath=Join-Path $workspacePath 'benchmarks/GpuManaged/bin/Release/net8.0/SimdSkia.Benchmark.GpuManaged.dll'
    if($ManagedDirectory){$managedPath=Join-Path ([IO.Path]::GetFullPath($ManagedDirectory)) 'SimdSkia.Benchmark.GpuManaged.dll';if(-not(Test-Path -LiteralPath $managedPath)){throw 'Frozen managed GPU worker is missing.'}}
    $referencePath=Join-Path $workspacePath 'benchmarks/GpuReference/bin/Release/net8.0/SimdSkia.Benchmark.GpuReference.dll'
    $env:DOTNET_TieredCompilation='0'
    $env:DOTNET_ReadyToRun='0'
    $env:DOTNET_gcServer='0'
    $sceneArguments=@();if($Scenes){$sceneArguments=@('--scenes',$Scenes)}
    $jobs=@()
    $random=[Random]::new(20261012)
    for($fork=0;$fork -lt $Forks;$fork++){
        $order=@('original','simd')
        for($i=$order.Count-1;$i -gt 0;$i--){$j=$random.Next($i+1);$temporary=$order[$i];$order[$i]=$order[$j];$order[$j]=$temporary}
        foreach($engine in $order){$jobs+=@{Engine=$engine;Fork=$fork}}
    }
    $sourceHashes=[ordered]@{};foreach($sourceFile in @(rg --files src -g '*.cs' -g '*.csproj' -g '*.comp' -g '*.spv' -g '!**/obj/**' -g '!**/bin/**')){$sourceHashes[$sourceFile]=(Get-FileHash -LiteralPath $sourceFile).Hash}
    $manifest=[ordered]@{ProductSourceSha256=$sourceHashes;Date=(Get-Date -Format 'yyyy-MM-dd');StartedUtc=[DateTimeOffset]::UtcNow.ToString('o');Backend='Vulkan GPU round-trip';Baseline='SkiaSharp 3.119.4';Forks=$Forks;Samples=$Samples;TargetSampleMilliseconds=$SampleMilliseconds;WarmupMilliseconds=$WarmupMilliseconds;Sizes=$Sizes;Scenes=$Scenes;Font=$fontPath;FontSha256=(Get-FileHash -LiteralPath $fontPath).Hash;Seed=20261012;TieredCompilation=0;ReadyToRun=0;ServerGc=0;JobOrder=$jobs;Complete=$false}
    $runtimeHashes=[ordered]@{}
    foreach($worker in @($managedPath,$referencePath)){
        foreach($file in Get-ChildItem -LiteralPath (Split-Path -Parent $worker) -File | Where-Object {$_.Extension -eq '.dll' -or $_.Name -like '*.deps.json' -or $_.Name -like '*.runtimeconfig.json'}){
            $runtimeHashes[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash
        }
    }
    $manifest.ManagedWorker=$managedPath;$manifest.RuntimeSha256=$runtimeHashes
    $manifest.SourceNote=if($ManagedDirectory){'Frozen managed runtime supplied explicitly; RuntimeSha256 identifies the measured binaries. ProductSourceSha256 records the workspace and is not a claim that it built the frozen runtime.'}else{'Measured managed runtime built from the recorded workspace source.'}
    $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $outputPath 'run.json') -Encoding UTF8
    foreach($job in $jobs){
        $jobPath=Join-Path $outputPath ($job.Engine+'-'+$job.Fork)
        $programPath=if($job.Engine -eq 'original'){$referencePath}else{$managedPath}
        Write-Output ('Running '+$job.Engine+' fork '+$job.Fork)
        dotnet $programPath --engine $job.Engine --fork $job.Fork --out $jobPath --font $fontPath --sizes $Sizes --samples $Samples --sample-ms $SampleMilliseconds --warmup-ms $WarmupMilliseconds @sceneArguments 2>&1 | Tee-Object -FilePath (Join-Path $outputPath ($job.Engine+'-'+$job.Fork+'.log'))
        if($LASTEXITCODE -ne 0){throw ('Benchmark failed: '+$job.Engine+' fork '+$job.Fork)}
    }
    foreach($sourceFile in $sourceHashes.Keys){if((Get-FileHash -LiteralPath $sourceFile).Hash -ne $sourceHashes[$sourceFile]){throw ('Product source changed during measurement: '+$sourceFile)}}
    foreach($runtimeFile in $runtimeHashes.Keys){if((Get-FileHash -LiteralPath $runtimeFile).Hash -ne $runtimeHashes[$runtimeFile]){throw ('Worker runtime changed during measurement: '+$runtimeFile)}}
    $manifest.Complete=$true
    $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $outputPath 'run.json') -Encoding UTF8
    dotnet benchmarks/GpuReport/bin/Release/net8.0/GpuReport.dll $outputPath $Report
    if($LASTEXITCODE -eq 2){Write-Output 'Measurements completed; native GPU pixel quality gate FAILED. Reports retain failed rows.';exit 2}
    if($LASTEXITCODE -ne 0){throw 'Report generation failed.'}
}
finally{$env:DOTNET_TieredCompilation=$savedTier;$env:DOTNET_ReadyToRun=$savedR2R;$env:DOTNET_gcServer=$savedServer;Pop-Location}
