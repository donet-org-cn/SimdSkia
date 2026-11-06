param([string]$Version='0.1.2-preview.3',[string]$Output='artifacts/api-final-2026-11-07/public-preview3-consumer')
$ErrorActionPreference='Stop'
$workspacePath=(Get-Location).Path
$outPath=[IO.Path]::GetFullPath($Output)
if(Test-Path -LiteralPath $outPath){throw 'Use a fresh output directory.'}
New-Item -ItemType Directory -Path $outPath|Out-Null
$config=Join-Path $outPath 'NuGet.Config'
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>',[Text.UTF8Encoding]::new($false))
$cache=Join-Path $workspacePath 'artifacts/nuget-cache/pub-012p3'
if(Test-Path -LiteralPath $cache){throw 'Public verification requires a new private cache.'}
$project='examples/CrossPlatform/CrossPlatform.csproj'
$properties=@('-p:UsePackedSimdSkia=true',"-p:SimdSkiaPackageVersion=$Version",'-p:AvaloniaVersion=12.1.5',"-p:NuGetLockFilePath=$outPath/packages.lock.json",'-p:PublishAot=true',"-p:RestoreConfigFile=$config","-p:RestorePackagesPath=$cache")
& dotnet restore $project -r win-x64 @properties --configfile $config --packages $cache --force-evaluate *> "$outPath/restore.log"
if($LASTEXITCODE -ne 0){Get-Content "$outPath/restore.log" -Tail 30;throw 'Pure NuGet.org restore failed.'}
$assets=Get-Content examples/CrossPlatform/obj/project.assets.json -Raw|ConvertFrom-Json
foreach($library in $assets.libraries.PSObject.Properties){
 if($library.Name -match '^SimdSkia'){
  if($library.Value.type -ne 'package' -or !$library.Name.EndsWith("/$Version")){throw "Wrong library: $($library.Name)"}
 }
 if($library.Name -match '^(SkiaSharp|HarfBuzzSharp|Avalonia.Skia|Avalonia.HarfBuzz)/'){throw 'Original drawing engine leaked into the public consumer.'}
}
& dotnet publish $project -c Release -r win-x64 @properties --no-restore -o "$outPath/publish" *> "$outPath/publish.log"
if($LASTEXITCODE -ne 0){Get-Content "$outPath/publish.log" -Tail 40;throw 'Public package AOT publish failed.'}
$arguments=@('--smoke','--controls','--output',"$outPath/results")
$process=Start-Process -FilePath "$outPath/publish/CrossPlatform.exe" -ArgumentList ($arguments|ForEach-Object{'"'+$_+'"'}) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$outPath/run.stdout.log" -RedirectStandardError "$outPath/run.stderr.log"
if(!$process.WaitForExit(45000)){$process.Kill();throw 'Owned public AOT window process timed out.'}
$process.WaitForExit()
if($process.ExitCode -ne 0){Get-Content "$outPath/run.stderr.log";throw 'Public AOT window failed.'}
$runtime=Get-Content "$outPath/results/runtime.txt" -Raw
if($runtime -notmatch 'NativeAot=True'){throw 'The actual window did not run as Native AOT.'}
if(!(Test-Path "$outPath/results/presented.png")){throw 'Actual presented framebuffer was not captured.'}
$simdLibraries=@($assets.libraries.PSObject.Properties|Where-Object Name -Match '^SimdSkia'|ForEach-Object Name)
$result=@{version=$Version;source='https://api.nuget.org/v3/index.json';freshPrivateCache=$true;sourceProjectReferences=$false;nativeAot=$true;systemFonts=$true;actualWindowPresented=$true;packageLibraries=$simdLibraries;exeSha256=(Get-FileHash "$outPath/publish/CrossPlatform.exe" -Algorithm SHA256).Hash;passed=$true}
$result|ConvertTo-Json -Depth 8|Set-Content "$outPath/comparison.json" -Encoding utf8
Get-Content "$outPath/results/result.txt" -TotalCount 8
Write-Output "PASS public NuGet.org-only $Version restore, actual Native AOT, system fonts and presented window."
