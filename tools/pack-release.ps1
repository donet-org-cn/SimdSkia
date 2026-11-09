param(
    [Parameter(Mandatory=$true)][ValidatePattern('^0\.1\.[01234]$')][string]$Version,
    [string]$Output='artifacts/nuget-stable',
    [switch]$DesktopOnly
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try {
    python tools/sync-avalonia-readme.py --check
    if($LASTEXITCODE -ne 0){throw 'Package usage README is stale.'}
    $packagePath=[IO.Path]::GetFullPath($Output)
    New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
    $projects=@('SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Gpu.Direct2D','SimdSkia.Compatibility','SimdSkia.Avalonia','SimdSkia.Avalonia.Desktop','SimdSkia.Avalonia.Gpu','SimdSkia','SimdSkia.Pdf')
    if($Version -eq '0.1.4' -and !$DesktopOnly){$projects+='SimdSkia.Avalonia.Android'}
    foreach($project in $projects){
        dotnet pack "src/$project/$project.csproj" -c Release -p:Version=$Version -p:PackageVersion=$Version -p:StableRelease=true -o $packagePath --nologo
        if($LASTEXITCODE -ne 0){throw "Stable release packing failed: $project"}
    }
    $verifyArguments=@('tools/verify-preview-packages.py','--directory',$packagePath,'--version',$Version)
    if($DesktopOnly){$verifyArguments+='--desktop-only'}
    python @verifyArguments
    if($LASTEXITCODE -ne 0){throw 'Stable package content verification failed.'}
}
finally {Pop-Location}
