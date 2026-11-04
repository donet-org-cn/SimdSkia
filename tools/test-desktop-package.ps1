param(
 [string]$Version='0.1.1',
 [string]$AvaloniaVersion='12.1.5',
 [string]$Packages='artifacts/nuget-preview',
 [string]$Output='artifacts/desktop-package-consumer',
 [ValidateSet('cpu','direct2d','vulkan')][string]$Backend='cpu',
 [switch]$NativeAot,
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
 # Keep the Native AOT library paths below the Windows linker MAX_PATH limit.
 $cache=Join-Path $workspacePath ('artifacts/nuget-cache/av-'+$contentHash.Substring(0,16))
 dotnet restore examples/CrossPlatform/CrossPlatform.csproj -p:UsePackedSimdSkia=true -p:SimdSkiaPackageVersion=$Version -p:AvaloniaVersion=$AvaloniaVersion -p:NuGetLockFilePath=$outPath/packages.lock.json --configfile $config --packages $cache --force-evaluate
 if($LASTEXITCODE -ne 0){throw 'Packed desktop restore failed.'}
 $assets=Get-Content examples/CrossPlatform/obj/project.assets.json -Raw|ConvertFrom-Json
 foreach($library in $assets.libraries.PSObject.Properties){if($library.Name -match '^SimdSkia' -and $library.Value.type -eq 'project'){throw 'A source engine ProjectReference leaked into the package consumer.'}}
 if(!$assets.libraries.PSObject.Properties["Avalonia/$AvaloniaVersion"]){throw 'The requested Avalonia version was not resolved.'}
 dotnet build examples/CrossPlatform/CrossPlatform.csproj -c Release -p:UsePackedSimdSkia=true -p:SimdSkiaPackageVersion=$Version -p:AvaloniaVersion=$AvaloniaVersion -p:NuGetLockFilePath=$outPath/packages.lock.json --no-restore -o "$outPath/bin" *> "$outPath/build.log"
 if($LASTEXITCODE -ne 0){Get-Content "$outPath/build.log";throw 'Packed desktop build failed.'}
 foreach($hostName in @('Avalonia.Win32','Avalonia.Win32.Automation','Avalonia.X11','Avalonia.FreeDesktop','Avalonia.FreeDesktop.AtSpi','Avalonia.Native')){
  if([Reflection.AssemblyName]::GetAssemblyName("$outPath/bin/$hostName.dll").Version.ToString() -ne "$AvaloniaVersion.0"){throw "Mismatched $hostName for Avalonia $AvaloniaVersion."}
 }
 if(Test-Path "$outPath/bin/hosts"){throw 'Unused host variants leaked into application output.'}
 $runArgs=@("$outPath/bin/CrossPlatform.dll",'--smoke','--controls','--output',"$outPath/results")
 if($Backend -ne 'cpu'){$runArgs+=@('--gpu',$Backend)}
 if(!$SystemFonts){$runArgs+=@('--font',(Resolve-Path baseline/fonts/ZCOOLXiaoWei-Regular.ttf).Path)}
 & dotnet @runArgs *> "$outPath/run.log"
 if($LASTEXITCODE -ne 0){Get-Content "$outPath/run.log";throw 'Packed desktop window failed.'}
 Get-Content "$outPath/results/result.txt" -TotalCount 16
 if($NativeAot){
  $prefix=if($IsWindows){'win'}elseif($IsMacOS){'osx'}else{'linux'}
  $arch=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
  dotnet publish examples/CrossPlatform/CrossPlatform.csproj -c Release -r "$prefix-$arch" -p:PublishAot=true -p:UsePackedSimdSkia=true -p:SimdSkiaPackageVersion=$Version -p:AvaloniaVersion=$AvaloniaVersion -p:NuGetLockFilePath=$outPath/packages.lock.json -p:RestoreConfigFile=$config -p:RestorePackagesPath=$cache -o "$outPath/aot" *> "$outPath/aot-build.log"
  if($LASTEXITCODE -ne 0){Get-Content "$outPath/aot-build.log";throw 'Packed desktop Native AOT publish failed.'}
  $exe=Join-Path $outPath ('aot/CrossPlatform'+$(if($IsWindows){'.exe'}else{''}))
  $aotArgs=@('--smoke','--controls','--font',(Resolve-Path baseline/fonts/ZCOOLXiaoWei-Regular.ttf).Path,'--output',"$outPath/aot-results")
  if($IsWindows){$p=Start-Process -FilePath $exe -ArgumentList ($aotArgs|ForEach-Object{'"'+$_+'"'}) -PassThru -Wait -WindowStyle Hidden;if($p.ExitCode -ne 0){throw 'Native AOT window failed.'}}
  else{& $exe @aotArgs;if($LASTEXITCODE -ne 0){throw 'Native AOT window failed.'}}
  if((Get-Content "$outPath/aot-results/runtime.txt" -Raw) -notmatch 'NativeAot=True'){throw 'Consumer did not run as Native AOT.'}
 }
 Write-Output 'PASS isolated NuGet desktop consumer; no source engine reference.'
}
finally{Pop-Location}
