// Managed geometry from SkiaSharp v3.119.4 (MIT). See docs/licenses/SkiaSharp-MIT.txt.
#nullable disable
namespace SkiaSharp;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public partial struct SKPoint : IEquatable<SKPoint> {
		// public float x
		private Single x;
		public Single X {
			readonly get => x;
			set => x = value;
		}

		// public float y
		private Single y;
		public Single Y {
			readonly get => y;
			set => y = value;
		}

		public readonly bool Equals (SKPoint obj) =>
#pragma warning disable CS8909
			x == obj.x && y == obj.y;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKPoint f && Equals (f);

		public static bool operator == (SKPoint left, SKPoint right) =>
			left.Equals (right);

		public static bool operator != (SKPoint left, SKPoint right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (x);
			hash.Add (y);
			return hash.ToHashCode ();
		}

	}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public partial struct SKPointI : IEquatable<SKPointI> {
		// public int32_t x
		private Int32 x;
		public Int32 X {
			readonly get => x;
			set => x = value;
		}

		// public int32_t y
		private Int32 y;
		public Int32 Y {
			readonly get => y;
			set => y = value;
		}

		public readonly bool Equals (SKPointI obj) =>
#pragma warning disable CS8909
			x == obj.x && y == obj.y;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKPointI f && Equals (f);

		public static bool operator == (SKPointI left, SKPointI right) =>
			left.Equals (right);

		public static bool operator != (SKPointI left, SKPointI right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (x);
			hash.Add (y);
			return hash.ToHashCode ();
		}

	}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public partial struct SKPoint3 : IEquatable<SKPoint3> {
		// public float x
		private Single x;
		public Single X {
			readonly get => x;
			set => x = value;
		}

		// public float y
		private Single y;
		public Single Y {
			readonly get => y;
			set => y = value;
		}

		// public float z
		private Single z;
		public Single Z {
			readonly get => z;
			set => z = value;
		}

		public readonly bool Equals (SKPoint3 obj) =>
#pragma warning disable CS8909
			x == obj.x && y == obj.y && z == obj.z;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKPoint3 f && Equals (f);

		public static bool operator == (SKPoint3 left, SKPoint3 right) =>
			left.Equals (right);

		public static bool operator != (SKPoint3 left, SKPoint3 right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (x);
			hash.Add (y);
			hash.Add (z);
			return hash.ToHashCode ();
		}

	}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public partial struct SKSize : IEquatable<SKSize> {
		// public float w
		private Single w;
		public Single Width {
			readonly get => w;
			set => w = value;
		}

		// public float h
		private Single h;
		public Single Height {
			readonly get => h;
			set => h = value;
		}

		public readonly bool Equals (SKSize obj) =>
#pragma warning disable CS8909
			w == obj.w && h == obj.h;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKSize f && Equals (f);

		public static bool operator == (SKSize left, SKSize right) =>
			left.Equals (right);

		public static bool operator != (SKSize left, SKSize right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (w);
			hash.Add (h);
			return hash.ToHashCode ();
		}

	}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public partial struct SKSizeI : IEquatable<SKSizeI> {
		// public int32_t w
		private Int32 w;
		public Int32 Width {
			readonly get => w;
			set => w = value;
		}

		// public int32_t h
		private Int32 h;
		public Int32 Height {
			readonly get => h;
			set => h = value;
		}

		public readonly bool Equals (SKSizeI obj) =>
#pragma warning disable CS8909
			w == obj.w && h == obj.h;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKSizeI f && Equals (f);

		public static bool operator == (SKSizeI left, SKSizeI right) =>
			left.Equals (right);

		public static bool operator != (SKSizeI left, SKSizeI right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (w);
			hash.Add (h);
			return hash.ToHashCode ();
		}

	}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public partial struct SKRect : IEquatable<SKRect> {
		// public float left
		private Single left;
		public Single Left {
			readonly get => left;
			set => left = value;
		}

		// public float top
		private Single top;
		public Single Top {
			readonly get => top;
			set => top = value;
		}

		// public float right
		private Single right;
		public Single Right {
			readonly get => right;
			set => right = value;
		}

		// public float bottom
		private Single bottom;
		public Single Bottom {
			readonly get => bottom;
			set => bottom = value;
		}

		public readonly bool Equals (SKRect obj) =>
#pragma warning disable CS8909
			left == obj.left && top == obj.top && right == obj.right && bottom == obj.bottom;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKRect f && Equals (f);

		public static bool operator == (SKRect left, SKRect right) =>
			left.Equals (right);

		public static bool operator != (SKRect left, SKRect right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (left);
			hash.Add (top);
			hash.Add (right);
			hash.Add (bottom);
			return hash.ToHashCode ();
		}

	}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public partial struct SKRectI : IEquatable<SKRectI> {
		// public int32_t left
		private Int32 left;
		public Int32 Left {
			readonly get => left;
			set => left = value;
		}

		// public int32_t top
		private Int32 top;
		public Int32 Top {
			readonly get => top;
			set => top = value;
		}

		// public int32_t right
		private Int32 right;
		public Int32 Right {
			readonly get => right;
			set => right = value;
		}

		// public int32_t bottom
		private Int32 bottom;
		public Int32 Bottom {
			readonly get => bottom;
			set => bottom = value;
		}

		public readonly bool Equals (SKRectI obj) =>
#pragma warning disable CS8909
			left == obj.left && top == obj.top && right == obj.right && bottom == obj.bottom;
#pragma warning restore CS8909

		public readonly override bool Equals (object obj) =>
			obj is SKRectI f && Equals (f);

		public static bool operator == (SKRectI left, SKRectI right) =>
			left.Equals (right);

		public static bool operator != (SKRectI left, SKRectI right) =>
			!left.Equals (right);

		public readonly override int GetHashCode ()
		{
			var hash = new HashCode ();
			hash.Add (left);
			hash.Add (top);
			hash.Add (right);
			hash.Add (bottom);
			return hash.ToHashCode ();
		}

	}