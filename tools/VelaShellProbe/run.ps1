param(
    [ValidateSet('original','bad-host','cpu','direct2d','vulkan')][string]$Backend='cpu',
    [string]$Checkout='artifacts/velashell-compat-2026-10-27/VelaShell',
    [string]$Output='',
    [int]$TimeoutSeconds=115,
    [switch]$Combining,
    [ValidateSet('all','terminal','support')][string]$Scenes='all'
)
$ErrorActionPreference='Stop'
$simdRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$velaRoot=[IO.Path]::GetFullPath((Join-Path $simdRoot $Checkout))
if(!$Output){$Output="artifacts/velashell-compat-2026-10-27/$Backend"}
$resultRoot=[IO.Path]::GetFullPath((Join-Path $simdRoot $Output))
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
$executable=Join-Path $velaRoot 'src/VelaShell/bin/Debug/net11.0/VelaShell.exe'
if(!(Test-Path -LiteralPath $executable)){throw 'Build the isolated VelaShell probe first.'}
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName=$executable
$start.WorkingDirectory=[IO.Path]::GetDirectoryName($executable)
$start.Arguments='--data-root "'+(Join-Path $resultRoot 'data')+'"'
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
$start.EnvironmentVariables['VELASHELL_PROBE_BACKEND']=$Backend
$start.EnvironmentVariables['VELASHELL_PROBE_OUTPUT']=$resultRoot
$start.EnvironmentVariables['VELASHELL_PROBE_OFFSCREEN']='1'
$start.EnvironmentVariables['VELASHELL_NO_SPLASH']='1'
$start.EnvironmentVariables['VELASHELL_PROBE_COMBINING']=if($Combining){'1'}else{'0'}
$start.EnvironmentVariables['VELASHELL_PROBE_SCENES']=$Scenes
$start.EnvironmentVariables.Remove('VELASHELL_SOFTWARE_RENDER')
$process=[Diagnostics.Process]::Start($start)
$stdout=$process.StandardOutput.ReadToEndAsync()
$stderr=$process.StandardError.ReadToEndAsync()
$watch=[Diagnostics.Stopwatch]::StartNew()
$samples=[Collections.Generic.List[object]]::new()
$timedOut=$false
while(!$process.WaitForExit(1000)){
    $process.Refresh()
    $samples.Add([pscustomobject]@{seconds=$watch.Elapsed.TotalSeconds;cpuSeconds=$process.TotalProcessorTime.TotalSeconds;workingSetMiB=$process.WorkingSet64/1MB;privateMiB=$process.PrivateMemorySize64/1MB})
    if($watch.Elapsed.TotalSeconds -ge $TimeoutSeconds){$timedOut=$true;$process.Kill();$process.WaitForExit();break}
}
[IO.File]::WriteAllText((Join-Path $resultRoot 'stdout.log'),$stdout.Result,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $resultRoot 'stderr.log'),$stderr.Result,[Text.UTF8Encoding]::new($false))
$envelope=[pscustomobject]@{backend=$Backend;exitCode=$process.ExitCode;timedOut=$timedOut;seconds=$watch.Elapsed.TotalSeconds;samples=$samples.ToArray()}
$envelope|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $resultRoot 'process.json') -Encoding UTF8
Write-Output ($Backend+': exit='+$process.ExitCode+'; timeout='+$timedOut+'; seconds='+[Math]::Round($watch.Elapsed.TotalSeconds,1))
if(Test-Path -LiteralPath (Join-Path $resultRoot 'result.json')){
    $result=Get-Content -LiteralPath (Join-Path $resultRoot 'result.json') -Raw -Encoding UTF8|ConvertFrom-Json
    Write-Output ('Scenes='+$result.scenes.Count+'; failures='+$result.failureCount+'; renderer='+$result.renderer+'; frames='+$result.presentedFrames)
}else{Get-Content -LiteralPath (Join-Path $resultRoot 'stderr.log') -Tail 15 -Encoding UTF8}
