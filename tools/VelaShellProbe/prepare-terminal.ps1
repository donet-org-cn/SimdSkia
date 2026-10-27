param([string]$Checkout='artifacts/velashell-compat-2026-10-27/VelaShell')
$ErrorActionPreference='Stop'
$simdRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$velaRoot=[IO.Path]::GetFullPath((Join-Path $simdRoot $Checkout))
$session=Join-Path $velaRoot 'tests/VelaShell.Terminal.RenderTests/SkiaTestSession.cs'
$project=Join-Path $velaRoot 'tests/VelaShell.Terminal.RenderTests/VelaShell.Terminal.RenderTests.csproj'
$text=[IO.File]::ReadAllText($session)
if(!$text.Contains('.UseSkia()')){throw 'Unexpected upstream terminal test renderer.'}
[IO.File]::WriteAllText($session,$text.Replace('.UseSkia()','.UseVelaTestRenderer()'),[Text.UTF8Encoding]::new($false))
$adapter=[Security.SecurityElement]::Escape((Join-Path $simdRoot 'src/SimdSkia.Avalonia/SimdSkia.Avalonia.csproj'))
$helper=[Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'TerminalRenderer.cs'))
$text=[IO.File]::ReadAllText($project)
$references=@"
  <ItemGroup>
    <ProjectReference Include="$adapter" />
    <Compile Include="$helper" Link="ProbeTerminalRenderer.cs" />
  </ItemGroup>
"@
[IO.File]::WriteAllText($project,$text.Replace('</Project>',$references+"`n</Project>"),[Text.UTF8Encoding]::new($false))
Write-Output 'Prepared original terminal pixel tests with a process-specific backend switch.'
