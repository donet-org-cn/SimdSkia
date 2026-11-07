param([Parameter(Mandatory=$true)][string]$Compiler)
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath=Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer.comp'
$outputPath=Join-Path $workspacePath 'src/SimdSkia.Gpu/Shaders/renderer-ordered.spv'
$generatedPath=Join-Path $workspacePath 'artifacts/renderer-ordered.generated.comp'
$source=[IO.File]::ReadAllText($sourcePath)
$declaration='layout(std140,binding=2) uniform Params { uvec4 parameterWords[40]; };'
$access='uint param(int i){return parameterWords[i/4][i%4];}'
$origin="    uvec2 local=gl_GlobalInvocationID.xy;`n    uvec2 pixel=uvec2(param(37),param(38))+local;"
$source=$source.Replace("`r`n","`n")
foreach($expected in @($declaration,$access,'void main(){',$origin)){if(-not $source.Contains($expected)){throw 'Base shader changed; review ordered batch generation.'}}
$generated=$source.Replace($declaration,'layout(std140,binding=2) uniform Params { uvec4 batchInfo; uvec4 parameterWords[768]; };').Replace($access,'uint drawIndex; uint param(int i){return parameterWords[drawIndex*48u+uint(i/4)][i%4];}').Replace('void main(){','void drawOne(uvec2 pixel){').Replace($origin,'    uvec2 local=pixel-uvec2(param(37),param(38));')
$generated+="`nvoid main(){ uvec2 pixel=batchInfo.xy+gl_GlobalInvocationID.xy; for(drawIndex=0u;drawIndex<batchInfo.z;drawIndex++)drawOne(pixel); }`n"
[IO.File]::WriteAllText($generatedPath,$generated,[Text.UTF8Encoding]::new($false))
& $Compiler -V --target-env vulkan1.0 $generatedPath -o $outputPath
if($LASTEXITCODE -ne 0){throw 'Ordered batch GLSL compilation failed.'}
$record=[ordered]@{Target='Vulkan 1.0 / SPIR-V 1.0';Compiler=(& $Compiler --version | Select-Object -First 1);CompilerSha256=(Get-FileHash -LiteralPath $Compiler).Hash;BaseSourceSha256=(Get-FileHash -LiteralPath $sourcePath).Hash;GeneratedSourceSha256=(Get-FileHash -LiteralPath $generatedPath).Hash;SpirvSha256=(Get-FileHash -LiteralPath $outputPath).Hash;BatchDraws=16;ParameterStrideBytes=768;UniformRangeBytes=12304;RuntimeCompilerRequired=$false}
$record|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $workspacePath 'docs/shader-build-ordered.json') -Encoding utf8
