using System.Buffers.Binary;
using System.Text;

namespace SimdSkia.Text;

internal sealed record InstalledFace(string Path,int Index,string Family,string[] Names,int Weight,int Width,int Slant,long Length,DateTime WriteTime);

// Keep only metadata in the process-wide catalog. Outline/table caches are weakly
// held so repeated FromFamilyName calls do not reload fonts or retain them forever.
internal static class InstalledFonts
{
    private static readonly Lazy<InstalledFace[]> faces=new(Discover);
    private static readonly Dictionary<(string,int),WeakReference<TrueTypeFont>> fonts=[];
    internal static (TrueTypeFont Font,InstalledFace Face) Match(string? family,int weight,int width,int slant)
    {
        var available=faces.Value;if(available.Length==0)throw new FileNotFoundException("No supported installed static TrueType font was found.");
        IEnumerable<InstalledFace> candidates=available.Where(f=>f.Names.Contains(family??"",StringComparer.OrdinalIgnoreCase));
        if(!candidates.Any())
        {
            string[] preferred=OperatingSystem.IsWindows()?["Segoe UI","Microsoft YaHei","Arial"]:["Arial","DejaVu Sans","Liberation Sans","Helvetica"];
            var selected=preferred.Select(p=>available.FirstOrDefault(f=>f.Names.Contains(p,StringComparer.OrdinalIgnoreCase))).FirstOrDefault(f=>f is not null)??available[0];
            candidates=available.Where(f=>f.Family==selected.Family);
        }
        var face=candidates.OrderBy(f=>Math.Abs(f.Width-width)*10000+(f.Slant==slant?0:1000)+Math.Abs(f.Weight-weight)).First();
        lock(fonts)
        {
            var key=(face.Path,face.Index);
            if(!fonts.TryGetValue(key,out var reference)||!reference.TryGetTarget(out var font))
            {
                font=TrueTypeFont.FromInstalledSource(FontSource.OpenInstalled(face.Path,face.Length,face.WriteTime),face.Index);
                if(fonts.Count>=128)foreach(var dead in fonts.Where(p=>!p.Value.TryGetTarget(out _)).Select(p=>p.Key).ToArray())fonts.Remove(dead);
                fonts[key]=new(font);
            }
            return(font,face);
        }
    }
    internal static (string Family,int Weight,int Width,int Slant) Describe(TrueTypeFont font)
    {
        var family=font.TryGetTable("name",out var names)?ReadNames(names.Span).FirstOrDefault()??"Unknown":"Unknown";int weight=400,width=5,slant=0;
        if(font.TryGetTable("OS/2",out var os)&&os.Length>=8){weight=U16(os.Span,4);width=U16(os.Span,6);}
        if(font.TryGetTable("head",out var head)&&head.Length>=46)slant=(U16(head.Span,44)&2)!=0?1:0;
        return(family,weight,width,slant);
    }
    private static ushort U16(ReadOnlySpan<byte> b,int p)=>BinaryPrimitives.ReadUInt16BigEndian(b[p..]);
    private static uint U32(ReadOnlySpan<byte> b,int p)=>BinaryPrimitives.ReadUInt32BigEndian(b[p..]);
    private static string[] ReadNames(ReadOnlySpan<byte> table)
    {
        if(table.Length<6)throw new InvalidDataException("Truncated name table.");int count=U16(table,2),start=U16(table,4);
        if(count>(table.Length-6)/12)throw new InvalidDataException("Invalid name records.");var names=new List<(string Name,int Priority)>();
        for(int i=0;i<count;i++)
        {
            var r=table.Slice(6+i*12,12);int platform=U16(r,0),id=U16(r,6),n=U16(r,8),at=start+U16(r,10);
            if(id is not (1 or 16)||platform is not (0 or 3))continue;
            if(at>table.Length-n||(n&1)!=0)throw new InvalidDataException("Invalid font name string.");
            names.Add((Encoding.BigEndianUnicode.GetString(table.Slice(at,n)),(id==16?4:0)+(U16(r,4)==0x409?2:0)+(platform==3?1:0)));
        }
        return names.OrderByDescending(n=>n.Priority).Select(n=>n.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static InstalledFace[] Discover()
    {
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots=OperatingSystem.IsWindows()?[Environment.GetFolderPath(Environment.SpecialFolder.Fonts),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Fonts")]
            :OperatingSystem.IsMacOS()?["/System/Library/Fonts","/Library/Fonts",Path.Combine(home,"Library","Fonts")]
            :["/usr/share/fonts","/usr/local/share/fonts",Path.Combine(home,".fonts"),Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME")??Path.Combine(home,".local","share"),"fonts")];
        var result=new List<InstalledFace>();int files=0;
        foreach(var root in roots.Distinct())
        {
            if(!Directory.Exists(root))continue;
            foreach(string path in Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint}).Order(StringComparer.Ordinal))
            {
                if(!new[]{".ttf",".ttc",".otf"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))continue;
                if(++files>8192)throw new NotSupportedException("Installed font catalog exceeds 8192 files.");
                try{ReadFile(path,result);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or OverflowException or ArgumentException){/* Unsupported or corrupt system faces cannot be candidates. */}
            }
        }
        return result.ToArray();
    }
    private static void ReadFile(string path,List<InstalledFace> result)
    {
        using var file=File.OpenRead(path);long length=file.Length;var write=File.GetLastWriteTimeUtc(file.SafeFileHandle);
        byte[] Read(long at,int count){if(at<0||count<0||at>length-count)throw new InvalidDataException("Font table outside file.");if(count>4*1024*1024)throw new NotSupportedException("Oversized font metadata.");var b=new byte[count];file.Position=at;file.ReadExactly(b);return b;}
        var header=Read(0,12);int count=U32(header,0)==0x74746366?checked((int)U32(header,8)):1;
        if(count is <1 or >4096)throw new InvalidDataException("Invalid TTC count.");var directory=count==1&&U32(header,0)!=0x74746366?null:Read(12,checked(count*4));
        for(int face=0;face<count;face++)
        {
            uint offset=directory is null?0:U32(directory,face*4);var h=Read(offset,12);if(U32(h,0) is not (0x10000 or 0x74727565))continue;
            var entries=Read(offset+12,U16(h,4)*16);var tables=new Dictionary<string,(uint At,int Size)>();
            for(int i=0;i<entries.Length;i+=16){var e=entries.AsSpan(i,16);tables.Add(Encoding.ASCII.GetString(e[..4]),(U32(e,8),checked((int)U32(e,12))));}
            if(!tables.ContainsKey("glyf")||new[]{"fvar","gvar","COLR","CPAL","CBDT","CBLC","sbix","SVG "}.Any(tables.ContainsKey))continue;
            if(!tables.TryGetValue("name",out var n))continue;var names=ReadNames(Read(n.At,n.Size));if(names.Length==0)continue;
            int weight=400,width=5,slant=0;
            if(tables.TryGetValue("OS/2",out var os)&&os.Size>=8){var b=Read(os.At,8);weight=U16(b,4);width=U16(b,6);}
            if(tables.TryGetValue("head",out var head)&&head.Size>=46)slant=(U16(Read(head.At,46),44)&2)!=0?1:0;
            result.Add(new(path,face,names[0],names,weight,width,slant,length,write));
        }
    }
}
