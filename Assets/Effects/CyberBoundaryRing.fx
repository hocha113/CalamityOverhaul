// ============================================================================
//CyberBoundaryRing.fx 赛博领域边界环
//采样 s0 + s1 噪声；coords 归一化圆盘 UV
//
//L1 细环 + 48 等距静态刻度(坐标系的"标尺")
//L2 环化为六段弧(六边形单元宣言) + 段隙角节点，整体超慢旋转
//L3 无常驻边界(撤墙全世界接管)：升 L3 时本环由 C# 驱动随墙飞出屏幕渐隐，
//   飞行期沿用 L2 形态权重(w3 并入 w2 处理)
//
//常驻舒适约定：无 sin 脉动，全部时间项为慢速 UV 漂移。
//极角约束：normAngle 只与整数倍角组合(4/6/7/12/48)，跨 ±π 连续。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float ringProgress;     //环在归一化空间中的位置
float ringThickness;    //环厚度(归一化)
float fadeAlpha;        //整体淡出 0~1
float3 tierWeights;     //三层几何权重(方格/蜂巢/L3)

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    float w1 = tierWeights.x;
    //L3 无常驻环，飞行期按 L2 形态呈现
    float w2 = tierWeights.y + tierWeights.z;

    //火纹噪声：扰动环边缘(幅度收敛，慢漂移)
    float n1 = tex2D(noiseTex, float2(normAngle * 4.0 + uTime * 0.30, 0.15 + uTime * 0.012)).r;
    float n2 = tex2D(noiseTex, float2(normAngle * 7.0 - uTime * 0.22, 0.55)).g;
    float noiseDisp = (n1 * 0.6 + n2 * 0.4 - 0.5) * 0.026;

    float adjDist = dist + noiseDisp;
    float signedOut = adjDist - ringProgress;   //>0 在环外侧

    //主环遮罩：L1 收细
    float thick = ringThickness * (0.70 * w1 + 1.0 * w2);
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

    //外侧火舌：L1 微弱 / L2 中等
    float tongueZone = saturate(signedOut / (thick * 3.0));
    float tongueFade = (1.0 - tongueZone) * (1.0 - tongueZone);
    float tongueNoise = tex2D(noiseTex,
        float2(normAngle * 12.0 + uTime * 0.15, adjDist * 3.0 - uTime * 0.35)).r;
    float tongue = smoothstep(0.55, 0.82, tongueNoise) * tongueFade * step(0.0, signedOut);
    tongue *= 0.30 * w1 + 0.75 * w2;

    //层级色板：权重直混(w1+w2 恒为1)
    float3 coreRed = float3(0.78, 0.10, 0.07) * w1
                   + float3(0.94, 0.22, 0.09) * w2;
    float3 hotEdge = float3(1.0, 0.44, 0.22);
    float3 emberDark = float3(0.42, 0.03, 0.04);

    //主环着色：内缘核心红 → 外缘热边
    float edgeBlend = saturate(signedOut / thick);
    float3 ringColor = lerp(coreRed, hotEdge, edgeBlend * 0.6) * (ringMask * 0.95);

    float3 tongueColor = lerp(emberDark, coreRed, tongueFade) * tongue * 0.55;
    float3 tickColor = float3(0.95, 0.30, 0.16) * tickGlow * 0.45;
    float3 cornerColor = float3(1.0, 0.50, 0.24) * cornerGlow * 0.55;

    //合成
    float3 finalColor = (ringColor + tongueColor + tickColor + cornerColor) * fadeAlpha;
    float alpha = saturate(ringMask * 0.85 + tongue * 0.30
        + tickGlow * 0.25 + cornerGlow * 0.30) * fadeAlpha;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass CyberBoundaryRingPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
