// ============================================================================
// CyberEnergyOrb.fx — 赛博能量球着色器
// 多层噪声扰动 + Colormap 映射 + 菲涅尔边缘辉光 + 数字脉冲
// 对一张灰度纹理（如SoftGlow）应用，产生高质感能量球效果
// 支持领域超驱模式：高温红炽故障风格 + 黑墙撕裂
// ============================================================================

float uTime;
float fadeAlpha;
float3 coreColor;       // 最亮的内核色
float3 glowColor;       // 中间辉光色
float3 auraColor;       // 边缘外层色
float orbScale;          // 能量球的脉动缩放

// ---- 超驱参数 ----
float overdriveAmount;   // 0=正常 1=完全超驱
float glitchBurst;       // 0-1 间歇性故障爆发强度
float3 odCoreColor;      // 超驱核心色（白热）
float3 odGlowColor;      // 超驱辉光色（红炽）
float3 odAuraColor;      // 超驱光晕色（深红）

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

// SpriteBatch 自动将纹理绑定到 register(s0)
sampler baseSamp : register(s0);

// 简单哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

struct PSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    
    // 超驱色彩混合
    float od = overdriveAmount;
    float3 effCore = lerp(coreColor, odCoreColor, od);
    float3 effGlow = lerp(glowColor, odGlowColor, od);
    float3 effAura = lerp(auraColor, odAuraColor, od);

    // 以纹理中心为原点的极坐标
    float2 center = uv - 0.5;
    float dist = length(center);          // 0=中心 0.5=边缘
    float angle = atan2(center.y, center.x);
    
    // 基础灰度形状（圆形衰减）
    float baseTex = tex2D(baseSamp, uv).r;
    
    // ============================================================
    // A. 多层噪声扰动 —— 能量表面流动
    // ============================================================
    float timeMultiplier = 1.0 + od * 2.5; // 超驱时流动极度加速
    float2 noiseUV1 = float2(
        dist * 2.0 + uTime * 0.3 * timeMultiplier,
        angle * 0.318 + uTime * 0.15 * timeMultiplier
    );
    float n1 = tex2D(noiseSamp, frac(noiseUV1)).r;
    
    float2 noiseUV2 = float2(
        dist * 4.0 - uTime * 0.5 * timeMultiplier + 0.37,
        angle * 0.637 - uTime * 0.25 * timeMultiplier
    );
    float n2 = tex2D(noiseSamp, frac(noiseUV2)).g;
    
    float2 noiseUV3 = float2(
        dist * 8.0 + uTime * 0.8 * timeMultiplier,
        angle * 1.27 + uTime * 0.4 * timeMultiplier
    );
    float n3 = tex2D(noiseSamp, frac(noiseUV3)).b;
    
    float energyField = n1 * 0.5 + n2 * 0.3 + n3 * 0.2;
    
    // ============================================================
    // B. Colormap 映射 —— 径向距离+能量场决定颜色
    // ============================================================
    float radialGrad = 1.0 - smoothstep(0.0, 0.32, dist);
    float solidCore = 1.0 - smoothstep(0.0, 0.2, dist);
    radialGrad = max(radialGrad, solidCore);
    
    float cmapInput = saturate(radialGrad + (energyField - 0.5) * 0.35 * radialGrad);
    
    // 超驱时能量场扰动暴走
    cmapInput = saturate(cmapInput + od * (energyField - 0.5) * 0.5);
    
    float3 color;
    if (cmapInput < 0.35)
    {
        float t = cmapInput / 0.35;
        color = lerp(effAura * 0.5, effGlow, t);
    }
    else if (cmapInput < 0.7)
    {
        float t = (cmapInput - 0.35) / 0.35;
        color = lerp(effGlow, effCore, t);
    }
    else
    {
        //超驱时减少向纯白的过渡：保留主题核心色身份（橙/黄），
        //避免在 Additive 下与外层叠加变成纯白光球
        float t = (cmapInput - 0.7) / 0.3;
        float whiteBlend = t * t * (1.0 - od * 0.55);
        color = lerp(effCore, float3(1.0, 0.97, 0.93), whiteBlend);
    }
    
    // ============================================================
    // C. 菲涅尔边缘辉光 —— 边缘高亮环（超驱加成大幅降低）
    // ============================================================
    float fresnelInner = 1.0 - smoothstep(0.15, 0.30, dist);
    float fresnelRing = smoothstep(0.20, 0.28, dist) * (1.0 - smoothstep(0.28, 0.35, dist));
    //3.0→1.0：菲涅尔环不再翻三倍，避免边缘也跟着过曝
    float3 fresnelColor = effGlow * fresnelRing * (1.5 + od * 1.0);
    
    // ============================================================
    // D. 数字脉冲纹 —— 赛博科幻质感
    // ============================================================
    float pulseSpeed = 6.0 + od * 12.0;
    float ringPulse = sin(dist * 40.0 - uTime * pulseSpeed) * 0.5 + 0.5;
    ringPulse = pow(ringPulse, 8.0);
    ringPulse *= smoothstep(0.32, 0.10, dist) * (0.15 + od * 0.3);
    
    float rayAngle = frac(angle * 2.546 + uTime * (0.5 + od * 0.6));
    float rays = pow(abs(sin(rayAngle * 3.14159 * 6.0)), 20.0);
    rays *= smoothstep(0.30, 0.15, dist) * (0.1 + od * 0.25);
    
    // ============================================================
    // E. 表面明暗变化 —— 伪3D球体光照
    // ============================================================
    float2 lightDir = float2(-0.4, -0.5);
    float lightDot = dot(normalize(center), normalize(lightDir));
    float lighting = 0.7 + 0.3 * lightDot;
    
    // ============================================================
    // 合成
    // ============================================================
    float3 finalColor = color * lighting;
    finalColor += fresnelColor;
    finalColor += effCore * ringPulse;
    finalColor += effGlow * rays;
    
    //球体中心白热高光 —— 超驱下混入 effCore 主题色，避免叠加成纯白
    //  - 白色基底在 OD 下逐渐被主题色取代（OD=1 时 60% 主题色）
    //  - 整体强度 0.8+od*1.0 → 0.6+od*0.25，超驱时不再翻倍
    float coreHot = pow(saturate(1.0 - dist / 0.16), 2.5);
    float3 coreHotColor = lerp(float3(1.0, 0.98, 0.95), effCore, od * 0.6);
    finalColor += coreHotColor * coreHot * (0.6 + od * 0.25);
    
    // alpha
    float alpha = saturate(radialGrad * 1.5);
    alpha *= 1.0 - smoothstep(0.28, 0.34, dist);
    alpha += fresnelRing * 0.4;
    alpha = saturate(alpha) * fadeAlpha;

    // ============================================================
    // F. 超驱故障层 —— "黑墙撕裂"美学
    // 同 CyberTraceBeam.fx 的设计哲学：减少亮度叠加，强化深色撕裂、
    // 数据丢失、RGB 通道分离等"破坏式"故障，避免光球变成手电筒。
    // ============================================================
    if (od > 0.01)
    {
        float burst = glitchBurst;

        //球体遮罩，防止故障特效泄露到圆形轮廓外形成方形边际
        float orbMask = 1.0 - smoothstep(0.18, 0.32, dist);
        float orbEdgeMask = 1.0 - smoothstep(0.22, 0.34, dist);

        // F-1. RGB 通道分离（保留作为故障感关键 —— 色相扰动而非亮度扰动）
        float splitDist = od * (0.015 + burst * 0.05);
        float splitAngle = uTime * 3.5 + burst * 8.0;
        float2 splitDir = float2(cos(splitAngle), sin(splitAngle)) * splitDist;
        float rChan = tex2D(baseSamp, uv + splitDir).r;
        float bChan = tex2D(baseSamp, uv - splitDir).r;
        float3 rgbSplit;
        rgbSplit.r = finalColor.r * (0.6 + rChan * 0.7);
        rgbSplit.g = finalColor.g;
        rgbSplit.b = finalColor.b * (0.6 + bChan * 0.7);
        finalColor = lerp(finalColor, rgbSplit, od * 0.8);

        // F-2. 方块腐蚀（去掉纯白叠加，改用主题色 + 大幅降低强度）
        //  - 颜色从 (1.0, 0.94, 0.82) 改为 effGlow→effCore 渐变
        //  - 强度 (0.45 + burst*1.0) → (0.22 + burst*0.4)
        float2 blockUV = floor(uv * (10.0 + burst * 15.0)) / (10.0 + burst * 15.0);
        float blockID2 = hash21(blockUV + float2(floor(uTime * 12.0), 0.0));
        float blockThresh = 0.82 - burst * 0.35;
        float blockOn = step(blockThresh, blockID2) * orbMask;
        float3 blockCol = lerp(effGlow, effCore, burst * 0.6);
        finalColor += blockCol * blockOn * od * (0.22 + burst * 0.4);
        alpha += blockOn * od * 0.10;

        // F-3. 扫描线干扰（强度大幅降低，改用辉光色而非核心色）
        float scanlineOD = frac(uv.y * 80.0 + uTime * 2.5);
        scanlineOD = step(0.92, scanlineOD) * orbMask;
        finalColor += effGlow * scanlineOD * od * 0.4;

        // F-4. 全局闪烁（幅度大幅降低 3.5→1.2，避免整屏闪烁）
        float flickOD = hash21(float2(floor(uTime * 25.0), 9.3));
        float flickMag = burst * (flickOD - 0.3) * 1.2;
        finalColor *= 1.0 + flickMag;

        // F-5. 超驱菲涅尔增强（红炽外环 —— 加成大幅降低）
        float odRing = smoothstep(0.18, 0.28, dist) * (1.0 - smoothstep(0.28, 0.40, dist));
        finalColor += effGlow * odRing * od * (0.5 + burst * 0.6);
        alpha += odRing * od * 0.18;

        // F-6. 水平撕裂带（强化为"黑墙"：更频繁、更深、连 alpha 一起挖空）
        //  - 阈值 0.90→0.80（更频繁）
        //  - 暗化系数 0.6→0.92（接近全黑）
        //  - alpha 同步下降，制造真正的"光球缺口"
        float tearY = floor(uv.y * 15.0);
        float tearHash = hash21(float2(tearY, floor(uTime * 18.0)));
        float tearOn = step(0.80 - burst * 0.20, tearHash) * od * orbEdgeMask;
        finalColor *= 1.0 - tearOn * 0.92;
        alpha *= 1.0 - tearOn * 0.50;

        // F-7. 数据丢失黑块（NEW —— 像马赛克一样掏空光球局部）
        float2 corBlockUV = floor(uv * (12.0 + burst * 8.0)) / (12.0 + burst * 8.0);
        float corHash = hash21(corBlockUV + float2(floor(uTime * 9.0), 4.7));
        float corOn = step(0.88 - burst * 0.18, corHash) * od * orbMask;
        finalColor *= 1.0 - corOn * 0.85;
        alpha *= 1.0 - corOn * 0.40;

        // F-8. alpha 加成大幅降低（0.4→0.1）
        alpha *= 1.0 + od * 0.1;
    }
    
    return float4(finalColor * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass CyberEnergyOrbPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
