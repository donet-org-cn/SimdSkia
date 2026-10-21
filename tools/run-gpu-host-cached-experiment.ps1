param(
    [string]$Baseline='artifacts/performance-gpu/reassessment-2026-10-12',
    [string]$Output='artifacts/performance-gpu/host-cached-experiment-2026-10-12'
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
$savedTier=$env:DOTNET_TieredCompilation
$savedR2R=$env:DOTNET_ReadyToRun
$savedServer=$env:DOTNET_gcServer
try {
    $parent=Get-Content -LiteralPath (Join-Path $Baseline 'run.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if(-not $parent.Complete){throw 'The current-product GPU baseline must finish first.'}
    $outputPath=[IO.Path]::GetFullPath((Join-Path $workspacePath $Output))
    if(Test-Path -LiteralPath (Join-Path $outputPath 'run.json')){throw 'Evidence already exists; supply a new output.'}
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    $prototypePath='experiments/GpuHostCached/Worker/bin/Release/net8.0/SimdSkia.Benchmark.GpuHostCached.dll'
    dotnet build experiments/GpuHostCached/Worker -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Prototype build failed.'}
    $hashes=[ordered]@{}
    foreach($sourceFile in @(rg --files experiments/GpuHostCached -g '*.cs' -g '*.csproj' -g '!**/obj/**' -g '!**/bin/**')){$hashes[$sourceFile]=(Get-FileHash -LiteralPath $sourceFile).Hash}
    $manifest=[ordered]@{Baseline=[IO.Path]::GetFullPath((Join-Path $workspacePath $Baseline));Variant='isolated HOST_CACHED target-buffer preference; other product algorithms and shader unchanged';PrototypeSourceSha256=$hashes;GpuAssemblySha256=(Get-FileHash -LiteralPath 'experiments/GpuHostCached/Worker/bin/Release/net8.0/SimdSkia.Gpu.dll').Hash;Forks=$parent.Forks;Samples=$parent.Samples;TargetSampleMilliseconds=$parent.TargetSampleMilliseconds;WarmupMilliseconds=$parent.WarmupMilliseconds;Sizes=$parent.Sizes;Scenes=$parent.Scenes;Font=$parent.Font;Seed=$parent.Seed;Complete=$false}
    $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $outputPath 'run.json') -Encoding UTF8
    $env:DOTNET_TieredCompilation='0';$env:DOTNET_ReadyToRun='0';$env:DOTNET_gcServer='0'
    $sceneArguments=@();if($parent.Scenes){$sceneArguments=@('--scenes',$parent.Scenes)}
    for($fork=0;$fork -lt $parent.Forks;$fork++){
        Write-Output ('HOST_CACHED experiment fork '+$fork)
        dotnet $prototypePath --engine simd --fork $fork --out (Join-Path $outputPath ('simd-'+$fork)) --font $parent.Font --sizes $parent.Sizes --samples $parent.Samples --sample-ms $parent.TargetSampleMilliseconds --warmup-ms $parent.WarmupMilliseconds @sceneArguments 2>&1 | Tee-Object -FilePath (Join-Path $outputPath ('simd-'+$fork+'.log'))
        if($LASTEXITCODE -ne 0){throw 'Prototype worker failed.'}
    }
    foreach($sourceFile in $hashes.Keys){if((Get-FileHash -LiteralPath $sourceFile).Hash -ne $hashes[$sourceFile]){throw 'Prototype changed during measurement.'}}
    foreach($property in $parent.ProductSourceSha256.PSObject.Properties){if((Get-FileHash -LiteralPath $property.Name).Hash -ne $property.Value){throw ('Production changed before/during the experiment: '+$property.Name)}}
    $manifest.Complete=$true
    $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $outputPath 'run.json') -Encoding UTF8
}
finally{$env:DOTNET_TieredCompilation=$savedTier;$env:DOTNET_ReadyToRun=$savedR2R;$env:DOTNET_gcServer=$savedServer;Pop-Location}
