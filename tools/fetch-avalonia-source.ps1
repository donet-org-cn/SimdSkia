param()
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$archive=Join-Path $workspacePath 'baseline/avalonia-source-12.1.4.zip'
$destination=Join-Path $workspacePath 'baseline/avalonia-source-12.1.4'
$uri='https://codeload.github.com/AvaloniaUI/Avalonia/zip/refs/tags/12.1.4'
$expected='AF6A2868AA226068811B1885C1A09FA658A05900250C33F2C4D32C77D9D6D39D'
if(-not(Test-Path -LiteralPath $archive)){Invoke-WebRequest $uri -OutFile $archive -TimeoutSec 120}
if((Get-FileHash -LiteralPath $archive).Hash -ne $expected){throw 'Pinned Avalonia archive hash differs; investigate before updating the baseline.'}
New-Item -ItemType Directory -Force -Path $destination|Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach($entry in $zip.Entries){
        $prefix='Avalonia-12.1.4/src/Skia/Avalonia.Skia/'
        $relative=if($entry.FullName.StartsWith($prefix)){$entry.FullName.Substring($prefix.Length)}elseif($entry.FullName -eq 'Avalonia-12.1.4/licence.md'){'licence.md'}else{''}
        if(-not $relative -or -not $entry.Name){continue}
        $path=[IO.Path]::GetFullPath((Join-Path $destination $relative))
        if(-not $path.StartsWith($destination+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive path escapes source directory.'}
        New-Item -ItemType Directory -Force -Path (Split-Path $path)|Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$true)
    }
}
finally{$zip.Dispose()}
$hashes=[ordered]@{};foreach($file in (Get-ChildItem -LiteralPath $destination -Recurse -File|Where-Object Extension -eq '.cs'|Sort-Object FullName)){$hashes[$file.FullName.Substring($destination.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $file.FullName).Hash}
$record=[ordered]@{Version='12.1.4';Archive=$uri;ArchiveSha256=$expected;License='MIT; licence.md in source archive';ProductDependency=$false;SourceFilesSha256=$hashes}
$record|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $workspacePath 'docs/avalonia-source-baseline.json') -Encoding UTF8
Write-Output ('Pinned Avalonia.Skia source files: '+$hashes.Count)
