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
$adapterPath=Join-Path $workspacePath 'src/SimdSkia.Avalonia/obj/project.assets.json'
if(Test-Path -LiteralPath $adapterPath){
    $assets=Get-Content -LiteralPath $adapterPath -Raw|ConvertFrom-Json
    $allowed=@('Avalonia/12.1.4','Avalonia.BuildServices/11.3.2','Avalonia.Remote.Protocol/12.1.4','MicroCom.Runtime/0.11.6','Clipper2/2.0.0')
    foreach($library in $assets.libraries.PSObject.Properties){if($library.Value.type -eq 'package' -and $library.Name -notin $allowed){throw ('Unexpected Avalonia adapter dependency: '+$library.Name)}}
    foreach($target in $assets.targets.PSObject.Properties){foreach($library in $target.Value.PSObject.Properties){if($library.Value.native){throw ('Native asset in Avalonia adapter: '+$library.Name)};foreach($runtimeTarget in $library.Value.runtimeTargets.PSObject.Properties){if($runtimeTarget.Value.assetType -eq 'native'){throw ('Native runtime target in Avalonia adapter: '+$library.Name)}}}}
    $outputPath=Join-Path $workspacePath 'examples/AvaloniaControls/bin/Release/net8.0'
    if(Test-Path -LiteralPath $outputPath){$forbidden=Get-ChildItem -LiteralPath $outputPath -Recurse -File|Where-Object {$_.Name -match '(?i)(SkiaSharp|HarfBuzz|libEGL|libGLES|Angle)'};if($forbidden){throw 'Native drawing/font dependency leaked into managed Avalonia example.'}}
    Write-Output 'PASS: optional Avalonia adapter uses only its five pinned managed packages; example output has no Skia/HarfBuzz/ANGLE assets.'
}
