"""Freeze incremental API, pixel, lifetime and measured CPU evidence. Never overwrite historical failures."""
import gzip,hashlib,json,shutil
from pathlib import Path
source=Path('artifacts/api-completion-2026-11-06');output=Path('docs/evidence/query-effects-codec-2026-11-06')
if output.exists():raise SystemExit('Use a new snapshot directory.')
output.mkdir(parents=True);load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
folders=['image-filter-check-21','blob-check-16','canvas-query-check-3','backend-descriptions-1','color-spaces-5','path-effects-2','mask-filters-4','encoder-options-3','codec-streaming-4','pma-timing-1','raster-api-timing-2','regression-9']
# Query/backend directory names can vary between research runs; retain the exact
# existing oracle folder, not a made-up success count.
folders=[f for f in folders if (source/f).exists()];frames={}
for name in folders:
    for file in (source/name).rglob('*'):
        if not file.is_file():continue
        if name.startswith('regression') and file.parent!=source/name:continue
        if file.suffix not in ['.json','.log','.bin','.rgba']:continue
        destination=output/file.relative_to(source);destination.parent.mkdir(parents=True,exist_ok=True)
        if file.suffix=='.rgba':
            payload=file.read_bytes();key=hashlib.sha256(payload).hexdigest();frames[str(file.relative_to(source)).replace('\\','/')]=key;destination=output/'frames'/(key+'.rgba.gz');destination.parent.mkdir(parents=True,exist_ok=True)
            if not destination.exists():
                with gzip.GzipFile(str(destination),'wb',mtime=0) as stream:stream.write(payload)
            continue
        if file.suffix in ['.bin','.rgba']:
            with gzip.GzipFile(str(destination)+'.gz','wb',mtime=0) as stream:stream.write(file.read_bytes())
        else:shutil.copy2(file,destination)
(output/'frame-objects.json').write_text(json.dumps(frames,indent=2)+'\n',encoding='utf-8')
for name in ['full-build-12.log','aot-publish-9.log','aot-result-9.log','pma-read-exact-summary.json','query-effects-codec-source-hashes.json','basic-8c.log','gpu-sampling-8b.log']:
    shutil.copy2(source/name,output/name)
