// Public contracts from SkiaSharp v3.119.4 (MIT); C# owned arrays replace native pointer storage.
using SimdSkia;
#nullable disable

using System;

namespace SkiaSharp
{
	// Base

    public class SKRunBuffer
    {
        internal readonly ushort[] GlyphArray;
        internal readonly Array PositionArray;
        internal readonly byte[] TextArray;
        internal readonly uint[] ClusterArray;
        internal readonly FontSnapshot Font;
        internal readonly SKRect? ExplicitBounds;
        internal readonly int Kind;
        internal readonly SKPoint Origin;
        internal readonly bool HasText;
        internal bool Valid=true;
        internal SKRunBuffer(FontSnapshot font,int count,int kind,SKPoint origin,int textSize,bool hasText,SKRect? bounds)
        {
            Font=font;Kind=kind;Origin=origin;ExplicitBounds=bounds;HasText=hasText;GlyphArray=new ushort[count];
            PositionArray=kind switch {1=>new float[count],2=>new SKPoint[count],3=>new SKRotationScaleMatrix[count],_=>Array.Empty<float>()};
            TextArray=hasText?new byte[textSize]:[];ClusterArray=hasText?new uint[count]:[];
        }
        internal void Validate()=>ObjectDisposedException.ThrowIf(!Valid,this);
        public int Size {get{Validate();return GlyphArray.Length;}}
        public Span<ushort> Glyphs {get{Validate();return GlyphArray;}}
        public void SetGlyphs(ReadOnlySpan<ushort> glyphs)=>glyphs.CopyTo(Glyphs);
        [Obsolete("Use Glyphs instead.")]
        public Span<ushort> GetGlyphSpan()=>Glyphs;
    }

	public sealed class SKHorizontalRunBuffer : SKRunBuffer
	{
        internal SKHorizontalRunBuffer(FontSnapshot font,int count,SKPoint origin,int textSize,SKRect? bounds):base(font,count,1,origin,textSize,false,bounds){}



		public Span<float> Positions {get{Validate();return (float[])PositionArray;}}

		public void SetPositions (ReadOnlySpan<float> positions) => positions.CopyTo (Positions);

		[Obsolete ("Use Positions instead.")]
		public Span<float> GetPositionSpan () => Positions;
	}

	public sealed class SKPositionedRunBuffer : SKRunBuffer
	{
        internal SKPositionedRunBuffer(FontSnapshot font,int count,SKPoint origin,int textSize,SKRect? bounds):base(font,count,2,origin,textSize,false,bounds){}



		public Span<SKPoint> Positions {get{Validate();return (SKPoint[])PositionArray;}}

		public void SetPositions (ReadOnlySpan<SKPoint> positions) => positions.CopyTo (Positions);

		[Obsolete ("Use Positions instead.")]
		public Span<SKPoint> GetPositionSpan () => Positions;
	}

	public sealed class SKRotationScaleRunBuffer : SKRunBuffer
	{
        internal SKRotationScaleRunBuffer(FontSnapshot font,int count,SKPoint origin,int textSize,SKRect? bounds):base(font,count,3,origin,textSize,false,bounds){}



		public Span<SKRotationScaleMatrix> Positions {get{Validate();return (SKRotationScaleMatrix[])PositionArray;}}

		public void SetPositions (ReadOnlySpan<SKRotationScaleMatrix> positions) => positions.CopyTo (Positions);

		[Obsolete ("Use Positions instead.")]
		public Span<SKRotationScaleMatrix> GetRotationScaleSpan () => Positions;

		[Obsolete ("Use SetPositions instead.")]
		public void SetRotationScale (ReadOnlySpan<SKRotationScaleMatrix> positions) => SetPositions (positions);
	}

	// Text

	public class SKTextRunBuffer : SKRunBuffer
	{
        internal SKTextRunBuffer(FontSnapshot font,int count,int kind,SKPoint origin,int textSize,bool hasText,SKRect? bounds):base(font,count,kind,origin,textSize,hasText,bounds){}
        internal SKTextRunBuffer(FontSnapshot font,int count,SKPoint origin,int textSize,SKRect? bounds):this(font,count,0,origin,textSize,true,bounds){}



		public int TextSize {get{Validate();return TextArray.Length;}}

		public Span<byte> Text {get{Validate();return TextArray;}}

		public Span<uint> Clusters {get{Validate();return ClusterArray;}}

		public void SetText (ReadOnlySpan<byte> text) => text.CopyTo (Text);

		public void SetClusters (ReadOnlySpan<uint> clusters) => clusters.CopyTo (Clusters);
	}

	public sealed class SKHorizontalTextRunBuffer : SKTextRunBuffer
	{
        internal SKHorizontalTextRunBuffer(FontSnapshot font,int count,SKPoint origin,int textSize,SKRect? bounds):base(font,count,1,origin,textSize,true,bounds){}



		public Span<float> Positions {get{Validate();return (float[])PositionArray;}}

		public void SetPositions (ReadOnlySpan<float> positions) => positions.CopyTo (Positions);
	}

	public sealed class SKPositionedTextRunBuffer : SKTextRunBuffer
	{
        internal SKPositionedTextRunBuffer(FontSnapshot font,int count,SKPoint origin,int textSize,SKRect? bounds):base(font,count,2,origin,textSize,true,bounds){}



		public Span<SKPoint> Positions {get{Validate();return (SKPoint[])PositionArray;}}

		public void SetPositions (ReadOnlySpan<SKPoint> positions) => positions.CopyTo (Positions);
	}

	public sealed class SKRotationScaleTextRunBuffer : SKTextRunBuffer
	{
        internal SKRotationScaleTextRunBuffer(FontSnapshot font,int count,SKPoint origin,int textSize,SKRect? bounds):base(font,count,3,origin,textSize,true,bounds){}



		public Span<SKRotationScaleMatrix> Positions {get{Validate();return (SKRotationScaleMatrix[])PositionArray;}}

		public void SetPositions (ReadOnlySpan<SKRotationScaleMatrix> positions) => positions.CopyTo (Positions);
	}


}
