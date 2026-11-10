// Public value contracts from SkiaSharp 3.119.4 (MIT).
using System.Runtime.InteropServices;
namespace SkiaSharp;
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe partial struct SKJpegEncoderOptions : IEquatable<SKJpegEncoderOptions> {
		// public int fQuality
		private readonly Int32 fQuality;

		// public sk_jpegencoder_downsample_t fDownsample
		private readonly SKJpegEncoderDownsample fDownsample;

		// public sk_jpegencoder_alphaoption_t fAlphaOption
		private readonly SKJpegEncoderAlphaOption fAlphaOption;

		// public const sk_data_t* xmpMetadata
		private readonly IntPtr xmpMetadata;

		// public const sk_colorspace_icc_profile_t* fICCProfile
		private readonly IntPtr fICCProfile;

		// public const char* fICCProfileDescription
		private readonly IntPtr fICCProfileDescription;

		public readonly bool Equals (SKJpegEncoderOptions obj) =>
#pragma warning disable CS8909
			fQuality == obj.fQuality && fDownsample == obj.fDownsample && fAlphaOption == obj.fAlphaOption && xmpMetadata == obj.xmpMetadata && fICCProfile == obj.fICCProfile && fICCProfileDescription == obj.fICCProfileDescription;
#pragma warning restore CS8909

		public readonly override bool Equals (object? obj) =>
			obj is SKJpegEncoderOptions f && Equals (f);

		public static bool operator == (SKJpegEncoderOptions left, SKJpegEncoderOptions right) =>
			left.Equals (right);

		public static bool operator != (SKJpegEncoderOptions left, SKJpegEncoderOptions right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fQuality);
			hash.Add (fDownsample);
			hash.Add (fAlphaOption);
			hash.Add (xmpMetadata);
			hash.Add (fICCProfile);
			hash.Add (fICCProfileDescription);
			return hash.ToHashCode ();
		}

	}
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe partial struct SKPngEncoderOptions : IEquatable<SKPngEncoderOptions> {
		// public sk_pngencoder_filterflags_t fFilterFlags
		private readonly SKPngEncoderFilterFlags fFilterFlags;

		// public int fZLibLevel
		private readonly Int32 fZLibLevel;

		// public void* fComments
		private readonly IntPtr fComments;

		// public const sk_colorspace_icc_profile_t* fICCProfile
		private readonly IntPtr fICCProfile;

		// public const char* fICCProfileDescription
		private readonly IntPtr fICCProfileDescription;

		public readonly bool Equals (SKPngEncoderOptions obj) =>
#pragma warning disable CS8909
			fFilterFlags == obj.fFilterFlags && fZLibLevel == obj.fZLibLevel && fComments == obj.fComments && fICCProfile == obj.fICCProfile && fICCProfileDescription == obj.fICCProfileDescription;
#pragma warning restore CS8909

		public readonly override bool Equals (object? obj) =>
			obj is SKPngEncoderOptions f && Equals (f);

		public static bool operator == (SKPngEncoderOptions left, SKPngEncoderOptions right) =>
			left.Equals (right);

		public static bool operator != (SKPngEncoderOptions left, SKPngEncoderOptions right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fFilterFlags);
			hash.Add (fZLibLevel);
			hash.Add (fComments);
			hash.Add (fICCProfile);
			hash.Add (fICCProfileDescription);
			return hash.ToHashCode ();
		}

	}
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe partial struct SKWebpEncoderOptions : IEquatable<SKWebpEncoderOptions> {
		// public sk_webpencoder_compression_t fCompression
		private readonly SKWebpEncoderCompression fCompression;

		// public float fQuality
		private readonly Single fQuality;

		// public const sk_colorspace_icc_profile_t* fICCProfile
		private readonly IntPtr fICCProfile;

		// public const char* fICCProfileDescription
		private readonly IntPtr fICCProfileDescription;

		public readonly bool Equals (SKWebpEncoderOptions obj) =>
#pragma warning disable CS8909
			fCompression == obj.fCompression && fQuality == obj.fQuality && fICCProfile == obj.fICCProfile && fICCProfileDescription == obj.fICCProfileDescription;
#pragma warning restore CS8909

		public readonly override bool Equals (object? obj) =>
			obj is SKWebpEncoderOptions f && Equals (f);

		public static bool operator == (SKWebpEncoderOptions left, SKWebpEncoderOptions right) =>
			left.Equals (right);

		public static bool operator != (SKWebpEncoderOptions left, SKWebpEncoderOptions right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fCompression);
			hash.Add (fQuality);
			hash.Add (fICCProfile);
			hash.Add (fICCProfileDescription);
			return hash.ToHashCode ();
		}

	}
public unsafe partial struct SKJpegEncoderOptions
	{
		public static readonly SKJpegEncoderOptions Default;

		static SKJpegEncoderOptions ()
		{
			Default = new SKJpegEncoderOptions (100, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore);
		}

		public SKJpegEncoderOptions (int quality)
		{
			fICCProfile = default;
			fICCProfileDescription = default;
			xmpMetadata = default;

			fQuality = quality;
			fDownsample = SKJpegEncoderDownsample.Downsample420;
			fAlphaOption = SKJpegEncoderAlphaOption.Ignore;
		}

		public SKJpegEncoderOptions (int quality, SKJpegEncoderDownsample downsample, SKJpegEncoderAlphaOption alphaOption)
		{
			fICCProfile = default;
			fICCProfileDescription = default;
			xmpMetadata = default;

			fQuality = quality;
			fDownsample = downsample;
			fAlphaOption = alphaOption;
		}

		public SKJpegEncoderAlphaOption AlphaOption => fAlphaOption;

		public SKJpegEncoderDownsample Downsample => fDownsample;

		public int Quality => fQuality;
	}
public unsafe partial struct SKPngEncoderOptions
	{
		public static readonly SKPngEncoderOptions Default;

		static SKPngEncoderOptions ()
		{
			Default = new SKPngEncoderOptions (SKPngEncoderFilterFlags.AllFilters, 6);
		}

		public SKPngEncoderOptions (SKPngEncoderFilterFlags filterFlags, int zLibLevel)
		{
			fICCProfile = default;
			fICCProfileDescription = default;

			fFilterFlags = filterFlags;
			fZLibLevel = zLibLevel;
			fComments = default;
		}

		public SKPngEncoderFilterFlags FilterFlags => fFilterFlags;

		public int ZLibLevel => fZLibLevel;
	}
public unsafe partial struct SKWebpEncoderOptions
	{
		public static readonly SKWebpEncoderOptions Default;

		static SKWebpEncoderOptions ()
		{
			Default = new SKWebpEncoderOptions (SKWebpEncoderCompression.Lossy, 100);
		}

		public SKWebpEncoderOptions (SKWebpEncoderCompression compression, float quality)
		{
			fICCProfile = default;
			fICCProfileDescription = default;

			fCompression = compression;
			fQuality = quality;
		}

		public SKWebpEncoderCompression Compression => fCompression;

		public float Quality => fQuality;
	}
