param(
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$Packages='artifacts/nuget-preview',
    [string]$Output='artifacts/android-package-consumer',
    [string]$AndroidSdkDirectory=$env:ANDROID_HOME,
    [string]$JavaSdkDirectory=$env:JAVA_HOME,
    [switch]$PublicNuGet
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspacePath
try {
    $outputPath=[IO.Path]::GetFullPath($Output)
    $projectPath=Join-Path $outputPath 'project'
    $resultsPath=Join-Path $outputPath 'results'
    New-Item -ItemType Directory -Path $projectPath,$resultsPath -Force | Out-Null
    $samplePath=Join-Path $workspacePath 'examples/Mobile.Android'
    Copy-Item -Path "$samplePath/*.cs" -Destination $projectPath -Force
    Copy-Item -LiteralPath "$samplePath/Resources" -Destination $projectPath -Recurse -Force
    [xml]$project=Get-Content "$samplePath/Mobile.Android.csproj" -Raw
    # The isolated consumer reuses the sample's normal Application/Activity/UI.
    # Its engine references come exclusively from the package feed.
    foreach($reference in @($project.SelectNodes('//ProjectReference'))){$reference.ParentNode.RemoveChild($reference) | Out-Null}
    foreach($import in @($project.SelectNodes('//Import'))){$import.ParentNode.RemoveChild($import) | Out-Null}
    foreach($asset in $project.SelectNodes('//AndroidAsset')){
        $asset.SetAttribute('Include',[IO.Path]::GetFullPath((Join-Path $samplePath $asset.GetAttribute('Include'))))
    }
    $consumerProject=Join-Path $projectPath 'Mobile.Android.csproj'
    $project.Save($consumerProject)
    $cachePath=Join-Path $outputPath ('packages-'+[Guid]::NewGuid().ToString('N'))
    $configPath=Join-Path $outputPath 'NuGet.Config'
    $feed=''
    if(!$PublicNuGet){
        $packagePath=[IO.Path]::GetFullPath($Packages)
        $escaped=[Security.SecurityElement]::Escape($packagePath)
        $feed="<add key=`"release`" value=`"$escaped`"/>"
    }
    [IO.File]::WriteAllText($configPath,"<configuration><packageSources><clear/>$feed<add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>")
    $properties=@('-p:UsePackedSimdSkia=true',"-p:SimdSkiaPackageVersion=$Version",'-p:RestorePackagesWithLockFile=true',
        "-p:AndroidSdkDirectory=$AndroidSdkDirectory","-p:JavaSdkDirectory=$JavaSdkDirectory")
    dotnet restore $consumerProject @properties -r android-arm64 --configfile $configPath --packages $cachePath --no-http-cache --force-evaluate *> "$outputPath/restore.log"
    if($LASTEXITCODE -ne 0){Get-Content "$outputPath/restore.log" -Tail 25;throw 'Android package-only restore failed.'}
    $assets=Get-Content "$projectPath/obj/project.assets.json" -Raw | ConvertFrom-Json
    $engines=@($assets.libraries.PSObject.Properties | Where-Object Name -like 'SimdSkia*')
    if($engines.Count -lt 5){throw 'Missing Android engine packages.'}
    foreach($library in $engines){
        if($library.Value.type -ne 'package' -or !$library.Name.EndsWith("/$Version")){throw "Source or stale engine reference: $($library.Name)"}
    }
    $forbidden=@($assets.libraries.PSObject.Properties.Name | Where-Object {$_ -match '^(SkiaSharp|HarfBuzz|Avalonia\.Skia|Avalonia\.Android|Mono\.Cecil)/'})
    if($forbidden.Count){throw "Unexpected rendering/tool dependency: $forbidden"}
    dotnet publish $consumerProject @properties -c Release -r android-arm64 --no-restore -o "$outputPath/publish" *> "$outputPath/publish.log"
    if($LASTEXITCODE -ne 0){Get-Content "$outputPath/publish.log" -Tail 30;throw 'Android package-only Release publish failed.'}
    $apk=Get-Item "$outputPath/publish/org.simdskia.androidsample-Signed.apk"
    $archive=[IO.Compression.ZipFile]::OpenRead($apk.FullName)
    try {
        $names=@($archive.Entries.FullName)
        $forbidden=@($names | Where-Object {$_ -match '(?i)SkiaSharp|HarfBuzz|Avalonia\.Skia|Mono\.Cecil'})
        if($forbidden.Count){throw "Unexpected APK asset: $forbidden"}
        foreach($license in @('assets/Fonts/OFL.txt','assets/Licenses/Avalonia-MIT.txt','assets/Licenses/AndroidX-Apache-2.0.txt')){
            if($license -notin $names){throw "Missing APK license: $license"}
        }
        $aot=@($names | Where-Object {$_ -match '/libaot-.*\.dll\.so$'})
        if($aot.Count -eq 0){throw 'Missing Mono AOT output.'}
    }
    finally {$archive.Dispose()}
    $summary=[ordered]@{
        version=$Version;passed=$true;source=if($PublicNuGet){'NuGet.org-only'}else{'local release feed and NuGet.org'}
        freshPrivateCache=$true;enginePackages=@($engines.Name);sourceEngineProjects=$false
        apkBytes=$apk.Length;apkSha256=(Get-FileHash $apk.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        monoAotAssemblies=$aot.Count;forbiddenAssets=$forbidden;deviceRunPerformed=$false
    }
    $summary | ConvertTo-Json -Depth 6 | Set-Content "$resultsPath/results.json" -Encoding utf8
    Copy-Item "$projectPath/packages.lock.json" "$resultsPath/packages.lock.json" -Force
    Write-Output "PASS actual Android $Version packages, independent cache, Release/Mono AOT APK and dependency checks"
}
finally {Pop-Location}
