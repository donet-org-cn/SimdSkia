using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkiaSharp;
using System.Diagnostics;
using SimdSkia.NativeControls;

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
    private static SKTypeface? typeface;
    private static SKFont? font;
    private static string fontPath = "";
    private const string Chinese = "独立窗口 中文绘制";
    private static Exception? failure;

    [STAThread]
    private static int Main()
    {
        if (!OperatingSystem.IsWindows()) return 1;
        nint window = 0;
        try
        {
            fontPath = SystemFontPath();
            typeface = SKTypeface.FromFile(fontPath);
            foreach (char c in Chinese)
                if (c != ' ' && typeface.GetGlyph(c) == 0)
                    throw new InvalidOperationException($"System font has no glyph for U+{(int)c:X4}.");
            font = new SKFont(typeface, 28);
            // Keep coordinates in physical pixels; no GDI/DWM scaling of the raster.
            if (SetProcessDpiAwarenessContext((nint)(-4)) == 0)
                throw WindowsError();
            nint instance = GetModuleHandleW(null);
            const string className = "SimdSkiaGlowControls";
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
            window = CreateWindowExW(0, className, "SimdSkia - Glow Controls / Native AOT",
                0x00cf0000, 80, 80, 760, 540, 0, 0, instance, 0);
            if (window == 0) throw WindowsError();
            if (SetTimer(window, 1, 30, 0) == 0) throw WindowsError();
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
            return 0;
        }
        catch (Exception error)
        {
            MessageBoxW(0, error.Message, "SimdSkia error", 0x10);
            return 1;
        }
        finally
        {
            if (window != 0 && IsWindow(window) != 0) DestroyWindow(window);
            canvas?.Dispose(); bitmap?.Dispose(); brush.Dispose();
            font?.Dispose(); typeface?.Dispose();
            uiFont?.Dispose(); smallFont?.Dispose();
            progressGradient?.Dispose();
            foreach(var path in paths.Values)path.Dispose();
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
                case 0x24:
                    var sizes=(Native.MinMax*)lParam;sizes->MinimumTrack=new Native.Point{X=600,Y=510};return 0;
                case Destroy: KillTimer(window, 1); PostQuitMessage(failure is null ? 0 : 1); return 0;
                case EraseBackground: return 1; // Every paint copies the entire opaque buffer.
                case Size:
                    if(wParam==1)KillTimer(window,1);
                    else {lastTick=Stopwatch.GetTimestamp();if(SetTimer(window,1,30,0)==0)throw WindowsError();}
                    InvalidateRect(window, 0, 0); return 0;
                case Click:
                    SetFocus(window);
                    int x=(short)(long)lParam, y=(short)((long)lParam>>16);
                    focus = Hit(x,y);
                    if(focus==0) {
                        float scroll=Math.Max(0,uiFont!.MeasureText(editor.Text[..editor.Caret])-(bitmap!.Width-104));
                        int index=0;float best=float.MaxValue;
                        for(int i=0;i<=editor.Text.Length;i++){
                            if(i<editor.Text.Length&&char.IsLowSurrogate(editor.Text[i]))continue;
                            float distance=Math.Abs(48-scroll+uiFont.MeasureText(editor.Text[..i])-x);
                            if(distance<best){best=distance;index=i;}
                        }
                        editor.MoveTo(index,(GetKeyState(0x10)&0x8000)!=0);caretEpoch=Stopwatch.GetTimestamp();
                        PositionIme(window);
                    }
                    if(focus>0){pressed=focus;Native.SetCapture(window);}
                    InvalidateRect(window,0,0); return 0;
                case 0x200:
                    hoverX=(short)(long)lParam;hoverY=(short)((long)lParam>>16);
                    var tracking=new Native.TrackMouse{Size=(uint)sizeof(Native.TrackMouse),Flags=2,Window=window};
                    Native.TrackMouseEvent(ref tracking);return 0;
                case 0x2a3: hoverX=-1;hoverY=-1;return 0;
                case 0x202:
                    int hit=Hit((short)(long)lParam,(short)((long)lParam>>16));
                    if(pressed>0&&hit==pressed)Activate(pressed);
                    pressed=-1;Native.ReleaseCapture();InvalidateRect(window,0,0);return 0;
                case 0x215: pressed=-1;return 0;
                case 0x8: focus=-1;pressed=-1;return 0;
                case 0x10d: PositionIme(window);break;
                case 0x100:
                    bool shift=(GetKeyState(0x10)&0x8000)!=0;
                    if(wParam==9) focus=(focus+(shift?2:1)+3)%3;
                    else if(focus==0) {
                        if(wParam==37)editor.Left(shift);else if(wParam==39)editor.Right(shift);
                        else if(wParam==36)editor.MoveTo(0,shift);else if(wParam==35)editor.MoveTo(editor.Text.Length,shift);
                        else if(wParam==46)editor.Delete(false);
                    } else if((wParam==32||wParam==13)&&((long)lParam&(1L<<30))==0) { Activate(focus); }
                    caretEpoch=Stopwatch.GetTimestamp();InvalidateRect(window,0,0);return 0;
                case 0x102:
                    if(focus==0) {
                        if(wParam==1)editor.SelectAll();
                        else if(wParam==8)editor.Delete(true);
                        else if(wParam==3||wParam==24){if(Copy(window)&&wParam==24)editor.Delete(false);}
                        else if(wParam==22)Paste(window);
                        else if(wParam>=32 && wParam!=127) editor.Insert(((char)wParam).ToString(),cp=>typeface!.GetGlyph(cp)!=0,out inputError);
                        PositionIme(window);
                        caretEpoch=Stopwatch.GetTimestamp();InvalidateRect(window,0,0);
                    }
                    return 0;
                case 0x113:
                    long now=Stopwatch.GetTimestamp();double dt=(now-lastTick)/(double)Stopwatch.Frequency;lastTick=now;
                    animationTime+=dt;if(running)progressTime+=dt;
                    InvalidateRect(window,0,0);return 0;
                case Paint: Render(window); return 0;
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
            DrawControls(width,height);
            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            fixed (byte* pixels = bitmap.GetPixelSpan())
            {
                int lines = StretchDIBits(dc, 0, 0, width, height, 0, 0, width, height, pixels, &info, 0, 0x00cc0020);
                if (lines != height) throw new InvalidOperationException($"Presented {lines}/{height} scan lines.");
            }
        }
        finally { EndPaint(window, &paint); }
    }



    private static readonly LineEditor editor = new();
    private static readonly Dictionary<(float,float,float,float,float),SKPath> paths = [];
    private static SKFont? uiFont, smallFont;
    private static SKShader? progressGradient;
    private static int gradientWidth;
    private static int focus=0,hoverX=-1,hoverY=-1,pressed=-1;
    private static bool running=true;
    private static double progressTime,animationTime;
    private static long lastTick=Stopwatch.GetTimestamp(),caretEpoch=Stopwatch.GetTimestamp();
    private static string inputError="";
    private static int Hit(int x,int y)=>x>=36&&x<=bitmap!.Width-36&&y>=132&&y<=188?0:
        y>=350&&y<=398?(x>=36&&x<=232?1:x>=248&&x<=408?2:-1):-1;
    private static void Activate(int id){if(id==1)running=!running;if(id==2){progressTime=0;running=true;}}
    private static void PositionIme(nint window)
    {
        nint context=Native.ImmGetContext(window);if(context==0)return;
        try{
            float advance=uiFont?.MeasureText(editor.Text[..editor.Caret])??0;
            var position=new Native.Point{X=(int)Math.Min(bitmap!.Width-48,48+advance),Y=188};
            var form=new Native.CompositionForm{Style=2,Position=position};Native.ImmSetCompositionWindow(context,ref form);
            var candidate=new Native.CandidateForm{Style=0x40,Position=position};Native.ImmSetCandidateWindow(context,ref candidate);
        }finally{Native.ImmReleaseContext(window,context);}
    }
    private static bool Copy(nint window)
    {
        string text=editor.Selected;if(text.Length==0)return false;
        if(Native.OpenClipboard(window)==0){inputError="剪贴板正在使用，请重试";return false;}
        nint data=0;
        try{
            data=Native.GlobalAlloc(2,(nuint)((text.Length+1)*2));if(data==0)throw WindowsError();
            char* pointer=(char*)Native.GlobalLock(data);if(pointer==null)throw WindowsError();
            try{text.AsSpan().CopyTo(new Span<char>(pointer,text.Length));pointer[text.Length]='\0';}finally{Native.GlobalUnlock(data);}
            if(Native.EmptyClipboard()==0||Native.SetClipboardData(13,data)==0)throw WindowsError();
            data=0;inputError="";return true;
        }finally{if(data!=0)Native.GlobalFree(data);Native.CloseClipboard();}
    }
    private static void Paste(nint window)
    {
        if(Native.OpenClipboard(window)==0){inputError="剪贴板正在使用，请重试";return;}
        try{
            nint data=Native.GetClipboardData(13);if(data==0)return;
            char* pointer=(char*)Native.GlobalLock(data);if(pointer==null)throw WindowsError();
            try{
                int available=(int)Math.Min((nuint)(LineEditor.Limit+2),Native.GlobalSize(data)/2),length=0;
                while(length<available&&pointer[length]!='\0')length++;
                if(length>LineEditor.Limit){inputError="最多输入 128 个 UTF-16 字符";return;}
                editor.Insert(new string(pointer,0,length),cp=>typeface!.GetGlyph(cp)!=0,out inputError);
            }finally{Native.GlobalUnlock(data);}
        }finally{Native.CloseClipboard();}
    }
    private static void Color(SKColor color) { brush.Shader=null;brush.Style=SKPaintStyle.Fill;brush.Color=color; }
    private static SKPath Rounded(float x,float y,float w,float h,float r)
    {
        var key=(x,y,w,h,r);
        if(paths.TryGetValue(key,out var path))return path;
        if(paths.Count>128){foreach(var old in paths.Values)old.Dispose();paths.Clear();}
        path=new SKPath();float k=.55228475f*r;
        path.MoveTo(x+r,y);path.LineTo(x+w-r,y);path.CubicTo(x+w-r+k,y,x+w,y+r-k,x+w,y+r);
        path.LineTo(x+w,y+h-r);path.CubicTo(x+w,y+h-r+k,x+w-r+k,y+h,x+w-r,y+h);
        path.LineTo(x+r,y+h);path.CubicTo(x+r-k,y+h,x,y+h-r+k,x,y+h-r);
        path.LineTo(x,y+r);path.CubicTo(x,y+r-k,x+r-k,y,x+r,y);path.Close();paths[key]=path;return path;
    }
    private static void Glow(float x,float y,float w,float h,SKColor color,float strength)
    {
        for(int i=8;i>=1;i--) {
            float d=i*1.4f;Color(new SKColor(color.Red,color.Green,color.Blue,(byte)((10-i)*strength)));
            canvas!.DrawPath(Rounded(x-d,y-d,w+2*d,h+2*d,12+d),brush);
        }
        Color(color);canvas!.DrawPath(Rounded(x,y,w,h,12),brush);
        Color(new SKColor(16,25,43));canvas.DrawPath(Rounded(x+1,y+1,w-2,h-2,11),brush);
    }
    private static void Text(string text,float x,float y,SKFont f,SKColor color)
    {Color(color);canvas!.DrawText(text,x,y,f,brush);}
    private static void DrawControls(int width,int height)
    {
        uiFont??=new SKFont(typeface!,22);smallFont??=new SKFont(typeface!,14);
        var cyan=new SKColor(66,220,239);var muted=new SKColor(132,151,180);var white=new SKColor(229,239,255);
        canvas!.Clear(new SKColor(10,15,27));
        Text("SIMDSKIA  /  GLOW LAB",36,45,font!,white);
        Text("纯 C# 自绘控件 · 系统字体 · Native AOT",36,77,smallFont,muted);
        Text("输入内容",36,116,smallFont,muted);
        float fw=Math.Max(200,width-72), pulse=(float)(.8+.2*Math.Sin(animationTime*2));
        Glow(36,132,fw,56,focus==0?cyan:new SKColor(52,74,105),focus==0?pulse:0.15f);
        int saved=canvas.Save();canvas.ClipRect(new SKRect(48,139,width-48,183));
        float caretWidth=uiFont.MeasureText(editor.Text[..editor.Caret]);
        float scroll=Math.Max(0,caretWidth-(fw-32));float origin=48-scroll;
        if(focus==0&&editor.Start!=editor.End){
            float left=origin+uiFont.MeasureText(editor.Text[..editor.Start]);float right=origin+uiFont.MeasureText(editor.Text[..editor.End]);
            Color(new SKColor(35,87,121));canvas.DrawRect(new SKRect(left,145,right,175),brush);
        }
        Text(editor.Text,origin,169,uiFont,white);
        if(focus==0&&(Stopwatch.GetTimestamp()-caretEpoch)/(double)Stopwatch.Frequency%1<.5){Color(cyan);canvas.DrawRect(SKRect.Create(origin+caretWidth,145,2,29),brush);}
        canvas.RestoreToCount(saved);
        Text(inputError.Length>0?inputError:"支持中英文 · Ctrl+A / C / V · Tab 切换焦点",36,213,smallFont,inputError.Length>0?new SKColor(255,135,150):muted);
        float progress=(float)Math.Min(1,(progressTime%12)/10);
        Text("流光进度",36,253,uiFont,white);Text($"{(int)(progress*100)}%",width-100,253,uiFont,cyan);
        Glow(36,270,fw,22,cyan,.5f);
        saved=canvas.Save();float edge=38+(fw-4)*progress;
        canvas.ClipRect(new SKRect(38,272,edge,290));
        if(progressGradient is null||gradientWidth!=width){
            progressGradient?.Dispose();gradientWidth=width;
            progressGradient=SKShader.CreateLinearGradient(new(36,0),new(width-36,0),[new SKColor(43,105,236),cyan],SKShaderTileMode.Clamp);
        }
        Color(SKColors.White);brush.Shader=progressGradient;canvas.DrawPath(Rounded(38,272,fw-4,18,9),brush);brush.Shader=null;
        float shine=48+(float)(progressTime*.35%1)*Math.Max(1,edge-60);
        for(int i=-8;i<=8;i++){Color(new SKColor(235,255,255,(byte)((9-Math.Abs(i))*12)));canvas.DrawRect(SKRect.Create(shine+i*2,274,2,14),brush);}
        canvas.RestoreToCount(saved);
        Text(running?"正在运行 · 每 12 秒循环":"动画已暂停",36,319,smallFont,muted);
        Button(36,350,196,48,1,running?"暂停动画":"继续动画",cyan);
        Button(248,350,160,48,2,"重新开始",new SKColor(166,127,255));
        Text("悬停与焦点发光  /  点击按钮控制动画",36,height-24,smallFont,muted);
    }
    private static void Button(float x,float y,float w,float h,int id,string label,SKColor color)
    {
        bool hover=hoverX>=x&&hoverX<x+w&&hoverY>=y&&hoverY<y+h;
        Glow(x,y,w,h,color,hover||focus==id?1.5f:.35f);
        if(hover||pressed==id){Color(new SKColor(color.Red,color.Green,color.Blue,(byte)(pressed==id?95:40)));canvas!.DrawPath(Rounded(x+2,y+2,w-4,h-4,10),brush);}
        Text(label,x+(w-uiFont!.MeasureText(label))/2,y+31,uiFont,new SKColor(230,241,255));
    }
    [LibraryImport("user32")] private static partial nint SetFocus(nint window);
    [LibraryImport("user32")] private static partial short GetKeyState(int key);
    [LibraryImport("user32",SetLastError=true)] private static partial nuint SetTimer(nint window,nuint id,uint interval,nint callback);
    [LibraryImport("user32")] private static partial int KillTimer(nint window,nuint id);

    private static Exception WindowsError() =>
        new InvalidOperationException($"Windows API failed (error {Marshal.GetLastPInvokeError()}).");

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
    [LibraryImport("kernel32", SetLastError = true)] private static partial uint GetWindowsDirectoryW(char* buffer, uint capacity);
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
