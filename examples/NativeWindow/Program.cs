#if INCLUDE_VERIFICATION
using System.Diagnostics;
using System.Text.Json;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkiaSharp;

// Win32 owns the window and input. SimdSkia renders every client-area pixel;
// GDI only transfers the finished BGRA buffer and reads it back for verification.
[SupportedOSPlatform("windows")]
internal static unsafe partial class Program
{
    private const uint Paint = 0x000f, Destroy = 0x0002, Size = 0x0005, Close = 0x0010;
    private const uint EraseBackground = 0x0014, Click = 0x0201, VerifyStep = 0x8001;
    private static readonly SKColor Background = new(242, 245, 250);
    private static readonly SKColor Blue = new(44, 105, 210);
    private static readonly SKColor Green = new(25, 160, 115);
    private static SKBitmap? bitmap;
    private static SKCanvas? canvas;
    private static readonly SKPaint brush = new() { IsAntialias = true };
#if INCLUDE_TEXT
    private static SKTypeface? typeface;
    private static SKFont? font;
    private static string fontPath = "";
    private const string Chinese = "独立窗口 中文绘制";
#endif
    private static Exception? failure;
#if INCLUDE_VERIFICATION
    private static string? verifyDirectory;
    private static int frames, verifiedFrames, phase;
    private static bool stepQueued;
#endif
    private static bool alternate;

