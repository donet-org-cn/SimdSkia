# SimdCodecs 隔离评估

后续完整接入与严格校验的真实开销见 `docs/SIMDCODECS-INTEGRATION-2026-11-01.md`。
当前源项目已采用新库，以下独立评估属于替换前快照。`run-integrated.ps1` 使用冻结的
旧基准目录与当前输出做串行交替比较；旧二进制必须在修改依赖前冻结，不能让两边
悄悄引用同一份新实现。也可将 preview.9 标签导出到隔离目录，用
`-p:SimdSkiaEngineRoot=隔离目录/src` 构建基准并输出到独立目录，再构建当前端。
运行前先完成所有编译/AOT/窗口测试，避免性能采样与构建竞争 CPU。

比较 SimdSkia 当前图片编解码与 NuGet `Sdcb.SimdCodecs.Jpeg/Png 1.2.0`。
仅此基准引用新依赖，不加入生产项目/解决方案，不改变 .NET 8 公共基线。
测试双方均运行于 .NET 10；上游包仅支持 net10.0。

```powershell
dotnet build benchmarks/CodecEvaluation -c Release
dotnet benchmarks/CodecEvaluation/bin/Release/net10.0/CodecEvaluation.dll prepare artifacts/simdcodecs-evaluation-2026-11-01/fixtures
dotnet benchmarks/CodecEvaluation/bin/Release/net10.0/CodecEvaluation.dll validate artifacts/simdcodecs-evaluation-2026-11-01/fixtures tests/Imaging/fixtures.json artifacts/simdcodecs-evaluation-2026-11-01/validation.json
dotnet benchmarks/CodecEvaluation/bin/Release/net10.0/CodecEvaluation.dll quality artifacts/simdcodecs-evaluation-2026-11-01/fixtures artifacts/simdcodecs-evaluation-2026-11-01/quality.json
./benchmarks/CodecEvaluation/run.ps1
```

两个 1920×1080 自制不透明图案（UI/纹理），不是照片语料。每场景每引擎三个
独立串行进程，0.5 秒预热、至少 1.2 秒采样，顺序交替。计时不含文件读取；
解码双方都产生有所有权的 PixelBuffer，并包含释放成本。候选适配调用公开
PixelBuffer.Rent，包含清零成本，未把永久复用目标缓冲的内核时间冒充应用 API 时间。
编码双方都返回新的完整压缩 byte[]；JPEG 同为 quality=90、4:4:4、baseline、默认 Huffman；
PNG 同为 Optimal、单片串行，过滤/压缩算法不同，必须同时比较文件体积和无损输出。

像素、AOT 和无硬件指令路径验证：

```powershell
$env:DOTNET_EnableHWIntrinsic='0'
dotnet benchmarks/CodecEvaluation/bin/Release/net10.0/CodecEvaluation.dll validate artifacts/simdcodecs-evaluation-2026-11-01/fixtures tests/Imaging/fixtures.json artifacts/simdcodecs-evaluation-2026-11-01/validation-scalar.json
Remove-Item Env:DOTNET_EnableHWIntrinsic
dotnet publish benchmarks/CodecEvaluation/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -o artifacts/simdcodecs-evaluation-2026-11-01/aot
./artifacts/simdcodecs-evaluation-2026-11-01/aot/AotSmoke.exe tests/Imaging/fixtures.json artifacts/simdcodecs-evaluation-2026-11-01/validation-aot.json
```

`python benchmarks/CodecEvaluation/summarize.py` 从实际包缓存审计 nuspec 和 DLL，要求
host.json、三轮完整矩阵及上述结果存在，再检查
41 个候选像素摘要在 SIMD/标量/实际 AOT 一致，输出 packages.json 和 results.json；已冻结的完整审计和
结果在 `docs/evidence/simdcodecs-2026-11-01/results.json`。重新核对包元数据可读取
obj/project.assets.json 的 packageFolders，再检查对应 NuGet 缓存的 nuspec。

`nativeModules` 是运行记录中的宽松名称过滤，含匹配 compression/skia 的托管模块；
只有诸如 **System.IO.Compression.Native.dll** 的实际原生模块才作为原生压缩证据。
PNG 原型不执行 ICC→sRGB 变换，已保留 Display-P3 失败，不能作为生产替代实现。
读取器尺寸在此原型中来自受控夹具；接入任意图片前还需保留引擎的输入/解码预算、
元数据和颜色处理、stride、Alpha、异常及生命周期合同。

第三方仅在此评估使用：Sdcb JPEG/PNG/Abstractions/Color.Icc 均 1.2.0、MIT，
包不携带原生绘图库；PNG 使用运行时 ZLibStream，实测调用原生压缩。
JpegLibrary 0.4.32 沿用引擎已有依赖和通知。未复制或修改上游算法。

完整结论：`docs/SIMDCODECS-EVALUATION-2026-11-01.md`。
