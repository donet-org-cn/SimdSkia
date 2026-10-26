# 发光控件与流光进度条：独立 Native AOT 示例

Windows 自绘窗口，不依赖 Avalonia。SimdSkia 绘制暗色界面、发光文本框、流光进度条、暂停/继续与重置按钮；Windows 只负责窗口、输入、剪贴板、输入法候选窗口和像素呈现。

![发光控件](../../docs/evidence/native-controls-2026-10-23.png)

## 使用

```powershell
dotnet publish examples/NativeControls/NativeControls.csproj -c Release -o artifacts/native-controls-aot/final
& .\artifacts\native-controls-aot\final\NativeControls.exe
```

需要 Windows x64、.NET 10 Native AOT 工具链和系统微软雅黑 `Windows\Fonts\msyh.ttc`。不打包字体、Skia/HarfBuzz DLL 或 .NET 运行时；采用前一示例的体积优先与精简诊断配置。

- 文本框：点击定位光标、Shift+方向键选区、左右/Home/End、退格/Delete、Ctrl+A/C/X/V；输入上限 128 个 UTF-16 字符，支持基本中英文单行文字。不支持的字体字符、复杂文字排版或超长输入会显示提示。
- 动画：约 30 ms 请求一次重绘，按单调时钟经过时间推进；每 12 秒循环。暂停同时冻结进度和流光，重置从零重新运行。最小化时停止计时器。
- 按钮：青色/紫色发光边缘、悬停高亮、按下颜色变化；鼠标在按钮外释放会取消操作。Tab 切换焦点，Enter/空格激活按钮。
- 发光：多层透明圆角轮廓叠加；文本框焦点有轻微呼吸效果。进度条使用蓝青渐变和移动亮条。全部效果在 CPU 上由 C# 绘制，不调用原生绘图滤镜或 GPU。

提交后的中文由 SimdSkia 绘制，系统默认 IME 负责预编辑和候选面板定位。此示例没有完整控件框架、无障碍、撤销栈、多行编辑或复杂脚本排版。

## 实际验证

```powershell
dotnet run --project tests/NativeControls -c Release
python tools/test-native-controls.py
```

第二条命令需要可见 Windows 桌面和 Python Pillow。脚本从不含 PDB 的部署目录启动真实 AOT EXE，检测动画变化、暂停冻结、继续、重置、英文编辑和中文字符消息、按钮按下反馈、调整大小及正常关闭；保留截图、文件 SHA-256、模块清单和 ZIP。自动化测试不覆盖真实 IME 的完整选词流程，也不修改用户剪贴板来测试复制粘贴。

输出：`artifacts/native-controls-aot/portable/NativeControls.exe`、`native-controls-aot.zip`、`results.json`。体积和测试结果见[交付报告](../../docs/NATIVE-CONTROLS-2026-10-23.md)。
