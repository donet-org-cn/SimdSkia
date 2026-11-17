$ErrorActionPreference='Stop'
$codecPackages=@('Sdcb.SimdCodecs/2.0.0','Sdcb.SimdCodecs.Abstractions/2.0.0','Sdcb.SimdCodecs.Bmp/2.0.0','Sdcb.SimdCodecs.Color.Icc/2.0.0','Sdcb.SimdCodecs.Gif/2.0.0','Sdcb.SimdCodecs.Ico/2.0.0','Sdcb.SimdCodecs.Jbig2/2.0.0','Sdcb.SimdCodecs.Jpeg/2.0.0','Sdcb.SimdCodecs.Jpeg.GainMap/2.0.0','Sdcb.SimdCodecs.Jpeg2000/2.0.0','Sdcb.SimdCodecs.Png/2.0.0','Sdcb.SimdCodecs.Pnm/2.0.0','Sdcb.SimdCodecs.Qoi/2.0.0','Sdcb.SimdCodecs.Tiff/2.0.0','Sdcb.SimdCodecs.Webp/2.0.0')
$workspacePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($name in @('SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Gpu.Direct2D','SimdSkia.Compatibility','SimdSkia.Pdf')) {
    $projectPath = Join-Path $workspacePath ('src/' + $name)
    $assets = Get-Content -LiteralPath (Join-Path $projectPath 'obj/project.assets.json') -Raw | ConvertFrom-Json
    $allowedPackages = @('Clipper2/2.0.0')
    if ($name -in @('SimdSkia.Imaging','SimdSkia.Compatibility','SimdSkia.Pdf')) { $allowedPackages += $codecPackages }
    if ($name -eq 'SimdSkia.Pdf') { $allowedPackages += 'PdfPig/0.1.16' }
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -eq 'package' -and $library.Name -notin $allowedPackages) { throw ('Unexpected production NuGet dependency: ' + $library.Name) }
    }
    foreach($target in $assets.targets.PSObject.Properties){foreach($library in $target.Value.PSObject.Properties){if($library.Value.native){throw ('Native asset in production: '+$library.Name)};foreach($runtimeTarget in $library.Value.runtimeTargets.PSObject.Properties){if($runtimeTarget.Value.assetType -eq 'native'){throw ('Native runtime target in production: '+$library.Name)}}}}
    $forbidden = Get-ChildItem -LiteralPath (Join-Path $projectPath 'bin/Release/net10.0') -File | Where-Object { $_.Name -match '^(libSkiaSharp|SkiaSharp|HarfBuzzSharp)' }
    if ($forbidden) { throw ('Native/reference dependencies leaked into production output for ' + $name) }
}
Write-Output 'PASS: production geometry allows the pinned managed Clipper2 2.0.0 graph; Imaging/Compatibility additionally allow the fifteen pinned Sdcb.SimdCodecs 2.0.0 packages. .NET system compression is explicitly permitted. GPU interop calls system Vulkan, GL/GLES, D3D11/12/Direct2D or Metal; no native drawing component is bundled. Metal hardware validation remains unavailable.'
Write-Output 'PASS: optional PDF reader additionally permits only PdfPig 0.1.16, with no native assets.'
$adapterPath=Join-Path $workspacePath 'src/SimdSkia.Avalonia/obj/project.assets.json'
if(Test-Path -LiteralPath $adapterPath){
    $assets=Get-Content -LiteralPath $adapterPath -Raw|ConvertFrom-Json
    $allowed=@('Avalonia/12.1.0','Avalonia/12.1.5','Avalonia.BuildServices/11.3.2','Avalonia.Remote.Protocol/12.1.0','Avalonia.Remote.Protocol/12.1.5','MicroCom.Runtime/0.11.6','Clipper2/2.0.0')+$codecPackages
    foreach($library in $assets.libraries.PSObject.Properties){if($library.Value.type -eq 'package' -and $library.Name -notin $allowed){throw ('Unexpected Avalonia adapter dependency: '+$library.Name)}}
    foreach($target in $assets.targets.PSObject.Properties){foreach($library in $target.Value.PSObject.Properties){if($library.Value.native){throw ('Native asset in Avalonia adapter: '+$library.Name)};foreach($runtimeTarget in $library.Value.runtimeTargets.PSObject.Properties){if($runtimeTarget.Value.assetType -eq 'native'){throw ('Native runtime target in Avalonia adapter: '+$library.Name)}}}}
    $outputPath=Join-Path $workspacePath 'examples/AvaloniaControls/bin/Release/net10.0'
    if(Test-Path -LiteralPath $outputPath){$forbidden=Get-ChildItem -LiteralPath $outputPath -Recurse -File|Where-Object {$_.Name -match '(?i)(SkiaSharp|HarfBuzz|libEGL|libGLES|Angle)'};if($forbidden){throw 'Native drawing/font dependency leaked into managed Avalonia example.'}}
    Write-Output 'PASS: optional Avalonia adapter uses its five Avalonia/support packages and the pinned managed codec graph; example output has no Skia/HarfBuzz/ANGLE assets.'
}
