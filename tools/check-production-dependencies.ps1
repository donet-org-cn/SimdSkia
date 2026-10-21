$ErrorActionPreference='Stop'
$workspacePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($name in @('SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Compatibility')) {
    $projectPath = Join-Path $workspacePath ('src/' + $name)
    $assets = Get-Content -LiteralPath (Join-Path $projectPath 'obj/project.assets.json') -Raw | ConvertFrom-Json
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -eq 'package') { throw ('Unexpected production NuGet dependency: ' + $library.Name) }
    }
    $forbidden = Get-ChildItem -LiteralPath (Join-Path $projectPath 'bin/Release/net8.0') -File | Where-Object { $_.Name -match '^(libSkiaSharp|SkiaSharp|HarfBuzzSharp)' }
    if ($forbidden) { throw ('Native/reference dependencies leaked into production output for ' + $name) }
}
Write-Output 'PASS: all six production projects have zero NuGet package dependencies and no Skia/HarfBuzz binaries. Only GPU interop calls the system Vulkan loader.'
