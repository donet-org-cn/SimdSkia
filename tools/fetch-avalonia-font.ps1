$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destinationPath=Join-Path $workspacePath 'baseline/fonts'
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$sourceRoot='https://raw.githubusercontent.com/google/fonts/dac28ca2a883c27a817104b72bfa8d5e7e973954/ofl/zcoolxiaowei/'
$fontPath=Join-Path $destinationPath 'ZCOOLXiaoWei-Regular.ttf'
$expectedHash='A42B620140F493DB42F741351DFBF343C0936D58588EE8004B8B2A218D997FF1'
if(-not(Test-Path -LiteralPath $fontPath) -or (Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash -ne $expectedHash){Invoke-WebRequest -UseBasicParsing -Uri ($sourceRoot+'ZCOOLXiaoWei-Regular.ttf') -OutFile $fontPath -TimeoutSec 120}
if((Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash -ne $expectedHash){throw 'Avalonia CJK fixture checksum mismatch.'}
$licensePath=Join-Path $destinationPath 'ZCOOLXiaoWei-OFL.txt'
if(-not(Test-Path -LiteralPath $licensePath)){Invoke-WebRequest -UseBasicParsing -Uri ($sourceRoot+'OFL.txt') -OutFile $licensePath -TimeoutSec 30}
Write-Output $fontPath
