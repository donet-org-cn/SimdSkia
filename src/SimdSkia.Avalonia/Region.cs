using Avalonia;
using Avalonia.Platform;

namespace SimdSkia.Avalonia;

internal sealed class Region:IPlatformRenderInterfaceRegion
{
    private readonly List<LtrbPixelRect> rects=[];
    private bool disposed;
    private Geometry? geometry;
    private void Check()=>ObjectDisposedException.ThrowIf(disposed,this);
    public bool IsEmpty {get{Check();return rects.Count==0;}}
    private static LtrbPixelRect Rect(int l,int t,int r,int b)=>new(){Left=l,Top=t,Right=r,Bottom=b};
    public LtrbPixelRect Bounds {get{Check();return rects.Count==0?default:Rect(rects.Min(r=>r.Left),rects.Min(r=>r.Top),rects.Max(r=>r.Right),rects.Max(r=>r.Bottom));}}
    public IList<LtrbPixelRect> Rects {get{Check();return rects.AsReadOnly();}}
    internal Geometry Geometry
    {
        get
        {
            Check();if(geometry is not null)return geometry;
            var segments=new List<PathSegment>();foreach(var rect in rects)SimdSkia.PathGeometry.Rectangle(new(rect.Left,rect.Top,rect.Right,rect.Bottom)).AppendTransformedTo(segments,Matrix3.Identity);
            return geometry=new(new SimdSkia.PathGeometry(segments));
        }
    }
    public void AddRect(LtrbPixelRect rect)
    {
        Check();if(rect.Right<=rect.Left||rect.Bottom<=rect.Top)return;var pending=new List<LtrbPixelRect>{rect};
        foreach(var old in rects)
        {
            var next=new List<LtrbPixelRect>();foreach(var p in pending)
            {
                int l=Math.Max(p.Left,old.Left),t=Math.Max(p.Top,old.Top),r=Math.Min(p.Right,old.Right),b=Math.Min(p.Bottom,old.Bottom);
                if(l>=r||t>=b){next.Add(p);continue;}
                if(p.Top<t)next.Add(Rect(p.Left,p.Top,p.Right,t));if(b<p.Bottom)next.Add(Rect(p.Left,b,p.Right,p.Bottom));if(p.Left<l)next.Add(Rect(p.Left,t,l,b));if(r<p.Right)next.Add(Rect(r,t,p.Right,b));
            }
            pending=next;
        }
        rects.AddRange(pending);geometry=null;
    }
    public bool Contains(Point p){Check();return rects.Any(r=>p.X>=r.Left&&p.X<r.Right&&p.Y>=r.Top&&p.Y<r.Bottom);}
    public bool Intersects(LtrbRect r){Check();return rects.Any(p=>p.Left<r.Right&&p.Right>r.Left&&p.Top<r.Bottom&&p.Bottom>r.Top);}
    public void Reset(){Check();rects.Clear();geometry=null;}
    public void Dispose(){disposed=true;rects.Clear();geometry=null;}
}
