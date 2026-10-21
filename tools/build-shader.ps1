param([string]$Compiler)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $Compiler){$Compiler=Join-Path $workspacePath 'baseline/vulkan/glslang/bin/glslang.exe'}
if(-not(Test-Path -LiteralPath $Compiler)){throw 'Supply a Khronos glslang compiler path. Product builds use the committed SPIR-V and do not require this tool.'}
& $Compiler -V --target-env vulkan1.0 (Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer.comp') -o (Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer.spv')
if($LASTEXITCODE -ne 0){throw 'GLSL compilation failed.'}
