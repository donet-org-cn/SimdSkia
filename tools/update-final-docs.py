"""Update current documentation while retaining dated historical reports."""
from pathlib import Path

version='0.1.2-preview.2'
if f'## {version}：公共API与GPU优化预览' in Path('README.md').read_text(encoding='utf-8'):
    raise SystemExit('Current documentation update already applied; edit current sections directly to retain history once.')
report='PUBLIC-API-FINAL-2026-11-07.md'
summary=('固定 SkiaSharp **3.119.4 / 3235 项**，当前匹配 **3230 项、201 公开类型、5 缺口、0 额外签名**。'
         '从99项冻结快照补入94项；五项原生句柄/版本合同按用户要求保留，不伪造替代语义。'
         '新增真实Alpha8、浮点颜色/Blender/抖动、LCD/中文点阵、SVG/XPS、原生v103 SKP子集与GL/D3D12/Metal GPU资源链路。'
         'Vulkan增加非重叠矩形合批、设备内复制、直接完成读回与去分配；Metal没有Mac GPU实机验证。'
         '**JPEG、部分文字/曲线、整页画质及性能/内存差距仍存在，不代表全面行为或二进制兼容。**')
prefix=(f'# {version} 更新\n\n3230/3235项固定基线，保留5项原生合同缺口；实际SVG/XPS/SKP、Alpha8、浮点颜色与GPU资源互操作。'
        'Vulkan合批/设备复制/完成读回优化；Metal无Mac GPU，尚未实机验证。Avalonia接受>=12.1.0且<13.0.0，已验证宿主12.1.0–12.1.5。'
        'JPEG、文字/曲线/整页画质及性能/内存仍有差距。Sdcb.SimdCodecs保持1.4.0；.NET系统压缩与macOS官方窗口宿主是已允许的原生例外。'
        '完整测试、CPU/GPU/内存性能及兼容限制见包内CHANGELOG.md和[本版报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/'+report+')。\n\n')
path=Path('README.md');text=path.read_text(encoding='utf-8')
start=text.index('独立 C# 2D 引擎，内部使用')
body=text[start:];end=body.find('## 示例、实现与测试报告')
if end!=-1:body=body[:end]
first=body.find('## 0.1.0 更新了什么')
if first!=-1:body=body[:first]+body[body.index('## NuGet 包怎么选',first):]
body=body.replace('0.1.1',version)
body=body.replace('当前已发布的 12.1.0–12.1.5 已通过','已验证的 12.1.0–12.1.5 在此前版本已通过')
body=body.replace('**macOS GPU/Metal 尚未实现，完整兼容性和画质验收仍未全部通过。**','**Metal核心已有实现但无Mac GPU实机验证，尚未接入Avalonia窗口呈现；完整兼容性和画质验收仍未全部通过。**')
body=body.replace('0.1.2-preview.2 为正式补丁版本，无需勾选预发行版。','0.1.2-preview.2 是预览版，NuGet 界面需要勾选“包含预发行版”；公开下载状态以发布记录为准。')
body=body.replace('Windows Direct2D/D3D11，纯 C# 系统 API 互操作，无第三方绑定包','Windows Direct2D/D3D11及D3D12互操作，C#系统API绑定')
body=body.replace('Vulkan 离屏后端，需要系统 Vulkan loader/驱动','Vulkan、GL/GLES与Metal核心（Metal未实机验证），需系统API/驱动')
body=body.replace('`preview.6` 默认','默认')
path.write_text('# SimdSkia\n\n## '+version+'：公共API与GPU优化预览\n\n'+summary+'\n\n'
               +'[完整测试、CPU/GPU和内存报告](docs/'+report+') · [发布状态](docs/NUGET-PREVIEW.md) · [更新日志](docs/RELEASE-NOTES.md)\n\n'
               +body+'## 示例与验证\n\n```powershell\ndotnet build SimdSkia.sln -c Release\ndotnet run --project examples/Rectangle -c Release\ndotnet run --project examples/Compatibility99 -c Release\npython tools/run-final-compatibility.py --output artifacts/reproduce/final-api\n```\n\n'
               +'Compatibility99实际输出中英文PNG、带链接PDF及读取后的页面、向量SVG/XPS/SKP、延迟图片和WebP，并读取验证真实Alpha8。需要先按tools/fetch-test-font.ps1准备许可明确的测试字体；应用默认发现系统字体，无需默认携带字体资源。\n\n'
               +'GPU对照需要真实驱动，Metal无硬件不能计为通过。严格画质失败保持退出2，无后端/无硬件保持退出3；没有测试执行的dotnet test输出不能当作通过。性能、所有失败、逐项清单及复现命令见上方报告，旧版结果只在各自日期报告中使用。\n\n'
               +'生产包不调用Google Skia，不携带其原生绘图库；PNG/TIFF允许.NET系统压缩，macOS窗口使用允许的官方原生宿主。CPU光栅/几何/字体/基础排版仍为C#。第三方依赖、用途/许可证与原生边界见[依赖说明](docs/DEPENDENCIES.md)。\n\n'
               +'[进度](docs/PROGRESS.md) · [兼容报告](docs/COMPATIBILITY.md) · [验证](docs/VALIDATION.md) · [架构](docs/ARCHITECTURE.md) · [MIT许可证](LICENSE)\n',encoding='utf-8')

