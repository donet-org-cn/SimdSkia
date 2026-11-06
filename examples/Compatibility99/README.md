# 公共 API 集成示例

在仓库根目录运行：

```powershell
dotnet run --project examples/Compatibility99 -c Release
```

输出到 `artifacts/examples/compatibility99`：运行时着色器背景、Perlin、彩色 Coons 曲面、中英文文字、录制回放 PNG、多页接口生成的一页带 URL 链接 PDF，以及重新读取后的页面 PNG。第二个参数可指定 TrueType 字体文件。

另输出真实向量 `vector.svg`、`vector.xps`、原生v103子集 `vector.skp`、反序列化后延迟光栅化的 `lazy-picture.png`，以及浮点颜色/抖动绘制的 `float-color.webp`。运行时还读取并验证真实1字节Alpha8数据；不把RGBA缓冲当作Alpha8计量。SKP只导出独立的受支持向量场景，不把不支持的运行时着色器偷偷扁平化成图片。

本例为 CPU 渲染；新增网格、Perlin 和运行时着色器不表示 GPU 后端已实现这些能力。兼容基线为 SkiaSharp 3.119.4 的已实现子集，不提供程序集二进制替换保证。
