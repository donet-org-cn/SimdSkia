# Qcxt OCR ingress benchmark

This benchmark targets the actual Qcxt.OCR.SkiaSharp module and its resolved SkiaSharp 4.153.1 dependency. The original Qcxt repository is read-only: source and models are linked, all generated files stay in this workspace. Reference workers include the complete original SkiaOcrExtensions.cs; Managed workers reuse the exact RunBitmap method and adapt PNG byte decoding. The latter is a comparison adapter, not a claim that the complete extension compiles against SimdSkia.

Configure the ignored machine-local `Target.local.props`:

```xml
<Project>
  <PropertyGroup>
    <QcxtRoot>F:/基础/qcxt.framework</QcxtRoot>
    <TargetSkiaRoot>E:/data/.nuget/skiasharp/4.153.1</TargetSkiaRoot>
    <TargetNativeSkiaRoot>E:/data/.nuget/skiasharp.nativeassets.win32/4.153.1</TargetNativeSkiaRoot>
  </PropertyGroup>
</Project>
```

Locate NuGet roots from the target module's obj/project.assets.json rather than assuming the default user package cache. Use matching managed/native versions. This Windows benchmark uses .NET 10 and the target's CPU default OCR options, with the current ONNX model assets and real 500x500 PPM fixture.

```powershell
powershell -NoProfile -File tools/run-ocr-benchmarks.ps1 -Output artifacts/ocr/my-run
python tools/report-ocr-benchmarks.py artifacts/ocr/my-run
```

Raw OCR, retained bitmap OCR, and PNG decode+OCR each run in their own fresh worker, as does decoding at each size. Model loading, input loading, JSON and pixel checks are outside timing. Decode buffer allocations and actual OCR allocations/GC remain inside timing. Total managed allocation counts cover all managed threads, important for the automatic parallel OCR pipeline; native malloc is not included.

`CompatibilityProbe` links the unmodified extension to SimdSkia. Its build currently fails (missing SKStream/SKPixmap and further stream/pixmap APIs), intentionally preserving evidence of the compatibility gap. It is not in the production solution.

Licenses: the linked Qcxt.OCR source/model distribution retains its own Apache-2.0/third-party notices; the Qcxt.OCR.SkiaSharp extension is MIT. No target source, model or notices are modified or redistributed by these project links. SkiaSharp 4.153.1 and its native library are used only by the isolated Reference worker. Production SimdSkia dependencies remain unchanged.

The portable .NET 6/8 target branches are not benchmarked here. SimdSkia production currently targets .NET 8, so this experiment does not establish .NET 6 support or binary compatibility. See docs/OCR-PERFORMANCE.md for measured numbers, format support and transparent-PNG differences.
