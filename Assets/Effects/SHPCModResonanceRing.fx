// ============================================================================
//SHPCModResonanceRing.fx 共振机匣驻波震荡环
//单 quad Additive；节拍命中掀起的环形震荡波
//环周驻波扇贝：sin(angle*k) 仅整数 k（k·2π 为 2π 整数倍，无接缝）；
//尾随干涉环用 dist（径向单调，无接缝）；噪声只经 frac 进 tex2D(wrap)
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

float uTime;            //帧域 ×0.045
float ringProgress;     //0~1 波前扩张进度（相对绘制半径）
float fadeAlpha;        //整体淡入淡出 0~1
float waveBoost;        //节奏层 0~1
float wavePhase;        //生命进度相位，驱动尾随干涉环外涌
float3 beatBright;      //洋红亮
float3 beatMain;        //洋红主
float3 beatDeep;        //洋红深

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    if (dist > 1.0)
        return float4(0, 0, 0, 0);

    //======== A. 驻波波前：环半径被 sin(angle*k) 扇贝调制，波腹随时间鼓动 ========
    //k=12 整数 → 环周驻波无缝；cos 时间包络与护层着色器同款驻波节拍
    float scallopAmp = (0.030 + waveBoost * 0.016) * cos(uTime * 6.0);
    float scallop = sin(angle * 12.0) * scallopAmp;
    float frontR = ringProgress + scallop;
    float frontDist = abs(dist - frontR);
    float thickness = 0.050 + (1.0 - ringProgress) * 0.030;
    float front = 1.0 - smoothstep(0.0, thickness, frontDist);
    float frontGlow = exp(-frontDist * frontDist * 500.0);

    //波腹亮斑：扇贝波腹处（|sin|→1）比波节更亮，环上出现 12 个脉动亮结
    float bellies = abs(sin(angle * 12.0));
    float bellyHot = front * (0.45 + 0.55 * bellies);

    //======== B. 尾随干涉环：波前身后按驻波频率荡开的同心细环，向外涌动 ========
    float interior = smoothstep(frontR, frontR * 0.25, dist);   //波前内侧渐入
    float ripple = sin(dist * 46.0 - wavePhase * 5.0);
    ripple = smoothstep(0.55, 1.0, ripple);                     //取正峰细环
    float ripples = ripple * interior * (0.35 + waveBoost * 0.2);
    //靠近波前的干涉环更亮，向心衰减
    ripples *= smoothstep(0.0, frontR, dist);

    //======== C. 中心拍点闪核：命中点残留的收缩亮核 ========
    float coreFlash = 1.0 - smoothstep(0.0, 0.16 * (1.0 - ringProgress * 0.6), dist);
    coreFlash *= (1.0 - ringProgress) * (1.0 - ringProgress);

    //======== D. 噪声碎屑：波前外缘拖出的能量尘（wrap 采样，无缝） ========
    float debrisNoise = tex2D(noiseSamp, frac(float2(normAngle * 6.0, dist * 2.5 - uTime * 1.2))).g;
    float debris = smoothstep(frontR + thickness * 2.5, frontR, dist)
                 * smoothstep(frontR - thickness * 4.0, frontR, dist);
    debris *= step(0.52, debrisNoise) * 0.45;

    //======== 合成 ========
    float3 color = float3(0.0, 0.0, 0.0);
    color += beatBright * bellyHot * 1.05;
    color += beatBright * frontGlow * 0.40;
    color += beatMain   * front * 0.50;
    color += beatMain   * ripples * 0.85;
    color += beatBright * coreFlash * 0.90;
    color += beatDeep   * debris;

    float alpha = saturate(
        front * 0.62
        + frontGlow * 0.30
        + ripples * 0.34
        + coreFlash * 0.5
        + debris * 0.4
    );
    alpha *= fadeAlpha;

    return float4(color * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModResonanceRingPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
