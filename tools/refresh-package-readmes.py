"""Keep usage in current package readmes on the current preview version."""
from pathlib import Path
version='0.1.2-preview.1'
p=Path('docs/NUGET-README.md');s=p.read_text(encoding='utf-8');prefix='\n'.join(s.splitlines()[:4])+'\n\n'
body=s[s.index('Packages target .NET 10 or later:'):].split('## Standalone SkiaSharp namespace usage')[0]
body=body.replace('0.1.1',version).replace('version `0.1.0`','version `'+version+'`')
body=body.replace('Vulkan offscreen rendering; requires the system Vulkan loader/driver','Vulkan and GL/GLES; experimental Metal core without hardware validation; requires system APIs/drivers')
body=body.replace('macOS GPU/Metal is not implemented.','The Metal core is unverified and is not wired into Avalonia presentation.')
sample='''## Standalone SkiaSharp namespace usage

```shell
dotnet add package SimdSkia --version 0.1.2-preview.1
```

```csharp
using SkiaSharp;
using var bitmap = new SKBitmap(160, 100);
using var canvas = new SKCanvas(bitmap);
canvas.Clear(SKColors.White);
using var paint = new SKPaint { Color = SKColors.Blue };
canvas.DrawRect(10, 10, 80, 50, paint);
if (bitmap.GetPixel(20, 20) != SKColors.Blue)
    throw new Exception("Pixel validation failed.");
using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
using var file = File.Create("rectangle.png");
png.SaveTo(file);
```

The facade targets the documented SkiaSharp 3.119.4 subset. The five native contracts remain missing, and rendering/behavior parity is incomplete. Dispose GPU resources explicitly on their creating thread; GC finalization equivalence is not established.
'''
p.write_text(prefix+body+sample,encoding='utf-8')
p=Path('src/SimdSkia.Pdf/README.md');s=p.read_text(encoding='utf-8');prefix='\n'.join(s.splitlines()[:4])+'\n\n'
body=s[s.index('Optional C# PDF parsing and page rendering'):]
start=body.find('**New in 0.1.0-preview.9:**')
if start!=-1:
    end=body.index('```shell',start)
    body=body[:start]+'The optional parser is not pulled in by ordinary Avalonia or compatibility users. Keep all direct SimdSkia package versions aligned.\n\n'+body[end:]
body=body.replace('0.1.1',version)
p.write_text(prefix+'# SimdSkia.Pdf '+version+'\n\nRequires .NET 10.\n\n'+body,encoding='utf-8')
print('Current standalone/PDF usage versions and backend limits synchronized.')
