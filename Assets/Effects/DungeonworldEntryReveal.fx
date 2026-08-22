// ============================================================================
//DungeonworldEntryReveal.fx 地牢子世界入场揭示，落底后棺门自中央竖缝向两侧推开
//uReveal: 0=闭合黑幕;0..1 推开(CPU 已做 SmoothStep 缓动);>1 残余黑角淡出
//预乘输出进 AlphaBlend:黑幕本体 rgb=0,金缘与顶光加进 rgb
//直线算术,无动态分支,无采样器;参数接口与 CybCourseEntryReveal 同构(uTime/uReveal/uAspectRatio)
// ============================================================================

float uTime;
float uReveal;
float uAspectRatio;

#define CANDLE_HI float3(1.0000, 0.9137, 0.7216)
#define COLDLIGHT float3(0.7800, 0.8500, 1.0000)

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 PSDungeonworldEntryReveal(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = float2((uv.x - 0.5) * uAspectRatio, uv.y);
    float open01 = saturate(uReveal);
    float fade = saturate((uReveal - 1.0) / 0.18);

    //门缝半宽:随 reveal 推开到全屏外
    float halfOpen = open01 * (0.5 * uAspectRatio + 0.08);
    //石门缘毛口:沿 y 的双频噪声啃边,棺门不是刀切直线
    float torn = (valueNoise(float2(uv.y * 26.0, 3.7)) - 0.5) * 0.022
               + (valueNoise(float2(uv.y * 90.0, 9.1)) - 0.5) * 0.008;
    float doorD = abs(p.x) - halfOpen + torn;

    float cover = smoothstep(-0.012, 0.010, doorD);
    float alpha = cover * (1.0 - fade);

    //门缘受光一线金
    float rim = exp(-abs(doorD) * 70.0) * saturate(uReveal * 3.0) * (1.0 - fade);
    //顶光柱下压(教堂天光,只照进已揭开区域)
    float shaftEnv = smoothstep(0.06, 0.5, uReveal) * (1.0 - fade);
    float shaft = exp(-abs(p.x) * 2.6) * exp(-uv.y * 2.4) * (1.0 - cover) * shaftEnv;

    float3 glow = CANDLE_HI * rim * 0.5
                + COLDLIGHT * shaft * 0.30
                + CANDLE_HI * shaft * shaft * 0.12;

    return float4(glow, alpha);
}

technique DungeonworldEntryReveal
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSDungeonworldEntryReveal();
    }
}
