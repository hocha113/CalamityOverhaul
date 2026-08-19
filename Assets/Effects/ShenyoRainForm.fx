// ============================================================================
//ShenyoRainForm.fx
//BlurTech: 雨痕密度遮罩的可分离高斯模糊（同 HimayoPortraitAssembly 结构）
//CompositeTech: 黑雨汇聚成形——密度场显形出黑水剪影，未定形区垂坠拉丝，
//               澄清前沿滞后于显形前沿，黑水退去露出立绘本色
// ============================================================================

sampler uImage0 : register(s0);
sampler uMask : register(s1);
sampler uNoise : register(s2);

float2 uDelta;
float2 uTexelSize;
float uProgress;
float uTime;
float3 uMurkColor;   //黑水浊色：近黑带青壳，不是纯黑
float3 uEdgeColor;   //成形边水膜：溺月惨白
float3 uStreakColor; //径流水光：湿墨冷青

static const float gw[7] = { 0.1964, 0.1747, 0.1216, 0.0662, 0.0281, 0.0093, 0.0024 };

float4 BlurPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float density = tex2D(uImage0, coords).a * gw[0];
    [unroll]
    for (int i = 1; i < 7; i++)
    {
        float2 offset = uDelta * i;
        density += tex2D(uImage0, coords + offset).a * gw[i];
        density += tex2D(uImage0, coords - offset).a * gw[i];
    }
    return float4(density, density, density, density);
}

float4 CompositePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float progress = saturate(uProgress);
    //定形度：后段收束，水的形变与材质随之归零
    float settle = smoothstep(0.72, 0.97, progress);
    float loose = 1.0 - settle;

    //噪声三路：蠕动 na、拉丝 nb、竖向拉长向下滚动的径流纹 rivulet
    float2 noiseUVa = coords * float2(2.4, 3.6) + float2(uTime * 0.05, -uTime * 0.04);
    float2 noiseUVb = coords * float2(5.2, 2.0) + float2(-uTime * 0.04, uTime * 0.06);
    float na = tex2D(uNoise, noiseUVa).r;
    float nb = tex2D(uNoise, noiseUVb).r;
    float rivulet = tex2D(uNoise, float2(coords.x * 9.0, coords.y * 0.85 - uTime * 0.42)).r;

    //未定形区采样点上移=内容下垂（水的垂坠拉丝），横向随噪声蠕动
    float2 warped = coords;
    warped.y -= loose * (na - 0.38) * 0.10;
    warped.x += loose * sin(coords.y * 19.0 + uTime * 4.6 + nb * 6.0) * 0.010;

    float4 portrait = tex2D(uImage0, warped);
    if (portrait.a <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    //雨痕密度 + 邻域膨胀（湿痕自身向外洇开一圈）
    float density = tex2D(uMask, warped).r;
    float neighborDensity = max(
        max(tex2D(uMask, warped + float2(uTexelSize.x * 2.0, 0)).r,
            tex2D(uMask, warped - float2(uTexelSize.x * 2.0, 0)).r),
        max(tex2D(uMask, warped + float2(0, uTexelSize.y * 2.0)).r,
            tex2D(uMask, warped - float2(0, uTexelSize.y * 2.0)).r));
    density = max(density, neighborDensity * 0.85);

    //自上而下的补全前沿：伞顶先积水，末段兜底把孔洞收满；径流纹给前沿毛边
    float sweepT = smoothstep(0.28, 0.94, progress);
    float sweepY = lerp(-0.10, 1.10, sweepT);
    float sweepJitter = (nb - 0.5) * 0.12 * (1.0 - sweepT) + (rivulet - 0.5) * 0.05;
    float sweepField = 1.0 - smoothstep(sweepY - 0.04, sweepY + 0.07, coords.y + sweepJitter);
    float field = max(density * 1.35, sweepField);

    //显形（黑水抵达）与澄清（黑水退去见本色）双前沿，澄清滞后半拍
    float reveal = smoothstep(0.27, 0.45, field);
    float clarityFront = smoothstep(0.52, 0.80, field) * smoothstep(0.34, 0.88, progress);
    float clarity = saturate(max(clarityFront, settle * settle) + (rivulet - 0.5) * loose * -0.15);

    //黑水材质：浊色蠕动明暗 + 竖向径流挂亮
    float streak = smoothstep(0.68, 0.94, rivulet) * loose;
    float3 murk = uMurkColor * (0.62 + 0.38 * na);
    murk += uStreakColor * streak * 0.22;

    float3 bodyColor = lerp(murk, portrait.rgb, clarity);
    //刚澄清的区域短暂挂一层冷青水膜，随定形蒸干
    float wetRemain = saturate(clarity - settle) * 0.10;
    bodyColor += uStreakColor * wetRemain * (0.4 + 0.6 * rivulet);

    //成形边界水膜：窄带、微脉动，是水光不是霓虹
    float outer = smoothstep(0.16, 0.32, field);
    float inner = smoothstep(0.42, 0.60, field);
    float edgeBand = saturate(outer - inner);
    float edgePulse = 0.82 + 0.18 * sin(uTime * 3.2 + coords.y * 26.0 + na * 4.0);

    float outputAlpha = portrait.a * reveal * vertexColor.a;
    float3 body = bodyColor * reveal * vertexColor.rgb;
    float3 glow = uEdgeColor * edgeBand * edgePulse * portrait.a * vertexColor.a * 0.55;
    float glowAlpha = edgeBand * portrait.a * vertexColor.a * 0.24;

    return float4(body + glow, saturate(outputAlpha + glowAlpha));
}

technique BlurTech
{
    pass P0
    {
        PixelShader = compile ps_3_0 BlurPS();
    }
}

technique CompositeTech
{
    pass P0
    {
        PixelShader = compile ps_3_0 CompositePS();
    }
}
