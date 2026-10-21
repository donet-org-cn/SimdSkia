using System.Reflection;
using Avalonia;
using Avalonia.Headless;

namespace SimdSkia.ControlValidation;

internal static class HeadlessHost
{
    // Fixed-version test/window host only. Calling UseHeadless would install HarfBuzz;
    // initialize the exact same managed windowing platform without its font subsystem.
    internal static AppBuilder UseManagedHeadless(this AppBuilder builder)=>builder.UseWindowingSubsystem(()=>
    {
        var method=typeof(AvaloniaHeadlessPlatform).GetMethod("Initialize",BindingFlags.NonPublic|BindingFlags.Static)??throw new MissingMethodException("Avalonia.Headless 12.1.4.Initialize");
        try{method.Invoke(null,[new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}]);}
        catch(TargetInvocationException e) when(e.InnerException is not null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
    },"ManagedHeadless12.1.4");
}
