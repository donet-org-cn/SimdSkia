# Avalonia 12.1.4 实际接入：2026-10-21

新增可独立发布的 [Native AOT 对照应用](../examples/AvaloniaAot/README.md)，包含 SimdSkia 与原版两个共享源码的真实 Win32 程序。使用固定 .NET 10，兼容层补充了图像画刷内部 getter 的精确裁剪元数据保留；AOT 运行实际检查 ImageBrush 像素与原生依赖隔离。完整体积、CPU、内存、速度和画质结果见 [AOT 报告](AVALONIA-AOT-COMPARISON.md)。

新增 `src/SimdSkia.Avalonia`。它注册 Avalonia 自身的绘图、字体和排版服务，现已运行真正的 Button、TextBox、CheckBox、TextBlock、Border、Image 和 Path。控件、样式、布局和输入仍由 Avalonia 执行，绘制不加载 Avalonia.Skia、SkiaSharp、HarfBuzzSharp、FreeType 或 ANGLE。

已经验证真实 Win32 软件窗口完成 framebuffer 呈现，以及无头窗口的鼠标点击、中文输入、150% DPI 切换、窗口尺寸变化和重绘。这是可运行的受测子集，完整应用替换还存在下表所列缺口。当前窗口后端是 CPU；既有 Vulkan compute 仍是另一条离屏路径。

## 使用

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
dotnet run --project examples/AvaloniaControls -c Release
# Windows 交互窗口：按钮可点击，输入框可输入中文
dotnet run --project examples/AvaloniaControls -c Release -- --window
# 自动退出的真实 Win32 验证，窗口置于屏幕外，不参与性能计时
dotnet run --project examples/AvaloniaControls -c Release -- --smoke-window
```

首条运行命令输出 `artifacts/avalonia-controls/managed.png`。对照示例的字体是固定提交、固定 SHA-256 的 OFL 简体中文 TrueType 文件，不使用系统字体代替测试结果。一般 Windows 应用可省略字体配置并读取系统默认字体，不内置或分发 Windows 字体；显式字体路径依然受支持。

应用引用 `SimdSkia.Avalonia.csproj`，在 `AppBuilder` 注册：

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseStandardRuntimePlatformSubsystem()
    .UseWin32()
    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
    .UseSimdSkia() // Windows：读取系统当前界面字体，无需附带字体资源
    .StartWithClassicDesktopLifetime(args);
```

Windows 自动字体的发现方式、回退集合、限制及测试见 [系统默认字体](WINDOWS-SYSTEM-FONTS.md)。显式传入 `options.FontFiles` 时仍以该列表的首个家族作为默认，不自动加入系统字体；Linux/macOS 暂需使用显式配置。性能/画质对照示例继续使用固定字体以保证可重复性。

具体包引用见 [示例项目](../examples/AvaloniaControls/AvaloniaControls.csproj)。`Avalonia.Win32` 的常规包依赖 ANGLE 原生组件；示例以 `PackageDownload` 固定版本，只引用其中托管 DLL。保留 `Avalonia.Desktop` 会传递引入原版渲染组件，因此不能仅修改启动方法就声称满足无原生绘制依赖的部署要求。当前实际桌面宿主验证限 Windows；Linux/macOS 有托管无头/离屏验收，尚无本机桌面宿主承诺。

使用 `TextHintingMode.None` 和 `TextRenderingMode.Antialias`。未指定的文本选项采用无 hint 的灰度覆盖；显式 Strong/Light hinting、LCD subpixel 会报错。示例在根控件设置这些属性。现支持共享字形 mask、设备亚像素相位、baseline snap 和 sRGB 文字预混合；显式文本模式优先于形状 EdgeMode。栅格结果仍有原版差异，详见 [最新性能及画质报告](AVALONIA-TEXT-CACHE.md)。

## 当前实现与边界

