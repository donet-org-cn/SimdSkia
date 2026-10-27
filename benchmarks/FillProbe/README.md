# 大块填充诊断

```powershell
dotnet publish benchmarks/FillProbe -c Release -r win-x64 -o artifacts/fill-probe
artifacts/fill-probe/FillProbe.exe
```

比较普通 `Span<uint>.Fill`、256 B / 1 KiB 初始块的逐倍复制和展开 SSE2 写入，8 种长度、3 次轮换顺序。先核验全部像素再计时；这是热点诊断，不能代替实际 Avalonia 测量。早期试验中大缓冲复制出现回退，保留原始失败数据。本轮仅将实测有收益的 16–256 KiB 范围用于通用 x64 候选，并用真实控件页复测；大缓冲、等通道色、AVX2 和其他架构保留原路径。没有设置更高的最低 x64 指令集。
