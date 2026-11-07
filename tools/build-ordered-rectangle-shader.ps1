param([Parameter(Mandatory=$true)][string]$Compiler)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath=Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer-ordered.comp'
$outputPath=Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer-ordered.spv'
& $Compiler -V --target-env vulkan1.0 $sourcePath -o $outputPath
if($LASTEXITCODE -ne 0){throw 'Ordered batch GLSL compilation failed.'}
$record=[ordered]@{Target='Vulkan 1.0 / SPIR-V 1.0';Compiler=(& $Compiler --version | Select-Object -First 1);CompilerSha256=(Get-FileHash -LiteralPath $Compiler).Hash;SourceSha256=(Get-FileHash -LiteralPath $sourcePath).Hash;SpirvSha256=(Get-FileHash -LiteralPath $outputPath).Hash;BatchDraws=64;ParameterStrideBytes=48;UniformRangeBytes=3104;RuntimeCompilerRequired=$false}
$record|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $workspacePath 'docs/shader-build-ordered.json') -Encoding utf8
