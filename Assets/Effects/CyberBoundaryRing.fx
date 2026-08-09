// ============================================================================
//CyberBoundaryRing.fx 赛博领域常驻边界环
//采样 s0 + s1 噪声；coords 归一化圆盘 UV
//
//三层换形(tierWeights 加权，C#端归一化)：
//  L1 细环 + 48 等距静态刻度(坐标系的"标尺")
//  L2 环化为六段弧(六边形单元宣言) + 段隙角节点，整体超慢旋转
//  L3 实心主环 + 24 根与域内辐条同相位对齐的重刻度 + 向内数据带
//    (环与场共用域心与角度基准，重刻度即辐条的边界插座)
//
//常驻舒适约定：无 sin 脉动，全部时间项为慢速 UV 漂移。
//极角约束：normAngle 只与整数倍角组合(4/6/7/12/24/48)，跨 ±π 连续。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float ringProgress;     //环在归一化空间中的位置
float ringThickness;    //环厚度(归一化)
float fadeAlpha;        //整体淡出 0~1
float3 tierWeights;     //三层几何权重(方格/蜂巢/极阵)

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    float w1 = tierWeights.x;
    float w2 = tierWeights.y;
    float w3 = tierWeights.z;

    //火纹噪声：扰动环边缘(幅度收敛，慢漂移)
    float n1 = tex2D(noiseTex, float2(normAngle * 4.0 + uTime * 0.30, 0.15 + uTime * 0.012)).r;
    float n2 = tex2D(noiseTex, float2(normAngle * 7.0 - uTime * 0.22, 0.55)).g;
    float noiseDisp = (n1 * 0.6 + n2 * 0.4 - 0.5) * (0.026 + 0.012 * w3);

    float adjDist = dist + noiseDisp;
    float signedOut = adjDist - ringProgress;   //>0 在环外侧

    //主环遮罩：L1 收细，L3 略厚
    float thick = ringThickness * (0.70 * w1 + 1.0 * w2 + 1.10 * w3);
    thick = max(thick, 0.0001);
    float ringMask = 1.0 - smoothstep(0.0, thick, abs(signedOut));
    //内侧硬切：领域内部不留残光
    float innerCut = smoothstep(-thick * 0.30, 0.0, signedOut);
    ringMask *= innerCut;

    //L2：环化为六段弧(整数倍角，超慢整体旋转)
    float segPhase = frac(normAngle * 6.0 + uTime * 0.012);
    float segMask = smoothstep(0.020, 0.055, segPhase) * smoothstep(0.980, 0.945, segPhase);
    ringMask *= lerp(1.0, segMask, w2 * 0.85);

    //L2：段隙角节点(用未分段的环径向掩码)
    float segGapDist = min(segPhase, 1.0 - segPhase);
    float cornerNode = 1.0 - smoothstep(0.0, 0.030, segGapDist);
    float cornerRadial = 1.0 - smoothstep(0.0, thick * 1.3, abs(signedOut));
    float cornerGlow = cornerNode * cornerRadial * innerCut * w2;

    //L1：48 等距静态刻度，径向略穿出环
    float tickPhase = frac(normAngle * 48.0);
    float tickDist = min(tickPhase, 1.0 - tickPhase);
    float tick = 1.0 - smoothstep(0.0, 0.06, tickDist);
    float tickRadial = 1.0 - smoothstep(thick, thick * 2.6, abs(signedOut));
    float tickGlow = tick * tickRadial * innerCut * w1;

    //L3：24 根重刻度，与域内 24 辐条同角度基准——辐条的边界插座
    //跨环内外延伸，不吃 innerCut(要向内迎接辐条)
    float tick24Phase = frac(normAngle * 24.0);
    float tick24Dist = min(tick24Phase, 1.0 - tick24Phase);
    float tick24 = 1.0 - smoothstep(0.0, 0.045, tick24Dist);
    float tick24Radial = 1.0 - smoothstep(0.0, thick * 1.25, abs(signedOut));
    float tick24Glow = tick24 * tick24Radial * w3;

    //外侧火舌：L1 微弱 / L2 中等 / L3 让位给向内数据带
    float tongueZone = saturate(signedOut / (thick * 3.0));
    float tongueFade = (1.0 - tongueZone) * (1.0 - tongueZone);
    float tongueNoise = tex2D(noiseTex,
        float2(normAngle * 12.0 + uTime * 0.15, adjDist * 3.0 - uTime * 0.35)).r;
    float tongue = smoothstep(0.55, 0.82, tongueNoise) * tongueFade * step(0.0, signedOut);
    tongue *= 0.30 * w1 + 0.75 * w2;

    //L3：向内数据带(条纹相位随时间向小半径推进=向域心流动)
    float insideAmt = saturate(-signedOut / (thick * 6.0));
    float bandEnv = (1.0 - insideAmt) * (1.0 - insideAmt) * step(0.0, -signedOut);
    float flowN = tex2D(noiseTex,
        float2(normAngle * 12.0 + uTime * 0.02, adjDist * 3.0 + uTime * 0.40)).r;
    float inwardBand = smoothstep(0.52, 0.80, flowN) * bandEnv * w3;

    //层级色板：权重直混(恒和为1)
    float3 coreRed = float3(0.78, 0.10, 0.07) * w1
                   + float3(0.92, 0.20, 0.09) * w2
                   + float3(1.0, 0.40, 0.14) * w3;
    float3 hotEdge = float3(1.0, 0.44, 0.22) * (w1 + w2)
                   + float3(1.0, 0.62, 0.32) * w3;
    float3 emberDark = float3(0.42, 0.03, 0.04);

    //主环着色：内缘核心红 → 外缘热边
    float edgeBlend = saturate(signedOut / thick);
    float3 ringColor = lerp(coreRed, hotEdge, edgeBlend * 0.6) * (ringMask * 0.95);

    float3 tongueColor = lerp(emberDark, coreRed, tongueFade) * tongue * 0.55;
    float3 bandColor = float3(1.0, 0.30, 0.12) * inwardBand * 0.60;
    float3 tickColor = float3(0.95, 0.30, 0.16) * tickGlow * 0.45;
    float3 cornerColor = float3(1.0, 0.50, 0.24) * cornerGlow * 0.55;
    float3 tick24Color = float3(1.0, 0.46, 0.20) * tick24Glow * 0.60;

    //合成
    float3 finalColor = (ringColor + tongueColor + bandColor + tickColor
        + cornerColor + tick24Color) * fadeAlpha;
    float alpha = saturate(ringMask * 0.85 + tongue * 0.30 + inwardBand * 0.32
        + tickGlow * 0.25 + cornerGlow * 0.30 + tick24Glow * 0.35) * fadeAlpha;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass CyberBoundaryRingPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
