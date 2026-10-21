"""Summarize isolated CPU SK-call replay. Never turn quality failures into wins."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
from statistics import median
import struct
import zlib

parser = argparse.ArgumentParser()
parser.add_argument("root", type=Path)
parser.add_argument("report", type=Path)
args = parser.parse_args()
root = args.root.resolve()
manifests = {phase: json.loads((root / f"{phase}-run.json").read_text(encoding="utf-8-sig")) for phase in ("before", "after")}
for m in manifests.values():
    assert m["Complete"] and m["Forks"] == 3 and m["Samples"] == 5
    assert m["Sizes"] == "1024x768", "This report describes the fixed 1024x768 protocol."
for key in ("Avalonia", "SkiaSharp", "FontSha256", "FixtureSha256", "Samples", "TargetSampleMilliseconds", "Sizes", "TieredCompilation", "ReadyToRun", "ServerGc"):
    assert manifests["before"][key] == manifests["after"][key], key

workers = {}
for engine in ("before", "original", "scalar", "simd"):
    workers[engine] = [json.loads((root / f"{engine}-{fork}/results.json").read_text()) for fork in range(3)]
reference_runtime = workers["original"][0]["Runtime"]
audit_path = root / "avalonia-api.json"
if not audit_path.exists():
    audit = json.loads((Path(__file__).resolve().parents[1] / "docs/avalonia-api-12.1.4.json").read_text())
    assert audit["ReplacementSha256"] == workers["simd"][0]["Runtime"]["AssemblySha256"], "Audit DLL differs from measured DLL"
    audit_path.write_text(json.dumps(audit, indent=2))
audit = json.loads(audit_path.read_text())
for group in workers.values():
    for w in group:
        for key in ("Framework", "OS", "Architecture", "Cpu", "FontSha256", "ServerGc", "TieredCompilation", "ReadyToRun"):
            assert w["Runtime"][key] == reference_runtime[key], key
scenes = ["ui-labels", "ui-scroll", "ui-dpi-scroll", "ui-text-rebuild", "ui-opaque-images"]
rows = []
diffs = []

def png(path, width, height, pixels):
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))
    raw = b"".join(b"\0" + pixels[y*width*4:(y+1)*width*4] for y in range(height))
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))

for scene in scenes:
    selection = {engine: [next(w for w in worker["Workloads"] if w["Scene"] == scene) for worker in group] for engine, group in workers.items()}
    pixel_data = {}
    for engine, cases in selection.items():
        data = [(root / f"{engine}-{fork}" / case["PixelsFile"]).read_bytes() for fork, case in enumerate(cases)]
        assert all(d == data[0] for d in data), (scene, engine, "nondeterministic output")
        pixel_data[engine] = data[0]
    assert pixel_data["simd"] == pixel_data["scalar"], (scene, "SIMD pixels differ")
    expected = pixel_data["original"]
    before_delta = [abs(a-b) for a, b in zip(pixel_data["before"], pixel_data["simd"])]
    diffs.append({"Scene": scene, "BeforeAfterMaximumChannelError": max(before_delta), "ScalarSimdIdentical": True})
    for engine, cases in selection.items():
        batches = [s for case in cases for s in case["Samples"]]
        operations = sum(s["Operations"] for s in batches)
        latency = [s["TotalMilliseconds"] / s["Operations"] for s in batches]
        ticks = []
        for fork, case in enumerate(cases):
            payload = (root / f"{engine}-{fork}" / case["LatencyFile"]).read_bytes()
            ticks += [x[0]*1000/workers[engine][fork]["Runtime"]["StopwatchFrequency"] for x in struct.iter_unpack("<q", payload)]
        assert len(ticks) == operations
        ticks.sort()
        errors = [abs(a-b) for a, b in zip(expected, pixel_data[engine])]
        threshold = cases[0]["AllowedPixelError"]
        rows.append({"Scene": scene, "Engine": engine, "MedianMilliseconds": median(latency), "P95FrameMilliseconds": ticks[int((len(ticks)-1)*.95)],
                     "CpuMillisecondsPerFrame": sum(s["CpuMilliseconds"] for s in batches)/operations,
                     "AllocatedBytesPerFrame": sum(s["AllocatedBytes"] for s in batches)/operations,
                     "WorkingSetBytes": median(c["WarmResident"]["WorkingSetBytes"] for c in cases),
                     "PrivateBytes": median(c["WarmResident"]["PrivateBytes"] for c in cases),
                     "ManagedLiveBytesAfterGc": median(c["AfterGc"]["ManagedLiveBytes"] for c in cases),
                     "MaxChannelError": max(errors), "MeanAbsoluteChannelError": sum(errors)/len(errors),
                     "RootMeanSquaredChannelError": math.sqrt(sum(e*e for e in errors)/len(errors)),
                     "ChannelsAboveThreshold": sum(e > threshold for e in errors), "AllowedPixelError": threshold,
                     "PixelPass": max(errors) <= threshold, "Forks": 3, "Batches": len(batches), "Frames": operations})
        if engine == "simd":
            heat = bytearray(len(expected))
            for i in range(0, len(heat), 4):
                error = max(errors[i:i+4]);heat[i:i+4] = bytes((min(255, error*4), 0, 0, 255))
            png(root / f"{scene}-difference-x4.png", cases[0]["Width"], cases[0]["Height"], heat)
            png(root / f"{scene}-managed.png", cases[0]["Width"], cases[0]["Height"], pixel_data[engine])
            png(root / f"{scene}-original.png", cases[0]["Width"], cases[0]["Height"], expected)

with (root / "summary.csv").open("w", newline="", encoding="utf-8") as f:
    writer = csv.DictWriter(f, fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
(root / "pixel-validation.json").write_text(json.dumps({"BeforeAfter": diffs, "Results": [{k: r[k] for k in ("Scene", "Engine", "MaxChannelError", "AllowedPixelError", "PixelPass", "ChannelsAboveThreshold")} for r in rows]}, indent=2))
lookup = {(r["Scene"], r["Engine"]): r for r in rows}
labels = {"ui-labels": "24 行中英文标签", "ui-scroll": "列表 1× DPI 滚动", "ui-dpi-scroll": "列表 1.5× DPI 滚动", "ui-text-rebuild": "每帧重建相同文字对象", "ui-opaque-images": "48 张不透明图片"}
lines = ["# Avalonia 使用面与针对性优化（" + manifests["after"]["Date"] + "）", "",
         "本轮按 Avalonia.Skia 12.1.4 的定位文字调用链优化。保留文字与滚动的 CPU 耗时、分配已明显下降；原版文字画质仍未达标。**尚不能直接替换 Avalonia.Skia，也没有完成 Avalonia 窗口集成或窗口 GPU 性能比较。**", "",
         "## 实测范围", "",
         "原版为 SkiaSharp 3.119.4；隔离进程加载原生 Skia，产品六个程序集仍无 Skia/字体/编解码原生依赖。Avalonia.Skia 的实际恢复结果见 [锁文件](../probes/AvaloniaDependencyProbe/packages.lock.json)。本轮不使用 OCR 模块的 SkiaSharp 4.153.1 作为 Avalonia 基线。", "",
         "以下文字场景从 `GlyphRunImpl` 使用的 `SKTextBlobBuilder.AllocatePositionedRun → SetGlyphs/SetPositions → Build → DrawingContextImpl.DrawText(blob)` 提炼为 SK 调用回放。每帧创建新画布包装对象、全帧清屏，复用文字对象；滚动与重建场景分别测量。不包含控件布局、文字 shaping、合成器、脏区、窗口呈现或完整 Avalonia 帧率。", "",
         "图片行是现有 `DrawBitmap` 的代表性不透明图片场景。Avalonia 的 `ImmutableBitmap` 实际调用 `DrawImage(image, sourceRect, destRect, SKSamplingOptions, paint)`，该重载及采样类型仍缺失；没有把图片场景称为完整 Avalonia 位图接入。底层行采样/拷贝优化可在后续适配时复用。", "",
         "[Avalonia 官方 GlyphRunImpl 源码](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Skia/Avalonia.Skia/GlyphRunImpl.cs)、[DrawingContextImpl](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Skia/Avalonia.Skia/DrawingContextImpl.cs)、[FramebufferRenderTarget](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Skia/Avalonia.Skia/FramebufferRenderTarget.cs)。FramebufferRenderTarget 可保留 surface，并在绘制会话结束释放 canvas 包装对象；因此缓存不能仅依赖单个 canvas 的寿命。", "",
         f"硬件/环境：{reference_runtime['Cpu']}；{reference_runtime['Framework']}；{reference_runtime['Architecture']}。尺寸 1024×768、RGBA8888 Premul；固定 Zen Old Mincho 字体。原版/替代均明确使用 16 px、灰度抗锯齿、None hinting、Subpixel=true、BaselineSnap=false、LinearMetrics=true、无加粗/自动 hint；硬矩形裁剪，图片最近邻采样。该设置不代表 Avalonia 默认文字选项。", "",
         "每个实现 3 个独立进程，每场景 5 批，每批目标 100 ms；帧时间取 15 批均值的中位数，另保存逐帧 p95。顺序固定种子打乱，测试串行执行。关闭 TieredCompilation、ReadyToRun、Server GC。优化前是本轮刚补齐定位文字 API、尚未加入新缓存的版本，冻结源码和 DLL 保存在原始数据目录；它不代表旧版本曾支持 Avalonia。", "",
         "## 帧时间与 CPU", "", "文字各行均为**画质未达标的诊断数据**，不能据此宣称同画质胜过原版。图片场景逐字节一致。", "",
         "| 场景 | 优化前 ms | 优化后 ms | 原版 ms | 自身提速 | CPU ms/帧：优化后 / 原版 | 原版像素验证 |", "|---|---:|---:|---:|---:|---:|---|"]
for scene in scenes:
    b, s, n = [lookup[scene, engine] for engine in ("before", "simd", "original")]
    lines.append(f"| {labels[scene]} | {b['MedianMilliseconds']:.3f} | {s['MedianMilliseconds']:.3f} | {n['MedianMilliseconds']:.3f} | {b['MedianMilliseconds']/s['MedianMilliseconds']:.1f}× | {s['CpuMillisecondsPerFrame']:.3f} / {n['CpuMillisecondsPerFrame']:.3f} | {'通过' if s['PixelPass'] else '失败，最大误差 '+str(s['MaxChannelError'])} |")
lines += ["", "CPU ms 是进程累计 CPU 时间除以完成帧数；不等于整机 CPU 百分比。本数据只比较 CPU 光栅。", "", "## 内存与分配", "",
          "工作集/私有内存为进程热态实测，含运行时与缓存；分配列只含托管当前渲染线程，原版的 native 分配未被 GC 计数覆盖。托管分配较少不能推断总内存较少。", "",
          "| 场景 | 分配 KiB/帧：前 → 后 / 原版 | 工作集 MiB：前 → 后 / 原版 | 私有内存 MiB：后 / 原版 |", "|---|---:|---:|---:|"]
for scene in scenes:
    b, s, n = [lookup[scene, engine] for engine in ("before", "simd", "original")]
    lines.append(f"| {labels[scene]} | {b['AllocatedBytesPerFrame']/1024:.1f} → {s['AllocatedBytesPerFrame']/1024:.1f} / {n['AllocatedBytesPerFrame']/1024:.1f} | {b['WorkingSetBytes']/2**20:.1f} → {s['WorkingSetBytes']/2**20:.1f} / {n['WorkingSetBytes']/2**20:.1f} | {s['PrivateBytes']/2**20:.1f} / {n['PrivateBytes']/2**20:.1f} |")
lines += ["", "新增缓存：CPU 全局文字覆盖率 LRU 最多 512 项 / 16 MiB 估算数据；每个活字体的定位几何缓存最多 64 项 / 4 MiB。数据预算不包含字典/节点对象头、字体解析缓存、活动位图、既有每画布缓存与运行时池。缓存会增加常驻内存；Dispose 后 CPU 全局 LRU 可继续保留几何/覆盖率，直到淘汰，不保留 native 句柄。", "",
          "每帧重建场景重建的是**相同 glyph 和位置**，可命中内容缓存；不是每帧产生全新内容、字号或任意变换。回放每个标签新建 builder，而 Avalonia 自身有 builder 池；因此此处是同调用负载的压力比较，分配量不代表整个应用。全新内容依然需要解析/细分/光栅化，不能将热缓存成绩外推。", "", "## 冷启动", "",
          "下表为额外独立进程单次探针：字体此前未加载，原生/托管基本位图入口先初始化；构造含字体读取与 blob 建立。每项仅 1 次，是延迟诊断而非统计性能结论。", "",
          "| 场景 | 构造 ms：Simd / 原版 | 首帧 ms：Simd / 原版 | 第 9 帧 ms：Simd / 原版 |", "|---|---:|---:|---:|"]
for scene in ("ui-labels", "ui-scroll", "ui-text-rebuild", "ui-opaque-images"):
    s, n = [json.loads((root / f"{engine}-startup-{scene}.json").read_text()) for engine in ("simd", "original")]
    lines.append(f"| {labels[scene]} | {s['ConstructionMilliseconds']:.2f} / {n['ConstructionMilliseconds']:.2f} | {s['FrameMilliseconds'][0]:.2f} / {n['FrameMilliseconds'][0]:.2f} | {s['FrameMilliseconds'][8]:.3f} / {n['FrameMilliseconds'][8]:.3f} |")
lines += ["", "## 画质与行为限制", "",
          "文字阈值仍为最大通道误差 32，图片为 0，没有因性能结果调整阈值。四个文字场景均超阈值；灰度覆盖、字体缩放/光栅规则仍与原版不同，hinting/LCD 不支持。标量与 SIMD 的 5 个最终帧逐字节相同；优化前后文字最大变化为 " + str(max(d['BeforeAfterMaximumChannelError'] for d in diffs)) + "，由局部采样坐标归一化引起，16×16 采样与几何细分容差均保留。", "",
          "[文字差异图（红色误差放大 4 倍）](../" + (args.root / "ui-labels-difference-x4.png").as_posix() + ")；同目录保存原版/替代 PNG、原始 RGBA、逐帧 ticks、每批 CPU/内存以及 JSON/CSV。", "",
          "定位文字原版探针确认：glyph ID 相同、宽度最大微差约 0.000001；Blob.Bounds 最大微差约 0.00001。`GetGlyphWidths` 的 6 个 bounds 中，字母 g 的底部仍有 1 px 差异（替代 4、原版 3），该行为缺口保留记录，不视为通过。", "",
          "其他边界：目前按 run 合并轮廓；互相重叠 glyph 的透明合成、特殊字体 metrics、默认字体选项不具备完整行为兼容。新属性只接受当前实际支持的 unhinted/linear/unsnapped/no-embolden 状态，其余明确抛错；默认值与 Skia 默认值的差异仍在兼容清单中。", "",
          "## Avalonia 接入差距与下一步", "",
          f"真实 Avalonia.Skia.dll 引用了 {audit['UsedSkiaTypes']} 个 Skia 类型，本实现存在 {audit['PresentSkiaTypes']} 个；{audit['MatchedSkiaMemberReferences']}/{audit['MatchedSkiaMemberReferences']+audit['MissingSkiaMemberReferences']} 个成员引用存在签名，仍缺 {audit['MissingSkiaMemberReferences']} 个。逐成员/调用者清单见 [Avalonia API 扫描](avalonia-api-12.1.4.json)，原始数据目录也保留对应 DLL 的快照。包含属性访问器，C# 已内联的枚举常量不在计数内。存在签名不等于行为通过；native Handle 及不支持的字体配置仍会拒绝。", "",
          "| 使用面 | 本轮状态 | 接入要求 / 优先级 |", "|---|---|---|",
          "| 字形 run / text blob | 新增定位 run 构建、不可变 blob、字形度量和绘制；内容与覆盖率缓存 | P0：先修字体画质、bounds 和默认选项，再补 managed shaping/字体匹配；常用中文 TTC 仍不支持 |",
          "| 矩形 / 仿射 / 图片 | 既有绘制链；新增轴对齐无 AA 图片行采样与不透明连续行 SIMD 拷贝 | P0：DrawImage/SKSamplingOptions 接口、圆角、任意/AA clip、双线性采样、脏区 regions |",
          "| 透明层 / 特效 | SaveLayer、opacity mask、阴影/blur、组合 brush 等不足 | P0：必须实现真实离屏分组与合成，不能逐项乘 alpha 替代分组透明度 |",
          "| 几何 / 命中测试 | 基础路径/描边存在；路径运算、测量、复杂 join/cap 不足 | P1：几何 bounds、区域/距离测试、布尔运算 |",
          "| 图片输入 / 颜色 | 纯 C# PNG；JPEG/WebP/ICC 等不足 | P1：补解码、颜色管理及 Avalonia bitmap/stream 调用面 |",
          "| 窗口 GPU 后端 | 现有 Vulkan 离屏 compute，可验证 text blob；尚无 Avalonia 窗口呈现适配 | P0：复用宿主 Vulkan device/queue、窗口 image/swapchain、同步与线程约束；避免逐帧 CPU 回读 |", "",
          "Avalonia 原 `GlyphRunImpl` 构造阶段为了 bounds 固定请求 Full hinting / LCD，即使显示选项另设也会经过这一步。因此当前新增的子集仍不能直接重编译运行原 Avalonia.Skia。可选择为 Avalonia 的平台渲染接口建立独立 SimdSkia 适配层，逐项补齐，保留明确不支持报告；本轮未创建空适配器冒充接入。", "",
          "现有 GPU 报告测的是离屏绘制、等待完成并回读 CPU；Avalonia 窗口通常直接 GPU 合成与呈现，两者不可当成相同的帧率。**本轮没有新的窗口 GPU 提速结论。**后续验收需相同窗口场景、DPI、字体效果、脏区与设备，分别测 CPU 工作、提交时间和实际 GPU 完成/呈现。", "",
          "## 复现", "", "```powershell", "dotnet build SimdSkia.sln -c Release", "dotnet run --project examples/AvaloniaText -c Release", "dotnet run --project tests/Avalonia -c Release", "dotnet run --project tests/Avalonia/Reference -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf artifacts/avalonia/tests-reference", "dotnet run --project tools/AvaloniaAudit -c Release -- baseline/avalonia.skia.12.1.4/lib/net8.0/Avalonia.Skia.dll src/SimdSkia.Compatibility/bin/Release/net8.0/SimdSkia.Compatibility.dll docs/avalonia-api-12.1.4.json", "powershell -File tools/run-avalonia-benchmarks.ps1 -Phase after -Output artifacts/avalonia/new-run", "# 要比较自身改动：改动前以 -Phase before 保存，再改动代码并以 -Phase after 写入同一新目录。", f"python tools/report-avalonia-benchmarks.py {args.root.as_posix()} docs/AVALONIA-OPTIMIZATION.md", "```", "",
          "报告程序要求完整的 before/after 数据，验证配置与多进程像素一致性后生成；不覆盖冻结版本。原始证据：[数据目录](../" + args.root.as_posix() + ") / [汇总 CSV](../" + (args.root / "summary.csv").as_posix() + ") / [像素结果](../" + (args.root / "pixel-validation.json").as_posix() + ")。"]
args.report.write_text("\n".join(lines)+"\n", encoding="utf-8")
print(f"Wrote {len(rows)} CPU rows; native quality passes: {sum(r['PixelPass'] for r in rows if r['Engine']=='simd')}/5; all scalar/SIMD frames identical.")
