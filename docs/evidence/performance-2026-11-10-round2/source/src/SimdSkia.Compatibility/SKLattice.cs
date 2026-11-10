namespace SkiaSharp;

public struct SKLattice : IEquatable<SKLattice>
{
    public int[]? XDivs {readonly get;set;}
    public int[]? YDivs {readonly get;set;}
    public SKLatticeRectType[]? RectTypes {readonly get;set;}
    public SKRectI? Bounds {readonly get;set;}
    public SKColor[]? Colors {readonly get;set;}
    // Array-valued fields use reference equality, as in the baseline value type.
    public readonly bool Equals(SKLattice obj)=>ReferenceEquals(XDivs,obj.XDivs)&&ReferenceEquals(YDivs,obj.YDivs)&&ReferenceEquals(RectTypes,obj.RectTypes)&&Bounds==obj.Bounds&&ReferenceEquals(Colors,obj.Colors);
    public readonly override bool Equals(object? obj)=>obj is SKLattice other&&Equals(other);
    public readonly override int GetHashCode()=>HashCode.Combine(XDivs,YDivs,RectTypes,Bounds,Colors);
    public static bool operator ==(SKLattice left,SKLattice right)=>left.Equals(right);
    public static bool operator !=(SKLattice left,SKLattice right)=>!left.Equals(right);
}
