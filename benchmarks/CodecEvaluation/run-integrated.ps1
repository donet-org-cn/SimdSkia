param([string]$Output='artifacts/simdcodecs-integration-2026-11-01',[double]$Seconds=1.2,[int]$Rounds=3)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));Push-Location -LiteralPath $workspacePath
try {
    $outputPath=[IO.Path]::GetFullPath($Output);$runs=Join-Path $outputPath 'runs';New-Item -ItemType Directory -Force $runs|Out-Null
    $fixtures=[IO.Path]::GetFullPath('artifacts/simdcodecs-evaluation-2026-11-01/fixtures')
    $before=Join-Path $outputPath 'frozen-baseline/CodecEvaluation.dll'
    $after=Join-Path $workspacePath 'benchmarks/CodecEvaluation/bin/Release/net10.0/CodecEvaluation.dll'
    if(!(Test-Path $before)){throw 'Build or freeze the preview.9 baseline first; see benchmark README.'}
    foreach($round in 1..$Rounds){foreach($scene in @('ui','texture')){foreach($operation in @('decode-jpeg444','decode-jpeg420','decode-png','encode-jpeg444','encode-png')){
        $order=if($round%2 -eq 1){@('before','after')}else{@('after','before')}
        foreach($engine in $order){
            $binary=if($engine -eq 'before'){$before}else{$after};$key="$scene-$operation-$engine-$round";$result=Join-Path $runs "$key.json"
            & dotnet $binary run $fixtures existing $scene $operation $result $Seconds.ToString([Globalization.CultureInfo]::InvariantCulture) > (Join-Path $runs "$key.log") 2>&1
            if($LASTEXITCODE -ne 0){Get-Content (Join-Path $runs "$key.log");throw "Integrated codec benchmark failed: $key"}
            $data=Get-Content $result -Raw|ConvertFrom-Json;$data.backend=$engine;[IO.File]::WriteAllText($result,($data|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
        }
    } };Write-Output "Completed integrated before/after round $round/$Rounds"}
}
finally {Pop-Location}