    [STAThread]
#if INCLUDE_VERIFICATION
    private static int Main(string[] args)
#else
    private static int Main()
#endif
    {
        if (!OperatingSystem.IsWindows()) return 1;
        nint window = 0;
        try
        {
#if INCLUDE_VERIFICATION
            if (args.Length != 0)
            {
                if (args.Length != 2 || args[0] != "--verify")
                    throw new ArgumentException("Usage: NativeWindow.exe [--verify output-directory]");
                verifyDirectory = Path.GetFullPath(args[1]);
                Directory.CreateDirectory(verifyDirectory);
            }
#endif
#if INCLUDE_TEXT
            fontPath = SystemFontPath();
            typeface = SKTypeface.FromFile(fontPath);
            foreach (char c in Chinese)
                if (c != ' ' && typeface.GetGlyph(c) == 0)
                    throw new InvalidOperationException($"System font has no glyph for U+{(int)c:X4}.");
            font = new SKFont(typeface, 28);
#endif
            // Keep coordinates in physical pixels; no GDI/DWM scaling of the raster.
            if (SetProcessDpiAwarenessContext((nint)(-4)) == 0)
                throw WindowsError();
            nint instance = GetModuleHandleW(null);
            const string className = "SimdSkiaStandaloneWindow";
            fixed (char* classNamePointer = className)
            {
                WindowClass wc = new()
                {
                    Size = (uint)sizeof(WindowClass), Style = 3,
                    Procedure = &WindowProcedure, Instance = instance,
                    Cursor = LoadCursorW(0, (nint)32512), ClassName = classNamePointer
                };
                if (RegisterClassExW(&wc) == 0) throw WindowsError();
            }
            window = CreateWindowExW(0, className, "SimdSkia - Native AOT / click to change color",
                0x00cf0000, 80, 80, 640, 420, 0, 0, instance, 0);
            if (window == 0) throw WindowsError();
            ShowWindow(window, 5);
            UpdateWindow(window);
            Message message;
            int status;
            while ((status = GetMessageW(&message, 0, 0, 0)) > 0)
            {
                TranslateMessage(&message);
                DispatchMessageW(&message);
            }
            if (status < 0) throw WindowsError();
            if (failure is not null) throw new InvalidOperationException("Window rendering failed.", failure);
#if INCLUDE_VERIFICATION
            if (verifyDirectory is not null && (phase != 3 || verifiedFrames < 3))
                throw new InvalidOperationException("Verification ended before initial, resize and input frames completed.");
#endif
            return 0;
        }
        catch (Exception error)
        {
#if INCLUDE_VERIFICATION
            if (verifyDirectory is not null) File.WriteAllText(Path.Combine(verifyDirectory, "error.txt"), error.ToString());
            else MessageBoxW(0, error.ToString(), "SimdSkia error", 0x10);
#else
            MessageBoxW(0, error.Message, "SimdSkia error", 0x10);
#endif
            return 1;
        }
        finally
        {
            if (window != 0 && IsWindow(window) != 0) DestroyWindow(window);
            canvas?.Dispose(); bitmap?.Dispose(); brush.Dispose();
#if INCLUDE_TEXT
            font?.Dispose(); typeface?.Dispose();
#endif
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        // Exceptions must never escape a reverse P/Invoke callback.
        try
        {
            switch (message)
            {
                case Destroy: PostQuitMessage(failure is null ? 0 : 1); return 0;
                case EraseBackground: return 1; // Every paint copies the entire opaque buffer.
                case Size: InvalidateRect(window, 0, 0); return 0;
                case Click: alternate = !alternate; InvalidateRect(window, 0, 0); return 0;
                case Paint: Render(window); return 0;
#if INCLUDE_VERIFICATION
                case VerifyStep:
                    stepQueued = false;
                    if (phase == 0)
                    {
                        phase = 1;
                        if (SetWindowPos(window, 0, 0, 0, 780, 500, 0x0006) == 0)
                            throw WindowsError();
                    }
                    else if (phase == 1)
                    {
                        phase = 2;
                        PostMessageW(window, Click, 0, 0);
                    }
                    else if (phase == 2)
                    {
                        phase = 3;
                        WriteVerification();
                        PostMessageW(window, Close, 0, 0);
                    }
                    return 0;
#endif
            }
            return DefWindowProcW(window, message, wParam, lParam);
        }
        catch (Exception error)
        {
            failure = error;
            PostQuitMessage(1);
            return 0;
        }
    }

    private static void Render(nint window)
    {
        PaintData paint;
        nint dc = BeginPaint(window, &paint);
        try
        {
            if (dc == 0 || GetClientRect(window, out Rect bounds) == 0)
                throw WindowsError();
            int width = bounds.Right, height = bounds.Bottom;
            if (width <= 0 || height <= 0) return;
            if (bitmap is null || bitmap.Width != width || bitmap.Height != height)
            {
                canvas?.Dispose(); bitmap?.Dispose();
                bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                canvas = new SKCanvas(bitmap);
            }
            canvas!.Clear(Background);
            brush.Color = alternate ? Green : Blue;
            canvas.DrawRect(SKRect.Create(24, 24, 160, 72), brush);
            brush.Color = Green;
            canvas.DrawCircle(240, 60, 36, brush);
            brush.Color = new SKColor(230, 165, 50);
            canvas.DrawRect(SKRect.Create(24, height - 32, Math.Max(1, width - 48), 8), brush);
#if INCLUDE_TEXT
            brush.Color = new SKColor(30, 40, 60);
            canvas.DrawText("SimdSkia / Native AOT", 24, 152, font!, brush);
            canvas.DrawText(Chinese, 24, 202, font!, brush);
#endif
            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            fixed (byte* pixels = bitmap.GetPixelSpan())
            {
                int lines = StretchDIBits(dc, 0, 0, width, height, 0, 0, width, height, pixels, &info, 0, 0x00cc0020);
                if (lines != height) throw new InvalidOperationException($"Presented {lines}/{height} scan lines.");
            }
#if INCLUDE_VERIFICATION
            frames++;
#endif
        }
        finally { EndPaint(window, &paint); }
#if INCLUDE_VERIFICATION
        if (verifyDirectory is not null && !stepQueued)
        {
            nint checkDc = GetDC(window);
            try
            {
                if (checkDc == 0) throw WindowsError();
                CheckPixel(checkDc, 4, 4, Background);
                CheckPixel(checkDc, 48, 48, alternate ? Green : Blue);
                CheckPixel(checkDc, 240, 60, Green);
                if (phase > 0 && (bitmap!.Width <= 640 || bitmap.Height <= 420))
                    throw new InvalidOperationException("Resize did not allocate a larger raster.");
#if INCLUDE_TEXT
                int ink = 0;
                for (int y = 170; y < 205; y++)
                    for (int x = 24; x < 300; x++)
                        if (bitmap!.GetPixel(x, y) != Background) ink++;
                if (ink < 200) throw new InvalidOperationException("Chinese text produced no meaningful ink.");
#endif
                verifiedFrames++;
                WriteBitmap(Path.Combine(verifyDirectory, $"frame-{phase}.bmp"));
                stepQueued = true;
                PostMessageW(window, VerifyStep, 0, 0);
            }
            finally { if (checkDc != 0) ReleaseDC(window, checkDc); }
        }
#endif
    }

#if INCLUDE_VERIFICATION
    private static void CheckPixel(nint dc, int x, int y, SKColor expected)
    {
        uint rgb = (uint)(expected.Red | expected.Green << 8 | expected.Blue << 16);
        if (bitmap!.GetPixel(x, y) != expected || GetPixel(dc, x, y) != rgb)
            throw new InvalidOperationException($"Raster/window pixel mismatch at ({x},{y}).");
    }

    private static void WriteBitmap(string path)
    {
        // Minimal top-down BGRA BMP dump: no image codec is added to this size probe.
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0x4d42); writer.Write(54 + bitmap!.ByteCount); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(bitmap.Width); writer.Write(-bitmap.Height);
        writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(0); writer.Write(bitmap.ByteCount);
        writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        writer.Write(bitmap.GetPixelSpan());
    }