| 功能 | 实际实现 | 尚未完成 / 明确拒绝 |
|---|---|---|
| 启动与版本 | `UseSimdSkia` 替换三项平台服务；严格检查 12.1.4.0 | 其他 Avalonia 版本需要单独适配；不替换任何原版程序集身份 |
| 软件窗口 | Win32 framebuffer、离屏层、保存 PNG、present 计数；锁定期间持有存储 | Vulkan 窗口 swapchain、GPU 控件合成、GPU interop |
| 几何 | 矩形、椭圆、每角独立圆角、二/三次曲线、SVG 椭圆弧、填充规则、布尔组合、变换、命中测试、距离与裁取 | 透视变换、精确匹配原版所有几何数值边界 |
| 描边 | flat/round/square cap，miter/round/bevel join，虚线和零长度圆点；可变 pen 与几何编辑使缓存失效 | 特殊无效参数的所有原版容错行为 |
| 裁剪及合成 | 矩形、AA 圆角、几何/区域裁剪；分组透明度、ROI 离屏层、opacity mask；Blur/DropShadow/内外 BoxShadow | SrcOver/Src 以外图片 blend；圆角阴影画质仍失败；未知效果拒绝 |
| 画刷 | 单色、多停点渐变，Pad/Repeat/Reflect、变换；ImageBrush/VisualBrush、tile/flip/stretch/source/destination | 高 DPI 图案仍有原版差异；acrylic/material 和复杂退化渐变未完成 |
| 图片 | RGBA/BGRA、三种 Alpha、stride、源裁切、nearest/bilinear/Mitchell、缩放/锁定；PNG Adam7/16 位/部分 ICC 与 C# JPEG | 四分量渐进 JPEG 拒绝；部分 ICC 未验收；WebP/GIF/BMP/APNG 和无 profile 的宽色域未实现 |
| 字体 | glyf TTF/TTC v1/v2、face 选择、cmap4/12、轮廓、字体表/回退、基本中英文 LTR、定位与下划线交点 | CFF/变量/彩色字体、hint VM、复杂脚本、mark、RTL、通用 GSUB；合成 bold/oblique 拒绝，需实际字体文件 |
| 字距 | 默认 Latin/DFLT `kern` 的 GPOS pair1/2、pair extension 与旧 kern0；15 份原版中英文字形/cluster/advance/offset 夹具通过 | 消耗第二字形的 GPOS pair、GDEF filtering、device/variation 与其他 lookup 显式拒绝；不是完整 OpenType shaper |
| 用户自绘 | 标准 Avalonia DrawingContext | 请求 `ISkiaSharpApiLeaseFeature` 等 Skia 专属接口的控件需要迁移 |

最新边界、命令与验收见 [本轮实现说明](IMPLEMENTATION-DETAILS-2026-10-21.md)、[测试报告](TEST-REPORT-2026-10-21.md)。高 DPI 画刷、圆角阴影仍有原版画质失败；字体集合不代表 CFF/hint/复杂排版已完成。

可选扩展查询返回 null 表示没有该扩展，不伪造 Skia 句柄。效果接口由适配层显式实现；现在支持 Blur 和 DropShadow，其他未知效果仍抛异常，防止 Avalonia 因未实现效果接口而静默忽略绘制。

接口来自 Avalonia 的私有渲染契约。普通 NuGet `ref` 程序集故意阻止第三方实现这些接口；适配层编译时引用同包未修改的 `lib/net8.0` 运行时程序集，启动时检查精确版本。未修改、签名或伪造 Avalonia 程序集。这与 SkiaSharp 兼容层的 425/3235 项清单是两条接入途径：运行这份 Avalonia 示例不意味着缺失的 SK* API 已补齐。

## 验收

```powershell
dotnet run --project tests/AvaloniaControls -c Release -- --frames 5
$env:DOTNET_EnableHWIntrinsic='0'
dotnet run --project tests/AvaloniaControls -c Release -- --frames 5 --output artifacts/avalonia-controls/scalar
Remove-Item Env:DOTNET_EnableHWIntrinsic
powershell -NoProfile -File tools/check-production-dependencies.ps1
```

6581 项断言包含真实合成窗口、鼠标及输入、DPI/resize、资源持有、异常、分组重叠像素、mask、图片预乘过滤、各种格式/stride、SIMD 对独立 Porter-Duff 预期、轴向对通用采样、拒绝合成字体、原版排版夹具，以及字形缓存/顺序合成/预混合、文本模式与原版硬裁剪舍入。`tests/AvaloniaControls.Reference` 才引用原版后端与 HarfBuzz；这两个原版库不在示例或生产引用图内。

固定字体与 Clipper/Avalonia 许可证见 [依赖文档](DEPENDENCIES.md) 及 `docs/licenses`。下一优先级是修复文字/曲线/DPI 像素 gate、降低首次覆盖生成成本和托管工作集；其后补滤镜/图像画刷及 Vulkan 窗口呈现。不能将当前失败的画质检查或 CPU 测量当作已达到完整 Avalonia 的上线要求。
