// Value contracts and managed algorithms from SkiaSharp v3.119.4 (MIT). See licenses/SkiaSharp-MIT.txt.
#nullable disable
using System.Runtime.InteropServices;
namespace SkiaSharp;
[StructLayout(LayoutKind.Sequential)]
public partial struct SKMatrix44 : IEquatable<SKMatrix44> {
		// public float m00
		private Single m00;
		public Single M00 {
			readonly get => m00;
			set => m00 = value;
		}

		// public float m01
		private Single m01;
		public Single M01 {
			readonly get => m01;
			set => m01 = value;
		}

		// public float m02
		private Single m02;
		public Single M02 {
			readonly get => m02;
			set => m02 = value;
		}

		// public float m03
		private Single m03;
		public Single M03 {
			readonly get => m03;
			set => m03 = value;
		}

		// public float m10
		private Single m10;
		public Single M10 {
			readonly get => m10;
			set => m10 = value;
		}

		// public float m11
		private Single m11;
		public Single M11 {
			readonly get => m11;
			set => m11 = value;
		}

		// public float m12
		private Single m12;
		public Single M12 {
			readonly get => m12;
			set => m12 = value;
		}

		// public float m13
		private Single m13;
		public Single M13 {
			readonly get => m13;
			set => m13 = value;
		}

		// public float m20
		private Single m20;
		public Single M20 {
			readonly get => m20;
			set => m20 = value;
		}

		// public float m21
		private Single m21;
		public Single M21 {
			readonly get => m21;
			set => m21 = value;
		}

		// public float m22
		private Single m22;
		public Single M22 {
			readonly get => m22;
			set => m22 = value;
		}

		// public float m23
		private Single m23;
		public Single M23 {
			readonly get => m23;
			set => m23 = value;
		}

		// public float m30
		private Single m30;
		public Single M30 {
			readonly get => m30;
			set => m30 = value;
		}

		// public float m31
		private Single m31;
		public Single M31 {
			readonly get => m31;
			set => m31 = value;
		}

		// public float m32
		private Single m32;
		public Single M32 {
			readonly get => m32;
			set => m32 = value;
		}

		// public float m33
		private Single m33;
		public Single M33 {
			readonly get => m33;
			set => m33 = value;
		}

		public readonly bool Equals (SKMatrix44 obj) =>
#pragma warning disable CS8909
			m00 == obj.m00 && m01 == obj.m01 && m02 == obj.m02 && m03 == obj.m03 && m10 == obj.m10 && m11 == obj.m11 && m12 == obj.m12 && m13 == obj.m13 && m20 == obj.m20 && m21 == obj.m21 && m22 == obj.m22 && m23 == obj.m23 && m30 == obj.m30 && m31 == obj.m31 && m32 == obj.m32 && m33 == obj.m33;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKMatrix44 f && Equals (f);

		public static bool operator == (SKMatrix44 left, SKMatrix44 right) =>
			left.Equals (right);