    private static void WriteVerification()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        using var output = File.Create(Path.Combine(verifyDirectory!, "metrics.json"));
        using var json = new Utf8JsonWriter(output, new() { Indented = true });
        json.WriteStartObject();
        json.WriteBoolean("nativeAot", !RuntimeFeature.IsDynamicCodeSupported);
        json.WriteNumber("frames", frames); json.WriteNumber("verifiedFrames", verifiedFrames);
        json.WriteBoolean("resizeVerified", phase == 3);
        json.WriteBoolean("clickVerified", alternate);
        json.WriteNumber("width", bitmap!.Width); json.WriteNumber("height", bitmap.Height);
        json.WriteString("runtime", Environment.Version.ToString());
#if INCLUDE_TEXT
        json.WriteBoolean("includesText", true); json.WriteString("systemFont", fontPath);
#else
        json.WriteBoolean("includesText", false);
#endif
        json.WriteStartArray("loadedModules");
        foreach (ProcessModule module in process.Modules) json.WriteStringValue(module.ModuleName);
        json.WriteEndArray(); json.WriteEndObject();
    }
#endif

    private static Exception WindowsError() =>
        new InvalidOperationException($"Windows API failed (error {Marshal.GetLastPInvokeError()}).");

#if INCLUDE_TEXT
    private static string SystemFontPath()
    {
        // This sample selects the system-installed Microsoft YaHei explicitly;
        // querying all shell special-folder mappings adds unused code to AOT.
        Span<char> path = stackalloc char[260];
        uint length;
        fixed (char* pointer = path) length = GetWindowsDirectoryW(pointer, (uint)path.Length);
        if (length == 0) throw WindowsError();
        if (length >= path.Length)
        {
            path = new char[checked((int)length + 1)];
            fixed (char* pointer = path) length = GetWindowsDirectoryW(pointer, (uint)path.Length);
            if (length == 0) throw WindowsError();
            if (length >= path.Length) throw new IOException("Windows directory length changed during discovery.");
        }
        return new string(path[..(int)length]) + "\\Fonts\\msyh.ttc";
    }
#endif

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public Point Point; public uint Private; }
    [StructLayout(LayoutKind.Sequential)] private struct PaintData { public nint Dc; public int Erase; public Rect Rect; public int Restore, Update; public fixed byte Reserved[32]; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XResolution, YResolution; public uint Colors, Important; }
    [StructLayout(LayoutKind.Sequential)] private struct WindowClass
    {
        public uint Size, Style;
        public delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint> Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public char* MenuName;
        public char* ClassName;
        public nint SmallIcon;
    }

    [LibraryImport("kernel32", StringMarshalling = StringMarshalling.Utf16)] private static partial nint GetModuleHandleW(string? name);
#if INCLUDE_TEXT
    [LibraryImport("kernel32", SetLastError = true)] private static partial uint GetWindowsDirectoryW(char* buffer, uint capacity);
#endif
    [LibraryImport("user32", SetLastError = true)] private static partial ushort RegisterClassExW(WindowClass* value);
    [LibraryImport("user32")] private static partial nint LoadCursorW(nint instance, nint name);
    [LibraryImport("user32", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)] private static partial nint CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [LibraryImport("user32")] private static partial int ShowWindow(nint window, int command);
    [LibraryImport("user32")] private static partial int UpdateWindow(nint window);
    [LibraryImport("user32", SetLastError = true)] private static partial int GetMessageW(Message* message, nint window, uint first, uint last);
    [LibraryImport("user32")] private static partial int TranslateMessage(Message* message);
    [LibraryImport("user32")] private static partial nint DispatchMessageW(Message* message);
    [LibraryImport("user32")] private static partial nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [LibraryImport("user32")] private static partial void PostQuitMessage(int code);
    [LibraryImport("user32")] private static partial int PostMessageW(nint window, uint message, nuint wParam, nint lParam);
    [LibraryImport("user32")] private static partial int InvalidateRect(nint window, nint rect, int erase);
    [LibraryImport("user32")] private static partial nint BeginPaint(nint window, PaintData* paint);
    [LibraryImport("user32")] private static partial int EndPaint(nint window, PaintData* paint);
    [LibraryImport("user32")] private static partial int GetClientRect(nint window, out Rect rect);
    [LibraryImport("user32")] private static partial nint GetDC(nint window);
    [LibraryImport("user32")] private static partial int ReleaseDC(nint window, nint dc);
    [LibraryImport("user32")] private static partial int DestroyWindow(nint window);
    [LibraryImport("user32")] private static partial int IsWindow(nint window);
    [LibraryImport("user32", SetLastError = true)] private static partial int SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [LibraryImport("user32", SetLastError = true)] private static partial int SetProcessDpiAwarenessContext(nint context);
    [LibraryImport("user32", StringMarshalling = StringMarshalling.Utf16)] private static partial int MessageBoxW(nint window, string message, string title, uint flags);
    [LibraryImport("gdi32")] private static partial int StretchDIBits(nint dc, int x, int y, int width, int height, int sx, int sy, int sw, int sh, void* pixels, BitmapInfo* info, uint usage, uint rop);
    [LibraryImport("gdi32")] private static partial uint GetPixel(nint dc, int x, int y);
}
