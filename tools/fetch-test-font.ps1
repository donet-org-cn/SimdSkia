$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destinationPath=Join-Path $workspacePath 'baseline/fonts'
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$sourceRoot='https://raw.githubusercontent.com/google/fonts/dac28ca2a883c27a817104b72bfa8d5e7e973954/ofl/zenoldmincho/'
$fontPath=Join-Path $destinationPath 'ZenOldMincho-Regular.ttf'
if(-not(Test-Path -LiteralPath $fontPath)){Invoke-WebRequest -Uri ($sourceRoot+'ZenOldMincho-Regular.ttf') -OutFile $fontPath -TimeoutSec 120}
if((Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash -ne '4C051A78A21C4E8E9DCCF1C754776D33F356B8CC6EF95D9B64761B9BAE814B84'){throw 'CJK font fixture checksum mismatch.'}
$licensePath=Join-Path $destinationPath 'OFL.txt'
if(-not(Test-Path -LiteralPath $licensePath)){Invoke-WebRequest -Uri ($sourceRoot+'OFL.txt') -OutFile $licensePath -TimeoutSec 30}
Write-Output $fontPath
