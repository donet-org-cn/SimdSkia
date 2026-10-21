param([string]$Output='artifacts/completion/filter-final',[int]$Forks=3,[switch]$NoBuild)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try{
    if($Forks -lt 1){throw 'Forks must be positive.'}
    if(-not $NoBuild){dotnet build benchmarks/Filters -c Release --nologo -v:q;if($LASTEXITCODE -ne 0){throw 'Filter benchmark build failed.'}}
    $outputPath=[IO.Path]::GetFullPath((Join-Path $workspacePath $Output))
    if(Test-Path -LiteralPath (Join-Path $outputPath 'manifest.json')){throw 'Use a fresh output directory.'}
    New-Item -ItemType Directory -Path $outputPath -Force|Out-Null
    $manifest=@{Forks=$Forks;Scope='CPU Gaussian filter; identical scalar/AVX2 integer algorithm, independent correctness checks in AvaloniaControls';WarmupMs=750;MeasurementMs=1500;SourceSha256=(Get-FileHash src/SimdSkia.Cpu/ManagedFilters.cs).Hash;TieredCompilation=$env:DOTNET_TieredCompilation;HardwareIntrinsics=$env:DOTNET_EnableHWIntrinsic}
    $manifest|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $outputPath 'manifest.json') -Encoding utf8
    foreach($fork in 0..($Forks-1)){
        $modes=if($fork%2 -eq 0){@('scalar','simd')}else{@('simd','scalar')}
        foreach($mode in $modes){dotnet benchmarks/Filters/bin/Release/net8.0/Filters.dll $mode (Join-Path $outputPath ($mode+'-'+$fork+'.json'));if($LASTEXITCODE -ne 0){throw 'Filter worker failed.'}}
    }
    if((Get-FileHash src/SimdSkia.Cpu/ManagedFilters.cs).Hash -ne $manifest.SourceSha256){throw 'Filter source changed during measurement.'}
    python tools/report-filter-benchmarks.py $outputPath
    if($LASTEXITCODE -ne 0){throw 'Filter report failed.'}
}
finally{Pop-Location}
