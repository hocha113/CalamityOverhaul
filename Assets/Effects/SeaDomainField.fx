// ============================================================================
// SeaDomainField.fx 海洋领域场
// s0+s1 噪声；单 DrawCall 全领域
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
float fadeAlpha;        //整体淡出 0~1
float contentFade;     //填充内容淡化系数 0~1（玩家移动时降低，环形边界不受影响）
float layerCount;       //领域层数 1~10
float layerRadii[10];   //各层归一化半径 (0~1范围，1=绘制区域边缘)

float3 deepColor;       //深海核心色
float3 shallowColor;    //浅层过渡色
float3 causticColor;    //焦散光纹色
float3 ringInnerColor;  //内层环色
float3 ringOuterColor;  //外层环色

// 焦散光网生成
// 双层域扭曲纹理噪声取最小值，模拟水面折射产生的明暗光网
float causticPattern(float2 p, float time)
{
    //第一层域扭曲
    float2 warpUV = float2(p.x * 0.3 + time * 0.025, p.y * 0.3 + time * 0.018);
    float2 warp = tex2D(noiseSamp, frac(warpUV)).rg * 0.35;
    float n1 = tex2D(noiseSamp, frac(p * 0.7 + warp + time * float2(0.012, 0.009))).r;

    //第二层域扭曲（不同方向和频率）
    float2 warpUV2 = float2(p.x * 0.35 - time * 0.02, p.y * 0.4 + time * 0.028);
    float2 warp2 = tex2D(noiseSamp, frac(warpUV2 + 0.5)).gb * 0.35;
    float n2 = tex2D(noiseSamp, frac(p * 0.8 + warp2 - time * float2(0.014, 0.01))).g;

    //取最小值产生沃罗诺伊式的焦散亮纹
    float c = min(n1, n2);
    return pow(c, 0.5) * 1.6;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;  // -1 ~ 1
    float dist = length(centered);           // 0=中心, 1=边缘
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318; // 0~1

    // ======== 域边界裁剪 ========
    float edgeFade = 1.0 - smoothstep(0.86, 1.0, dist);
    if (edgeFade <= 0.001)
        return float4(0, 0, 0, 0);

    // ======== A. 深海底色渐变 ========
    //中心深暗，向外渐浅
    float depthGrad = smoothstep(0.0, 0.85, dist);
    float3 baseOcean = lerp(deepColor, shallowColor, depthGrad);

    //纹理噪声注入有机变化，避免纯色块感
    float2 flowUV = float2(
        centered.x * 0.4 + uTime * 0.015,
        centered.y * 0.4 + uTime * 0.012
    );
    float baseFlow = tex2D(noiseSamp, frac(flowUV)).r;
    baseOcean *= 0.82 + baseFlow * 0.36;

    // ======== B. 焦散光网 ========
    float caustic = causticPattern(centered * 1.5, uTime);
    //中心区域焦散更明亮（光线从上方穿透水面）
    float causticMask = smoothstep(1.0, 0.15, dist);
    float3 causticContrib = causticColor * caustic * causticMask * 0.4;

    // ======== C. 暗流涌动（径向+切向水流）========
    //极坐标空间的流动纹理
    float2 polarFlow = float2(normAngle * 6.0 + uTime * 0.22, dist * 4.0 - uTime * 0.15);
    float flowField = tex2D(noiseSamp, frac(polarFlow)).r;
    float2 polarFlow2 = float2(normAngle * 10.0 - uTime * 0.18, dist * 6.0 + uTime * 0.13);
    float flowField2 = tex2D(noiseSamp, frac(polarFlow2)).g;
    float flow = (flowField + flowField2) * 0.5;
    flow = smoothstep(0.35, 0.65, flow) * 0.18;

    // ======== D. 层级边界环 ========
    float ringBrightness = 0.0;
    float ringGlowTotal = 0.0;
    int iLayerCount = (int)layerCount;

    for (int i = 0; i < 10; i++)
    {
        if (i >= iLayerCount)
            break;

        float lr = layerRadii[i];
        float layerProgress = (float)i / max(1.0, layerCount - 1.0);

        //三频波浪扰动（每层独立相位和速度）
        float waveSpeed = 2.5 * (1.0 - layerProgress * 0.4);
        float wave = sin(angle * 2.0 + uTime * waveSpeed + lr * 12.0) * 0.016;
        wave += sin(angle * 5.0 - uTime * waveSpeed * 0.7 + lr * 25.0) * 0.007;
        wave += sin(angle * 8.0 + uTime * waveSpeed * 0.4 + lr * 40.0) * 0.003;

        //噪声扰动使波浪更有机
        float edgeNoise = tex2Dlod(noiseSamp, float4(normAngle * 4.0 + uTime * 0.3 + lr * 2.0, lr, 0, 0)).r;
        wave += (edgeNoise - 0.5) * 0.025;

        float adjDist = dist + wave;
        float ringDist = abs(adjDist - lr);

        //锐利主环线
        float ring = 1.0 - smoothstep(0.0, 0.01, ringDist);
        //柔和高斯辉光
        float glow = exp(-ringDist * ringDist * 1200.0);
        //呼吸脉动
        float pulse = 0.7 + 0.3 * sin(uTime * 1.5 + lr * 15.0);

        ringBrightness += ring * 0.85 * pulse;
        ringGlowTotal += glow * 0.45 * pulse;
    }
    ringBrightness = saturate(ringBrightness);
    ringGlowTotal = saturate(ringGlowTotal);

    //环颜色随径向位置渐变
    float3 currentRingColor = lerp(ringInnerColor, ringOuterColor, saturate(dist));

    // ======== E. 深海微光（生物荧光粒子）========
    float2 sparkleUV1 = float2(centered.x * 4.0 + uTime * 0.12, centered.y * 4.0 - uTime * 0.08);
    float2 sparkleUV2 = float2(centered.x * 6.5 - uTime * 0.09, centered.y * 6.5 + uTime * 0.11);
    float s1 = tex2D(noiseSamp, frac(sparkleUV1)).r;
    float s2 = tex2D(noiseSamp, frac(sparkleUV2)).g;
    float sparkle = pow(abs(s1 * s2), 3.5) * 5.0;
    sparkle *= smoothstep(1.0, 0.35, dist);

    // ======== F. 光柱穿透（从上方照入水中的柔和神光）========
    float2 rayUV = float2(centered.x * 1.2 + uTime * 0.055, centered.y * 0.6 + uTime * 0.07);
    float rayNoise = tex2D(noiseSamp, frac(rayUV)).r;
    float godRay = smoothstep(0.52, 0.73, rayNoise) * 0.28;
    //仅上半球，模拟光从水面照下
    godRay *= smoothstep(0.2, -0.7, centered.y);
    godRay *= smoothstep(1.0, 0.25, dist);

    // ======== 合成 ========
    //填充内容随玩家移动淡化，环形边界不受影响
    float cf = contentFade;
    float3 finalColor = baseOcean * 0.6 * cf;
    finalColor += causticContrib * cf;
    finalColor += shallowColor * flow * cf;
    finalColor += currentRingColor * (ringBrightness + ringGlowTotal);
    finalColor += causticColor * sparkle * 0.15 * cf;
    finalColor += float3(0.55, 0.8, 1.0) * godRay * cf;

    //透明度合成：环线使用fadeAlpha，填充内容额外乘contentFade
    float ringAlpha = (ringBrightness + ringGlowTotal) * 0.4;
    float fillAlpha = lerp(0.2, 0.55, depthGrad) * cf
        + caustic * causticMask * 0.08 * cf
        + sparkle * 0.05 * cf;
    float fieldAlpha = saturate(ringAlpha + fillAlpha);

    float alpha = edgeFade * fadeAlpha * fieldAlpha;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SeaDomainFieldPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
