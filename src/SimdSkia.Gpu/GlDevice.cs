using System.Runtime.InteropServices;
using System.Text;
namespace SimdSkia.Gpu;

/// <summary>Borrowed current OpenGL/GLES device. Only procedure addresses and
/// actual driver objects are retained; this never creates a native Skia object.</summary>
internal sealed unsafe class GlDevice:IDisposable
{
    private readonly Func<string,nint> get;
    private IDisposable? owner;
    private readonly Dictionary<string,nint> procedures=[];
    private readonly int thread=Environment.CurrentManagedThreadId;
    internal uint Program;
    internal bool Abandoned;
    private bool disposed;
    private readonly List<WeakReference<GlImage>> images=[];
    internal int MaximumSize {get;}
    internal bool Gles {get;}
    private readonly bool coreProfile;
    private readonly bool pixelBuffers;
    private nint currentContextProcedure,currentContext;
    internal string Renderer {get;}
    internal GlDevice(Func<string,nint> get,IDisposable? owner)
    {
        this.get=get;this.owner=owner;
        try
        {
            string version=String(0x1F02);if(version.Length==0)throw new InvalidOperationException("A current GL context is required.");Gles=version.StartsWith("OpenGL ES",StringComparison.Ordinal);Renderer=String(0x1F01);
            string numeric=(Gles?version[10..]:version).Split(' ')[0];if(Version.TryParse(numeric,out var apiVersion))pixelBuffers=apiVersion>=new Version(Gles?3:2,Gles?0:1);
            if(!Gles&&Version.TryParse(version.Split(' ')[0],out var parsed)&&parsed>=new Version(3,2)){int profile=0;((delegate* unmanaged[Stdcall]<uint,int*,void>)P("glGetIntegerv"))(0x9126,&profile);coreProfile=(profile&1)!=0;}
            int maximum=0;((delegate* unmanaged[Stdcall]<uint,int*,void>)P("glGetIntegerv"))(0x0D33,&maximum);if(maximum<=0)throw new InvalidOperationException("Invalid GL maximum texture size.");MaximumSize=Math.Min(maximum,32768);
            const string vertex="attribute vec2 position;uniform vec2 targetSize;uniform float yScale;uniform mat3 inverseMatrix;uniform vec4 imageSource;uniform vec4 imageDestination;uniform vec2 imageSize;uniform int kind;varying vec2 devicePosition;varying vec2 imageCoordinate;void main(){devicePosition=position;vec2 local=(inverseMatrix*vec3(position,1.0)).xy;imageCoordinate=kind==2?(imageSource.xy+(local-imageDestination.xy)*imageSource.zw/imageDestination.zw)/imageSize:vec2(0.0);gl_Position=vec4((position/targetSize*2.0-1.0)*vec2(1.0,yScale),0.0,1.0);}";
            const string fragment="""
                #ifdef GL_ES
                precision highp float;
                precision highp int;
                #endif
                varying vec2 devicePosition;
                varying vec2 imageCoordinate;
                uniform sampler2D maskTexture;uniform sampler2D imageTexture;uniform sampler2D destinationTexture;
                uniform vec4 maskBounds;uniform vec2 targetSize;uniform vec4 color;uniform int kind;uniform int mode;
                uniform mat3 inverseMatrix;uniform vec4 imageSource;uniform vec4 imageDestination;uniform vec2 imageSize;uniform int imageFlip;uniform int targetFlip;uniform int imageStraight;uniform int imageOpaque;
                uniform vec4 gradientLine;uniform vec4 gradientColors[16];uniform float gradientPositions[16];uniform int gradientCount;uniform int tileMode;
                void main(){
                    float coverage=texture2D(maskTexture,(devicePosition-maskBounds.xy)/maskBounds.zw).r;
                    vec4 source=color;vec2 p=(inverseMatrix*vec3(devicePosition,1.0)).xy;
                    if(kind==1){vec2 d=gradientLine.zw-gradientLine.xy;float den=dot(d,d);float t=den>0.0?dot(p-gradientLine.xy,d)/den:1.0;
                        if(tileMode==1)t=fract(t);else if(tileMode==2)t=1.0-abs(mod(t,2.0)-1.0);else if(tileMode==3&&(t<0.0||t>1.0)){coverage=0.0;}else t=clamp(t,0.0,1.0);
                        source=gradientColors[0];for(int i=0;i<15;i++){if(i+1>=gradientCount)break;if(t>=gradientPositions[i]){float span=gradientPositions[i+1]-gradientPositions[i];float f=span>0.0?clamp((t-gradientPositions[i])/span,0.0,1.0):1.0;source=mix(gradientColors[i],gradientColors[i+1],f);}}source.a*=color.a;
                    }else if(kind==2){vec2 uv=imageCoordinate;if(imageFlip!=0)uv.y=1.0-uv.y;source=texture2D(imageTexture,uv);if(imageOpaque!=0)source.a=1.0;else if(imageStraight!=0)source.rgb*=source.a;source*=color.a;}
                    if(kind!=2)source.rgb*=source.a;
                    if(mode==0)gl_FragColor=source*coverage;else{vec2 uv=devicePosition/targetSize;if(targetFlip!=0)uv.y=1.0-uv.y;vec4 destination=texture2D(destinationTexture,uv);gl_FragColor=mix(destination,mode==2?vec4(0.0):source,coverage);}
                }
                """;
            string vertexSource=vertex,fragmentSource=fragment;
            if(coreProfile){vertexSource="#version 150\n"+vertex.Replace("attribute ","in ",StringComparison.Ordinal).Replace("varying ","out ",StringComparison.Ordinal);fragmentSource="#version 150\nout vec4 fragmentColor;\n"+fragment.Replace("varying ","in ",StringComparison.Ordinal).Replace("texture2D(","texture(",StringComparison.Ordinal).Replace("gl_FragColor","fragmentColor",StringComparison.Ordinal);}
            uint vs=Compile(0x8B31,vertexSource),fs=0;
            try
            {
                fs=Compile(0x8B30,fragmentSource);Program=((delegate* unmanaged[Stdcall]<uint>)P("glCreateProgram"))();if(Program==0)throw new InvalidOperationException("GL program allocation failed.");
                ((delegate* unmanaged[Stdcall]<uint,uint,void>)P("glAttachShader"))(Program,vs);((delegate* unmanaged[Stdcall]<uint,uint,void>)P("glAttachShader"))(Program,fs);
                byte[] name="position\0"u8.ToArray();fixed(byte* pointer=name)((delegate* unmanaged[Stdcall]<uint,uint,byte*,void>)P("glBindAttribLocation"))(Program,0,pointer);((delegate* unmanaged[Stdcall]<uint,void>)P("glLinkProgram"))(Program);int success=0;((delegate* unmanaged[Stdcall]<uint,uint,int*,void>)P("glGetProgramiv"))(Program,0x8B82,&success);if(success==0)throw new InvalidOperationException("GL program link failed: "+Log(Program,false));CheckError();
            }
            finally{if(vs!=0)((delegate* unmanaged[Stdcall]<uint,void>)P("glDeleteShader"))(vs);if(fs!=0)((delegate* unmanaged[Stdcall]<uint,void>)P("glDeleteShader"))(fs);}
            foreach(string name in new[]{"eglGetCurrentContext","wglGetCurrentContext","glXGetCurrentContext","CGLGetCurrentContext"}){nint procedure=get(name);if(procedure is 0 or 1 or 2 or 3 or -1)continue;nint value=((delegate* unmanaged[Stdcall]<nint>)procedure)();if(value!=0){currentContextProcedure=procedure;currentContext=value;break;}}
        }
        catch{Dispose();throw;}
    }
    internal nint P(string name)
    {if(procedures.TryGetValue(name,out var address))return address;address=get(name);if(address==0)address=get(name+"EXT");if(address==0)throw new NotSupportedException("Required GL procedure is unavailable: "+name);procedures.Add(name,address);return address;}
    internal nint Optional(string name)=>get(name);
    internal string String(uint name)=>Marshal.PtrToStringAnsi(((delegate* unmanaged[Stdcall]<uint,nint>)P("glGetString"))(name))??"";
    internal void Check()
    {ObjectDisposedException.ThrowIf(disposed,this);if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Use the GL device on its creating thread with its context current.");if(Abandoned)throw new InvalidOperationException("The GL device has been abandoned.");if(currentContextProcedure!=0){if(((delegate* unmanaged[Stdcall]<nint>)currentContextProcedure)()!=currentContext)throw new InvalidOperationException("The creating GL context must be current.");}else if(Program!=0&&((delegate* unmanaged[Stdcall]<uint,byte>)P("glIsProgram"))(Program)==0)throw new InvalidOperationException("The GL program is unavailable in the current context.");}
    internal void Register(GlImage image)
    {if(images.Count>=256)images.RemoveAll(weak=>!weak.TryGetTarget(out var value)||value.IsDisposed);images.Add(new(image));}
    internal void AbandonImages(bool releaseResources)
    {foreach(var weak in images)if(weak.TryGetTarget(out var image))image.ReleaseDeviceResource(releaseResources);images.Clear();}
    internal uint Upload(PixelBuffer source,bool mipmapped=false)
    {
        Check();using var rgba=PixelBuffer.Rent(source.Width,source.Height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);
        for(int y=0;y<source.Height;y++)
        {
            var row=rgba.GetRowSpan(y);var from=source.GetRowSpan(y)[..(source.Width*source.BytesPerPixel)];
            if(source.Format!=PixelFormat.Alpha8&&source.Alpha!=AlphaFormat.Straight)
            {if(source.Format==PixelFormat.Rgba8888)from.CopyTo(row);else SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(from,row,SimdSkia.Cpu.CpuOptions.EnableSimd);if(source.Alpha==AlphaFormat.Opaque)for(int x=0;x<source.Width;x++)row[x*4+3]=255;}
            else for(int x=0;x<source.Width;x++)rgba.WritePixel(x,y,source.ReadPixel(x,y));
        }
        using var pin=rgba.Pin();uint texture=Texture(source.Width,source.Height,0x1908,pin.Address);
        try{if(mipmapped){((delegate* unmanaged[Stdcall]<uint,void>)P("glGenerateMipmap"))(0x0DE1);CheckError();}return texture;}catch{DeleteTexture(ref texture);throw;}
    }
    internal void CheckError(){uint error=((delegate* unmanaged[Stdcall]<uint>)P("glGetError"))();if(error!=0)throw new InvalidOperationException($"OpenGL operation failed: 0x{error:X}.");}
    private uint Compile(uint type,string source)
    {
        uint shader=((delegate* unmanaged[Stdcall]<uint,uint>)P("glCreateShader"))(type);if(shader==0)throw new InvalidOperationException("GL shader allocation failed.");
        try{var bytes=Encoding.UTF8.GetBytes(source);fixed(byte* data=bytes){byte* pointer=data;int length=bytes.Length;((delegate* unmanaged[Stdcall]<uint,int,byte**,int*,void>)P("glShaderSource"))(shader,1,&pointer,&length);}((delegate* unmanaged[Stdcall]<uint,void>)P("glCompileShader"))(shader);int status=0;((delegate* unmanaged[Stdcall]<uint,uint,int*,void>)P("glGetShaderiv"))(shader,0x8B81,&status);if(status==0)throw new InvalidOperationException("GL shader compile failed: "+Log(shader,true));return shader;}
        catch{((delegate* unmanaged[Stdcall]<uint,void>)P("glDeleteShader"))(shader);throw;}
    }
    private string Log(uint value,bool shader)
    {byte* bytes=stackalloc byte[8192];int length=0;((delegate* unmanaged[Stdcall]<uint,int,int*,byte*,void>)P(shader?"glGetShaderInfoLog":"glGetProgramInfoLog"))(value,8192,&length,bytes);return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(bytes,Math.Clamp(length,0,8192)));}
    internal int Uniform(string name)
    {byte[] text=Encoding.UTF8.GetBytes(name+"\0");fixed(byte* p=text)return ((delegate* unmanaged[Stdcall]<uint,byte*,int>)P("glGetUniformLocation"))(Program,p);}
    internal void BindTexture(uint unit,uint texture){((delegate* unmanaged[Stdcall]<uint,void>)P("glActiveTexture"))(0x84C0+unit);((delegate* unmanaged[Stdcall]<uint,uint,void>)P("glBindTexture"))(0x0DE1,texture);}
    internal uint Texture(int width,int height,uint format,nint pixels)
    {
        if(coreProfile&&format==0x1909)format=0x1903;
        uint texture=0;((delegate* unmanaged[Stdcall]<int,uint*,void>)P("glGenTextures"))(1,&texture);if(texture==0)throw new InvalidOperationException("GL texture allocation failed.");
        try{BindTexture(0,texture);PixelStorage(false);Parameter(0x2801,0x2600);Parameter(0x2800,0x2600);Parameter(0x2802,0x812F);Parameter(0x2803,0x812F);((delegate* unmanaged[Stdcall]<uint,int,int,int,int,int,uint,uint,nint,void>)P("glTexImage2D"))(0x0DE1,0,(int)format,width,height,0,format,0x1401,pixels);CheckError();return texture;}catch{DeleteTexture(ref texture);throw;}
    }
    internal void PixelStorage(bool pack)
    {
        var store=(delegate* unmanaged[Stdcall]<uint,int,void>)P("glPixelStorei");store(pack?0x0D05u:0x0CF5u,1);
        if(pixelBuffers){((delegate* unmanaged[Stdcall]<uint,uint,void>)P("glBindBuffer"))(pack?0x88EBu:0x88ECu,0);store(pack?0x0D02u:0x0CF2u,0);store(pack?0x0D03u:0x0CF3u,0);store(pack?0x0D04u:0x0CF4u,0);}
    }
    internal void Parameter(uint name,int value)=>((delegate* unmanaged[Stdcall]<uint,uint,int,void>)P("glTexParameteri"))(0x0DE1,name,value);
    internal void DeleteTexture(ref uint texture){uint value=texture;texture=0;if(value!=0&&!Abandoned)((delegate* unmanaged[Stdcall]<int,uint*,void>)P("glDeleteTextures"))(1,&value);}
    public void Dispose()
    {if(disposed)return;if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose the GL device on its creating thread.");try{AbandonImages(!Abandoned);if(Program!=0&&!Abandoned)((delegate* unmanaged[Stdcall]<uint,void>)P("glDeleteProgram"))(Program);Program=0;}finally{disposed=true;Interlocked.Exchange(ref owner,null)?.Dispose();}}
}
