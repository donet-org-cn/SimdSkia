param([string]$Output='artifacts/avalonia-controls/comparison',[string]$Font='baseline/fonts/ZCOOLXiaoWei-Regular.ttf',[int]$Frames=600,[int]$Forks=3,[string]$BeforeDirectory='', [switch]$NoBuild)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $workspacePath
if($Frames -lt 100 -or $Forks -lt 1){throw 'Use at least 100 frames and one process fork.'}
if(-not $NoBuild){
    dotnet build tests/AvaloniaControls -c Release --nologo -v:q
    if($LASTEXITCODE -ne 0){throw 'Managed benchmark build failed.'}
    dotnet build tests/AvaloniaControls.Reference -c Release --nologo -v:q
    if($LASTEXITCODE -ne 0){throw 'Reference benchmark build failed.'}
}
$outputPath=[IO.Path]::GetFullPath((Join-Path $workspacePath $Output))
if(Test-Path -LiteralPath (Join-Path $outputPath 'manifest.json')){throw 'Use a new output directory to preserve earlier evidence.'}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$managedPath=Join-Path $workspacePath 'tests/AvaloniaControls/bin/Release/net8.0/SimdSkia.Avalonia.Controls.Tests.dll'
$referencePath=Join-Path $workspacePath 'tests/AvaloniaControls.Reference/bin/Release/net8.0/AvaloniaControls.Reference.dll'
$engines=@([pscustomobject]@{Name='after';Path=$managedPath;Arguments=@()},[pscustomobject]@{Name='reference';Path=$referencePath;Arguments=@()},[pscustomobject]@{Name='default';Path=$referencePath;Arguments=@('--native-default-opacity')})
if($BeforeDirectory){$engines=@([pscustomobject]@{Name='before';Path=(Join-Path ([IO.Path]::GetFullPath((Join-Path $workspacePath $BeforeDirectory))) 'SimdSkia.Avalonia.Controls.Tests.dll');Arguments=@()})+$engines}
$configurations=@([pscustomobject]@{Scene='controls';Scale='1'},[pscustomobject]@{Scene='scroll';Scale='1'},[pscustomobject]@{Scene='controls';Scale='1.5'})
$files=@();foreach($engine in $engines){
    $directory=Split-Path -Parent $engine.Path
    foreach($file in Get-ChildItem -LiteralPath $directory -Filter '*.dll' -File | Where-Object {$_.Name -match '^(SimdSkia|Avalonia|SkiaSharp|HarfBuzzSharp)'}){$files+=@{Engine=$engine.Name;Name=$file.Name;Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}}
}
$manifest=@{Backend='CPU software; real Avalonia controls, layout/templates, RenderTargetBitmap + ImmediateRenderer; no window-present or GPU timing';WarmupSeconds=2.5;Frames=$Frames;Forks=$Forks;Font=[IO.Path]::GetFullPath((Join-Path $workspacePath $Font));FontSha256=(Get-FileHash -LiteralPath $Font -Algorithm SHA256).Hash;Configurations=$configurations;Files=$files;SceneSha256=(Get-FileHash tests/AvaloniaControls/ControlScene.cs -Algorithm SHA256).Hash;ProgramSha256=(Get-FileHash tests/AvaloniaControls/Program.cs -Algorithm SHA256).Hash}
$manifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $outputPath 'manifest.json') -Encoding utf8
foreach($fork in 0..($Forks-1)){
    foreach($configuration in $configurations){
        for($step=0;$step -lt $engines.Count;$step++){
            $engine=$engines[($fork+$step)%$engines.Count]
            $name=('{0}-{1}-dpi{2}-fork{3}' -f $engine.Name,$configuration.Scene,$configuration.Scale,$fork)
            $runPath=Join-Path $outputPath $name
            New-Item -ItemType Directory -Path $runPath -Force|Out-Null
            Write-Output ('Running '+$name)
            $extraArguments=$engine.Arguments
            dotnet $engine.Path --bench --font $Font --frames $Frames --scene $configuration.Scene --scale $configuration.Scale --output $runPath @extraArguments *> (Join-Path $runPath 'process.log')
            if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $runPath 'process.log');throw ('Benchmark failed: '+$name)}
        }
    }
}
python tools/report-avalonia-controls.py $outputPath
if($LASTEXITCODE -eq 2){Write-Output 'Measurements completed; native pixel quality gate FAILED. See results.json and diff images.';exit 2}
if($LASTEXITCODE -ne 0){throw 'Benchmark report failed.'}
