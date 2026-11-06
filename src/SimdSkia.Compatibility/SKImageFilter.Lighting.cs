// SkiaSharp 3.119.4 forwarding overloads, MIT.
using SimdSkia.Cpu;
namespace SkiaSharp;
public unsafe partial class SKImageFilter
{
		public static SKImageFilter CreateDistantLitDiffuse (SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd) =>
			CreateDistantLitDiffuse (direction, lightColor, surfaceScale, kd, null, null);
		public static SKImageFilter CreateDistantLitDiffuse (SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input) =>
			CreateDistantLitDiffuse (direction, lightColor, surfaceScale, kd, input, null);
		public static SKImageFilter CreateDistantLitDiffuse (SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input, SKRect cropRect) =>
			CreateDistantLitDiffuse (direction, lightColor, surfaceScale, kd, input, &cropRect);
		public static SKImageFilter CreatePointLitDiffuse (SKPoint3 location, SKColor lightColor, float surfaceScale, float kd) =>
			CreatePointLitDiffuse (location, lightColor, surfaceScale, kd, null, null);
		public static SKImageFilter CreatePointLitDiffuse (SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input) =>
			CreatePointLitDiffuse (location, lightColor, surfaceScale, kd, input, null);
		public static SKImageFilter CreatePointLitDiffuse (SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input, SKRect cropRect) =>
			CreatePointLitDiffuse (location, lightColor, surfaceScale, kd, input, &cropRect);
		public static SKImageFilter CreateSpotLitDiffuse (SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd) =>
			CreateSpotLitDiffuse (location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, kd, null, null);
		public static SKImageFilter CreateSpotLitDiffuse (SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input) =>
			CreateSpotLitDiffuse (location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, kd, input, null);
		public static SKImageFilter CreateSpotLitDiffuse (SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input, SKRect cropRect) =>
			CreateSpotLitDiffuse (location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, kd, input, &cropRect);
		public static SKImageFilter CreateDistantLitSpecular (SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess) =>
			CreateDistantLitSpecular (direction, lightColor, surfaceScale, ks, shininess, null, null);
		public static SKImageFilter CreateDistantLitSpecular (SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input) =>
			CreateDistantLitSpecular (direction, lightColor, surfaceScale, ks, shininess, input, null);
		public static SKImageFilter CreateDistantLitSpecular (SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input, SKRect cropRect) =>
			CreateDistantLitSpecular (direction, lightColor, surfaceScale, ks, shininess, input, &cropRect);
		public static SKImageFilter CreatePointLitSpecular (SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess) =>
			CreatePointLitSpecular (location, lightColor, surfaceScale, ks, shininess, null, null);
		public static SKImageFilter CreatePointLitSpecular (SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input) =>
			CreatePointLitSpecular (location, lightColor, surfaceScale, ks, shininess, input, null);
		public static SKImageFilter CreatePointLitSpecular (SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input, SKRect cropRect) =>
			CreatePointLitSpecular (location, lightColor, surfaceScale, ks, shininess, input, &cropRect);
		public static SKImageFilter CreateSpotLitSpecular (SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess) =>
			CreateSpotLitSpecular (location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, ks, shininess, null, null);
		public static SKImageFilter CreateSpotLitSpecular (SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input) =>
			CreateSpotLitSpecular (location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, ks, shininess, input, null);
		public static SKImageFilter CreateSpotLitSpecular (SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input, SKRect cropRect) =>
			CreateSpotLitSpecular (location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, ks, shininess, input, &cropRect);
private static SKImageFilter CreateDistantLitDiffuse(SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input, SKRect* cropRect)=>Own(RasterImageFilter.Lighting(0,false,new System.Numerics.Vector3(direction.X,direction.Y,direction.Z),default,0,180,lightColor.ToCore(),surfaceScale,kd,1,Node(input),Crop(cropRect)),input);
private static SKImageFilter CreateDistantLitSpecular(SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input, SKRect* cropRect)=>Own(RasterImageFilter.Lighting(0,true,new System.Numerics.Vector3(direction.X,direction.Y,direction.Z),default,0,180,lightColor.ToCore(),surfaceScale,ks,shininess,Node(input),Crop(cropRect)),input);
private static SKImageFilter CreatePointLitDiffuse(SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input, SKRect* cropRect)=>Own(RasterImageFilter.Lighting(1,false,new System.Numerics.Vector3(location.X,location.Y,location.Z),default,0,180,lightColor.ToCore(),surfaceScale,kd,1,Node(input),Crop(cropRect)),input);
private static SKImageFilter CreatePointLitSpecular(SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input, SKRect* cropRect)=>Own(RasterImageFilter.Lighting(1,true,new System.Numerics.Vector3(location.X,location.Y,location.Z),default,0,180,lightColor.ToCore(),surfaceScale,ks,shininess,Node(input),Crop(cropRect)),input);
private static SKImageFilter CreateSpotLitDiffuse(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter? input, SKRect* cropRect)=>Own(RasterImageFilter.Lighting(2,false,new System.Numerics.Vector3(location.X,location.Y,location.Z),new System.Numerics.Vector3(target.X,target.Y,target.Z),specularExponent,cutoffAngle,lightColor.ToCore(),surfaceScale,kd,1,Node(input),Crop(cropRect)),input);
private static SKImageFilter CreateSpotLitSpecular(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter? input, SKRect* cropRect)=>Own(RasterImageFilter.Lighting(2,true,new System.Numerics.Vector3(location.X,location.Y,location.Z),new System.Numerics.Vector3(target.X,target.Y,target.Z),specularExponent,cutoffAngle,lightColor.ToCore(),surfaceScale,ks,shininess,Node(input),Crop(cropRect)),input);
}
