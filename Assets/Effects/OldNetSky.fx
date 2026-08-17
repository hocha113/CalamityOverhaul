// ============================================================================
//OldNetSky.fx 旧网天幕：站在墙外回望
//黑是主体：深空黑底占大头，亮度集中在四层——
//①深空渐变（头顶近黑/地平线残冷青）②双层视差星野（未熄灭的服务器，
//宏观种子确定；少量濒死红星极长周期明灭）③高空数据云薄带（缓涌，极暗）
//④墙侧地平余晖（黑墙的红晕随离墙距离衰减——"回望"构图的锚）
//全程序化零采样器；直线算术无动态分支；层间差速视差防"贴在镜头上的卡"
//AlphaBlend 预乘输出（整屏 quad，天幕在最底层，直接实底输出）
// ============================================================================

float uTime;
float uIntensity;
float2 uScreenSize;
//相机世界像素位置（视差源）
float2 uCam;
//宏观种子（同一存档星空不变）
float uSeed;
//墙右缘的屏幕x（像素，含缩放；远离墙时为大负值→余晖自然消失）
float uWallScreenX;

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//一层星野：cellPx 星格，parallax 视差系数；返回叠加色
float3 StarLayer(float2 px, float cellPx, float2 parallax, float density, float t, float heightFade)
{
    float2 sp = px + uCam * parallax;
    float2 cell = floor(sp / cellPx);
    float2 f = sp / cellPx - cell;

    float presence = hash21(cell + uSeed * 0.013);
    float gate = step(density, presence);

    //星点在格内的确定位置
    float2 starPos = float2(hash21(cell + 17.3), hash21(cell + 9.1));
    float d = length(f - starPos) * cellPx;

    float phase = hash21(cell + 5.9) * 6.2831;
    float twinkle = 0.62 + 0.38 * sin(t * (0.5 + presence) + phase);

    //濒死服务器：暗红，极长周期明灭，低谷近熄
    float dying = step(0.86, hash21(cell + 71.3));
    float slow = 0.5 + 0.5 * sin(t * 0.05 + phase * 3.0);
    float3 aliveCol = lerp(float3(0.59, 0.86, 0.92), float3(0.86, 0.94, 0.96), presence);
    float3 dyingCol = float3(0.78, 0.24, 0.18) * slow * slow;
    float3 starCol = lerp(aliveCol * twinkle, dyingCol, dying);

    float size = 1.0 + hash21(cell + 2.2) * 1.6;
    float star = exp(-d * d / (size * size));
    return starCol * star * gate * heightFade;
}

float4 PSSky(float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = uv * uScreenSize;
    float t = uTime;

    //①深空渐变：头顶近黑，地平线残一点冷青灰（黑是主体）
    float3 col = lerp(float3(0.006, 0.010, 0.022),
                      float3(0.038, 0.085, 0.10), uv.y * uv.y);

    //③高空数据云薄带：双频缓涌，极暗冷青（只在上半屏）
    float cloudBand = smoothstep(0.55, 0.15, uv.y);
    float cn = vnoise(float2(px.x * 0.0016 + uCam.x * 0.00004 + t * 0.008,
                             px.y * 0.004 + uSeed));
    float cloud = smoothstep(0.58, 0.9, cn) * cloudBand;
    col += float3(0.05, 0.12, 0.14) * cloud * 0.5;

    //②星野：远近两层差速视差，只铺上方 72% 屏幕，地平衰减
    float heightFade = smoothstep(0.78, 0.2, uv.y);
    col += StarLayer(px, 110.0, float2(0.05, 0.03), 0.72, t, heightFade) * 0.8;
    col += StarLayer(px + 37.0, 78.0, float2(0.12, 0.07), 0.62, t, heightFade);

    //④墙侧余晖：向东指数衰减的红雾 + 地平线增强（回望黑墙的锚）
    //远离墙时 uWallScreenX 为大负值，spill 自然趋零
    float dWall = px.x - uWallScreenX;
    float spill = exp(-max(dWall, 0.0) / 900.0);
    float horizon = smoothstep(0.3, 0.95, uv.y);
    float flick = 0.85 + 0.15 * vnoise(float2(px.y * 0.01, t * 0.6));
    col += float3(0.30, 0.035, 0.03) * spill * (0.4 + horizon * 0.6) * flick;

    col = saturate(col) * uIntensity;
    return float4(col, uIntensity);
}

technique TechSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSSky();
    }
}
