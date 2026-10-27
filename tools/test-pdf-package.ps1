param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+-preview\.\d+$')][string]$Version,
    [string]$Packages='artifacts/nuget-preview',
    [string]$Output='artifacts/pdf-package-consumer'
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try {
    $outputPath=[IO.Path]::GetFullPath($Output)
    $packagePath=[IO.Path]::GetFullPath($Packages)
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    $digest=(Get-FileHash -LiteralPath (Join-Path $packagePath 'manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $cachePath=Join-Path $outputPath "packages-$digest"
    $configPath=Join-Path $outputPath 'NuGet.Config'
    $escapedPath=[Security.SecurityElement]::Escape($packagePath)
    [IO.File]::WriteAllText($configPath,"<configuration><packageSources><clear/><add key=`"packed`" value=`"$escapedPath`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>",[Text.UTF8Encoding]::new($false))
    dotnet restore tests/PdfPackageConsumer/PdfPackageConsumer.csproj -p:SimdSkiaPackageVersion=$Version --configfile $configPath --packages $cachePath --force-evaluate
    if($LASTEXITCODE -ne 0){throw 'Packed PDF consumer restore failed.'}
    $assets=Get-Content tests/PdfPackageConsumer/obj/project.assets.json -Raw|ConvertFrom-Json
    foreach($library in $assets.libraries.PSObject.Properties){
        if($library.Name -match '^SimdSkia' -and $library.Value.type -ne 'package'){throw 'Source engine reference leaked into PDF package consumer.'}
    }
    dotnet build tests/PdfPackageConsumer/PdfPackageConsumer.csproj -c Release -p:SimdSkiaPackageVersion=$Version --no-restore -o "$outputPath/bin" > "$outputPath/build.log" 2>&1
    if($LASTEXITCODE -ne 0){Get-Content "$outputPath/build.log";throw 'Packed PDF consumer build failed.'}
    & dotnet "$outputPath/bin/PdfPackageConsumer.dll" "$outputPath/results" > "$outputPath/run.log" 2>&1
    if($LASTEXITCODE -ne 0){Get-Content "$outputPath/run.log";throw 'Packed PDF consumer validation failed.'}
    Get-Content "$outputPath/run.log"
    Write-Output 'PASS isolated PDF nupkg consumer; real exported/read pixels, external fixtures and unsupported-feature errors verified.'
}
finally {Pop-Location}
