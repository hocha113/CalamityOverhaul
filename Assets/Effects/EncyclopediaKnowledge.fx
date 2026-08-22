// ============================================================================
//EncyclopediaKnowledge.fx 海洋百科·全知仪式
//焦散水盘 + 双向符文环 + 知识核心 + 神光放射 + 完成冲击波；单 DrawCall
//SpriteBatch Additive 预乘 alpha；角向纹样仅用整数次谐波 sin(k*angle) 以避免极坐标接缝
// ============================================================================

sampler uImage0 : register(s0);

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;
float uRingScale;       //仪式环归一化半径 0~1
float uCoreIntensity;   //知识核心强度 0~1
float uShock;           //完成冲击波进度 0~1（0=未激活）
float uFade;            //整体淡出 0~1
float3 deepColor;       //深海盘底色
float3 glowColor;       //主辉光（环线/核晕）
float3 causticColor;    //焦散与高光色
float3 runeColor;       //符文色

//焦散水网：双层笛卡尔域扭曲取最小值，输入为笛卡尔坐标，天然连续，无极坐标接缝
float causticField(float2 p, float t)
{
    float2 w1 = tex2D(noiseSamp, frac(p * 0.5 + t * float2(0.020, 0.015))).rg * 0.40;
    float n1 = tex2D(noiseSamp, frac(p * 0.9 + w1 + t * float2(0.010, 0.012))).r;
    float2 w2 = tex2D(noiseSamp, frac(p * 0.6 - t * float2(0.018, 0.022) + 0.5)).gb * 0.40;
    float n2 = tex2D(noiseSamp, frac(p * 1.1 + w2 - t * float2(0.013, 0.009))).g;
    return pow(min(n1, n2), 0.5) * 1.7;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vcol : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;       //-1 ~ 1
    float dist = length(centered);              //0=中心，1=绘制区边缘
    float angle = atan2(centered.y, centered.x);

    float ring = max(uRingScale, 0.001);
    //圆形绘制边界
    float boundary = 1.0 - smoothstep(0.985, 1.0, dist);
    if (boundary <= 0.001 && uShock <= 0.001)
        return float4(0, 0, 0, 0);

    float3 col = float3(0, 0, 0);
    float a = 0.0;

    //======== A. 焦散水盘（仪式环内部填充）========
    float innerMask = 1.0 - smoothstep(ring * 0.86, ring, dist);
    if (innerMask > 0.001)
    {
        float depth = smoothstep(0.0, ring, dist);
        float3 oceanBase = lerp(deepColor * 1.5, deepColor, depth);
        float caus = causticField(centered * 2.2, uTime);
        float causMask = smoothstep(ring, ring * 0.1, dist);
        col += oceanBase * innerMask * 0.5;
        col += causticColor * caus * causMask * 0.32 * innerMask;
        a = max(a, innerMask * lerp(0.16, 0.42, depth));
    }

    //======== B. 双向符文环（仪式边界）========
    float ringDist = abs(dist - ring);
    //柔和高斯辉光环
    float mainGlow = exp(-ringDist * ringDist * 900.0);
    //锐利主环线
    float sharp = 1.0 - smoothstep(0.0, 0.006, ringDist);
    //外圈细密符文 + 内圈反向符文：整数次谐波 sin(k*angle) 连续无缝
    float runeOuter = 0.5 + 0.5 * sin(angle * 36.0 - uTime * 1.6);
    float runeInner = 0.5 + 0.5 * sin(angle * 18.0 + uTime * 1.1);
    float bandOuter = exp(-pow((dist - ring) * 24.0, 2.0));
    float bandInner = exp(-pow((dist - ring * 0.9) * 26.0, 2.0));
    float runes = pow(runeOuter, 6.0) * bandOuter + pow(runeInner, 5.0) * bandInner;
    float breathe = 0.7 + 0.3 * sin(uTime * 2.0);

    col += glowColor * mainGlow * breathe * 0.9;
    col += causticColor * sharp * 1.1;
    col += runeColor * runes * 0.85;
    a = max(a, saturate(mainGlow * 0.8 + sharp + runes * 0.7));

    //======== C. 知识核心 ========
    if (uCoreIntensity > 0.001)
    {
        float ci = uCoreIntensity;
        float coreHot = exp(-dist * 6.0);                  //白热内核
        float halo = exp(-dist * 2.2);                     //外晕
        //旋转神光臂：整数次谐波连续
        float rays = 0.5 + 0.5 * sin(angle * 6.0 + uTime * 2.5);
        rays = pow(rays, 5.0) * exp(-dist * 3.0);
        float corePulse = 0.8 + 0.2 * sin(uTime * 5.0);

        col += causticColor * coreHot * ci * 1.6 * corePulse;
        col += glowColor * halo * ci * 0.6;
        col += causticColor * rays * ci * 0.7;
        a = max(a, saturate((coreHot * 1.4 + halo * 0.5 + rays * 0.5) * ci));

        //======== D. 神光放射（向外）========
        float godray = 0.5 + 0.5 * sin(angle * 12.0 - uTime * 1.3);
        godray = pow(godray, 8.0)
               * smoothstep(ring * 1.05, ring * 0.4, dist)
               * smoothstep(0.1, 0.5, dist);
        col += glowColor * godray * 0.35 * ci;
        a = max(a, godray * 0.3 * ci);
    }

    //======== E. 完成冲击波 ========
    if (uShock > 0.001)
    {
        float sr = uShock;                       //0~1 扩散半径
        float sdist = abs(dist - sr);
        float shockRing = exp(-sdist * sdist * 350.0) * (1.0 - sr);
        col += causticColor * shockRing * 2.0;
        a = max(a, shockRing * (1.0 - sr));
    }

    a = saturate(a) * uFade;
    return float4(col * a, a) * vcol;
}

technique Technique1
{
    pass EncyclopediaKnowledgePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
