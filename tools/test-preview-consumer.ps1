param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+-preview\.\d+$')][string]$Version,
    [string]$Packages='artifacts/nuget-preview',
    [switch]$SystemFonts
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try {
    $packagePath=[IO.Path]::GetFullPath($Packages)
    $outputPath=[IO.Path]::GetFullPath('artifacts/nuget-consumer')
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    # A private cache proves these packages, rather than old globally cached builds,
    # were consumed. The source projects are not referenced by PackageConsumer.
    $digest=(Get-FileHash -LiteralPath (Join-Path $packagePath 'manifest.json') -Algorithm SHA256).Hash.Substring(0,12).ToLowerInvariant()
    $cachePath=Join-Path $outputPath "packages-$Version-$digest"
    $configPath=Join-Path $outputPath 'NuGet.Config'
    $escapedPackagePath=[Security.SecurityElement]::Escape($packagePath)
    [IO.File]::WriteAllText($configPath,"<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><packageSources><clear/><add key=`"preview`" value=`"$escapedPackagePath`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>",[Text.UTF8Encoding]::new($false))
    dotnet restore tests/PackageConsumer/PackageConsumer.csproj -p:SimdSkiaPackageVersion=$Version -p:PackageConsumerAot=true --configfile $configPath --packages $cachePath
    if($LASTEXITCODE -ne 0){throw 'Restoring the actual packed consumer failed.'}
    dotnet publish tests/PackageConsumer/PackageConsumer.csproj -c Release -p:SimdSkiaPackageVersion=$Version -p:PackageConsumerAot=true --no-restore -o "$outputPath/publish" > "$outputPath/publish.log" 2>&1
    if($LASTEXITCODE -ne 0){Get-Content "$outputPath/publish.log";throw 'Native AOT NuGet consumer compilation failed.'}
    $arguments="--hidden --output `"$outputPath`""
    if(!$SystemFonts){$font=(Resolve-Path -LiteralPath baseline/fonts/ZCOOLXiaoWei-Regular.ttf).Path;$arguments+=" --font `"$font`""}
    $startInfo=[Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName="$outputPath/publish/PackageConsumer.exe"
    $startInfo.Arguments=$arguments
    $startInfo.UseShellExecute=$false
    $startInfo.CreateNoWindow=$true
    $startInfo.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.RedirectStandardOutput=$true
    $startInfo.RedirectStandardError=$true
    $process=[Diagnostics.Process]::Start($startInfo)
    $stdout=$process.StandardOutput.ReadToEndAsync()
    $stderr=$process.StandardError.ReadToEndAsync()
    if(!$process.WaitForExit(30000)){$process.Kill();throw 'NuGet consumer did not finish within 30 seconds.'}
    [IO.File]::WriteAllText("$outputPath/stdout.log",$stdout.Result,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText("$outputPath/stderr.log",$stderr.Result,[Text.UTF8Encoding]::new($false))
    if($process.ExitCode -ne 0){Get-Content "$outputPath/stderr.log";throw "NuGet consumer exited with $($process.ExitCode)."}
    $metrics=Get-Content "$outputPath/metrics.json" -Raw|ConvertFrom-Json
    if(!$metrics.NativeAot -or !$metrics.PixelsVerified -or $metrics.Frames -le 0){throw 'NuGet consumer did not verify native rendering.'}
    if($metrics.LoadedModules -match '^(coreclr|clrjit|clr|mscorwks|mscorsvr)\.dll$'){throw 'JIT runtime loaded into the AOT consumer.'}
    $bytes=[IO.File]::ReadAllBytes($startInfo.FileName)
    $header=[BitConverter]::ToInt32($bytes,0x3c)+24
    if([BitConverter]::ToUInt16($bytes,$header+68) -ne 2 -or [BitConverter]::ToUInt64($bytes,$header+112+14*8) -ne 0){throw 'Consumer is not a native Windows GUI image.'}
    Get-Content "$outputPath/stdout.log"
}
finally{Pop-Location}
