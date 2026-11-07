$ErrorActionPreference='Stop'
$taskRoot=Join-Path $PWD 'artifacts/release-0.1.3/public-consumer'
New-Item -ItemType Directory -Path $taskRoot -Force|Out-Null
$taskConfig=Join-Path $taskRoot 'NuGet.Config'
[IO.File]::WriteAllText($taskConfig,'<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
$taskCache=Join-Path $taskRoot 'cache'
$taskLock=Join-Path $taskRoot 'packages.lock.json'
$taskProps=@('-p:UsePackedSimdSkia=true','-p:SimdSkiaPackageVersion=0.1.3','-p:AvaloniaVersion=12.1.5',"-p:NuGetLockFilePath=$taskLock",'-p:PublishAot=true')
dotnet restore examples/CrossPlatform/CrossPlatform.csproj @taskProps -r win-x64 --configfile $taskConfig --packages $taskCache --no-http-cache --force-evaluate *> "$taskRoot/restore.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/restore.log" -Tail 15;throw 'Public-only restore failed'}
$taskAssets=Get-Content examples/CrossPlatform/obj/project.assets.json -Raw|ConvertFrom-Json
foreach($item in $taskAssets.libraries.PSObject.Properties){if($item.Name -like 'SimdSkia*' -and ($item.Value.type -ne 'package' -or !$item.Name.EndsWith('/0.1.3'))){throw 'Unexpected engine source/version'}}
dotnet publish examples/CrossPlatform/CrossPlatform.csproj @taskProps -c Release -r win-x64 --no-restore -o "$taskRoot/publish" *> "$taskRoot/publish.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/publish.log" -Tail 15;throw 'Public-only AOT publish failed'}
$taskProcess=Start-Process -FilePath "$taskRoot/publish/CrossPlatform.exe" -ArgumentList @('--smoke','--controls','--output',"`"$taskRoot/results`"") -Wait -PassThru -WindowStyle Hidden
if($taskProcess.ExitCode -ne 0){throw 'Public-only Native AOT window failed'}
$taskRuntime=Get-Content "$taskRoot/results/runtime.txt" -Raw
if($taskRuntime -notmatch 'NativeAot=True'){throw 'Not actual Native AOT'}
if($taskRuntime -match '(?i)coreclr\.dll|clrjit\.dll|libSkiaSharp|HarfBuzz'){throw 'Unexpected runtime/native renderer'}
Get-Content "$taskRoot/results/runtime.txt" -TotalCount 4
'PASS NuGet.org-only, no HTTP cache, fresh private cache, real Native AOT default renderer'
