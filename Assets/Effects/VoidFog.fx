// ============================================================================
//VoidFog.fx 虚空聚落血雾着色器
//多层世界对齐的滚动柏林噪声 + 域弯曲扭流 + 局部暗金高光 + 玩家近距清场
// ============================================================================

sampler uImage0 : register(s0);//占位白纹理
sampler uNoise  : register(s1);//PerlinNoise 噪声纹理 wrap

float  uTime;//累积时间
float  uIntensity;//整体淡入淡出 0~1
float  uDensity;//密度倍率
float2 uScreenSize;//屏幕像素尺寸
float2 uWorldOffset;//Main.screenPosition 世界坐标
float2 uViewScale;//1/zoom 修正
float2 uPlayerScreen;//玩家屏幕坐标 像素
float  uClearRadius;//玩家周围完全清雾半径 像素
float  uFalloffRadius;//清雾过渡半径 像素
float4 uFogColorLow;//稀薄雾色
float4 uFogColorMid;//中浓雾色
float4 uFogColorHi;//浓厚雾色
float4 uHighlightColor;//高光色 暗金

float sampleNoise(float2 uv)
{
    return tex2D(uNoise, uv).r;
}

//域弯曲 用噪声扭曲采样坐标
float2 domainWarp(float2 uv, float t)
{
    float2 q;
    q.x = sampleNoise(uv + float2(0.0, 0.0) + t * 0.015);
    q.y = sampleNoise(uv + float2(5.2, 1.3) - t * 0.012);
    return uv + (q - 0.5) * 0.85;
}

float4 PSFog(float2 coords : TEXCOORD0, float4 vertColor : COLOR0) : COLOR0
{
    //还原像素对应的世界坐标 并归一化到噪声纹理空间
    float2 pixelPos = coords * uScreenSize;
    float2 worldPx = uWorldOffset + pixelPos * uViewScale;
    float2 baseUV = worldPx / 1600.0;

    float t = uTime;

    //域弯曲共享扭曲
    float2 wUV = domainWarp(baseUV, t);

    //四层不同尺度方向速度的噪声
    float n1 = sampleNoise(wUV * 0.9 + float2( t * 0.045, t * 0.018));
    float n2 = sampleNoise(wUV * 2.3 + float2(-t * 0.072, t * 0.055));
    float n3 = sampleNoise(wUV * 4.7 + float2( t * 0.110,-t * 0.083));
    float n4 = sampleNoise(wUV * 9.1 + float2(-t * 0.150, t * 0.130));

    float density = n1 * 0.46 + n2 * 0.28 + n3 * 0.16 + n4 * 0.10;

    //更陡峭的曲线 让雾团边缘更分明 中段不再糊成一片
    density = smoothstep(0.42, 0.82, density);

    //大尺度 patch 让部分区域几乎无雾
    float patch = sampleNoise(baseUV * 0.30 + float2(t * 0.010, t * 0.006));
    patch = smoothstep(0.18, 0.82, patch);
    density *= lerp(0.20, 1.20, patch);

    density *= uDensity * uIntensity;
    density = saturate(density);

    //=============================================================
    //玩家近距清场 距离玩家越近雾越稀薄 近处完全清空
    //smoothstep(clear, falloff, dist) 在 clear 处返回 0 在 falloff 处返回 1
    //=============================================================
    float playerDist = distance(pixelPos, uPlayerScreen);
    float proximityMask = smoothstep(uClearRadius, uFalloffRadius, playerDist);
    density *= proximityMask;

    //颜色分层 暗红 → 血红 → 暗黑红
    float3 col = lerp(uFogColorLow.rgb, uFogColorMid.rgb, smoothstep(0.10, 0.55, density));
    col = lerp(col, uFogColorHi.rgb, smoothstep(0.55, 0.95, density));

    //高光 暗金折射光斑 仅密度中段出现
    float hiNoise = sampleNoise(wUV * 3.3 + float2(-t * 0.060, t * 0.040));
    float hiSharp = sampleNoise(wUV * 6.5 + float2( t * 0.090,-t * 0.070));
    float hiMask = smoothstep(0.62, 0.88, hiNoise) * smoothstep(0.55, 0.85, hiSharp);
    float hiGate = smoothstep(0.18, 0.45, density) * (1.0 - smoothstep(0.55, 0.90, density));
    float highlight = hiMask * hiGate;

    float breath = 0.65 + 0.35 * sampleNoise(baseUV * 0.20 - float2(t * 0.018, t * 0.012));
    highlight *= breath;

    col += uHighlightColor.rgb * highlight * 1.2;

    //微小红色火星
    float spark = sampleNoise(wUV * 14.0 - float2(t * 0.180, t * 0.140));
    spark = smoothstep(0.94, 0.998, spark) * density;
    col += float3(1.0, 0.42, 0.12) * spark * 0.85;

    //alpha 使用更柔和的曲线 避免半透明区域显脏
    //gamma > 1 把低密度推向 0 减少"屏幕脏"观感
    float alpha = pow(density, 1.25);

    return float4(col, alpha);
}

technique VoidFog
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSFog();
    }
}
