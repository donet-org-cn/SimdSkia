# SimdSkia iOS 源码示例

示例使用 Avalonia 12.1.5 的 iOS 窗口和输入宿主，将 SimdSkia 的绘制提交给宿主的 `CAMetalLayer`。页面包含中文、径向渐变、可点击按钮和文本输入。回退到 CPU 的绘制命令会操作 Metal 的共享像素缓冲。

构建目标为 `net10.0-ios26.0`。在有兼容 Xcode 的 Apple Silicon Mac 上构建模拟器应用时，从仓库根目录执行以下命令。Intel Mac 使用 `iossimulator-x64`。

```sh
dotnet workload install ios
dotnet build examples/Mobile.iOS -c Release -r iossimulator-arm64
```

模拟器启动后可使用 Xcode 的 `simctl` 安装并启动生成的应用。设备构建使用 `ios-arm64`，签名按所用开发团队和设备的配置执行。

```sh
xcrun simctl install booted examples/Mobile.iOS/bin/Release/net10.0-ios26.0/iossimulator-arm64/Mobile.iOS.app
xcrun simctl launch booted org.simdskia.iossample
```

构建工具校验官方 iOS 宿主 DLL 的 SHA-256，移除其默认 Skia 和 HarfBuzz 注册。示例保留 UIKit 宿主、输入、生命周期和 Avalonia 控件，并将随应用分发的 OFL 中文字体设为默认字体。原生构建、屏幕呈现、输入和前后台切换待在由 Xcode 驱动的环境验收。
