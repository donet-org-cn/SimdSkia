// Value serialization contracts from SkiaSharp v3.119.4 (MIT).
#nullable disable
using System;
using System.Collections.Generic;
namespace SkiaSharp;
public unsafe readonly ref struct SKRuntimeEffectUniform
	{
		internal enum DataType
		{
			Empty,

			Float,
			FloatArray,
			Int32,
			Int32Array,
			Color,
		}

		public static SKRuntimeEffectUniform Empty => default;

		// fields

		private readonly float floatValue;
		private readonly ReadOnlySpan<float> floatArray;

		private readonly int intValue;
		private readonly ReadOnlySpan<int> intArray;

		private readonly SKColorF colorValue;

		// ctor

		private SKRuntimeEffectUniform (
			DataType type,
			int size,
			float floatValue = default,
			ReadOnlySpan<float> floatArray = default,
			int intValue = default,
			ReadOnlySpan<int> intArray = default,
			SKColorF colorValue = default)
		{
			Type = type;
			Size = size;

			this.floatValue = floatValue;
			this.floatArray = floatArray;

			this.intValue = intValue;
			this.intArray = intArray;

			this.colorValue = colorValue;
		}

		// properties

		public bool IsEmpty => Type == DataType.Empty;

		public int Size { get; }

		internal DataType Type { get; }

		// converters

		// float

		public static implicit operator SKRuntimeEffectUniform (float value) =>
			new SKRuntimeEffectUniform (DataType.Float, sizeof (float), floatValue: value);

		public static implicit operator SKRuntimeEffectUniform (float[] value) => (ReadOnlySpan<float>)value;

		public static implicit operator SKRuntimeEffectUniform (Span<float> value) => (ReadOnlySpan<float>)value;

		public static implicit operator SKRuntimeEffectUniform (ReadOnlySpan<float> value) =>
			new SKRuntimeEffectUniform (DataType.FloatArray, sizeof (float) * value.Length, floatArray: value);

		public static implicit operator SKRuntimeEffectUniform (SKPoint value) => (ReadOnlySpan<float>)new[] { value.X, value.Y };

		public static implicit operator SKRuntimeEffectUniform (SKSize value) => (ReadOnlySpan<float>)new[] { value.Width, value.Height };

		public static implicit operator SKRuntimeEffectUniform (SKPoint3 value) => (ReadOnlySpan<float>)new[] { value.X, value.Y, value.Z };

		// int

		public static implicit operator SKRuntimeEffectUniform (int value) =>
			new SKRuntimeEffectUniform (DataType.Int32, sizeof (int), intValue: value);

		public static implicit operator SKRuntimeEffectUniform (int[] value) => (ReadOnlySpan<int>)value;

		public static implicit operator SKRuntimeEffectUniform (Span<int> value) => (ReadOnlySpan<int>)value;

		public static implicit operator SKRuntimeEffectUniform (ReadOnlySpan<int> value) =>
			new SKRuntimeEffectUniform (DataType.Int32Array, sizeof (int) * value.Length, intArray: value);

		public static implicit operator SKRuntimeEffectUniform (SKPointI value) => (ReadOnlySpan<int>)new[] { value.X, value.Y };

		public static implicit operator SKRuntimeEffectUniform (SKSizeI value) => (ReadOnlySpan<int>)new[] { value.Width, value.Height };

		// color

		public static implicit operator SKRuntimeEffectUniform (SKColor value) => (SKColorF)value;

		public static implicit operator SKRuntimeEffectUniform (SKColorF value) =>
			new SKRuntimeEffectUniform (DataType.Color, sizeof (float) * 4, colorValue: value);

		// float matrix

		public static implicit operator SKRuntimeEffectUniform (float[][] value)
		{
			var floats = new List<float> ();
			foreach (var array in value) {
				floats.AddRange (array);
			}
			return floats.ToArray ();
		}

		public static implicit operator SKRuntimeEffectUniform (SKMatrix value) => value.Values;

		// writer

		public void WriteTo (Span<byte> data)
		{
			switch (Type) {
				// float
				case DataType.Float when data.Length == sizeof (float):
					fixed (void* v = &floatValue)
						new ReadOnlySpan<byte> (v, Size).CopyTo (data);
					break;
				case DataType.Float:
					throw new ArgumentOutOfRangeException (nameof (Type), $"Unknown float data type length: {data.Length}");

				// float array
				case DataType.FloatArray when data.Length == sizeof (int) * floatArray.Length:
					fixed (void* v = floatArray)
						new ReadOnlySpan<byte> (v, Size).CopyTo (data);
					break;
				case DataType.FloatArray:
					throw new ArgumentOutOfRangeException (nameof (Type), $"Unknown float array data type length: {data.Length}");

				// int
				case DataType.Int32 when data.Length == sizeof (int):
					fixed (void* v = &intValue)
						new ReadOnlySpan<byte> (v, Size).CopyTo (data);
					break;
				case DataType.Int32:
					throw new ArgumentOutOfRangeException (nameof (Type), $"Unknown int data type length: {data.Length}");

				// int array
				case DataType.Int32Array when data.Length == sizeof (int) * intArray.Length:
					fixed (void* v = intArray)
						new ReadOnlySpan<byte> (v, Size).CopyTo (data);
					break;
				case DataType.Int32Array:
					throw new ArgumentOutOfRangeException (nameof (Type), $"Unknown int array data type length: {data.Length}");

				// colors
				case DataType.Color when data.Length == sizeof (float) * 3:
					void* vc3 = stackalloc[] { colorValue.Red, colorValue.Green, colorValue.Blue };
					new ReadOnlySpan<byte> (vc3, data.Length).CopyTo (data);
					break;
				case DataType.Color when data.Length == sizeof (float) * 4:
					void* vc4 = stackalloc[] { colorValue.Red, colorValue.Green, colorValue.Blue, colorValue.Alpha };
					new ReadOnlySpan<byte> (vc4, data.Length).CopyTo (data);
					break;
				case DataType.Color:
					throw new ArgumentOutOfRangeException (nameof (Type), $"Unknown color data type length: {data.Length}");

				// empty
				case DataType.Empty:
					data.Fill (0);
					break;

				// error
				default:
					throw new ArgumentOutOfRangeException (nameof (Type), $"Unknown data type: '{Type}'");
			}
		}
	}
public unsafe readonly struct SKRuntimeEffectChild
	{
		private readonly SKObject value;

		public SKRuntimeEffectChild (SKShader shader)
		{
			value = shader;
		}

		public SKRuntimeEffectChild (SKColorFilter colorFilter)
		{
			value = colorFilter;
		}

		public SKRuntimeEffectChild (SKBlender blender)
		{
			value = blender;
		}

		public SKObject Value => value;

		public SKShader Shader => value as SKShader;

		public SKColorFilter ColorFilter => value as SKColorFilter;

		public SKBlender Blender => value as SKBlender;

		public static implicit operator SKRuntimeEffectChild (SKShader shader) => new (shader);

		public static implicit operator SKRuntimeEffectChild (SKColorFilter colorFilter) => new (colorFilter);

		public static implicit operator SKRuntimeEffectChild (SKBlender blender) => new (blender);
	}
