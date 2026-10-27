param([string]$Checkout='artifacts/velashell-compat-2026-10-27/VelaShell')
$ErrorActionPreference='Stop'
$simdRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$velaRoot=[IO.Path]::GetFullPath((Join-Path $simdRoot $Checkout))
$session=Join-Path $velaRoot 'tests/VelaShell.Tests/Views/VelaHeadlessApp.cs'
$project=Join-Path $velaRoot 'tests/VelaShell.Tests/VelaShell.Tests.csproj'
$text=[IO.File]::ReadAllText($session)
if(!$text.Contains('.UseSkia()')){throw 'Unexpected upstream view test renderer.'}
[IO.File]::WriteAllText($session,$text.Replace('.UseSkia()','.UseVelaViewRenderer()'),[Text.UTF8Encoding]::new($false))
$helper=[Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'ViewRenderer.cs'))
$text=[IO.File]::ReadAllText($project)
$references="  <ItemGroup><Compile Include=`"$helper`" Link=`"ProbeViewRenderer.cs`" /></ItemGroup>"
[IO.File]::WriteAllText($project,$text.Replace('</Project>',$references+"`n</Project>"),[Text.UTF8Encoding]::new($false))
Write-Output 'Prepared actual VelaShell view tests with a process-specific renderer.'
