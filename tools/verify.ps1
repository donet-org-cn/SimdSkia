param([switch]$Reference,[switch]$Gpu)
$ErrorActionPreference='Stop'
$workspacePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try {
    $referenceQualityFailed=$false
    dotnet build SimdSkia.sln -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project tests/SimdSkia.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed.' }
    dotnet run --project tests/CompatibilityExpansion/Managed -c Release --no-build -- artifacts/compat-expansion/managed
    if ($LASTEXITCODE -ne 0) { throw 'Standalone compatibility expansion tests failed.' }
    dotnet run --project tests/Pdf -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Managed PDF export/read tests failed.' }
    $fontPath=$env:SIMDSKIA_CJK_FONT
    if(-not $fontPath -and (Test-Path -LiteralPath baseline/fonts/ZenOldMincho-Regular.ttf)){$fontPath=(Resolve-Path baseline/fonts/ZenOldMincho-Regular.ttf).Path}
    if($fontPath){dotnet run --project tests/Features -c Release --no-build -- $fontPath}
    else{dotnet run --project tests/Features -c Release --no-build}
    if ($LASTEXITCODE -ne 0) { throw 'Feature tests failed.' }
    dotnet run --project tests/Imaging -c Release --no-build
    if($LASTEXITCODE -ne 0){throw 'Independent image codec/ICC fixture checks failed.'}
    if($fontPath){dotnet run --project tests/Optimization -c Release --no-build -- $fontPath}
    else{dotnet run --project tests/Optimization -c Release --no-build}
    if($LASTEXITCODE -ne 0){throw 'Exact-byte optimization checks failed.'}
    if($fontPath){dotnet run --project tests/Avalonia -c Release --no-build -- $fontPath artifacts/avalonia/tests-managed}
    else{dotnet run --project tests/Avalonia -c Release --no-build}
    if($LASTEXITCODE -ne 0){throw 'Avalonia text-chain checks failed.'}
    if(-not (Test-Path -LiteralPath baseline/fonts/ZCOOLXiaoWei-Regular.ttf)){throw 'Run tools/fetch-avalonia-font.ps1 before real Avalonia control verification.'}
    dotnet run --project tests/AvaloniaControls -c Release --no-build -- --frames 5 --output artifacts/avalonia-controls/verify
    if($LASTEXITCODE -ne 0){throw 'Real Avalonia control, glyph-mask and lifetime checks failed.'}
    dotnet run --project examples/Rectangle -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Rectangle example failed.' }
    dotnet run --project tools/ApiAudit -c Release --no-build -- extract src/SimdSkia.Compatibility/bin/Release/net8.0/SimdSkia.Compatibility.dll docs/api/implemented.json
    if ($LASTEXITCODE -ne 0) { throw 'Replacement API extraction failed.' }
    if ($Reference) {
        if (-not (Test-Path -LiteralPath baseline/skiasharp.3.119.4/lib/net6.0/SkiaSharp.dll)) { throw 'Run tools/fetch-baseline.ps1 first.' }
        dotnet run --project tools/ApiAudit -c Release --no-build -- extract baseline/skiasharp.3.119.4/lib/net6.0/SkiaSharp.dll docs/api/baseline-3.119.4.json
        if ($LASTEXITCODE -ne 0) { throw 'Baseline API extraction failed.' }
        dotnet run --project tools/ApiAudit -c Release --no-build -- compare docs/api/baseline-3.119.4.json docs/api/implemented.json docs/api/comparison.json
        if ($LASTEXITCODE -notin @(0,2)) { throw 'API audit failed.' }
        $apiReport = Get-Content -LiteralPath docs/api/comparison.json -Raw | ConvertFrom-Json
        if ($apiReport.AddedOrDifferent.Count -ne 0) { throw 'A replacement public signature does not match the pinned baseline. Inspect docs/api/comparison.json.' }
        # Exit 2 is expected while the tracked public API has missing entries.
        dotnet run --project tests/ManagedRenderer -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Managed rendering failed.' }
        dotnet run --project tests/ReferenceRenderer -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Isolated native reference failed. Check the RID and its reference native assets.' }
        dotnet run --project tests/Features -c Release --no-build -- $fontPath --reference-pngs
        if($LASTEXITCODE -ne 0){throw 'Native-encoded PNG decode fixtures failed.'}
        dotnet run --project tools/PixelCompare -c Release --no-build -- artifacts/reference artifacts/managed artifacts/diff
        if($LASTEXITCODE -eq 2){$referenceQualityFailed=$true;Write-Output 'FAIL: original pixel quality gate. Remaining independent tests will still run; final exit code is 2.'}
        elseif($LASTEXITCODE -ne 0){throw 'Pixel comparison failed to execute.'}
    }
    if($Gpu){
        dotnet run --project tests/GpuSampling -c Release --no-build
        if($LASTEXITCODE -ne 0){throw 'Vulkan sampled image/coverage checks failed.'}
        dotnet run --project tests/Gpu -c Release --no-build;if($LASTEXITCODE -ne 0){throw 'Vulkan device tests failed.'}
        dotnet run --project tests/GpuOptimization -c Release -- $fontPath
        if($LASTEXITCODE -ne 0){throw 'Vulkan exact-byte kernel comparisons failed.'}
        if($fontPath){dotnet run --project tests/Avalonia -c Release --no-build -- $fontPath artifacts/avalonia/tests-gpu --gpu}
        if($LASTEXITCODE -ne 0){throw 'Positioned-text Vulkan comparisons failed.'}
    }
    if($referenceQualityFailed){exit 2}
}
finally { Pop-Location }
