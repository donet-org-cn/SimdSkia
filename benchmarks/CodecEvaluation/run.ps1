param(
    [string]$Output='artifacts/simdcodecs-evaluation-2026-11-01',
    [int]$Rounds=3,
    [double]$Seconds=1.2
)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $workspacePath
try {
    $outputPath=[IO.Path]::GetFullPath($Output)
    $fixturePath=Join-Path $outputPath 'fixtures'
    $resultsPath=Join-Path $outputPath 'runs'
    New-Item -ItemType Directory -Force $resultsPath | Out-Null
    $hostMetadata=@{cpu=(Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name);rounds=$Rounds;seconds=$Seconds;warmupSeconds=0.5}
    [IO.File]::WriteAllText((Join-Path $outputPath 'host.json'),($hostMetadata | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    $binaryPath=Join-Path $workspacePath 'benchmarks/CodecEvaluation/bin/Release/net10.0/CodecEvaluation.dll'
    # Run builds, validation and AOT compilation before this script. No competing
    # compilation/measurement jobs: each engine/case gets a fresh serial process.
    $operations=@('decode-jpeg444','decode-jpeg420','decode-png','encode-jpeg444','encode-png')
    for($round=1;$round -le $Rounds;$round++) {
        foreach($scene in @('ui','texture')) {
            foreach($operation in $operations) {
                $engines=if($round%2 -eq 1){@('existing','sdcb')}else{@('sdcb','existing')}
                foreach($engine in $engines) {
                    $key="$scene-$operation-$engine-$round"
                    $resultPath=Join-Path $resultsPath "$key.json"
                    & dotnet $binaryPath run $fixturePath $engine $scene $operation $resultPath $Seconds.ToString([Globalization.CultureInfo]::InvariantCulture) > (Join-Path $resultsPath "$key.log") 2>&1
                    if($LASTEXITCODE -ne 0){Get-Content (Join-Path $resultsPath "$key.log");throw "Codec benchmark failed: $key"}
                }
            }
        }
        Write-Output "Completed serial codec benchmark round $round/$Rounds"
    }
}
finally {Pop-Location}
