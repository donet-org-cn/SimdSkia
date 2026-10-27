param([string]$Checkout='artifacts/velashell-compat-2026-10-27/VelaShell')
$ErrorActionPreference='Stop'
$simdRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$velaRoot=[IO.Path]::GetFullPath((Join-Path $simdRoot $Checkout))
$entry=Join-Path $velaRoot 'src/VelaShell/Program.cs'
$project=Join-Path $velaRoot 'src/VelaShell/VelaShell.csproj'
$entryText=[IO.File]::ReadAllText($entry)
$trigger='BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);'
if(!$entryText.Contains($trigger)){throw 'Unexpected upstream startup entry; inspect before modifying.'}
$entryText=$entryText.Replace($trigger,'SimdSkiaVelaProbe.Configure(BuildAvaloniaApp()).StartWithClassicDesktopLifetime(args);')
[IO.File]::WriteAllText($entry,$entryText,[Text.UTF8Encoding]::new($false))
$projectText=[IO.File]::ReadAllText($project)
$desktop=[Security.SecurityElement]::Escape((Join-Path $simdRoot 'src/SimdSkia.Avalonia.Desktop/SimdSkia.Avalonia.Desktop.csproj'))
$gpu=[Security.SecurityElement]::Escape((Join-Path $simdRoot 'src/SimdSkia.Avalonia.Gpu/SimdSkia.Avalonia.Gpu.csproj'))
$probe=[Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'Probe.cs'))
$references=@"
  <ItemGroup>
    <ProjectReference Include="$desktop" />
    <ProjectReference Include="$gpu" />
    <Compile Include="$probe" Link="SimdSkiaVelaProbe.cs" />
  </ItemGroup>
"@
$projectText=$projectText.Replace('</Project>',$references+"`n</Project>")
[IO.File]::WriteAllText($project,$projectText,[Text.UTF8Encoding]::new($false))
Write-Output 'Prepared isolated VelaShell runtime backend probe. No production engine or upstream remote was changed.'