compat=Path('docs/COMPATIBILITY.md');old=compat.read_text(encoding='utf-8')
history=old[old.index('## 下方为早期应用验收与实现历史'):]
table='''## 当前能力与边界

| 模块 | 已实现 | 仍需区分的限制 |
|---|---|---|
| 像素/图像 | 真正Alpha8、RGBA/BGRA、Span/指针/stride、提取/复制/缩放、懒图、编解码/Codec | 非全部像素格式/ICC目标；部分Span合同不同；压缩输入仍可能预解码 |
| 路径/画布 | 变换/描边/裁剪/路径效果/布尔、Picture、网格/Atlas/曲面、Lattice | 曲线布尔使用展平；部分AA/文字/整页严格画质失败 |
| 着色器/滤镜 | float/渐变/Perlin、RuntimeEffect子语言、CPU滤镜/Blender/ColorF/dither | 不是完整SkSL；GPU对新增复杂阶段明确拒绝，需显式回退 |
| 字体 | 系统发现、中文TrueType、基本中英文布局、LCD、单色嵌入点阵、TextBlob | 完整hinting/复杂脚本/全部彩色与可变格式未覆盖；字形像素不同 |
| GPU | Vulkan/Direct2D、GL/GLES、D3D12与真实资源导入/接管/回调/方向/完成读回 | Metal无实机验证；MSAA/部分采样/非sRGB/高级状态/SaveLayer仍有限制；部分路径有CPU桥接 |
| 文档 | 多页PDF生成及可选读取/显示；SVG/XPS向量；原生v103 SKP子集交叉读写 | PDF非完整规范且生成文字非搜索矢量；SKP不支持全部flattenable与操作码，明确报错 |

源码默认兼容目标为SkiaSharp3.119.4。元数据、Dispose、参数/异常、释放回调与画质分开记录；同名类型默认不能与原版无别名混编。程序集名称/强名称/公钥不同，没有二进制替换承诺。原版只在隔离对照项目使用。

'''
compat.write_text('# 兼容报告：SkiaSharp 3.119.4\n\n'+summary+'\n\n[本轮全部结果与限制]('+report+') · [逐项API](api/comparison.json)\n\n'+table+history,encoding='utf-8')
for name,title in [('PROGRESS.md','开发进度'),('VALIDATION.md','验证记录')]:
    path=Path('docs')/name;old=path.read_text(encoding='utf-8')
    path.write_text('# '+title+'：'+version+'\n\n'+summary+'\n\n[本轮完整测试/性能/内存与复现]('+report+')；发布状态见[NUGET-PREVIEW.md](NUGET-PREVIEW.md)。\n\n## 以下为历史检查点\n\n'+old,encoding='utf-8')
path=Path('docs/RELEASE-NOTES.md');old=path.read_text(encoding='utf-8')
notes=f'''## {version} — 公共API与GPU优化预览

- 固定3.119.4基线匹配3230/3235，201公开类型，额外0；从99项内部快照补入94项。5项原生句柄/版本接口按用户决定保留，不伪造native Skia对象。
- 汇入此前未发布的路径/混合/滤镜/文字/Picture/RuntimeEffect子集、Perlin、网格/Atlas/曲面、Lattice、PDF注释及资源回调实现；仍有明确参数/格式/语义限制。
- 本轮补充实际1字节Alpha8/ExtractAlpha、延迟Picture图片、WebP选项编码、浮点ColorF/Blender/抖动、BGRA水平LCD与中文EBLC/EBDT点阵、真实向量SVG/XPS、原生v103 SKP子集读写与双方交叉读取。
- 新增GL/GLES、D3D12、Metal核心及外部纹理/目标的真实借用/接管/方向/回调/完成读取。Metal没有Mac GPU，未实机验证；原版D3D12工厂在本机不可用，无同后端原版速度比值。
- Vulkan非重叠矩形合批、设备内复制取代外部表面CPU桥接、GPU清屏/完成读回及去分配；关闭/清理缓存会真实释放覆盖缓存。GPU图像被shader/Picture持有时延长回调至最后消费者，ToRasterImage独立复制。
- 566项GPU检查/556次精确缓冲对照、AVX2与SSE2各5568浮点缓冲对照通过；真实Native AOT新增CPU链路正常/标量各41检查、系统GL另18检查通过。完整CPU/Vulkan/Direct2D/GL、CPU时间/占用与内存实测见[最终报告]({report})，不以提交时间代替GPU完成。
- Avalonia接受>=12.1.0且<13.0.0；已验证宿主范围12.1.0–12.1.5，未来补丁需补宿主/回归。编解码依赖保持Sdcb.SimdCodecs1.4.0，.NET系统压缩和macOS官方窗口宿主的原生例外保持。
- **仍有JPEG、曲线/字体/整页严格画质失败、CPU内存和部分GPU/RuntimeEffect/WebP速度差距。预览发布不代表全面行为/二进制兼容，也不代表整体持平/最多落后10%目标已实现。**

以下为此前开发/发行历史，不能当成本版独立新测量。

'''
path.write_text('# SimdSkia 更新日志 / Release notes\n\n'+notes+old.split('\n',1)[1],encoding='utf-8')
for filename in ['docs/NUGET-README.md','src/SimdSkia.Pdf/README.md']:
    path=Path(filename);path.write_text(prefix+path.read_text(encoding='utf-8'),encoding='utf-8')
print('Updated current README, compatibility, validation, progress, release notes and package readmes; dated reports retained.')
