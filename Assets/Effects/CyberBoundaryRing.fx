// ============================================================================
// CyberBoundaryRing.fx 赛博领域常驻边界环
// 采样 s0 + s1 噪声；coords 归一化圆盘 UV
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float ringProgress;     //环在归一化空间中的位置
float ringThickness;    //环厚度(归一化)
float fadeAlpha;        //整体淡出 0~1
float layerTier;        //视觉层级 1~3(连续插值)

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    float tier = clamp(layerTier, 1.0, 3.0);
    float t2 = saturate(tier - 1.0);    //T2 装饰成分
    float t3 = saturate(tier - 2.0);    //T3 装饰成分

    // 火纹噪声：扰动环边缘，活跃度随层级提升
    float noiseSpeed = 1.2 + tier * 0.6;
    float n1 = tex2D(noiseTex, float2(normAngle * 4.0 + uTime * noiseSpeed * 0.45, 0.15 + uTime * 0.02)).r;
    float n2 = tex2D(noiseTex, float2(normAngle * 7.0 - uTime * noiseSpeed * 0.30, 0.55)).g;
    float noiseDisp = (n1 * 0.6 + n2 * 0.4 - 0.5) * (0.040 + 0.018 * t2 + 0.014 * t3);

    float adjDist = dist + noiseDisp;
    float signedOut = adjDist - ringProgress;   //>0 在环外侧

    // 主环遮罩
    float ringMask = 1.0 - smoothstep(0.0, ringThickness, abs(signedOut));
    //内侧硬切：环内侧快速消失，领域内部不留任何残光
    float innerCut = smoothstep(-ringThickness * 0.30, 0.0, signedOut);
    ringMask *= innerCut;

    // 层级色板：层级越高色温越热
    float3 coreRed = lerp(float3(0.78, 0.10, 0.07), float3(0.95, 0.24, 0.09), t2);
    coreRed = lerp(coreRed, float3(1.0, 0.46, 0.15), t3);
    float3 hotEdge = lerp(float3(1.0, 0.42, 0.22), float3(1.0, 0.68, 0.36), saturate((tier - 1.0) * 0.5));
    float3 emberDark = float3(0.42, 0.03, 0.04);

    // 主环着色：内缘核心红 → 外缘热边
    float edgeBlend = saturate(signedOut / max(ringThickness, 0.0001));
    float3 ringColor = lerp(coreRed, hotEdge, edgeBlend * 0.6) * (ringMask * 0.85);

    //环上微观数字刻纹（保留赛博身份，密度收敛）
    float gridA = frac(normAngle * 36.0);
    float gridMask = smoothstep(0.035, 0.0, min(gridA, 1.0 - gridA));
    ringColor += float3(0.25, 0.05, 0.03) * gridMask * ringMask * 0.22;

    // 外侧火舌：径向流动条纹，由环向外渐隐
    float tongueZone = saturate(signedOut / (ringThickness * 3.0));
    float tongueFade = (1.0 - tongueZone) * (1.0 - tongueZone);
    float tongueNoise = tex2D(noiseTex,
        float2(normAngle * 12.0 + uTime * 0.25, adjDist * 3.0 - uTime * (0.55 + 0.35 * t2))).r;
    float tongue = smoothstep(0.52, 0.80, tongueNoise) * tongueFade * step(0.0, signedOut);
    float3 tongueColor = lerp(emberDark, coreRed, tongueFade) * tongue * (0.55 + 0.25 * t2);

    // T2：热浪光弦（贴主环外侧的细亮线，非独立圆圈）
    float bandCenter = ringThickness * 1.30;
    float bandMask = 1.0 - smoothstep(0.0, ringThickness * 0.30, abs(signedOut - bandCenter));
    bandMask *= 0.72 + 0.28 * sin(uTime * 2.0 + normAngle * 25.1327);
    bandMask *= t2;
    float3 bandColor = hotEdge * bandMask * 0.40;

    // T3：旋转分段科技弧线
    float arcCenter = ringThickness * 2.1;
    float arcMask = 1.0 - smoothstep(0.0, ringThickness * 0.20, abs(signedOut - arcCenter));
    float arcPhase = frac(normAngle * 6.0 - uTime * 0.09);
    float arcSeg = smoothstep(0.04, 0.10, arcPhase) * smoothstep(0.60, 0.52, arcPhase);
    arcMask *= arcSeg * t3;
    float3 arcColor = float3(1.0, 0.60, 0.28) * arcMask * 0.55;

    // 合成
    float3 finalColor = (ringColor + tongueColor + bandColor + arcColor) * fadeAlpha;
    float alpha = saturate(ringMask * 0.80 + tongue * 0.35 + bandMask * 0.25 + arcMask * 0.35) * fadeAlpha;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass CyberBoundaryRingPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
