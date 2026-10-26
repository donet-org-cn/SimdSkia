using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimdSkia.NativeControls;

[SupportedOSPlatform("windows")]
internal static unsafe partial class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public Point Point; public uint Private; }
    [StructLayout(LayoutKind.Sequential)] internal struct PaintData { public nint Dc; public int Erase; public Rect Rect; public int Restore, Update; public fixed byte Reserved[32]; }
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XResolution, YResolution; public uint Colors, Important; }
    [StructLayout(LayoutKind.Sequential)] internal struct WindowClass
    {
        public uint Size, Style;
        public delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint> Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public char* MenuName;
        public char* ClassName;
        public nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct TrackMouse { public uint Size, Flags; public nint Window; public uint HoverTime; }
    [StructLayout(LayoutKind.Sequential)] internal struct MinMax { public Point Reserved, MaximumSize, MaximumPosition, MinimumTrack, MaximumTrack; }
    [StructLayout(LayoutKind.Sequential)] internal struct CompositionForm { public uint Style; public Point Position; public Rect Area; }
    [StructLayout(LayoutKind.Sequential)] internal struct CandidateForm { public uint Index, Style; public Point Position; public Rect Area; }

    [LibraryImport("kernel32", StringMarshalling = StringMarshalling.Utf16)] internal static partial nint GetModuleHandleW(string? name);
    [LibraryImport("kernel32", SetLastError = true)] internal static partial uint GetWindowsDirectoryW(char* buffer, uint capacity);
    [LibraryImport("kernel32", SetLastError = true)] internal static partial nint GlobalAlloc(uint flags, nuint bytes);
    [LibraryImport("kernel32", SetLastError = true)] internal static partial void* GlobalLock(nint handle);
    [LibraryImport("kernel32")] internal static partial int GlobalUnlock(nint handle);
    [LibraryImport("kernel32")] internal static partial nint GlobalFree(nint handle);
    [LibraryImport("kernel32")] internal static partial nuint GlobalSize(nint handle);
    [LibraryImport("user32", SetLastError = true)] internal static partial ushort RegisterClassExW(WindowClass* value);
    [LibraryImport("user32")] internal static partial nint LoadCursorW(nint instance, nint name);
    [LibraryImport("user32")] internal static partial nint SetCursor(nint cursor);
    [LibraryImport("user32")] internal static partial short GetKeyState(int key);
    [LibraryImport("user32", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)] internal static partial nint CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [LibraryImport("user32")] internal static partial int ShowWindow(nint window, int command);
    [LibraryImport("user32")] internal static partial int UpdateWindow(nint window);
    [LibraryImport("user32", SetLastError = true)] internal static partial int GetMessageW(Message* message, nint window, uint first, uint last);
    [LibraryImport("user32")] internal static partial int TranslateMessage(Message* message);
    [LibraryImport("user32")] internal static partial nint DispatchMessageW(Message* message);
    [LibraryImport("user32")] internal static partial nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [LibraryImport("user32")] internal static partial void PostQuitMessage(int code);
    [LibraryImport("user32")] internal static partial int InvalidateRect(nint window, nint rect, int erase);
    [LibraryImport("user32")] internal static partial nint BeginPaint(nint window, PaintData* paint);
    [LibraryImport("user32")] internal static partial int EndPaint(nint window, PaintData* paint);
    [LibraryImport("user32")] internal static partial int GetClientRect(nint window, out Rect rect);
    [LibraryImport("user32")] internal static partial nint SetFocus(nint window);
    [LibraryImport("user32")] internal static partial nint SetCapture(nint window);
    [LibraryImport("user32")] internal static partial int ReleaseCapture();
    [LibraryImport("user32")] internal static partial int TrackMouseEvent(ref TrackMouse tracking);
    [LibraryImport("user32")] internal static partial int DestroyWindow(nint window);
    [LibraryImport("user32")] internal static partial int IsWindow(nint window);
    [LibraryImport("user32", SetLastError = true)] internal static partial int SetProcessDpiAwarenessContext(nint context);
    [LibraryImport("user32", SetLastError = true)] internal static partial nuint SetTimer(nint window, nuint id, uint interval, nint callback);
    [LibraryImport("user32")] internal static partial int KillTimer(nint window, nuint id);
    [LibraryImport("user32", StringMarshalling = StringMarshalling.Utf16)] internal static partial int MessageBoxW(nint window, string message, string title, uint flags);
    [LibraryImport("user32")] internal static partial int OpenClipboard(nint window);
    [LibraryImport("user32")] internal static partial int CloseClipboard();
    [LibraryImport("user32")] internal static partial int EmptyClipboard();
    [LibraryImport("user32")] internal static partial nint GetClipboardData(uint format);
    [LibraryImport("user32")] internal static partial nint SetClipboardData(uint format, nint data);
    [LibraryImport("imm32")] internal static partial nint ImmGetContext(nint window);
    [LibraryImport("imm32")] internal static partial int ImmReleaseContext(nint window, nint context);
    [LibraryImport("imm32")] internal static partial int ImmSetCompositionWindow(nint context, ref CompositionForm form);
    [LibraryImport("imm32")] internal static partial int ImmSetCandidateWindow(nint context, ref CandidateForm form);
    [LibraryImport("gdi32")] internal static partial int StretchDIBits(nint dc, int x, int y, int width, int height, int sx, int sy, int sw, int sh, void* pixels, BitmapInfo* info, uint usage, uint rop);
}
