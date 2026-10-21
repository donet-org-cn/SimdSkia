param([switch]$IncludeWindowsReferenceRuntime)
$ErrorActionPreference='Stop'
$workspacePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baselinePath = Join-Path $workspacePath 'baseline'
New-Item -ItemType Directory -Path $baselinePath -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(@{ Id='avalonia.skia'; Version='12.1.4' }, @{ Id='skiasharp'; Version='3.119.4' })
if ($IncludeWindowsReferenceRuntime) { $packages += @{ Id='skiasharp.nativeassets.win32'; Version='3.119.4' } }
$records = @()
foreach ($package in $packages) {
    $name = $package.Id + '.' + $package.Version
    $destinationPath = Join-Path $baselinePath $name
    $packagePath = Join-Path $baselinePath ($name + '.nupkg')
    $uri = 'https://api.nuget.org/v3-flatcontainer/' + $package.Id + '/' + $package.Version + '/' + $name + '.nupkg'
    if (-not (Test-Path -LiteralPath $packagePath)) { Invoke-WebRequest -Uri $uri -OutFile $packagePath -TimeoutSec 180 }
    if (-not (Test-Path -LiteralPath $destinationPath)) { [IO.Compression.ZipFile]::ExtractToDirectory($packagePath,$destinationPath) }
    $records += @{ Package=$package.Id; Version=$package.Version; Source=$uri; Sha256=(Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash }
}
$sourcePath = Join-Path $baselinePath 'source'
New-Item -ItemType Directory -Path $sourcePath -Force | Out-Null
foreach ($name in @('SKColor.cs','SKColors.cs','SKCanvas.cs','SKBitmap.cs','SKImageInfo.cs','SKMatrix.cs','SKSurface.cs','SKPaint.cs','SKObject.cs','Definitions.cs')) {
    $sourceFile = Join-Path $sourcePath $name
    if (-not (Test-Path -LiteralPath $sourceFile)) { Invoke-WebRequest -Uri ('https://raw.githubusercontent.com/mono/SkiaSharp/v3.119.4/binding/SkiaSharp/' + $name) -OutFile $sourceFile -TimeoutSec 30 }
}
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $workspacePath 'docs/baseline-packages.json') -Encoding UTF8
Write-Output 'Pinned baseline fetched. Production projects do not reference these packages.'
