// ============================================================================
//ArchitectureWarp.fx 虚空聚落建筑时空扭曲/崩解
//采样 uImage0 建筑贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float visibility;     //0隐 1显 崩解/凝结总进度
float warpStrength;   //额外扭曲强度 0~1
float2 texelSize;     //1/宽 1/高

//简易1D/2D哈希
float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

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
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    //崩解进度：0=完全显示，1=完全隐身
    float dissolve = saturate(1.0 - visibility);
    //主体扭曲强度：演出warpStrength为主，崩解进度本身也额外贡献一部分
    float warp = saturate(warpStrength + dissolve * 0.5);

    //1. 连续扭曲偏移：两层正弦+噪声的叠加，模拟时空褶皱
    float2 warpUV = coords;
    float timeSlow = uTime * 0.9;
    float2 n1 = float2(
        sin(coords.y * 24.0 + timeSlow * 3.1) * 0.5 + valueNoise(coords * 6.0 + timeSlow) - 0.5,
        cos(coords.x * 18.0 - timeSlow * 2.4) * 0.5 + valueNoise(coords * 4.0 - timeSlow * 0.7) - 0.5
    );
    warpUV += n1 * texelSize * 30.0 * warp;

    //2. 行撕裂：按行随机瞬间偏移，块粒度随演出强度变细
    float timeTick = floor(uTime * 14.0);
    float rowPx = lerp(14.0, 4.0, warp);
    float rowIdx = floor(coords.y / (texelSize.y * rowPx));
    float rowRand = hash21(float2(rowIdx, timeTick));
    float rowActive = step(lerp(0.78, 0.35, warp), rowRand);
    float rowShift = (rowRand - 0.5) * 2.0 * texelSize.x * lerp(20.0, 75.0, warp * warp);
    warpUV.x += rowShift * rowActive;

    //3. 块状错位：整块矩形区间整体漂移
    float blockPx = lerp(46.0, 14.0, warp);
    float2 blockIdx = floor(coords / (texelSize * blockPx));
    float blockRand = hash21(blockIdx + timeTick * 0.73);
    float blockActive = step(lerp(0.92, 0.55, warp), blockRand);
    float2 blockShift = float2(
        (hash11(blockRand * 7.13) - 0.5) * texelSize.x * 34.0,
        (hash11(blockRand * 3.77) - 0.5) * texelSize.y * 16.0
    ) * warp;
    warpUV += blockShift * blockActive;

    //4. RGB 通道分离
    float split = lerp(1.5, 10.0, warp) * texelSize.x;
    float ang = uTime * 2.6;
    float2 rOff = float2(cos(ang), sin(ang)) * split;
    float2 bOff = float2(cos(ang + 2.09), sin(ang + 2.09)) * split;

    float4 colR = tex2D(uImage0, warpUV + rOff);
    float4 colG = tex2D(uImage0, warpUV);
    float4 colB = tex2D(uImage0, warpUV + bOff);

    float4 color;
    color.r = colR.r;
    color.g = colG.g;
    color.b = colB.b;
    color.a = (colR.a + colG.a + colB.a) / 3.0;

    if (color.a < 0.01)
        return float4(0.0, 0.0, 0.0, 0.0);

    //5. 溶解蒙版：两层噪声阈值，threshold=dissolve，低于阈值直接裁掉
    float dissolveNoise = valueNoise(coords * 22.0) * 0.6
                        + valueNoise(coords * 6.0 + uTime * 0.2) * 0.4;
    //边缘溶解：距离阈值越近，透明度越低，模拟逐像素燃烧
    float edgeDist = dissolveNoise - dissolve;
    if (edgeDist < 0.0)
        return float4(0.0, 0.0, 0.0, 0.0);

    //近阈值处加一条琥珀色边缘辉光，强化"崩解/凝结"的燃烧感
    float edgeBand = smoothstep(0.0, 0.12, edgeDist);
    float3 edgeGlow = float3(1.35, 0.85, 0.45);
    color.rgb = lerp(edgeGlow * (color.a), color.rgb, edgeBand);

    //6. 时空偏冷调：演出越强越偏冷蓝，稳定后回归正常
    float coolMix = warp * 0.35;
    float3 coolTint = color.rgb * float3(0.72, 0.82, 1.05);
    color.rgb = lerp(color.rgb, coolTint, coolMix);

    //7. 扫描线：轻微压暗偶数行，突出复古显像管撕裂感
    float scan = 0.92 + 0.08 * sin(coords.y / texelSize.y * 3.14159);
    color.rgb *= lerp(1.0, scan, warp * 0.5);

    //8. 整体透明度按visibility调制，避免溶解噪点与主体同样透明
    color.a *= visibility;
    color.rgb *= color.a;  //预乘alpha，保持边缘辉光正确参与SpriteBatch的AlphaBlend
    return color;
}

technique Tech
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
