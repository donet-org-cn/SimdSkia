using System.Runtime.InteropServices;
using System.Text;
namespace SkiaSharp;

/// <summary>Borrowed GL procedure table and immutable capability metadata.
/// This is not a GL drawing context; GRContext.CreateGl remains an API gap.</summary>
public unsafe class GRGlInterface : SKObject
{
    private readonly HashSet<string> extensions;
    private readonly Dictionary<string,IntPtr> procedures;
    private readonly bool complete;
    private readonly GRGlGetProcedureAddressDelegate get;
    private ShaderResources? library;
    internal (Func<string,IntPtr> Lookup,IDisposable? Owner) RetainProcedures()
    {ThrowIfDisposed();return (name=>get(name),library?.Retain());}
    private GRGlInterface(GRGlGetProcedureAddressDelegate get,HashSet<string> extensions,Dictionary<string,IntPtr> procedures,bool complete)
    {this.get=get;this.extensions=extensions;this.procedures=procedures;this.complete=complete;}
    public bool Validate(){ThrowIfDisposed();return complete;}
    public bool HasExtension(string extension){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(extension);return extensions.Contains(extension);}
    public static GRGlInterface Create()=>CreateSystem(false)??CreateAngle();
    public static GRGlInterface CreateAngle()=>OperatingSystem.IsWindows()?CreateSystem(true):null!;
    public static GRGlInterface Create(GRGlGetProcedureAddressDelegate get)=>Assemble(get,0);
    public static GRGlInterface CreateOpenGl(GRGlGetProcedureAddressDelegate get)=>Assemble(get,1);
    public static GRGlInterface CreateGles(GRGlGetProcedureAddressDelegate get)=>Assemble(get,2);
    public static GRGlInterface CreateAngle(GRGlGetProcedureAddressDelegate get)=>CreateGles(get);
    public static GRGlInterface CreateWebGl(GRGlGetProcedureAddressDelegate get)=>Assemble(get,3);
    // The baseline cross-platform .NET assembly compiles Evas support out. Its
    // factory returns null; the Tizen-specific binding is a separate target.
    public static GRGlInterface CreateEvas(IntPtr evas)=>null!;
    private static string? String(IntPtr value)
    {
        if(value==IntPtr.Zero)return null;byte* p=(byte*)value;int length=0;
        while(length<1024*1024&&p[length]!=0)length++;if(length==1024*1024)throw new InvalidOperationException("Unterminated GL capability string.");return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(p,length));
    }
    private static GRGlInterface Assemble(GRGlGetProcedureAddressDelegate get,int requiredStandard)
    {
        if(get is null)return null!;var table=new Dictionary<string,IntPtr>(StringComparer.Ordinal);
        IntPtr Lookup(string name){if(!table.TryGetValue(name,out var value)){value=get(name);table.Add(name,value);}return value;}
        var stringPointer=Lookup("glGetString");if(stringPointer==IntPtr.Zero)return null!;
        var glString=(delegate* unmanaged[Stdcall]<uint,IntPtr>)stringPointer;string? version=String(glString(0x1F02));if(string.IsNullOrEmpty(version))return null!;
        int standard=version.StartsWith("OpenGL ES",StringComparison.Ordinal)?2:version.StartsWith("WebGL",StringComparison.Ordinal)?3:1;
        if(requiredStandard!=0&&standard!=requiredStandard)return null!;
        int index=0;while(index<version.Length&&!char.IsAsciiDigit(version[index]))index++;int major=0,minor=0;
        while(index<version.Length&&char.IsAsciiDigit(version[index]))major=checked(major*10+version[index++]-'0');
        if(index<version.Length&&version[index]=='.'){index++;while(index<version.Length&&char.IsAsciiDigit(version[index]))minor=checked(minor*10+version[index++]-'0');}
        if(standard==3){major=major>=2?3:2;minor=0;}
        if(major<2||major>99)return null!;
        var extensions=new HashSet<string>(StringComparer.Ordinal);var integerPointer=Lookup("glGetIntegerv");var indexedPointer=Lookup("glGetStringi");
        if(major>=3&&integerPointer!=IntPtr.Zero&&indexedPointer!=IntPtr.Zero)
        {
            int count=0;((delegate* unmanaged[Stdcall]<uint,int*,void>)integerPointer)(0x821D,&count);
            if(count<0||count>65536)throw new InvalidOperationException("Invalid GL extension count.");
            var indexed=(delegate* unmanaged[Stdcall]<uint,uint,IntPtr>)indexedPointer;for(uint i=0;i<count;i++)if(String(indexed(0x1F03,i)) is {} ext)extensions.Add(ext);
        }
        else if(String(glString(0x1F03)) is {} all)foreach(var ext in all.Split(' ',StringSplitOptions.RemoveEmptyEntries))extensions.Add(ext);
        bool complete=true;
        void Require(string names,string? suffix=null)
        {
            foreach(var part in names.Split(' ',StringSplitOptions.RemoveEmptyEntries))
            {string name="gl"+part;var pointer=Lookup(name);if(pointer==IntPtr.Zero&&suffix is not null)pointer=Lookup(name+suffix);if(pointer==IntPtr.Zero)complete=false;}
        }
        Require("ActiveTexture AttachShader BindAttribLocation BindBuffer BindTexture BlendColor BlendEquation BlendFunc BufferData BufferSubData Clear ClearColor ColorMask CompileShader CompressedTexImage2D CompressedTexSubImage2D CopyTexSubImage2D CreateProgram CreateShader CullFace DeleteBuffers DeleteProgram DeleteShader DeleteTextures DepthMask Disable DisableVertexAttribArray DrawArrays DrawElements Enable EnableVertexAttribArray Finish Flush FrontFace GenBuffers GenTextures GetAttribLocation GetBufferParameteriv GetError GetIntegerv GetProgramInfoLog GetProgramiv GetShaderInfoLog GetShaderiv GetString GetUniformLocation IsTexture LineWidth LinkProgram PixelStorei ReadPixels Scissor ShaderSource StencilFunc StencilMask StencilOp TexImage2D TexParameteri TexParameteriv TexSubImage2D Uniform1f Uniform1fv Uniform1i Uniform1iv Uniform2f Uniform2fv Uniform2i Uniform2iv Uniform3f Uniform3fv Uniform3i Uniform3iv Uniform4f Uniform4fv Uniform4i Uniform4iv UniformMatrix2fv UniformMatrix3fv UniformMatrix4fv UseProgram VertexAttrib1f VertexAttrib2fv VertexAttrib3fv VertexAttrib4fv VertexAttribPointer Viewport");
        Require("BindFramebuffer BindRenderbuffer CheckFramebufferStatus DeleteFramebuffers DeleteRenderbuffers FramebufferRenderbuffer FramebufferTexture2D GenFramebuffers GenRenderbuffers GetFramebufferAttachmentParameteriv GetRenderbufferParameteriv RenderbufferStorage GenerateMipmap",standard==1?"EXT":null);
        if(standard!=1)Require("GetShaderPrecisionFormat");
        if(major>=3)
        {Require("GetStringi BindVertexArray DeleteVertexArrays GenVertexArrays MapBufferRange UnmapBuffer",standard==2?"OES":null);Require("BlitFramebuffer RenderbufferStorageMultisample",standard==2?"ANGLE":null);}
        if(extensions.Contains("GL_ARB_vertex_array_object")||extensions.Contains("GL_OES_vertex_array_object"))Require("BindVertexArray DeleteVertexArrays GenVertexArrays",standard==2?"OES":null);
        if(extensions.Contains("GL_EXT_discard_framebuffer"))Require("DiscardFramebuffer","EXT");
        if(standard==1&&(major>3||major==3&&minor>=2))Require("FenceSync ClientWaitSync DeleteSync");
        return new(get,extensions,table,complete);
    }
    private sealed class Libraries(params IntPtr[] handles):IDisposable
    {private IntPtr[]? owned=handles;public void Dispose(){var list=Interlocked.Exchange(ref owned,null);if(list is not null)foreach(var handle in list)if(handle!=IntPtr.Zero)NativeLibrary.Free(handle);GC.SuppressFinalize(this);}~Libraries(){Dispose();}}
    private static GRGlInterface CreateSystem(bool angle)
    {
        IntPtr gl=IntPtr.Zero,egl=IntPtr.Zero;
        try
        {
            if(angle)
            {if(!NativeLibrary.TryLoad("libGLESv2.dll",out gl)||!NativeLibrary.TryLoad("libEGL.dll",out egl))return null!;}
            else
            {string name=OperatingSystem.IsWindows()?"opengl32.dll":OperatingSystem.IsMacOS()?"/System/Library/Frameworks/OpenGL.framework/OpenGL":"libGL.so.1";if(!NativeLibrary.TryLoad(name,out gl))return null!;}
            IntPtr glHandle=gl,eglHandle=egl;
            IntPtr Lookup(string name)
            {
                NativeLibrary.TryGetExport(glHandle,name,out var direct);if(direct!=IntPtr.Zero)return direct;
                string proc=angle?"eglGetProcAddress":OperatingSystem.IsWindows()?"wglGetProcAddress":"glXGetProcAddressARB";
                IntPtr source=angle?eglHandle:glHandle;if(!NativeLibrary.TryGetExport(source,proc,out var get))return IntPtr.Zero;
                var text=Encoding.UTF8.GetBytes(name+"\0");fixed(byte* pointer=text){var value=((delegate* unmanaged[Stdcall]<byte*,IntPtr>)get)(pointer);long address=value.ToInt64();return address is 0 or 1 or 2 or 3 or -1?IntPtr.Zero:value;}
            }
            var result=angle?CreateGles(Lookup):CreateOpenGl(Lookup);if(result is null)return null!;
            result.library=new(new Libraries(gl,egl));gl=egl=IntPtr.Zero;return result;
        }
        finally{if(gl!=IntPtr.Zero)NativeLibrary.Free(gl);if(egl!=IntPtr.Zero)NativeLibrary.Free(egl);}
    }
    protected override void Dispose(bool disposing){extensions.Clear();procedures.Clear();library?.Dispose();library=null;}
}
