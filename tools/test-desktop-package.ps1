param(
 [string]$Version='0.1.0-preview.4',
 [string]$Packages='artifacts/nuget-preview-3',
 [string]$Output='artifacts/desktop-package-consumer',
 [ValidateSet('cpu','direct2d','vulkan')][string]$Backend='cpu',
 [switch]$SystemFonts
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try{
 $outPath=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $outPath|Out-Null
 $feedPath=[Security.SecurityElement]::Escape([IO.Path]::GetFullPath($Packages))
 $config=Join-Path $outPath 'NuGet.Config'
 [IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key=`"packed`" value=`"$feedPath`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>")
 # Repacking a preview version must not reuse an older binary from NuGet's cache.
 $manifest=Join-Path ([IO.Path]::GetFullPath($Packages)) 'manifest.json'
 $contentHash=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
 $cache=Join-Path $outPath "packages-$contentHash"
 dotnet restore examples/CrossPlatform/CrossPlatform.csproj -p:UsePackedSimdSkia=true -p:SimdSkiaPackageVersion=$Version --configfile $config --packages $cache --force-evaluate
 if($LASTEXITCODE -ne 0){throw 'Packed desktop restore failed.'}
 $assets=Get-Content examples/CrossPlatform/obj/project.assets.json -Raw|ConvertFrom-Json
 foreach($library in $assets.libraries.PSObject.Properties){if($library.Name -match '^SimdSkia' -and $library.Value.type -eq 'project'){throw 'A source engine ProjectReference leaked into the package consumer.'}}
 dotnet build examples/CrossPlatform/CrossPlatform.csproj -c Release -p:UsePackedSimdSkia=true -p:SimdSkiaPackageVersion=$Version --no-restore -o "$outPath/bin" *> "$outPath/build.log"
 if($LASTEXITCODE -ne 0){Get-Content "$outPath/build.log";throw 'Packed desktop build failed.'}
 $runArgs=@("$outPath/bin/CrossPlatform.dll",'--smoke','--controls','--output',"$outPath/results")
 if($Backend -ne 'cpu'){$runArgs+=@('--gpu',$Backend)}
 if(!$SystemFonts){$runArgs+=@('--font',(Resolve-Path baseline/fonts/ZCOOLXiaoWei-Regular.ttf).Path)}
 & dotnet @runArgs *> "$outPath/run.log"
 if($LASTEXITCODE -ne 0){Get-Content "$outPath/run.log";throw 'Packed desktop window failed.'}
 Get-Content "$outPath/results/result.txt" -TotalCount 16
 Write-Output 'PASS isolated NuGet desktop consumer; no source engine reference.'
}
finally{Pop-Location}
