param(
 [string]$Version='0.1.1',
 [string]$Packages='artifacts/avalonia-matrix/packages',
 [string]$Output='artifacts/avalonia-matrix/results',
 [string[]]$AvaloniaVersions=@('12.1.0','12.1.1','12.1.2','12.1.3','12.1.4','12.1.5'),
 [switch]$SkipDesktop,
 [switch]$NativeAot
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try{
 $outPath=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $outPath|Out-Null
 $feed=[IO.Path]::GetFullPath($Packages)
 $config=Join-Path $outPath 'NuGet.Config'
 [IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key=`"packed`" value=`"$([Security.SecurityElement]::Escape($feed))`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>")
 $manifestHash=(Get-FileHash "$feed/manifest.json" -Algorithm SHA256).Hash
 $cache=Join-Path $workspacePath ('artifacts/nuget-cache/av-'+$manifestHash.Substring(0,16))
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $zip=[IO.Compression.ZipFile]::OpenRead("$feed/SimdSkia.Avalonia.$Version.nupkg")
 try{$stream=$zip.GetEntry('lib/net10.0/SimdSkia.Avalonia.dll').Open();try{$sha=[Security.Cryptography.SHA256]::Create();$expected=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}}finally{$zip.Dispose()}
 $rows=@()
 foreach($av in $AvaloniaVersions){
  $rowPath=Join-Path $outPath $av;New-Item -ItemType Directory -Force $rowPath|Out-Null
  $props=@('-p:UsePackedSimdSkia=true',"-p:SimdSkiaPackageVersion=$Version","-p:AvaloniaVersion=$av","-p:NuGetLockFilePath=$rowPath/packages.lock.json")
  dotnet restore tests/AvaloniaControls @props --configfile $config --packages $cache --force-evaluate *> "$rowPath/restore.log"
  if($LASTEXITCODE -ne 0){Get-Content "$rowPath/restore.log";throw "Avalonia $av restore failed."}
  $assets=Get-Content tests/AvaloniaControls/obj/project.assets.json -Raw|ConvertFrom-Json
  if(!$assets.libraries.PSObject.Properties["Avalonia/$av"]){throw "Requested Avalonia $av was replaced by another version."}
  foreach($lib in $assets.libraries.PSObject.Properties){if($lib.Name -match '^SimdSkia' -and $lib.Value.type -eq 'project'){throw 'Source engine reference in binary compatibility test.'}}
  dotnet build tests/AvaloniaControls -c Release @props --no-restore -o "$rowPath/bin" *> "$rowPath/build.log"
  if($LASTEXITCODE -ne 0){Get-Content "$rowPath/build.log";throw "Avalonia $av consumer compile failed."}
  $actual=(Get-FileHash "$rowPath/bin/SimdSkia.Avalonia.dll" -Algorithm SHA256).Hash
  if($actual -ne $expected){throw 'Adapter was rebuilt/replaced during the matrix.'}
  $loaded=[Reflection.AssemblyName]::GetAssemblyName("$rowPath/bin/Avalonia.Base.dll").Version.ToString()
  if($loaded -ne "$av.0"){throw "Wrong Avalonia assembly: $loaded"}
  dotnet "$rowPath/bin/SimdSkia.Avalonia.Controls.Tests.dll" --frames 5 --font baseline/fonts/ZCOOLXiaoWei-Regular.ttf --output "$rowPath/controls" *> "$rowPath/run.log"
  if($LASTEXITCODE -ne 0){Get-Content "$rowPath/run.log";throw "Avalonia $av controls failed."}
  $aot=$NativeAot -and ($av -eq $AvaloniaVersions[0] -or $av -eq $AvaloniaVersions[-1])
  if(!$SkipDesktop){
   & "$PSScriptRoot/test-desktop-package.ps1" -Version $Version -AvaloniaVersion $av -Packages $feed -Output "$rowPath/desktop" -NativeAot:$aot *> "$rowPath/desktop.log"
   if($LASTEXITCODE -ne 0){Get-Content "$rowPath/desktop.log";throw "Avalonia $av desktop failed."}
   if((Get-FileHash "$rowPath/desktop/bin/SimdSkia.Avalonia.dll" -Algorithm SHA256).Hash -ne $expected){throw 'Desktop adapter differs from tested package.'}
  }
  $rows+=@{avalonia=$av;assemblyVersion=$loaded;adapterSha256=$actual;controls='passed';desktop=$(if($SkipDesktop){'not-run'}else{'passed'});nativeAot=$(if($aot -and !$SkipDesktop){'passed'}else{'not-run'})}
  $rows|ConvertTo-Json -Depth 8|Set-Content "$outPath/results.json" -Encoding utf8
  Write-Output "PASS Avalonia ${av}: same adapter binary, controls, desktop=$(!$SkipDesktop), NativeAot=$aot"
 }
}
finally{Pop-Location}
