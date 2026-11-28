using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;

namespace SimdSkia.Avalonia;

internal static class AvaloniaCompatibility
{
    internal static readonly Version MinimumVersion=new(12,1,0,0);
    internal static bool SupportsVersion(Version? version)=>
        version is not null&&version>=MinimumVersion;

#if !AVALONIA_12_2
    // Keep the precise property contracts used by tile brushes available in AOT.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties|DynamicallyAccessedMemberTypes.NonPublicProperties,typeof(IImageBrushSource))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties,"Avalonia.Utilities.IRef`1","Avalonia.Base")]
    [UnconditionalSuppressMessage("Trimming","IL2075",Justification="The image-source and IRef<IBitmapImpl> properties are explicitly retained.")]
#endif
    internal static void Validate()
    {
        var version=typeof(AppBuilder).Assembly.GetName().Version;
        if(!SupportsVersion(version))
            throw new NotSupportedException($"SimdSkia requires Avalonia 12.1.0 or later. Loaded version: {version}.");
#if !AVALONIA_12_2
        var bitmap=typeof(IImageBrushSource).GetProperty("Bitmap",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        var item=bitmap?.PropertyType.GetProperty("Item",BindingFlags.Instance|BindingFlags.Public);
        if(item?.GetMethod is null||!typeof(IBitmapImpl).IsAssignableFrom(item.PropertyType))
            throw new NotSupportedException($"Avalonia {version} has an incompatible image-brush bitmap contract.");
#endif
    }
}