for kind in ['implemented','comparison']:shutil.copy2(source/(kind+'-query-effects-codec.json'),output/(kind+'.json'))
shutil.copy2(Path('docs/api/baseline-3.119.4.json'),output/'baseline-3.119.4.json')
audit=load(output/'comparison.json');pma=load(output/'pma-timing-1/summary.json');raster=load(output/'raster-api-timing-2/summary.json')
assert all(load(output/f'{name}/manifest.json')['complete'] for name in ['pma-timing-1','raster-api-timing-2'])
status=dict(goalComplete=False,targetMaximumMissing=99,matching=audit['MatchingEntries'],missing=len(audit['MissingOrDifferent']),extra=len(audit['AddedOrDifferent']),publicTypes=165,baselineHash=audit['BaselineHash'],replacementHash=audit['ReplacementHash'],published=False,aotChecks=80,colorSpaceMetadata=436,colorSpacePixels='24/24',pathEffectPixels='369/720',maskFilterPixels='42/312',encoderOptionsPixels='88/88',codecMetadata=418,codecOutputs='21/21',knownJpegFailure=True,wholePage='0/18; not rerun',scope='Incremental public API and CPU transfer/raster microbenchmarks, no whole-app or GPU throughput parity')
(output/'status.json').write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')
lines=['# 颜色空间、路径效果、编码与像素转换优化（未发布）','',
'2026-11-06，Windows x64。固定 SkiaSharp 3.119.4 的 **3235 项**公共基线，本快照 **2726 匹配、165 类型、509 缺口、0 额外签名**；比上批 2549 增加 177 项。最多 99 项的停止条件尚未达到，开发继续。公共 NuGet **0.1.1 不变**，未提交/发布本批。元数据匹配不等于完整行为兼容，也不保证二进制替换。','',
'## 新增真实链路','',
'- 画布 clip/matrix/surface 查询、SaveLayerRec、magnifier 和旧版 paint intercept 重载；GL/Vulkan backend texture/render-target 是真实复制的**借用资源描述**，并不表示已实现外部图像导入或对应驱动。',
'- RGB matrix/TRC、linear/sRGB gamma、ICC 字节所有权与按需固定指针，像素读写按颜色空间转换；克隆图像/位图保留独立配置。支持 RGB/XYZ parametric ICC；采样曲线拟合、多通道曲线、非 sRGB 直接绘图/重采样、带 ICC 编码仍明确拒绝，不按 sRGB 静默处理。',
'- Dash/corner/trim/discrete/1D translate/rotate/morph/2D line/path/compose/sum 参与 DrawPath、形状、文字和 GetFillPath。Paint、Clone 和包装释放有独立语义；效果几何缓存最多 16 项/2 MiB，盖章与图深设有明确资源预算。',
'- 覆盖率表、gamma/clip 和 Normal/Solid/Outer/Inner mask blur，在 paint 颜色/渐变之前处理 A8 覆盖率；小 sigma 使用离散 Bessel 核，轴对齐矩形使用基线解析 profile/九宫格。缓存最多 64 项/8 MiB。GPU 显式回退/拒绝；mask-filtered images 仍未实现，报错而不漏画。',
'- JPEG 4:2:0/4:4:4、Ignore/BlendOnBlack 和 PNG 行过滤/zlib 数值级别参与真实编码。PNG 默认继续使用 Sdcb 快路径；显式参数在 Sdcb 容器基础上重过滤/压缩。4:2:2 仍因依赖能力明确拒绝；WebP 只有真实选项值合同，未增加空 Encode 重载。',
'- Codec 的 JPEG/BMP scanline 转移/跳行、PNG/GIF 延迟输出、SKStream 所有权、EXIF 和缩放/子集查询。**完整输入仍在创建时解码**，这些接口不是压缩输入实时增量或低内存解码，scaled/subset GetPixels 尚未支持。',
'- PMA→straight ReadPixels 按原版 float 操作顺序和 ties-to-even 实现 Vector256、Vector128 和标量路径，修复整数反预乘与原版相差 1 的问题。公开 GetPixel 的既有整数行为保持各自语义，未把所有反预乘混成一种。','',
'## 独立原版和生命周期验证','',
'| 检查 | 本批结果 |','| --- | --- |',
'| 颜色空间 | 436 项行为/元数据、24/24 像素传递通过 |',
'| 编码选项 | 88/88 过门槛，65 元数据一致；两边各 572 独立断言，PNG 精确相等，JPEG max20/RMSE3 |',
'| Codec | 418 元数据一致、21/21 输出通过；BMP/GIF/PNG 精确，JPEG max20/RMSE3，包含 JPEG 方向 1–8 |',
'| 路径效果 | 369/720 过 max32/RMSE2；8 项几何差异保留，严格工具仍退出 2 |',
'| MaskFilter | 42/312 过 max3/RMSE0.75；12 元数据一致，严格工具仍退出 2 |',
'| 图像滤镜扩展 | 746/840；元数据一致；稳定原生指针 SaveLayerRec 16/16、普通有界 SaveLayer 4/4 通过 |',
'| PMA 所有有效通道组合 | 32896 对、131584 通道字节精确一致；完整 65536 像素含边界探针也一致 |',
'| 实际关闭硬件指令 | 全部 intrinsics 禁用及 AVX2 禁用，与原版和普通模式输出均一致 |',
'| Native AOT | 80 项实际运行通过，包含新颜色、效果、编码、Codec 与资源描述 |',
'| Release 构建 | full-build-12，0 警告/错误 |',
'| 基础/像素/编解码/PDF/特性/字体/优化/Avalonia/控件/Direct2D/Vulkan/采样 | regression-9 均通过；旧 JPEG 独立严格门槛继续失败 |','',
'路径效果还受既有曲线覆盖率、描边、region 和采样精度影响；MaskFilter 的局部 blur/phase 也未过原版门槛。没有删失败场景或放宽阈值。旧字体、文字 blob 和整页 **0/18 未重测**仍保留；本批编码选项的 JPEG 成功不等于旧 JPEG 全套成功。','',
'原版托管 SaveLayerRec 有界重载存在返回局部矩形指针的生命周期问题，本库以生命周期正确的直接 native oracle 验证，并保留原托管空白输出。原版 PNG IncrementalDecode 的 Success 不设置 rowsDecoded 时会保留旧栈值；原始值另存 details.json，本库返回实际行数，不复制未定义数据。','',
'regression-8 曾暴露两条旧颜色空间拒绝断言失效，以及把路径的 half-pixel 规则用于图像采样而导致的 Vulkan 回退；保留失败记录。修正为基线静态颜色空间 Dispose 语义，并恢复图像采样的独立边界规则后，basic-8c 36/36、gpu-sampling-8b 617 项和完整 regression-9 通过。','',
'## PMA 像素读取：速度、CPU 和内存','',
'同机 i7-8700K（6 核/12 线程）、Windows 26300、.NET SDK 10.0.403/runtime10.0.14，CPU JIT。同一 worker，冻结原版/上批滤镜 API 快照/当前 DLL，2s 预热、0.8s 计时，三轮轮换、9 个串行进程；期间不编译或跑其他测试。三场景原版/当前输出逐字节相同，旧版分别有 5048、5048、35763 个差异字节。','',
'| 场景 | 原版 µs | 旧快照 µs | 当前 µs | 当前/原版 | 相对旧快照加速 | 原版/当前 CPU µs |','| --- | ---: | ---: | ---: | ---: | ---: | ---: |']
for r in pma:
    o,b,n=(r[k] for k in ['original','before','after']);lines.append(f"| {r['scene']} | {o['msPerFrame']*1000:.3f} | {b['msPerFrame']*1000:.3f} | {n['msPerFrame']*1000:.3f} | {r['afterOverOriginal']:.3f}× | {r['speedup']:.2f}× | {o['cpuMsPerFrame']*1000:.3f}/{n['cpuMsPerFrame']*1000:.3f} |")
