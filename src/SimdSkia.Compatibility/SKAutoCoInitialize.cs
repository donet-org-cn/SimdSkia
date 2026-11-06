using System.Runtime.InteropServices;
namespace SkiaSharp;

/// <summary>Balances only COM initialization acquired on this thread. No bundled native library.</summary>
public class SKAutoCoInitialize : IDisposable
{
    private readonly int thread=Environment.CurrentManagedThreadId;
    private readonly bool owns;
    private bool initialized;
    public SKAutoCoInitialize()
    {
        if(!OperatingSystem.IsWindows()){initialized=true;return;}
        int result=CoInitializeEx(IntPtr.Zero,6);owns=result>=0;
        initialized=owns||result==unchecked((int)0x80010106); // RPC_E_CHANGED_MODE: COM is already initialized.
    }
    public bool Initialized=>initialized;
    public void Uninitialize()
    {
        if(!initialized)return;
        if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("COM initialization must be balanced on its creating thread.");
        if(owns&&OperatingSystem.IsWindows())CoUninitialize();initialized=false;
    }
    public void Dispose()=>Uninitialize();
    [DllImport("ole32.dll",ExactSpelling=true)]private static extern int CoInitializeEx(IntPtr reserved,uint options);
    [DllImport("ole32.dll",ExactSpelling=true)]private static extern void CoUninitialize();
}
