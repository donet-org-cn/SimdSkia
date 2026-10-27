# 隔离字体轮廓诊断

```powershell
dotnet run --project tests/FontOutlineProbe -c Release -- baseline/fonts/ZCOOLXiaoWei-Regular.ttf artifacts/font-outlines/new-run
```

使用固定 SkiaSharp 3.119.4 导出字形路径，再将原版路径与托管 TrueType 路径交给同一个 C# 光栅器比较，以区分轮廓解析和覆盖率差异。只属于原版对照测试，不能被生产项目引用。

输出逐字形轮廓、几何边界和像素误差；这不是性能测试，也不代表整页文字已达标。诊断导出的原版轮廓不允许作为生产字体数据或代替纯 C# 字体实现。

进一步分离字体指令和保存字体时重算边界的影响（开发 Python 环境需要 `fontTools`）：

```powershell
python tools/diagnose-font-outlines.py --output artifacts/font-instruction-controls/new-run
```

生成“保留/移除指令 × 保留/重算边界”四组副本，每组实际执行 56 个原版轮廓对照，记录输入/副本 SHA-256、几何与像素结果。副本只在该输出目录，不安装为系统字体，不替换 `baseline`，也不允许用它们改善正式性能或画质分数。