lines+=['','| 场景 | 原版/当前工作集 MiB | 原版/当前 Private MiB | 原版/当前分配 B/次 | 原版/当前整机 CPU % |','| --- | ---: | ---: | ---: | ---: |']
for r in pma:
    o,n=(r[k] for k in ['original','after']);lines.append(f"| {r['scene']} | {o['workingSetMiB']:.2f}/{n['workingSetMiB']:.2f} | {o['privateMiB']:.2f}/{n['privateMiB']:.2f} | {o['allocatedBytesPerFrame']:.2f}/{n['allocatedBytesPerFrame']:.2f} | {o['cpuPercentOfMachine']:.2f}/{n['cpuPercentOfMachine']:.2f} |")
lines+=['','本库 Private 仍比原版多约 1.5–2.3 MiB；接近零的 GC 分配不等于整体内存更小。CPU% 是跑满循环占整机 12 个逻辑处理器的比例，不是固定 FPS 的 CPU 节省比例；应结合每次 CPU 时间评估。以上是转换微场景，不代表窗口、OCR 全链路或 GPU。','',
'## 九个已有 CPU 绘图微场景复测','',
'同尺寸/参数 worker，0.4s 预热、0.8s 计时，三轮、9 个串行进程。以下只展示本轮原版/当前；记录中的 before 是 raster-timing-1 的**算法初版**，不是前一最终优化快照或公开版本，因此不把它的大倍数改善归功于本批 API。raster-api-timing-1 因旧目录错误中断并标 INVALID，不用于报告；有效目录为 raster-api-timing-2，DLL 哈希各异。','',
'| 场景 | 原版/当前 µs | 当前/原版 | 原版/当前 CPU µs | 原版/当前 WS MiB | 原版/当前 Private MiB |','| --- | ---: | ---: | ---: | ---: | ---: |']
for r in raster:
    o,n=(r[k] for k in ['original','after']);lines.append(f"| {r['scene']} | {o['msPerFrame']*1000:.3f}/{n['msPerFrame']*1000:.3f} | {r['afterOverOriginal']:.3f}× | {o['cpuMsPerFrame']*1000:.3f}/{n['cpuMsPerFrame']*1000:.3f} | {o['workingSetMiB']:.2f}/{n['workingSetMiB']:.2f} | {o['privateMiB']:.2f}/{n['privateMiB']:.2f} |")
lines+=['','九场景最大落后约 8.5%，图片场景内存仍高。场景依赖热缓存，且已有文字/曲线/采样/整页画质差异未消除，**不能宣称整个引擎在同画质下达到最多落后10%**。GPU 本批跑的是实际设备回归与 fence/readback，没有做新的 GPU 吞吐对比；不与上述 CPU 数值混用。上批 image-filter sigma2/12 和阴影的性能落后仍然成立，本批未重测。','',
'## 复现','',
'```powershell',
'$env:PATH="$PWD\\artifacts\\dotnet-sdk-10.0.403;"+$env:PATH',
'dotnet build SimdSkia.sln -c Release',
'# 每次 --output 使用全新目录；路径/遮罩等尚有差异的严格工具退出 2。',
'python tools/check-color-spaces.py --output artifacts/recheck/color',
'python tools/check-path-effects.py --output artifacts/recheck/effects',
'python tools/check-mask-filters.py --output artifacts/recheck/masks',
'python tools/check-encoder-options.py --output artifacts/recheck/encoders',
'python tools/check-codec-streaming.py --output artifacts/recheck/codec',
'python tools/run-api-completion-regressions.py --output artifacts/recheck/regressions',
'dotnet publish tests/Foundations/NativeAot -c Release -r win-x64 -o artifacts/recheck/aot',
'artifacts/recheck/aot/NativeAot.exe --require-aot',
'```','',
'计时 worker/完整参数/哈希在证据 manifest.json 中。原版只存在于隔离 tests/benchmarks 工程，产品代码没有调用 libSkiaSharp。此阶段仍欠 509 项，接下来继续 RuntimeEffect、录制回放、GPU context/surface 及其他缺口，不把这个增量快照当作任务完成。','']
Path('docs/QUERY-EFFECTS-CODEC-API-2026-11-06.md').write_text('\n'.join(lines),encoding='utf-8')
hashes={str(f.relative_to(output)).replace('\\','/'):hashlib.sha256(f.read_bytes()).hexdigest() for f in output.rglob('*') if f.is_file()};(output/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8');print(json.dumps(status,indent=2));print('Evidence bytes',sum(f.stat().st_size for f in output.rglob('*') if f.is_file()))
