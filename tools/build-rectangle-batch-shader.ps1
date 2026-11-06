param([Parameter(Mandatory=$true)][string]$Compiler)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath=Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer.comp'
$outputPath=Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer-batch.spv'
$generatedPath=Join-Path $workspacePath 'artifacts/renderer-batch.generated.comp'
$source=[IO.File]::ReadAllText($sourcePath)
$original='layout(std140,binding=2) uniform Params { uvec4 parameterWords[40]; };'
if(-not $source.Contains($original)){throw 'Base shader parameter declaration changed; review batch layout.'}
$generated=$source.Replace($original,'layout(std140,binding=2) uniform Params { uvec4 parameterWords[768]; };').Replace('uint param(int i){return parameterWords[i/4][i%4];}','uint param(int i){return parameterWords[gl_GlobalInvocationID.z*48u+uint(i/4)][i%4];}')
[IO.File]::WriteAllText($generatedPath,$generated,[Text.UTF8Encoding]::new($false))
& $Compiler -V --target-env vulkan1.0 $generatedPath -o $outputPath
if($LASTEXITCODE -ne 0){throw 'Batch GLSL compilation failed.'}
$record=[ordered]@{Target='Vulkan 1.0 / SPIR-V 1.0';Compiler=(& $Compiler --version | Select-Object -First 1);CompilerSha256=(Get-FileHash -LiteralPath $Compiler -Algorithm SHA256).Hash;BaseSource='src/SimdSkia.Gpu/Shaders/renderer.comp';BaseSourceSha256=(Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash;GeneratedSourceSha256=(Get-FileHash -LiteralPath $generatedPath -Algorithm SHA256).Hash;Spirv='src/SimdSkia.Gpu/Shaders/renderer-batch.spv';SpirvSha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash;BatchDraws=16;ParameterStrideBytes=768;UniformRangeBytes=12288;RuntimeCompilerRequired=$false}
$record|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $workspacePath 'docs/shader-build-batch.json') -Encoding utf8
