param([string]$Output='artifacts/simdcodecs-integration-2026-11-01/packages')
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try {
    # Deliberately cannot match the public preview workflow's version pattern.
    $version='0.1.0-codecs-trial.1'
    $packagePath=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $packagePath|Out-Null
    $projects=@('SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Gpu.Direct2D','SimdSkia.Compatibility','SimdSkia.Avalonia','SimdSkia.Avalonia.Desktop','SimdSkia.Avalonia.Gpu','SimdSkia','SimdSkia.Pdf')
    foreach($project in $projects) {
        dotnet pack "src/$project/$project.csproj" -c Release -p:LocalCodecTrial=true -p:Version=$version -p:PackageVersion=$version '-p:PackageReleaseNotes=Unpublished .NET 10 trial: full Sdcb.SimdCodecs 1.2.0 dependency. BCL native compression explicitly allowed. Original JPEG pixel gate still has 3 cases with maximum error 2. See CHANGELOG.md and docs/SIMDCODECS-INTEGRATION-2026-11-01.md.' -o $packagePath --nologo
        if($LASTEXITCODE -ne 0){throw "Trial packing failed: $project"}
    }
    python tools/verify-codecs-trial.py --directory $packagePath
    if($LASTEXITCODE -ne 0){throw 'Trial package content check failed.'}
}
finally {Pop-Location}
