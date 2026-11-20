namespace SimdSkia.Gpu;
internal static class MetalKernel
{
    internal const string Source="""
        #include <metal_stdlib>
        using namespace metal;
        struct Params {
            uint4 bounds; // target width,height; dispatch left,top
            uint4 mask; // dispatch width,height; paint kind (0 solid,1 gradient,2 image,3 clear); composite
            float4 color;
            float4 inverse0;
            float4 inverse1;
            float4 source;
            float4 destination;
            uint4 image; // width,height,sampling,reserved
            float4 line;
            uint4 gradient; // count,extend,reserved,reserved
            float4 rect; // analytic device rectangle left,top,right,bottom
            int4 clip; // hard device clip left,top,right,bottom
            int4 area; // coverage mask left,top,width,height
            uint4 modes; // coverage (0 none,1 area,2 min-axis,3 aliased rect,4 rounded), has mask, ring, antialias
            float4 radii; // rounded: top-left,top-right,bottom-right,bottom-left
            float4 inner; // ring: inner rounded rectangle
            float4 innerRadii;
        };
        // Signed distance to a rounded rectangle with circular corners. Exact for
        // straight edges, so 0.5-d is the true area coverage there.
        float roundedDistance(float2 c,float4 r,float4 radii) {
            float2 center=(r.xy+r.zw)*0.5f,extent=(r.zw-r.xy)*0.5f,q=c-center;
            float radius=q.x<0?(q.y<0?radii.x:radii.w):(q.y<0?radii.y:radii.z);
            radius=min(radius,min(extent.x,extent.y));
            float2 d=abs(q)-extent+radius;return length(max(d,0.0f))+min(max(d.x,d.y),0.0f)-radius;
        }
        float roundedCoverage(float2 c,float4 r,float4 radii,bool aa) {float d=roundedDistance(c,r,radii);return aa?clamp(0.5f-d,0.0f,1.0f):(d<=0?1.0f:0.0f);}
        struct Stop {float4 color;float4 offset;};
        float4 unpack(uint value) {return float4(value&255u,(value>>8)&255u,(value>>16)&255u,value>>24)/255.0f;}
        uint pack(float4 value) {uint4 c=uint4(floor(clamp(value,0.0f,1.0f)*255.0f+0.5f));return c.x|(c.y<<8)|(c.z<<16)|(c.w<<24);}
        float4 tap(const device uint* image,int x,int y,uint width,uint height) {return unpack(image[uint(clamp(y,0,int(height)-1))*width+uint(clamp(x,0,int(width)-1))]);}
        kernel void render(device uint* output [[buffer(0)]],const device uchar* coverage [[buffer(1)]],const device uint* image [[buffer(2)]],constant Params& p [[buffer(3)]],constant Stop* stops [[buffer(4)]],uint2 local [[thread_position_in_grid]]) {
            if(local.x>=p.mask.x||local.y>=p.mask.y)return;
            uint2 pixel=local+p.bounds.zw;if(pixel.x>=p.bounds.x||pixel.y>=p.bounds.y)return;
            int2 at=int2(pixel);if(at.x<p.clip.x||at.y<p.clip.y||at.x>=p.clip.z||at.y>=p.clip.w)return;
            float cov=1.0f;
            if(p.modes.y!=0) {
                int2 m=at-p.area.xy;if(m.x<0||m.y<0||m.x>=p.area.z||m.y>=p.area.w)return;
                cov=float(coverage[m.y*p.area.z+m.x])/255.0f;
            }
            if(p.modes.x==3) {
                // Aliased rectangle: pixel centers inside, as the CPU rasterizer.
                float2 c=float2(at)+0.5f;if(c.x<p.rect.x||c.x>=p.rect.z||c.y<p.rect.y||c.y>=p.rect.w)return;
            } else if(p.modes.x==4) {
                float2 c=float2(at)+0.5f;bool aa=p.modes.w!=0;
                float a=roundedCoverage(c,p.rect,p.radii,aa);
                if(p.modes.z!=0)a=clamp(a-roundedCoverage(c,p.inner,p.innerRadii,aa),0.0f,1.0f);
                cov*=floor(a*255.0f+0.5f)/255.0f;
            } else if(p.modes.x!=0) {
                float2 lo=float2(at),hi=lo+1.0f;
                float cx=clamp(min(hi.x,p.rect.z)-max(lo.x,p.rect.x),0.0f,1.0f),cy=clamp(min(hi.y,p.rect.w)-max(lo.y,p.rect.y),0.0f,1.0f);
                cov*=floor((p.modes.x==1?cx*cy:min(cx,cy))*255.0f+0.5f)/255.0f;
            }
            if(cov==0)return;
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

        // Separable Gaussian with the CPU filter's 14-bit integer weights and
        // transparent extension: identical bytes to ManagedFilters.Blur.
        struct BlurParams {
            int4 bounds; // output left,top,right,bottom (already clamped)
            int4 size; // width,height,row stride in pixels,reserved
            int4 radius; // horizontal,vertical
        };
        uint4 bytes4(uint value) {return uint4(value&255u,(value>>8)&255u,(value>>16)&255u,value>>24);}
        uint packBytes(int4 sum) {uint4 c=uint4((sum+8192)/16384);return c.x|(c.y<<8)|(c.z<<16)|(c.w<<24);}
        kernel void blurHorizontal(const device uint* source [[buffer(0)]],device uint* output [[buffer(1)]],constant int* weights [[buffer(2)]],constant BlurParams& p [[buffer(3)]],uint2 g [[thread_position_in_grid]]) {
            int x=int(g.x),y=int(g.y);if(x>=p.size.x||y>=p.size.y)return;
            int rx=p.radius.x,ry=p.radius.y;uint index=uint(y*p.size.z+x);
            if(x<p.bounds.x||x>=p.bounds.z||y<max(0,p.bounds.y-ry)||y>=min(p.size.y,p.bounds.w+ry)){output[index]=0;return;}
            int4 sum=int4(0);int row=y*p.size.z;
            for(int k=-rx;k<=rx;k++){int at=x+k;if(at<0||at>=p.size.x)continue;sum+=int4(bytes4(source[row+at]))*weights[k+rx];}
            output[index]=packBytes(sum);
        }
        kernel void blurVertical(const device uint* source [[buffer(0)]],device uint* output [[buffer(1)]],constant int* weights [[buffer(2)]],constant BlurParams& p [[buffer(3)]],uint2 g [[thread_position_in_grid]]) {
            int x=int(g.x),y=int(g.y);if(x>=p.size.x||y>=p.size.y)return;
            int ry=p.radius.y;uint index=uint(y*p.size.z+x);
            if(x<p.bounds.x||x>=p.bounds.z||y<p.bounds.y||y>=p.bounds.w){output[index]=0;return;}
            int first=max(-ry,-y),last=min(ry,p.size.y-1-y);int4 sum=int4(0);
            for(int k=first;k<=last;k++)sum+=int4(bytes4(source[(y+k)*p.size.z+x]))*weights[k+ry];
            output[index]=packBytes(sum);
        }

        // Drop shadow: colorize the blurred alpha (PixelBuffer.Multiply rounding),
        // offset it, then draw the unfiltered layer over it.
        struct ShadowParams {
            int4 size; // width,height,row stride in pixels,integral offset
            float4 offset;
            uint4 color; // straight R,G,B,A bytes
        };
        uint multiply(uint value,uint alpha) {return (value*alpha+127u)/255u;}
        float4 colorized(const device uint* blurred,int x,int y,constant ShadowParams& p) {
            if(x<0||y<0||x>=p.size.x||y>=p.size.y)return float4(0);
            uint a=multiply(blurred[y*p.size.z+x]>>24,p.color.w);
            return float4(multiply(p.color.x,a),multiply(p.color.y,a),multiply(p.color.z,a),a)/255.0f;
        }
        kernel void dropShadow(const device uint* blurred [[buffer(0)]],const device uint* source [[buffer(1)]],device uint* output [[buffer(2)]],constant ShadowParams& p [[buffer(3)]],uint2 g [[thread_position_in_grid]]) {
            int x=int(g.x),y=int(g.y);if(x>=p.size.x||y>=p.size.y)return;
            float4 shadow;
            if(p.size.w!=0)shadow=colorized(blurred,x-int(p.offset.x),y-int(p.offset.y),p);
            else {
                float2 at=float2(x,y)-p.offset.xy;int2 b=int2(floor(at));float2 f=at-float2(b);
                shadow=mix(mix(colorized(blurred,b.x,b.y,p),colorized(blurred,b.x+1,b.y,p),f.x),mix(colorized(blurred,b.x,b.y+1,p),colorized(blurred,b.x+1,b.y+1,p),f.x),f.y);
            }
            uint index=uint(y*p.size.z+x);float4 top=unpack(source[index]);
            output[index]=pack(top+shadow*(1.0f-top.a));
        }

        // Window presentation: a full-screen triangle copies the retained
        // premultiplied RGBA buffer into the CAMetalLayer drawable, which is a
        // framebuffer-only render target.
        struct PresentParams {uint width;uint height;uint flip;uint srgb;};
        struct PresentVertex {float4 position [[position]];};
        vertex PresentVertex presentVertex(uint id [[vertex_id]]) {
            float2 p=float2(float((id<<1)&2u),float(id&2u));PresentVertex o;o.position=float4(p.x*2-1,1-p.y*2,0,1);return o;
        }
        float decode(float c) {return c<=0.04045f?c/12.92f:pow((c+0.055f)/1.055f,2.4f);}
        fragment float4 presentFragment(PresentVertex v [[stage_in]],const device uint* pixels [[buffer(0)]],constant PresentParams& p [[buffer(1)]]) {
            uint x=uint(v.position.x),y=uint(v.position.y);if(x>=p.width||y>=p.height)return float4(0);
            if(p.flip!=0)y=p.height-1-y;
            float4 c=unpack(pixels[y*p.width+x]);
            if(p.srgb!=0&&c.a>0){float3 s=c.rgb/c.a;c.rgb=float3(decode(s.r),decode(s.g),decode(s.b))*c.a;}
            return c;
        }
        """;
}
