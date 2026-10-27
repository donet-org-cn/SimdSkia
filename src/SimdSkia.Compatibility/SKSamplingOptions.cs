// API value definitions from SkiaSharp v3.119.4 (MIT). See docs/licenses/SkiaSharp-MIT.txt.

#nullable disable

namespace SkiaSharp;

public enum SKFilterMode {
		// NEAREST_SK_FILTER_MODE = 0
		Nearest = 0,
		// LINEAR_SK_FILTER_MODE = 1
		Linear = 1,
	}

public enum SKMipmapMode {
		// NONE_SK_MIPMAP_MODE = 0
		None = 0,
		// NEAREST_SK_MIPMAP_MODE = 1
		Nearest = 1,
		// LINEAR_SK_MIPMAP_MODE = 2
		Linear = 2,
	}

public readonly partial struct SKCubicResampler : IEquatable<SKCubicResampler> {
		// public float fB
		private readonly Single fB;
		public readonly Single B => fB;

		// public float fC
		private readonly Single fC;
		public readonly Single C => fC;

		public readonly bool Equals (SKCubicResampler obj) =>
#pragma warning disable CS8909
			fB == obj.fB && fC == obj.fC;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKCubicResampler f && Equals (f);

		public static bool operator == (SKCubicResampler left, SKCubicResampler right) =>
			left.Equals (right);

		public static bool operator != (SKCubicResampler left, SKCubicResampler right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fB);
			hash.Add (fC);
			return hash.ToHashCode ();
		}

	}

public partial struct SKCubicResampler
	{
		public static readonly SKCubicResampler Mitchell = new (1 / 3.0f, 1 / 3.0f);

		public static readonly SKCubicResampler CatmullRom = new (0.0f, 1 / 2.0f);

		public SKCubicResampler (float b, float c)
		{
			fB = b;
			fC = c;
		}
	}

public readonly partial struct SKSamplingOptions : IEquatable<SKSamplingOptions> {
		// public int fMaxAniso
		private readonly Int32 fMaxAniso;
		public readonly Int32 MaxAniso => fMaxAniso;

		// public bool fUseCubic
		private readonly Byte fUseCubic;
		public readonly bool UseCubic => fUseCubic > 0;

		// public sk_cubic_resampler_t fCubic
		private readonly SKCubicResampler fCubic;
		public readonly SKCubicResampler Cubic => fCubic;

		// public sk_filter_mode_t fFilter
		private readonly SKFilterMode fFilter;
		public readonly SKFilterMode Filter => fFilter;

		// public sk_mipmap_mode_t fMipmap
		private readonly SKMipmapMode fMipmap;
		public readonly SKMipmapMode Mipmap => fMipmap;

		public readonly bool Equals (SKSamplingOptions obj) =>
#pragma warning disable CS8909
			fMaxAniso == obj.fMaxAniso && fUseCubic == obj.fUseCubic && fCubic == obj.fCubic && fFilter == obj.fFilter && fMipmap == obj.fMipmap;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKSamplingOptions f && Equals (f);

		public static bool operator == (SKSamplingOptions left, SKSamplingOptions right) =>
			left.Equals (right);

		public static bool operator != (SKSamplingOptions left, SKSamplingOptions right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fMaxAniso);
			hash.Add (fUseCubic);
			hash.Add (fCubic);
			hash.Add (fFilter);
			hash.Add (fMipmap);
			return hash.ToHashCode ();
		}

	}

public partial struct SKSamplingOptions
	{
		public static readonly SKSamplingOptions Default = new ();

		public SKSamplingOptions (SKFilterMode filter, SKMipmapMode mipmap)
		{
			fUseCubic = default;
			fCubic = default;
			fMaxAniso = default;

			fFilter = filter;
			fMipmap = mipmap;
		}

		public SKSamplingOptions (SKFilterMode filter)
		{
			fUseCubic = default;
			fCubic = default;
			fMaxAniso = default;

			fFilter = filter;
			fMipmap = SKMipmapMode.None;
		}

		public SKSamplingOptions (SKCubicResampler resampler)
		{
			fMaxAniso = default;
			fFilter = default;
			fMipmap = default;

			fUseCubic = 1;
			fCubic = resampler;
		}

		public SKSamplingOptions (int maxAniso)
		{
			fUseCubic = default;
			fCubic = default;
			fFilter = default;
			fMipmap = default;

			fMaxAniso = Math.Max (1, maxAniso);
		}

		public bool IsAniso => MaxAniso != 0;
	}