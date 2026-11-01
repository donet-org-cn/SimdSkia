param([string]$Packages='artifacts/simdcodecs-integration-2026-11-01/packages',[string]$Output='artifacts/simdcodecs-integration-2026-11-01/package-codecs')
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));Push-Location -LiteralPath $workspacePath
try {
    $out=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Force $out|Out-Null
    $feed=[IO.Path]::GetFullPath($Packages);$digest=(Get-FileHash -LiteralPath (Join-Path $feed 'manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $cache=Join-Path $out "packages-$digest";$config=Join-Path $out 'NuGet.Config'
    $escaped=[Security.SecurityElement]::Escape($feed)
    [IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key=`"trial`" value=`"$escaped`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>",[Text.UTF8Encoding]::new($false))
    dotnet restore tests/CodecIntegration -p:UsePackedSimdSkia=true -p:SimdSkiaPackageVersion=0.1.0-codecs-trial.1 --configfile $config --packages $cache --force-evaluate
    if($LASTEXITCODE -ne 0){throw 'Trial codec package restore failed.'}
    $assets=Get-Content tests/CodecIntegration/obj/project.assets.json -Raw|ConvertFrom-Json
    foreach($library in $assets.libraries.PSObject.Properties){if($library.Name -match '^SimdSkia' -and $library.Value.type -ne 'package'){throw 'A source engine reference leaked into package validation.'}}
    dotnet build tests/CodecIntegration -c Release -p:UsePackedSimdSkia=true -p:SimdSkiaPackageVersion=0.1.0-codecs-trial.1 --no-restore -o "$out/bin" > "$out/build.log" 2>&1
    if($LASTEXITCODE -ne 0){Get-Content "$out/build.log";throw 'Trial codec package build failed.'}
    & dotnet "$out/bin/CodecIntegration.dll" "$out/results" > "$out/run.log" 2>&1
    if($LASTEXITCODE -ne 0){Get-Content "$out/run.log";throw 'Trial codec package pixel/metadata/lifetime validation failed.'}
    Get-Content "$out/run.log";Write-Output 'PASS real codec calls from actual trial nupkg graph; no source engine references.'
}
finally {Pop-Location}
