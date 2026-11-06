namespace SimdSkia.Gpu;
internal static class MetalKernel
{
    internal const string Source="""
        #include <metal_stdlib>
        using namespace metal;
        struct Params {
            uint4 bounds; // target width,height; mask left,top
            uint4 mask; // width,height,kind,composite
            float4 color;
            float4 inverse0;
            float4 inverse1;
            float4 source;
            float4 destination;
            uint4 image; // width,height,sampling,reserved
            float4 line;
            uint4 gradient; // count,extend,reserved,reserved
        };
        struct Stop {float4 color;float4 offset;};
        float4 unpack(uint value) {return float4(value&255u,(value>>8)&255u,(value>>16)&255u,value>>24)/255.0f;}
        uint pack(float4 value) {uint4 c=uint4(floor(clamp(value,0.0f,1.0f)*255.0f+0.5f));return c.x|(c.y<<8)|(c.z<<16)|(c.w<<24);}
        float4 tap(const device uint* image,int x,int y,uint width,uint height) {return unpack(image[uint(clamp(y,0,int(height)-1))*width+uint(clamp(x,0,int(width)-1))]);}
        kernel void render(device uint* output [[buffer(0)]],const device uchar* coverage [[buffer(1)]],const device uint* image [[buffer(2)]],constant Params& p [[buffer(3)]],constant Stop* stops [[buffer(4)]],uint2 local [[thread_position_in_grid]]) {
            if(local.x>=p.mask.x||local.y>=p.mask.y)return;
            uint2 pixel=local+p.bounds.zw;if(pixel.x>=p.bounds.x||pixel.y>=p.bounds.y)return;
            float cov=p.mask.z==3?1.0f:float(coverage[local.y*p.mask.x+local.x])/255.0f;if(cov==0)return;
            float2 devicePoint=float2(pixel)+0.5f;float2 point=float2(dot(p.inverse0.xyz,float3(devicePoint,1)),dot(p.inverse1.xyz,float3(devicePoint,1)));
            float4 source=p.color;
            if(p.mask.z==1) {
                float2 d=p.line.zw-p.line.xy;float den=dot(d,d);float t=den>0?dot(point-p.line.xy,d)/den:1;
                if(p.gradient.y==1)t=fract(t);else if(p.gradient.y==2)t=1-abs(fmod(fmod(t,2.0f)+2.0f,2.0f)-1);else if(p.gradient.y==3&&(t<0||t>1))cov=0;else t=clamp(t,0.0f,1.0f);
                source=stops[0].color;for(uint i=0;i+1<p.gradient.x;i++)if(t>=stops[i].offset.x){float span=stops[i+1].offset.x-stops[i].offset.x;float amount=span>0?clamp((t-stops[i].offset.x)/span,0.0f,1.0f):1;source=mix(stops[i].color,stops[i+1].color,amount);}source.a*=p.color.a;
            } else if(p.mask.z==2) {
                float2 uv=p.source.xy+(point-p.destination.xy)*p.source.zw/p.destination.zw;
                if(p.image.z==0) {if(p.image.w!=0)uv=nextafter(uv,float2(-INFINITY));source=tap(image,int(floor(uv.x)),int(floor(uv.y)),p.image.x,p.image.y);}
                else {float2 at=uv-0.5f;int2 base=int2(floor(at));float2 f=fract(at);source=mix(mix(tap(image,base.x,base.y,p.image.x,p.image.y),tap(image,base.x+1,base.y,p.image.x,p.image.y),f.x),mix(tap(image,base.x,base.y+1,p.image.x,p.image.y),tap(image,base.x+1,base.y+1,p.image.x,p.image.y),f.x),f.y);}source*=p.color.a;
            }
            if(p.mask.z!=2)source.rgb*=source.a;
            uint index=pixel.y*p.bounds.x+pixel.x;float4 destination=unpack(output[index]);
            if(p.mask.w==0)source=source*cov+destination*(1-source.a*cov);
            else source=mix(destination,p.mask.w==2?float4(0):source,cov);
            output[index]=pack(source);
        }
        """;
}
