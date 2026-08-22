// ============================================================================
// EowAcid.fx 世界吞噬者腐蚀酸液
// TechGlob: 飞行酸团，SDF噪声摆动轮廓+表面张力亮缘+悬浮渣点+各向异性高光
// TechPool: 地面残留酸池，液面弯月带+上浮泡+面下反光+噪声蚀边干涸
// 全部笛卡尔/半径场取样，无极角无分支；预乘输出，AlphaBlend 批
// ps_3_0
// ============================================================================

float uTime;
float uSeed;
float uStretch;    //Glob 速度拉伸 0.7~2.1
float uIntensity;  //总强度(淡入)
float uLife;       //Pool 0新鲜→1干涸
float uAspect;     //Pool 宽高比
float3 uColorDeep;
float3 uColorBright;

//哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//双倍频噪声
float fbm2(float2 p)
{
    float v = valueNoise(p) * 0.62;
    v += valueNoise(p * 2.13 + 17.7) * 0.38;
    return v;
}

// ----------------------------------------------------------------------------
// 酸团
// ----------------------------------------------------------------------------
float4 GlobPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;

    float r = length(p);

    //轮廓摆动：滚动笛卡尔噪声(液面蠕动)
    float wobble = (valueNoise(p * 2.3 + float2(uTime * 0.9, -uTime * 0.7) + uSeed * 17.0) - 0.5) * 0.24;
    float edge = r + wobble;

    //主体与张力亮缘(半径护栏：摆动幅度下也不触及quad边中点)
    float body = smoothstep(0.97, 0.6, edge) * smoothstep(1.0, 0.92, r);
    float rim = exp(-pow((edge - 0.72) * 7.5, 2.0));

    //悬浮渣点(内含腐化组织碎屑，更深色)
    float motes = smoothstep(0.66, 0.84, valueNoise(p * 5.2 + uSeed * 31.0 + float2(0.0, uTime * 0.4)));

    //各向异性高光：固定左上光位，沿长轴压扁
    float2 hp = p - float2(-0.3, -0.34);
    hp.x *= 2.3;
    float gloss = exp(-dot(hp, hp) * 8.5);

    float3 col = uColorDeep * body;
    col = lerp(col, uColorDeep * 0.45, motes * body * 0.85);
    col += uColorBright * rim * 0.8 * body;
    col += uColorBright * gloss * 0.5 * body;

    float alpha = saturate(body * 0.92 + rim * 0.2 * body);
    return float4(col * uIntensity, alpha * uIntensity);
}

// ----------------------------------------------------------------------------
// 酸池
// ----------------------------------------------------------------------------
float4 PoolPS(float2 uv : TEXCOORD0) : COLOR0
{
    float x = uv.x;
    float y = uv.y;
    float ax = x * uAspect;

    //液面高度起伏
    float surfY = 0.3 + (valueNoise(float2(ax * 1.5 + uTime * 0.55, uSeed * 23.0)) - 0.5) * 0.08;

    //端部弯月收窄(表面张力挂边)
    float endBite = smoothstep(0.0, 0.14, x) * smoothstep(1.0, 0.86, x);

    //液体区域(液面以下，底部渐隐入地)
    float below = smoothstep(surfY - 0.02, surfY + 0.08, y);
    float bottomFade = smoothstep(1.0, 0.5, y);
    float bodyMask = below * bottomFade * endBite;

    //上浮泡：网格噪声上卷
    float bub = smoothstep(0.74, 0.9, valueNoise(float2(ax * 3.2 + uSeed * 7.0, y * 2.4 - uTime * 1.4)));

    //面下各向异性反光带(窄横带，不做圆斑)
    float sheenBand = exp(-pow((y - surfY - 0.12) * 8.5, 2.0));
    float sheen = sheenBand * (0.4 + 0.6 * valueNoise(float2(ax * 2.1 - uTime * 0.9, uSeed * 3.0)));

    //液面亮线
    float surfLine = exp(-pow((y - surfY) * 20.0, 2.0));

    //干涸蚀边：噪声阈值自随机处啃穿
    float dryNoise = fbm2(float2(ax * 2.4, y * 1.8) + uSeed * 41.0);
    float erode = smoothstep(uLife * 1.25, uLife * 1.25 + 0.16, 0.6 + (dryNoise - 0.5) * 0.85);

    float3 col = uColorDeep * bodyMask * 0.95;
    //弯月边缘更暗更饱和
    col *= lerp(1.0, 0.66, 1.0 - endBite);
    col += uColorBright * surfLine * endBite * 0.8;
    col += uColorBright * sheen * bodyMask * 0.3;
    col += uColorBright * bub * bodyMask * 0.45;

    float alpha = saturate(bodyMask * 0.9 + surfLine * endBite * 0.28);
    col *= erode;
    alpha *= erode;

    return float4(col * uIntensity, alpha * uIntensity);
}

technique TechGlob
{
    pass P0
    {
        PixelShader = compile ps_3_0 GlobPS();
    }
}

technique TechPool
{
    pass P0
    {
        PixelShader = compile ps_3_0 PoolPS();
    }
}
