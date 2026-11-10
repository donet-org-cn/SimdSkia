# 2026-11-10 性能与回归证据

本目录对应工作区源码优化，正式 NuGet 为 0.1.4。修改前引擎冻结于 `1eceed6`，原版 SkiaSharp 3.119.4，控件 Avalonia 12.1.5。

- [性能报告与图表](../../PERFORMANCE-2026-11-10.md)
- [完整测试报告](../../VALIDATION-PERFORMANCE-2026-11-10.md)
- [测试命令与退出码](validation/results.json)
- [运行命令、构建和采集状态](collection-status.json)
- [后续内存、冻结回放及GPU确认状态](post-status.json)
- [真实原生EXE证明](native-proof.json)
- [源码哈希](source-sha256.json)

294个性能工作进程分为CPU基础12、CPU API 9、编解码90、Vulkan微场景12、GPU控件页54、AOT窗口60、图片长期持有45、GPU中文加长复测12。各套件的 `summary.json`、`runs.json`、每进程数据及日志分别保存在对应目录，CSV导出补充各实现的CPU与内存字段。

有效广泛回归82个命令，56通过，25个原版差异套件及1个约定API缺口审计返回2。原始扫描另保留一次专用工具参数错误，完整参数的冻结回放记录位于 `old-engine-replay`。

`frame-objects.json` 将原始RGBA/BGRA、延迟ticks和测试二进制输出映射至 `objects/<sha256>.gz`。对象以未压缩内容SHA256命名，gzip无损压缩。`sha256.json` 核对本目录每个归档文件的字节。核验命令如下。

```powershell
python tools/verify-final-evidence.py --directory docs/evidence/performance-2026-11-10
```

工具核验覆盖归档完整性。原版画质失败状态另见各套件比较结果及测试报告。
