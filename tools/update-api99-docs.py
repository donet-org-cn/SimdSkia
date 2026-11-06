"""Refresh current entry points, preserving dated historical reports."""
from pathlib import Path
import json
root=Path('docs/evidence/public-api-99-2026-11-07');status=json.loads((root/'status.json').read_text());assert status['missing']==99
summary='固定 SkiaSharp **3.119.4 / 3235 项**基线，当前源码 **3136 匹配、200 公开类型、99 缺口、0 额外签名**，已达到本轮两位数目标。从上一冻结快照增加410项：RuntimeEffect、Picture/Drawable、Lattice、Vulkan GRContext/离屏表面、float/Perlin shader、网格/Atlas/Coons Patch、画布视图、缓存控制和 PDF 注释等真实链路。完整 Release 零警告/错误，474413 项精确优化、255129 项字体压力、24 项新增实际 AOT（正常/显式标量）通过。曲面重复分配由约0.99MB/帧降至测量噪声量级；字体总预算保持2MiB并修复整页缓存抖动。JPEG、部分曲线/文字与整页严格画质仍有失败，网格/RuntimeEffect仍有性能差距，不能宣称全面兼容或整应用同画质持平。**未发布，NuGet 0.1.1 不含本轮新增源码能力**。'
link='[功能、兼容清单、CPU/GPU、CPU占用及内存实测](PUBLIC-API-99-2026-11-07.md)'
p=Path('README.md');s=p.read_text(encoding='utf-8-sig');start=s.index('## 2026-11-06 源码开发进展');end=s.index('### 以下为前批历史快照',start);s=s[:start]+'## 2026-11-07 源码开发进展（尚未发布）\n\n'+summary+' '+link.replace('](PUBLIC-','](docs/PUBLIC-')+'\n\n运行新增集成示例：`dotnet run --project examples/Compatibility99 -c Release`，输出曲面/Perlin/运行时着色器/中英文 PNG、带链接的 PDF 和重新读取的页面。[示例说明](examples/Compatibility99/README.md)。\n\n'+s[end:];p.write_text(s,encoding='utf-8')
for name,title in [('PROGRESS','进度'),('VALIDATION','验证')]:
 p=Path('docs/'+name+'.md');s=p.read_text(encoding='utf-8-sig');end=s.index('以下保留前批历史快照。');s=f'# {title}：2026-11-07\n\n## 本轮公共 API 两位数缺口目标已达到\n\n'+summary+' '+link+'\n\n'+s[end:];p.write_text(s,encoding='utf-8')
