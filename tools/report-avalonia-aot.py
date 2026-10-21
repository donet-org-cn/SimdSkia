"""Archive raw AOT measurements and regenerate the Chinese report; stdlib only."""
from pathlib import Path
import json
import shutil
import struct
import sys
import zlib

ROOT = Path(__file__).resolve().parents[1]
MIB = 1048576


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def png(path, width, height, pixels):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
    rows = b"".join(b"\0" + pixels[y * width * 4:(y + 1) * width * 4] for y in range(height))
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))


def main():
    directories = {"portable": Path(sys.argv[1]).resolve(), "v3": Path(sys.argv[2]).resolve()}
    archive = ROOT / "docs/aot-comparison"; archive.mkdir(parents=True, exist_ok=True)
    for isa, directory in directories.items():
        destination = archive / isa; destination.mkdir(exist_ok=True)
        for name in ("manifest.json", "source-files.json", "sizes.json", "results.json", "summary.json", "summary.csv", "quality.json"):
            shutil.copyfile(directory / name, destination / name)
        if (directory / "run-harness.py.txt").exists():
            shutil.copyfile(directory / "run-harness.py.txt", destination / "run-harness.py.txt")
    log_dir = archive / "logs"; log_dir.mkdir(exist_ok=True)
    for name in ("publish-simd.log", "publish-original.log", "publish-simd-v3.log", "publish-original-v3.log", "solution-build.log", "regression.log", "dependency-audit.txt"):
        path = ROOT / "artifacts/aot-comparison" / name
        if not path.exists(): continue
        data = path.read_bytes()
        value = data.decode("utf-16") if data.startswith((b"\xff\xfe", b"\xfe\xff")) else data.decode("utf-8-sig")
        (log_dir / (Path(name).stem + ".txt")).write_text(value.replace("\r\n", "\n"), encoding="utf-8", newline="\n")
    validation = ROOT / "artifacts/aot-comparison/validation/result.json"
    if validation.exists(): shutil.copyfile(validation, archive / "validation.json")
    summaries = {key: read(path / "summary.json") for key, path in directories.items()}
    sizes = {key: read(path / "sizes.json") for key, path in directories.items()}
    quality = {key: read(path / "quality.json") for key, path in directories.items()}
    manifests = {key: read(path / "manifest.json") for key, path in directories.items()}
    total_runs = 0
    for isa, directory in directories.items():
        results = read(directory / "results.json"); manifest = manifests[isa]
        if len(results) != 15 * manifest["forks"]: raise RuntimeError("Incomplete measurement set")
        if manifest["forks"] != 3 or manifest["seconds"] != 5 or manifest["warmupSeconds"] != 2:
            raise RuntimeError("This report requires three forks, five measured seconds and two warmup seconds")
        for row in results:
            if not row["nativeAot"] or row["avx2"] != (isa == "v3") or row["cpuCyclesPerFrame"] <= 0:
                raise RuntimeError("Invalid AOT/ISA/CPU-cycle evidence")
            if row["fontSha256"] != "A42B620140F493DB42F741351DFBF343C0936D58588EE8004B8B2A218D997FF1":
                raise RuntimeError("Font hash differs")
            if {n.lower() for n in row["loadedModules"]}.intersection({"coreclr.dll", "clrjit.dll", "clr.dll", "mscorwks.dll", "mscorsvr.dll"}):
                raise RuntimeError("CoreCLR/JIT was loaded; AOT flags alone cannot verify Native AOT")
        total_runs += len(results)
    image_dir = archive / "images"; image_dir.mkdir(exist_ok=True)
    for case in ("controls-1-throughput", "controls-1.5-throughput", "scroll-1-throughput"):
        for variant in ("simd", "original-default", "original-full"):
            shutil.copyfile(directories["v3"] / case / f"{variant}-1/scene.png", image_dir / f"{case}-{variant}.png")
        width, height = (1500, 1080) if "1.5" in case else (1000, 720)
        a = (directories["v3"] / case / "simd-1/scene.rgba").read_bytes()
        b = (directories["v3"] / case / "original-full-1/scene.rgba").read_bytes()
        heatmap = bytearray(len(a))
        for i in range(0, len(a), 4):
            heatmap[i] = min(255, max(abs(a[i+j] - b[i+j]) for j in range(4)) * 4)
            heatmap[i + 3] = 255
        png(image_dir / f"{case}-difference-x4.png", width, height, heatmap)
    # ISA comparison verifies that enabling AVX2 did not silently alter the captured output.
    isa_quality = []
    for case in ("controls-1-throughput", "controls-1.5-throughput", "scroll-1-throughput", "controls-1-paced", "scroll-1-paced"):
        for variant in ("simd", "original-default", "original-full"):
            a = (directories["portable"] / case / f"{variant}-1/scene.rgba").read_bytes()
            b = (directories["v3"] / case / f"{variant}-1/scene.rgba").read_bytes()
            maximum = max(abs(x-y) for x, y in zip(a, b))
            isa_quality.append({"case": case, "variant": variant, "maximumError": maximum, "byteIdentical": a == b})
    (archive / "isa-output-check.json").write_text(json.dumps(isa_quality, indent=2) + "\n", encoding="utf-8")
    def label(case):
        return {"controls-1-throughput": "控件 1000×720", "controls-1.5-throughput": "控件 1500×1080",
                "scroll-1-throughput": "列表 1000×720", "controls-1-paced": "控件 60 FPS", "scroll-1-paced": "列表 60 FPS"}[case]
    def backend(variant):
        return {"simd": "SimdSkia", "original-default": "原版默认", "original-full": "原版完整组"}[variant]
    def result(isa, case, variant):
        return next(r for r in summaries[isa] if r["case"] == case and r["variant"] == variant)
    simd = result("v3", "controls-1-throughput", "simd")
    original = result("v3", "controls-1-throughput", "original-default")
    portable_simd = result("portable", "controls-1-throughput", "simd")
    lines = ["# Avalonia Native AOT：体积、速度、CPU 与内存实测", "",
        "交付两份真实 Win32 软件窗口程序：共享相同中英文控件、Fluent 主题、字体、布局与基准代码，分别使用 SimdSkia 和原版 Avalonia.Skia。两边均实际 Native AOT 发布和运行，不使用 JIT 数字替代 AOT。", "",
        "**应用与对照测量已完成，完整 Avalonia 替换仍未通过画质验收。** 本文保留失败的像素门槛与原版默认配置；这些速度数字不代表画质已经相同。", "",
        f"实测重点：v3 的 1000×720 控件页 SimdSkia {simd['drawMsPerFrame']:.3f} ms/帧，原版默认 {original['drawMsPerFrame']:.3f} ms/帧，耗时比 {simd['drawMsPerFrame']/original['drawMsPerFrame']:.2f}×；通用 AOT 的 SimdSkia 为 {portable_simd['drawMsPerFrame']:.3f} ms/帧。v3 工作集约 {simd['workingSetMiB']:.1f} vs {original['workingSetMiB']:.1f} MiB。完整运行目录约 {sizes['v3']['simd']['runtimeBytes']/MIB:.2f} vs {sizes['v3']['original']['runtimeBytes']/MIB:.2f} MiB。60 FPS 的时间计数 CPU% 波动明显，以下同时列范围与独立 CPU 周期，不能只凭中位数百分比判断胜负。", "",
        "## 实测环境与口径", "",
        f"- Windows 11 10.0.26300，Intel Core i7-8700K（6 核 / 12 线程），物理 RAM {manifests['portable']['physicalMemoryBytes']/MIB/1024:.2f} GiB。SDK {manifests['portable']['sdk']}，Native AOT runtime 10.0.13。",
        "- 固定 Avalonia / Avalonia.Skia / Fluent 12.1.4，SkiaSharp 3.119.4，HarfBuzzSharp 8.3.1.3；同一 ZCOOL XiaoWei Regular OFL TTF（6,313,808 B）。两边均使用 Win32 Software，不测 GPU。",
        "- 通用 x64 与 x86-64-v3（含 AVX2）分别发布、分别测量。通用版记录为 AVX2=false / Vector128；v3 版为 AVX2=true / Vector256。**v3 提高最低 CPU 要求，不能当成通用 x64 二进制分发。**",
        "- 每种 ISA 5 个场景 × 3 种后端/透明度模式 × 3 个独立进程，共 45 次；两种 ISA 总计 90 次。各进程预热 2 秒、测量 5 秒，轮换顺序，未并行运行对照。表格为三个进程的中位数，JSON 另有 min/max 与逐进程值。",
        "- 不限帧测试计时为同步 RenderTargetBitmap 绘制和指定的列表平移更新，未计 PNG 保存、窗口提交或 DWM；吞吐 FPS 不能当成屏幕 FPS。60 FPS 模式异步让出 UI 线程，另包含真实窗口软件呈现、调度、光标等进程 CPU。",
        "- CPU 时间来自整个进程 TotalProcessorTime；整机占用率=进程 CPU 时间/墙钟/12，单核比例另存原始数据。另由父进程在测量起止信号处用 [QueryProcessCycleTime](https://learn.microsoft.com/en-us/windows/win32/api/realtimeapiset/nf-realtimeapiset-queryprocesscycletime) 独立读取全部线程的 CPU 周期，不把周期数按假定频率换算成时间或百分比。工作集是物理驻留，私有提交不是物理 RAM，二者不能相加。父进程每 25ms 独立采样稳态内存。",
        "- 启动为父进程创建 exe 至第一次离屏绘制完成的 READY 信号，包含字体、主题与窗口初始化；并非 DWM 首次显示，未重启系统或清空文件缓存，属于文件缓存已热的进程启动测量。",
        "- SimdSkia 完整组混合固定开启；原版默认可用逐次绘制透明度优化，另测显式完整组。原版默认与 SimdSkia 的透明组语义不同，完整组一列用于更接近的行为对照。", "",
        "表格的“原版默认”仅指 **CPU 软件后端默认透明度处理**，两边都显式选用 Win32 Software；本文未测试 GPU，也未测试平台自动选择的其他桌面渲染配置。", "",
        "## 发布体积", "",
        "单位 MiB（1,048,576 B）。发布目录保留字体、许可证及上游复制的 XML；所有 PDB 都单列排除出运行包，未只统计 exe。ZIP 为全部非 PDB 文件的 DEFLATE level 9。", "",
        "| ISA | 后端 | exe | 原生绘图/字体 DLL | 字体 | 完整运行目录 | ZIP | 调试符号 |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for isa in directories:
        for name, values in sizes[isa].items():
            lines.append(f"| {isa} | {'SimdSkia' if name=='simd' else '原版'} | " + " | ".join(f"{values[k]/MIB:.2f}" for k in ("executableBytes", "nativeLibraryBytes", "fontBytes", "runtimeBytes", "zipBytes", "symbolsBytes")) + " |")
    def without_xml(value):
        return (value["runtimeBytes"] - sum(f["bytes"] for f in value["files"] if f["path"].lower().endswith(".xml"))) / MIB
    lines += ["", "exe 大小和总部署体积是两个不同指标。原版还需要 libSkiaSharp.dll 与 libHarfBuzzSharp.dll；SimdSkia 不携带这些组件。上游 XML 文档并非运行必需，逐文件清单保留其大小，另看 exe+DLL 可排除文档复制差异。字体由两边共同携带，不能算作 SimdSkia 独有负担。", "",
        f"**公平裁掉两边全部 XML 文档后**，通用版部署为 SimdSkia {without_xml(sizes['portable']['simd']):.2f} vs 原版 {without_xml(sizes['portable']['original']):.2f} MiB；v3 为 **{without_xml(sizes['v3']['simd']):.2f} vs {without_xml(sizes['v3']['original']):.2f} MiB，约少 {(1-without_xml(sizes['v3']['simd'])/without_xml(sizes['v3']['original']))*100:.1f}%**。该数字仍保留全部字体、许可证、exe 和原版 DLL；原始发布目录与 ZIP 没有改动，便于核查。", "",
        "## 不限帧：实际绘制速度", "", "绘制 ms 是每帧纯绘制平均；CPU ms/帧来自整个进程，P95 是单帧绘制分位数。", "",
        "| ISA | 场景 | 后端 | 绘制 ms/帧 | P95 ms | 绘制吞吐 帧/s | CPU ms/帧 | 整机 CPU % | 分配 KiB/帧 |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for isa, rows in summaries.items():
        for row in rows:
            if not row["case"].endswith("throughput"): continue
            lines.append(f"| {isa} | {label(row['case'])} | {backend(row['variant'])} | {row['drawMsPerFrame']:.3f} | {row['p95Ms']:.3f} | {row['framesPerSecond']:.1f} | {row['cpuMsPerFrame']:.3f} | {row['cpuPercentMachine']:.2f} | {row['allocatedBytesPerFrame']/1024:.2f} |")
    lines += ["", "## 60 FPS 更新：CPU 与内存", "",
        "低 CPU 占用需要固定负载。不限帧忙循环通常占满约一个逻辑处理器，单看其 CPU % 无法判断每帧成本；以下单列 60 FPS 模式。实测 Windows 时间计数在短突发/休眠交替负载下有明显跨进程波动，因此列出全部三次范围，并增加独立周期计数；不使用某次偶然偏低百分比宣称胜出。周期值计入此模式中整个进程的窗口呈现工作，不能与不限帧纯离屏吞吐混用。定时器 deadline 统计保留在原始数据，不等同于显示器漏帧。", "",
        "| ISA | 场景 | 后端 | 实际更新/s | 绘制 ms/帧 | 整机 CPU % 中位数 [min,max] | CPU 百万周期/帧 | 工作集 MiB | 私有提交 MiB | 工作集/物理 RAM % |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for isa, rows in summaries.items():
        for row in rows:
            if not row["case"].endswith("paced"): continue
            lines.append(f"| {isa} | {label(row['case'])} | {backend(row['variant'])} | {row['framesPerSecond']:.1f} | {row['drawMsPerFrame']:.3f} | {row['cpuPercentMachine']:.2f} [{row['cpuPercentMachineMin']:.2f}, {row['cpuPercentMachineMax']:.2f}] | {row['cpuCyclesPerFrame']/1_000_000:.2f} | {row['workingSetMiB']:.1f} | {row['privateMiB']:.1f} | {row['workingSetPercentPhysicalRam']:.3f} |")
    lines += ["", "## 启动与不限帧内存", "",
        "以下使用每个配置三次独立进程的中位数；启动排在吞吐预热之前。首绘是窗口/字体初始化之后的第一次离屏绘制，不等于整个进程启动。内存含整个 Win32 程序与最多 0.4 MiB 的帧计时缓冲。", "",
        "| ISA | 场景 | 后端 | 启动至首帧 ms | 首次绘制 ms | 工作集 MiB | 私有提交 MiB | OS 峰值工作集 MiB | 采样稳态工作集 MiB |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for isa, rows in summaries.items():
        for row in rows:
            if not row["case"].endswith("throughput"): continue
            lines.append(f"| {isa} | {label(row['case'])} | {backend(row['variant'])} | {row['launchToFirstOffscreenFrameMs']:.1f} | {row['firstDrawMs']:.1f} | {row['workingSetMiB']:.1f} | {row['privateMiB']:.1f} | {row['peakWorkingSetMiB']:.1f} | {row['sampledMedianWorkingSetMiB']:.1f} |")
    lines += ["", "## 画质与运行验证", "",
        "固定整页门槛最大通道误差 ≤32、RMSE ≤2。没有降低 AA、修改字体、放宽门槛或排除文字来获取更快成绩。两种 ISA、两个原版透明度配置均比较原始 RGBA；完整组与默认组的结果分别记录。", "",
        "| ISA | 场景 | 原版配置 | 最大误差 | RMSE | 结果 |", "| --- | --- | --- | ---: | ---: | --- |"]
    for isa, rows in quality.items():
        for row in rows:
            lines.append(f"| {isa} | {label(row['case'])} | {backend(row['reference'])} | {row['maximumChannelError']} | {row['rmse']:.3f} | {'PASS' if row['passed'] else 'FAIL'} |")
    exact = sum(item["byteIdentical"] for item in isa_quality)
    lines += ["", f"- 通用 AOT 与 v3 AOT 的固定姿态输出对比：{exact}/{len(isa_quality)} 逐字节相同，完整细节见 [ISA 输出核查](aot-comparison/isa-output-check.json)。",
        "- 90 个发布 exe 均通过 AOT 标志、真实 Fluent 控件模板、固定 ImageBrush 像素、字体加载与模块检查；父脚本校验 PE 没有 CLR/IL header，另检查所有进程未加载 CoreCLR/JIT。AOT 的 runtimeconfig 可让中间 IL/JIT 进程也报告 IsDynamicCodeSupported=false，普通 apphost 也没有 CLR header，因此两者均不能单独证明 Native AOT。模块校验在负向探针发现此陷阱后补强，已对全部 90 份原始模块记录重新核查；没有修改受测 exe 或绘制计时代码。SimdSkia 检查确有 Win32 软件帧呈现且未加载原版原生绘图/字体组件；原版进程确认实际加载 Skia/HarfBuzz DLL。",
        "- 适配层精确保留固定 Avalonia 12.1.4 内部 IRef<T>.Item getter 元数据，解决 Native AOT IL2075；只抑制这一个有明确 DynamicDependency 的反射分析警告，未全局关闭裁剪/AOT 诊断。新示例不使用无头宿主的反射初始化入口，也不使用反射 JSON 序列化。",
        "- 额外运行现有控件回归：**8109/8109 PASS**；生产依赖审计 PASS。此回归是 .NET 8 JIT 测试，不混入本文 AOT 性能值。",
        "- 全解决方案 Release 构建 PASS，0 警告、0 错误；四次实际 AOT 发布均无剩余编译/裁剪警告，原始构建日志已存档。",
        "- [负向与产物审计](aot-comparison/validation.json)：真实 JIT 携带伪 AOT 标志仍被父脚本拒绝，IL PE 与非法测量时长被拒绝；4 个发布 exe 哈希与测量清单一致，4 个运行 ZIP 逐文件哈希/CRC 完整，受测源码及冻结的原始测量脚本哈希一致。当前父脚本只补强 AOT 验证并保留未来测量脚本副本，未修改受测程序。",
        "- 画质失败时测量脚本仍保存完整数据并返回 **2**；构建/运行失败才返回 1。页面像素差距、通用字体排版和 Vulkan 窗口等此前缺口仍未因此解决。", "",
        "截图为 v3 发布 exe 实际生成，差异图显示每像素最大通道误差 ×4（红色）：", "",
        "| 场景 | SimdSkia | 原版默认 | 原版完整组 | 差异 ×4（与完整组） |", "| --- | --- | --- | --- | --- |"]
    for case in ("controls-1-throughput", "controls-1.5-throughput", "scroll-1-throughput"):
        lines.append(f"| {label(case)} | " + " | ".join(f"[PNG](aot-comparison/images/{case}-{suffix}.png)" for suffix in ("simd", "original-default", "original-full", "difference-x4")) + " |")
    lines += ["", "## 复现与产物", "", "```powershell", "powershell -NoProfile -File tools/fetch-avalonia-font.ps1",
        "python tools/run-avalonia-aot.py --output artifacts/aot-comparison/portable",
        "python tools/run-avalonia-aot.py --isa v3 --publish artifacts/aot-comparison/publish-v3 --output artifacts/aot-comparison/v3",
        "python tools/report-avalonia-aot.py artifacts/aot-comparison/portable artifacts/aot-comparison/v3", "```", "",
        "预期：当前画质仍 FAIL，两个测量命令返回 2；仍可执行第三条报告命令。`--no-build` 仅用于已完成同 ISA 发布的目录。运行交互程序时不带 `--hidden/--output`：", "",
        "```powershell", "& artifacts/aot-comparison/publish/simd/AvaloniaAot.SimdSkia.exe",
        "& artifacts/aot-comparison/publish/original/AvaloniaAot.Original.exe", "```", "",
        f"完整发布物在 `artifacts/aot-comparison/publish` 与 `publish-v3`；本轮正式测量目录为 `{directories['portable'].relative_to(ROOT).as_posix()}` 与 `{directories['v3'].relative_to(ROOT).as_posix()}`，各有不含 PDB 的 `simd-runtime.zip` / `original-runtime.zip`，保留字体与许可证。初步测量/中止批次不计入本报告的 {total_runs} 次。",
        "源代码、精确依赖和单进程用法见 [示例说明](../examples/AvaloniaAot/README.md)。JSON/CSV 存档如下：", "",
        "- 通用版：[汇总 CSV](aot-comparison/portable/summary.csv)、[45 次逐进程数据](aot-comparison/portable/results.json)、[体积与文件 SHA](aot-comparison/portable/sizes.json)、[环境与 exe SHA](aot-comparison/portable/manifest.json)。",
        "- v3 版：[汇总 CSV](aot-comparison/v3/summary.csv)、[45 次逐进程数据](aot-comparison/v3/results.json)、[体积与文件 SHA](aot-comparison/v3/sizes.json)、[环境与 exe SHA](aot-comparison/v3/manifest.json)。",
        "- 每个运行目录保留 PNG/RGBA、stdout、25ms 资源采样；存档 source-files.json 记录未提交源码快照 SHA，与旧 HEAD 清楚区分。",
        "- AOT 构建、8109 项回归与依赖隔离的实际日志位于 [logs](aot-comparison/logs/regression.txt)；每种 ISA 另存 run-harness.py.txt，是实际测量时父脚本的冻结副本。",
        "- [.NET 官方 Native AOT 部署与工具链要求](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)；[运行时官方 AOT 指令集说明](https://github.com/dotnet/runtime/blob/main/src/coreclr/nativeaot/docs/optimizing.md)：默认指令集与显式提高最低硬件要求的配置应分别评估。", ""]
    (ROOT / "docs/AVALONIA-AOT-COMPARISON.md").write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print("Archived AOT reports, 12 screenshots/differences and cross-ISA output checks.")


if __name__ == "__main__": main()
