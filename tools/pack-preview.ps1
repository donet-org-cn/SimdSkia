param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+-preview\.\d+$')][string]$Version,
    [string]$Output='artifacts/nuget-preview'
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try {
    python tools/sync-avalonia-readme.py --check
    if($LASTEXITCODE -ne 0){throw 'Package usage README is stale.'}
    $packagePath=[IO.Path]::GetFullPath($Output)
    New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
    $projects=@('SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Gpu.Direct2D','SimdSkia.Compatibility','SimdSkia.Avalonia','SimdSkia.Avalonia.Desktop','SimdSkia.Avalonia.Gpu','SimdSkia')
    foreach($project in $projects){
        dotnet pack "src/$project/$project.csproj" -c Release -p:Version=$Version -p:PackageVersion=$Version -o $packagePath --nologo
        if($LASTEXITCODE -ne 0){throw "Preview packing failed: $project"}
    }
    python tools/verify-preview-packages.py --directory $packagePath --version $Version
    if($LASTEXITCODE -ne 0){throw 'Preview package content verification failed.'}
}
finally {Pop-Location}