p=Path('docs/COMPATIBILITY.md');s=p.read_text(encoding='utf-8-sig');old=s[s.index('最新真实应用验收:'):] if '最新真实应用验收:' in s else s[s.index('最新真实应用验收：'):]
table='''
## 当前源码能力与边界

| 模块 | 已实现链路 | 保留的差距 |
|---|---|---|
| 像素、图像、Surface | Span/指针/外部像素所有权、读取/复制/缩放、编码选项、Codec帧/scanline、RawShader、ApplyImageFilter | 非全部像素格式/颜色空间；压缩输入目前仍完整预解码；原生纹理导入等缺口 |
| 路径与画布 | 曲线/圆角/描边/路径效果/裁剪、路径布尔/Contains/convexity、Picture/Recorder/Drawable、NoDraw/NWay/Overdraw、NullSurface、Lattice、Vertices/Atlas/Coons Patch | 曲线布尔使用展平多边形；部分AA/文字严格画质失败；SKP/SVG/XPS仍缺 |
| 着色器/滤镜 | 渐变、float/两圆锥、Perlin、CPU RuntimeEffect子语言、组合/filter/blender、生成图片/着色器滤镜 | 完整SkSL未实现；已匹配签名也可能对未支持参数明确拒绝；画笔Blender/ColorF/dither等仍缺 |
| 字体/文字 | 中文TrueType、基本中英文布局、系统字体发现、轮廓与覆盖率缓存、TextBlob | LCD/嵌入位图/复杂排版等未全面匹配；2MiB为估算缓存预算，不是进程内存；本轮18glyph/130242字节压力尾值 |
| GPU | Vulkan与Direct2D后端、真实完成回读；借用Vulkan设备的GRContext/Surface、资源限制/清理/失效；GL函数表能力验证 | GL接口描述不是GL绘图；新增网格/Perlin/RuntimeEffect等不自动获得GPU能力；外部纹理、MSAA、mips、Metal/D3D GRContext等仍有缺口 |
| PDF | 光栅多页生成、URL/跨页目的地、注释录制回放、SKWStream；可选托管读取与已支持内容绘制 | 文字非可搜索矢量；无外观且无边框Link可读，可见注释/表单等仍报错；非完整PDF规范覆盖 |

元数据匹配不是二进制兼容或全部行为兼容。默认参数/重载/类型签名逐项见 `docs/api/comparison.json`；Dispose、资源回调、失败边界和画质实测见上方最新报告。调用方像素指针已有真实保留/固定机制，**不代表对象拥有 native Skia Handle**。GPU能力依后端逐条验证，未实现命令不会空绘制后当作成功。

## 下方为早期应用验收与实现历史

下列旧表、旧缓存预算和“仍缺”描述保留其原时点；当前能力以上表及2026-11-07报告为准。

'''
p.write_text('# 兼容报告：SkiaSharp 3.119.4\n\n'+summary+' '+link+'\n'+table+old,encoding='utf-8')
p=Path('docs/DEPENDENCIES.md');s=p.read_text(encoding='utf-8-sig');s=s.replace('# 依赖与许可证\n','# 依赖与许可证\n\n## 2026-11-07 公共 API 99项缺口源码批次\n\nCore新增对既已用于Avalonia层的 **Clipper2 [2.0.0]** 直接引用，用于C#路径布尔运算，BSL-1.0，纯托管、无原生组件；复用 `licenses/Clipper2-BSL-1.0.txt` 随Core包分发。RuntimeEffect、Perlin、Coons与固定边规则均由C#执行，移植算法保留固定基线Skia BSD许可；值合同/旧包装转发保留SkiaSharp MIT许可。不调用Google Skia原生绘图。ANGLE/原版native assets仍仅隔离测试；系统GPU驱动调用与此前允许的.NET压缩/macOS窗口宿主例外保持。当前版本和锁文件随完整Release构建验证，尚未发布。下面保留此前时点依赖记录。\n',1);p.write_text(s,encoding='utf-8')
p=Path('docs/RELEASE-NOTES.md');s=p.read_text(encoding='utf-8-sig');s=s.replace('# SimdSkia 更新日志 / Release notes\n','# SimdSkia 更新日志 / Release notes\n\n## 未发布 — 2026-11-07\n\n- 固定3.119.4基线达到3136/3235，剩余99项、额外0；相比上次冻结2726新增410项，未变更NuGet 0.1.1。\n- 新增真实RuntimeEffect子语言、Picture/Drawable、Lattice、Vulkan上下文/表面、float/Perlin shader、Vertices/Atlas/Coons Patch、画布视图、缓存控制与PDF注释/输出。\n- 移除网格临时包装，给曲面增加有界覆盖缓存，约0.99MB/帧分配降到噪声量级；总字体预算仍2MiB，通过packed run占比修复整页缓存抖动。\n- 完整构建、474413精确优化、255129字体与24新增实际AOT检查通过。JPEG/部分曲线/文字/整页画质失败仍保留，不宣称全面兼容或同画质全面追平。\n- [逐项能力、限制、CPU/GPU及内存实测](PUBLIC-API-99-2026-11-07.md)，[集成示例](../examples/Compatibility99/README.md)。下列内容保留历史时点。\n',1);p.write_text(s,encoding='utf-8')
with Path('docs/API-COMPLETION-WORKLOG-2026-11-06.md').open('a',encoding='utf-8') as f:f.write('\n\n2026-11-07最终冻结：3136/3235、200类型、99缺口、额外0，达到数量目标。新增API与缓存/性能实测、失败门槛和完整余项见 PUBLIC-API-99-2026-11-07.md，不能将metadata达标当作完整行为兼容。最终2MiB字体预算按128/128/1536/256KiB分配，修复首轮整页缓存抖动；首轮中断数据保留。完整Release17、regression12（仅旧JPEG严格失败）、474413精确优化、255129字体、AOT15正常/显式Scalar24项通过，未提交/发布。\n')
print('Updated README/progress/validation/compatibility/dependencies/release notes; historical reports preserved')
