# VelaShell compatibility probe

This tooling builds and instruments an **isolated clone** of VelaShell. It is not
part of a SimdSkia NuGet package. The target repository remains under its own
license (AGPL-3.0 / upstream commercial terms); its code is not copied into the
MIT engine. Read the target's AGENTS.md and host architecture before changing it.
Use PowerShell/C# for this integration; do not use Python in the target checkout.

The 2026-10-27 run uses upstream commit
`3c8a20ef739ef4fc2d3363a9d7aaf8ba15ca7b7c`, Avalonia 12.1.4 and the installed
.NET 11 RC1 SDK. Its selected SDK is confined to the experiment's parent directory.

From the SimdSkia root:

```powershell
$runRoot = 'artifacts/velashell-compat-2026-10-27'
New-Item -ItemType Directory -Path $runRoot -Force
git clone --depth 1 https://github.com/joesdu/VelaShell "$runRoot/VelaShell"
# Put a global.json selecting your installed .NET 11 SDK in $runRoot.
# First build unmodified upstream in Debug; Release requires the author's signing key.
Push-Location "$runRoot/VelaShell"
dotnet build src/VelaShell/VelaShell.csproj -c Debug
Pop-Location

./tools/VelaShellProbe/prepare.ps1
./tools/VelaShellProbe/prepare-terminal.ps1
./tools/VelaShellProbe/prepare-views.ps1
Push-Location "$runRoot/VelaShell"
dotnet build src/VelaShell/VelaShell.csproj -c Debug
Pop-Location
./tools/VelaShellProbe/run.ps1 -Backend original -Output "$runRoot/original-final"
./tools/VelaShellProbe/run.ps1 -Backend cpu -Output "$runRoot/cpu-final"
./tools/VelaShellProbe/run.ps1 -Backend direct2d -Output "$runRoot/direct2d-final"
./tools/VelaShellProbe/run.ps1 -Backend vulkan -Output "$runRoot/vulkan-final"
```

Preparation expects pristine entry points and deliberately fails if run twice.
Do not apply it to your normal checkout. Each run receives its own `--data-root`,
does not use the normal user's connections, disables the separate native splash,
and shuts down automatically. Windows are positioned outside the screen for automation.

`bad-host` deliberately retains the target's default ANGLE host and reproduces the
reported `CreateBackendContext` failure. `-Scenes support` limits an image/layout
reproduction to three windows/pages. `-Scenes terminal -Combining` reproduces the
unimplemented combining-mark case in a separate process; it is expected to fail.

The main probe renders a real desktop window, 15 settings pages, light theme,
English/Chinese switching, terminal Latin/CJK/ANSI styles and a text box. It
rejects blank or unshown windows and records Avalonia errors plus GPU counters.
The application's own crash logs remain under that run's isolated `data/logs`.
Offscreen `RenderTargetBitmap` captures are CPU snapshots even when the live
window uses a GPU; do not label these GPU framebuffer quality comparisons.

The upstream terminal pixel and view tests select their backend per process:

```powershell
Push-Location artifacts/velashell-compat-2026-10-27/VelaShell
$env:VELASHELL_PROBE_BACKEND = 'original' # repeat with cpu
dotnet test tests/VelaShell.Terminal.RenderTests -c Debug
dotnet test tests/VelaShell.Tests -c Debug --filter 'FullyQualifiedName~VelaShell.Tests.Views' --blame-hang-timeout 90s
Pop-Location
```

These use upstream assertions and real drawing (`UseHeadlessDrawing=false`),
not the empty default headless renderer. The desktop probe itself is independent
of those tests. `collect.ps1` gathers results and compares full-page pixels at
max error 32 / RMSE 2, preserving failures.

For manual exploration of the instrumented checkout:

```powershell
$env:VELASHELL_PROBE_BACKEND = 'cpu'
$env:VELASHELL_PROBE_INTERACTIVE = '1'
$env:VELASHELL_NO_SPLASH = '1'
& artifacts/velashell-compat-2026-10-27/VelaShell/src/VelaShell/bin/Debug/net11.0/VelaShell.exe --data-root F:/simdSkia/artifacts/velashell-manual
```

Runtime/native dependencies of VelaShell itself, including its Skia splash preview,
are deliberately retained so the app can compile. The probe does not demonstrate
removal of all original Skia libraries. Debug timings with diagnostic captures
are not throughput, CPU utilization or steady-state memory benchmarks.