		public static bool operator != (SKMatrix44 left, SKMatrix44 right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (m00);
			hash.Add (m01);
			hash.Add (m02);
			hash.Add (m03);
			hash.Add (m10);
			hash.Add (m11);
			hash.Add (m12);
			hash.Add (m13);
			hash.Add (m20);
			hash.Add (m21);
			hash.Add (m22);
			hash.Add (m23);
			hash.Add (m30);
			hash.Add (m31);
			hash.Add (m32);
			hash.Add (m33);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct SKRotationScaleMatrix : IEquatable<SKRotationScaleMatrix> {
		// public float fSCos
		private Single fSCos;
		public Single SCos {
			readonly get => fSCos;
			set => fSCos = value;
		}

		// public float fSSin
		private Single fSSin;
		public Single SSin {
			readonly get => fSSin;
			set => fSSin = value;
		}

		// public float fTX
		private Single fTX;
		public Single TX {
			readonly get => fTX;
			set => fTX = value;
		}

		// public float fTY
		private Single fTY;
		public Single TY {
			readonly get => fTY;
			set => fTY = value;
		}

		public readonly bool Equals (SKRotationScaleMatrix obj) =>
#pragma warning disable CS8909
			fSCos == obj.fSCos && fSSin == obj.fSSin && fTX == obj.fTX && fTY == obj.fTY;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKRotationScaleMatrix f && Equals (f);

		public static bool operator == (SKRotationScaleMatrix left, SKRotationScaleMatrix right) =>
			left.Equals (right);

		public static bool operator != (SKRotationScaleMatrix left, SKRotationScaleMatrix right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fSCos);
			hash.Add (fSSin);
			hash.Add (fTX);
			hash.Add (fTY);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct SKColorSpacePrimaries : IEquatable<SKColorSpacePrimaries> {
		// public float fRX
		private Single fRX;
		public Single RX {
			readonly get => fRX;
			set => fRX = value;
		}

		// public float fRY
		private Single fRY;
		public Single RY {
			readonly get => fRY;
			set => fRY = value;
		}

		// public float fGX
		private Single fGX;
		public Single GX {
			readonly get => fGX;
			set => fGX = value;
		}

		// public float fGY
		private Single fGY;
		public Single GY {
			readonly get => fGY;
			set => fGY = value;
		}

		// public float fBX
		private Single fBX;
		public Single BX {
			readonly get => fBX;
			set => fBX = value;
		}

		// public float fBY
		private Single fBY;
		public Single BY {
			readonly get => fBY;
			set => fBY = value;
		}

		// public float fWX
		private Single fWX;
		public Single WX {
			readonly get => fWX;
			set => fWX = value;
		}

		// public float fWY
		private Single fWY;
		public Single WY {
			readonly get => fWY;
			set => fWY = value;
		}

		public readonly bool Equals (SKColorSpacePrimaries obj) =>
#pragma warning disable CS8909
			fRX == obj.fRX && fRY == obj.fRY && fGX == obj.fGX && fGY == obj.fGY && fBX == obj.fBX && fBY == obj.fBY && fWX == obj.fWX && fWY == obj.fWY;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKColorSpacePrimaries f && Equals (f);

		public static bool operator == (SKColorSpacePrimaries left, SKColorSpacePrimaries right) =>
			left.Equals (right);

		public static bool operator != (SKColorSpacePrimaries left, SKColorSpacePrimaries right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fRX);
			hash.Add (fRY);
			hash.Add (fGX);
			hash.Add (fGY);
			hash.Add (fBX);
			hash.Add (fBY);
			hash.Add (fWX);
			hash.Add (fWY);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct SKColorSpaceTransferFn : IEquatable<SKColorSpaceTransferFn> {
		// public float fG
		private Single fG;
		public Single G {
			readonly get => fG;
			set => fG = value;
		}

		// public float fA
		private Single fA;
		public Single A {
			readonly get => fA;
			set => fA = value;
		}

		// public float fB
		private Single fB;
		public Single B {
			readonly get => fB;
			set => fB = value;
		}

		// public float fC
		private Single fC;
		public Single C {
			readonly get => fC;
			set => fC = value;
		}

		// public float fD
		private Single fD;
		public Single D {
			readonly get => fD;
			set => fD = value;
		}

		// public float fE
		private Single fE;
		public Single E {
			readonly get => fE;
			set => fE = value;
		}

		// public float fF
		private Single fF;
		public Single F {
			readonly get => fF;
			set => fF = value;
		}

		public readonly bool Equals (SKColorSpaceTransferFn obj) =>
#pragma warning disable CS8909
			fG == obj.fG && fA == obj.fA && fB == obj.fB && fC == obj.fC && fD == obj.fD && fE == obj.fE && fF == obj.fF;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKColorSpaceTransferFn f && Equals (f);

		public static bool operator == (SKColorSpaceTransferFn left, SKColorSpaceTransferFn right) =>
			left.Equals (right);

		public static bool operator != (SKColorSpaceTransferFn left, SKColorSpaceTransferFn right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fG);
			hash.Add (fA);
			hash.Add (fB);
			hash.Add (fC);
			hash.Add (fD);
			hash.Add (fE);
			hash.Add (fF);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct SKColorSpaceXyz : IEquatable<SKColorSpaceXyz> {
		// public float fM00
		private Single fM00;

		// public float fM01
		private Single fM01;

		// public float fM02
		private Single fM02;

		// public float fM10
		private Single fM10;

		// public float fM11
		private Single fM11;

		// public float fM12
		private Single fM12;

		// public float fM20
		private Single fM20;

		// public float fM21
		private Single fM21;

		// public float fM22
		private Single fM22;

		public readonly bool Equals (SKColorSpaceXyz obj) =>
#pragma warning disable CS8909
			fM00 == obj.fM00 && fM01 == obj.fM01 && fM02 == obj.fM02 && fM10 == obj.fM10 && fM11 == obj.fM11 && fM12 == obj.fM12 && fM20 == obj.fM20 && fM21 == obj.fM21 && fM22 == obj.fM22;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKColorSpaceXyz f && Equals (f);

		public static bool operator == (SKColorSpaceXyz left, SKColorSpaceXyz right) =>
			left.Equals (right);

		public static bool operator != (SKColorSpaceXyz left, SKColorSpaceXyz right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fM00);
			hash.Add (fM01);
			hash.Add (fM02);
			hash.Add (fM10);
			hash.Add (fM11);
			hash.Add (fM12);
			hash.Add (fM20);
			hash.Add (fM21);
			hash.Add (fM22);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct SKHighContrastConfig : IEquatable<SKHighContrastConfig> {
		// public bool fGrayscale
		private Byte fGrayscale;
		public bool Grayscale {
			readonly get => fGrayscale > 0;
			set => fGrayscale = value ? (byte)1 : (byte)0;
		}

		// public sk_highcontrastconfig_invertstyle_t fInvertStyle
		private SKHighContrastConfigInvertStyle fInvertStyle;
		public SKHighContrastConfigInvertStyle InvertStyle {
			readonly get => fInvertStyle;
			set => fInvertStyle = value;
		}

		// public float fContrast
		private Single fContrast;
		public Single Contrast {
			readonly get => fContrast;
			set => fContrast = value;
		}

		public readonly bool Equals (SKHighContrastConfig obj) =>
#pragma warning disable CS8909
			fGrayscale == obj.fGrayscale && fInvertStyle == obj.fInvertStyle && fContrast == obj.fContrast;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKHighContrastConfig f && Equals (f);

		public static bool operator == (SKHighContrastConfig left, SKHighContrastConfig right) =>
			left.Equals (right);

		public static bool operator != (SKHighContrastConfig left, SKHighContrastConfig right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fGrayscale);
			hash.Add (fInvertStyle);
			hash.Add (fContrast);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct SKFontMetrics : IEquatable<SKFontMetrics> {
		// public uint32_t fFlags
		private UInt32 fFlags;

		// public float fTop
		private Single fTop;

		// public float fAscent
		private Single fAscent;

		// public float fDescent
		private Single fDescent;

		// public float fBottom
		private Single fBottom;

		// public float fLeading
		private Single fLeading;

		// public float fAvgCharWidth
		private Single fAvgCharWidth;

		// public float fMaxCharWidth
		private Single fMaxCharWidth;

		// public float fXMin
		private Single fXMin;

		// public float fXMax
		private Single fXMax;

		// public float fXHeight
		private Single fXHeight;

		// public float fCapHeight
		private Single fCapHeight;

		// public float fUnderlineThickness
		private Single fUnderlineThickness;

		// public float fUnderlinePosition
		private Single fUnderlinePosition;

		// public float fStrikeoutThickness
		private Single fStrikeoutThickness;

		// public float fStrikeoutPosition
		private Single fStrikeoutPosition;

		public readonly bool Equals (SKFontMetrics obj) =>
#pragma warning disable CS8909
			fFlags == obj.fFlags && fTop == obj.fTop && fAscent == obj.fAscent && fDescent == obj.fDescent && fBottom == obj.fBottom && fLeading == obj.fLeading && fAvgCharWidth == obj.fAvgCharWidth && fMaxCharWidth == obj.fMaxCharWidth && fXMin == obj.fXMin && fXMax == obj.fXMax && fXHeight == obj.fXHeight && fCapHeight == obj.fCapHeight && fUnderlineThickness == obj.fUnderlineThickness && fUnderlinePosition == obj.fUnderlinePosition && fStrikeoutThickness == obj.fStrikeoutThickness && fStrikeoutPosition == obj.fStrikeoutPosition;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKFontMetrics f && Equals (f);

		public static bool operator == (SKFontMetrics left, SKFontMetrics right) =>
			left.Equals (right);

		public static bool operator != (SKFontMetrics left, SKFontMetrics right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fFlags);
			hash.Add (fTop);
			hash.Add (fAscent);
			hash.Add (fDescent);
			hash.Add (fBottom);
			hash.Add (fLeading);
			hash.Add (fAvgCharWidth);
			hash.Add (fMaxCharWidth);
			hash.Add (fXMin);
			hash.Add (fXMax);
			hash.Add (fXHeight);
			hash.Add (fCapHeight);
			hash.Add (fUnderlineThickness);
			hash.Add (fUnderlinePosition);
			hash.Add (fStrikeoutThickness);
			hash.Add (fStrikeoutPosition);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct GRGlFramebufferInfo : IEquatable<GRGlFramebufferInfo> {
		// public unsigned int fFBOID
		private UInt32 fFBOID;
		public UInt32 FramebufferObjectId {
			readonly get => fFBOID;
			set => fFBOID = value;
		}

		// public unsigned int fFormat
		private UInt32 fFormat;
		public UInt32 Format {
			readonly get => fFormat;
			set => fFormat = value;
		}

		// public bool fProtected
		private Byte fProtected;
		public bool Protected {
			readonly get => fProtected > 0;
			set => fProtected = value ? (byte)1 : (byte)0;
		}

		public readonly bool Equals (GRGlFramebufferInfo obj) =>
#pragma warning disable CS8909
			fFBOID == obj.fFBOID && fFormat == obj.fFormat && fProtected == obj.fProtected;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is GRGlFramebufferInfo f && Equals (f);

		public static bool operator == (GRGlFramebufferInfo left, GRGlFramebufferInfo right) =>
			left.Equals (right);

		public static bool operator != (GRGlFramebufferInfo left, GRGlFramebufferInfo right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fFBOID);
			hash.Add (fFormat);
			hash.Add (fProtected);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct GRGlTextureInfo : IEquatable<GRGlTextureInfo> {
		// public unsigned int fTarget
		private UInt32 fTarget;
		public UInt32 Target {
			readonly get => fTarget;
			set => fTarget = value;
		}

		// public unsigned int fID
		private UInt32 fID;
		public UInt32 Id {
			readonly get => fID;
			set => fID = value;
		}

		// public unsigned int fFormat
		private UInt32 fFormat;
		public UInt32 Format {
			readonly get => fFormat;
			set => fFormat = value;
		}

		// public bool fProtected
		private Byte fProtected;
		public bool Protected {
			readonly get => fProtected > 0;
			set => fProtected = value ? (byte)1 : (byte)0;
		}

		public readonly bool Equals (GRGlTextureInfo obj) =>
#pragma warning disable CS8909
			fTarget == obj.fTarget && fID == obj.fID && fFormat == obj.fFormat && fProtected == obj.fProtected;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is GRGlTextureInfo f && Equals (f);

		public static bool operator == (GRGlTextureInfo left, GRGlTextureInfo right) =>
			left.Equals (right);

		public static bool operator != (GRGlTextureInfo left, GRGlTextureInfo right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fTarget);
			hash.Add (fID);
			hash.Add (fFormat);
			hash.Add (fProtected);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct GRVkAlloc : IEquatable<GRVkAlloc> {
		// public uint64_t fMemory
		private UInt64 fMemory;
		public UInt64 Memory {
			readonly get => fMemory;
			set => fMemory = value;
		}

		// public uint64_t fOffset
		private UInt64 fOffset;
		public UInt64 Offset {
			readonly get => fOffset;
			set => fOffset = value;
		}

		// public uint64_t fSize
		private UInt64 fSize;
		public UInt64 Size {
			readonly get => fSize;
			set => fSize = value;
		}

		// public uint32_t fFlags
		private UInt32 fFlags;
		public UInt32 Flags {
			readonly get => fFlags;
			set => fFlags = value;
		}

		// public gr_vk_backendmemory_t fBackendMemory
		private IntPtr fBackendMemory;
		public IntPtr BackendMemory {
			readonly get => fBackendMemory;
			set => fBackendMemory = value;
		}

		// public bool _private_fUsesSystemHeap
		private Byte fUsesSystemHeap;

		public readonly bool Equals (GRVkAlloc obj) =>
#pragma warning disable CS8909
			fMemory == obj.fMemory && fOffset == obj.fOffset && fSize == obj.fSize && fFlags == obj.fFlags && fBackendMemory == obj.fBackendMemory && fUsesSystemHeap == obj.fUsesSystemHeap;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is GRVkAlloc f && Equals (f);

		public static bool operator == (GRVkAlloc left, GRVkAlloc right) =>
			left.Equals (right);

		public static bool operator != (GRVkAlloc left, GRVkAlloc right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fMemory);
			hash.Add (fOffset);
			hash.Add (fSize);
			hash.Add (fFlags);
			hash.Add (fBackendMemory);
			hash.Add (fUsesSystemHeap);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct GRVkImageInfo : IEquatable<GRVkImageInfo> {
		// public uint64_t fImage
		private UInt64 fImage;
		public UInt64 Image {
			readonly get => fImage;
			set => fImage = value;
		}

		// public gr_vk_alloc_t fAlloc
		private GRVkAlloc fAlloc;
		public GRVkAlloc Alloc {
			readonly get => fAlloc;
			set => fAlloc = value;
		}

		// public uint32_t fImageTiling
		private UInt32 fImageTiling;
		public UInt32 ImageTiling {
			readonly get => fImageTiling;
			set => fImageTiling = value;
		}

		// public uint32_t fImageLayout
		private UInt32 fImageLayout;
		public UInt32 ImageLayout {
			readonly get => fImageLayout;
			set => fImageLayout = value;
		}

		// public uint32_t fFormat
		private UInt32 fFormat;
		public UInt32 Format {
			readonly get => fFormat;
			set => fFormat = value;
		}

		// public uint32_t fImageUsageFlags
		private UInt32 fImageUsageFlags;
		public UInt32 ImageUsageFlags {
			readonly get => fImageUsageFlags;
			set => fImageUsageFlags = value;
		}

		// public uint32_t fSampleCount
		private UInt32 fSampleCount;
		public UInt32 SampleCount {
			readonly get => fSampleCount;
			set => fSampleCount = value;
		}

		// public uint32_t fLevelCount
		private UInt32 fLevelCount;
		public UInt32 LevelCount {
			readonly get => fLevelCount;
			set => fLevelCount = value;
		}

		// public uint32_t fCurrentQueueFamily
		private UInt32 fCurrentQueueFamily;
		public UInt32 CurrentQueueFamily {
			readonly get => fCurrentQueueFamily;
			set => fCurrentQueueFamily = value;
		}

		// public bool fProtected
		private Byte fProtected;
		public bool Protected {
			readonly get => fProtected > 0;
			set => fProtected = value ? (byte)1 : (byte)0;
		}

		// public gr_vk_ycbcrconversioninfo_t fYcbcrConversionInfo
		private GrVkYcbcrConversionInfo fYcbcrConversionInfo;
		public GrVkYcbcrConversionInfo YcbcrConversionInfo {
			readonly get => fYcbcrConversionInfo;
			set => fYcbcrConversionInfo = value;
		}

		// public uint32_t fSharingMode
		private UInt32 fSharingMode;
		public UInt32 SharingMode {
			readonly get => fSharingMode;
			set => fSharingMode = value;
		}

		public readonly bool Equals (GRVkImageInfo obj) =>
#pragma warning disable CS8909
			fImage == obj.fImage && fAlloc == obj.fAlloc && fImageTiling == obj.fImageTiling && fImageLayout == obj.fImageLayout && fFormat == obj.fFormat && fImageUsageFlags == obj.fImageUsageFlags && fSampleCount == obj.fSampleCount && fLevelCount == obj.fLevelCount && fCurrentQueueFamily == obj.fCurrentQueueFamily && fProtected == obj.fProtected && fYcbcrConversionInfo == obj.fYcbcrConversionInfo && fSharingMode == obj.fSharingMode;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is GRVkImageInfo f && Equals (f);

		public static bool operator == (GRVkImageInfo left, GRVkImageInfo right) =>
			left.Equals (right);

		public static bool operator != (GRVkImageInfo left, GRVkImageInfo right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fImage);
			hash.Add (fAlloc);
			hash.Add (fImageTiling);
			hash.Add (fImageLayout);
			hash.Add (fFormat);
			hash.Add (fImageUsageFlags);
			hash.Add (fSampleCount);
			hash.Add (fLevelCount);
			hash.Add (fCurrentQueueFamily);
			hash.Add (fProtected);
			hash.Add (fYcbcrConversionInfo);
			hash.Add (fSharingMode);
			return hash.ToHashCode ();
		}

	}

[StructLayout(LayoutKind.Sequential)]
public partial struct GrVkYcbcrConversionInfo : IEquatable<GrVkYcbcrConversionInfo> {
		// public uint32_t fFormat
		private UInt32 fFormat;
		public UInt32 Format {
			readonly get => fFormat;
			set => fFormat = value;
		}

		// public uint64_t fExternalFormat
		private UInt64 fExternalFormat;
		public UInt64 ExternalFormat {
			readonly get => fExternalFormat;
			set => fExternalFormat = value;
		}

		// public uint32_t fYcbcrModel
		private UInt32 fYcbcrModel;
		public UInt32 YcbcrModel {
			readonly get => fYcbcrModel;
			set => fYcbcrModel = value;
		}

		// public uint32_t fYcbcrRange
		private UInt32 fYcbcrRange;
		public UInt32 YcbcrRange {
			readonly get => fYcbcrRange;
			set => fYcbcrRange = value;
		}

		// public uint32_t fXChromaOffset
		private UInt32 fXChromaOffset;
		public UInt32 XChromaOffset {
			readonly get => fXChromaOffset;
			set => fXChromaOffset = value;
		}

		// public uint32_t fYChromaOffset
		private UInt32 fYChromaOffset;
		public UInt32 YChromaOffset {
			readonly get => fYChromaOffset;
			set => fYChromaOffset = value;
		}

		// public uint32_t fChromaFilter
		private UInt32 fChromaFilter;
		public UInt32 ChromaFilter {
			readonly get => fChromaFilter;
			set => fChromaFilter = value;
		}

		// public uint32_t fForceExplicitReconstruction
		private UInt32 fForceExplicitReconstruction;
		public UInt32 ForceExplicitReconstruction {
			readonly get => fForceExplicitReconstruction;
			set => fForceExplicitReconstruction = value;
		}

		// public uint32_t fFormatFeatures
		private UInt32 fFormatFeatures;
		public UInt32 FormatFeatures {
			readonly get => fFormatFeatures;
			set => fFormatFeatures = value;
		}

		public readonly bool Equals (GrVkYcbcrConversionInfo obj) =>
#pragma warning disable CS8909
			fFormat == obj.fFormat && fExternalFormat == obj.fExternalFormat && fYcbcrModel == obj.fYcbcrModel && fYcbcrRange == obj.fYcbcrRange && fXChromaOffset == obj.fXChromaOffset && fYChromaOffset == obj.fYChromaOffset && fChromaFilter == obj.fChromaFilter && fForceExplicitReconstruction == obj.fForceExplicitReconstruction && fFormatFeatures == obj.fFormatFeatures;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is GrVkYcbcrConversionInfo f && Equals (f);

		public static bool operator == (GrVkYcbcrConversionInfo left, GrVkYcbcrConversionInfo right) =>
			left.Equals (right);

		public static bool operator != (GrVkYcbcrConversionInfo left, GrVkYcbcrConversionInfo right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (fFormat);
			hash.Add (fExternalFormat);
			hash.Add (fYcbcrModel);
			hash.Add (fYcbcrRange);
			hash.Add (fXChromaOffset);
			hash.Add (fYChromaOffset);
			hash.Add (fChromaFilter);
			hash.Add (fForceExplicitReconstruction);
			hash.Add (fFormatFeatures);
			return hash.ToHashCode ();
		}

	}

public partial struct SKFontMetrics
	{
		private const uint flagsUnderlineThicknessIsValid = (1U << 0);
		private const uint flagsUnderlinePositionIsValid = (1U << 1);
		private const uint flagsStrikeoutThicknessIsValid = (1U << 2);
		private const uint flagsStrikeoutPositionIsValid = (1U << 3);

		public readonly float Top => fTop;

		public readonly float Ascent => fAscent;

		public readonly float Descent => fDescent;

		public readonly float Bottom => fBottom;

		public readonly float Leading => fLeading;

		public readonly float AverageCharacterWidth => fAvgCharWidth;

		public readonly float MaxCharacterWidth => fMaxCharWidth;

		public readonly float XMin => fXMin;

		public readonly float XMax => fXMax;

		public readonly float XHeight => fXHeight;

		public readonly float CapHeight => fCapHeight;

		public readonly float? UnderlineThickness => GetIfValid (fUnderlineThickness, flagsUnderlineThicknessIsValid);
		public readonly float? UnderlinePosition => GetIfValid (fUnderlinePosition, flagsUnderlinePositionIsValid);
		public readonly float? StrikeoutThickness => GetIfValid (fStrikeoutThickness, flagsStrikeoutThicknessIsValid);
		public readonly float? StrikeoutPosition => GetIfValid (fStrikeoutPosition, flagsStrikeoutPositionIsValid);

		private readonly float? GetIfValid (float value, uint flag) =>
			(fFlags & flag) == flag ? value : (float?)null;
	}

public partial struct SKHighContrastConfig
	{
		public static readonly SKHighContrastConfig Default;

		static SKHighContrastConfig ()
		{
			Default = new SKHighContrastConfig (false, SKHighContrastConfigInvertStyle.NoInvert, 0.0f);
		}

		public SKHighContrastConfig (bool grayscale, SKHighContrastConfigInvertStyle invertStyle, float contrast)
		{
			fGrayscale = grayscale ? (byte)1 : (byte)0;
			fInvertStyle = invertStyle;
			fContrast = contrast;
		}

		public readonly bool IsValid =>
			(int)fInvertStyle >= (int)SKHighContrastConfigInvertStyle.NoInvert &&
			(int)fInvertStyle <= (int)SKHighContrastConfigInvertStyle.InvertLightness &&
			fContrast >= -1.0 &&
			fContrast <= 1.0;
	}
