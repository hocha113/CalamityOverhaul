// ============================================================================
// VoidSuction.fx 虚空吸入演出全屏滤镜
// 径向失真+色差+暗角+黑闪；s0 场景 s1 噪声
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;             //累计时间（秒）
float suctionProgress;   //吸入进度0-1
float blackFlash;        //黑闪覆盖0-1

float2 focusCenter;      //吸入焦点（世界坐标）
float2 screenPosition;   //屏幕左上角（世界坐标）
float2 worldViewSize;    //缩放修正后的世界可视范围

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);

    if (suctionProgress < 0.001 && blackFlash < 0.001)
        return original;

    //世界坐标
    float2 worldPos = screenPosition + worldViewSize * coords;
    float2 toFocus = focusCenter - worldPos;
    float dist = length(toFocus);
    float2 dir = dist > 0.1 ? toFocus / dist : float2(0, 0);

    float effectRadius = max(worldViewSize.x, worldViewSize.y) * 0.8;
    float normDist = saturate(dist / max(effectRadius, 1.0));
    float falloff = saturate(1.0 - normDist * 0.5);

    //屏幕中心到边缘的UV距离
    float2 centerUV = coords - 0.5;
    float edgeDist = length(centerUV) * 2.0;

    float sp = suctionProgress;
    float sp2 = sp * sp;

    // =
    //径向吸入失真（强力，整个画面被拽向焦点）
    // =
    float pullStr = sp2 * 0.08 * falloff;
    //远离焦点的区域拉扯更明显
    pullStr *= (0.4 + normDist * 0.6);
    float2 pullOffset = dir * pullStr;
    //转换到UV空间
    pullOffset = pullOffset * worldViewSize / max(worldViewSize.x, worldViewSize.y);
    float2 distortedCoords = clamp(coords + pullOffset, 0.002, 0.998);

    //噪声扰动叠加（画面不是均匀被拉，而是撕裂式不均匀）
    float2 noiseUV = frac(worldPos * 0.0008 + float2(uTime * 0.05, -uTime * 0.03));
    float2 noiseDisp = tex2D(noiseTex, noiseUV).rg * 2.0 - 1.0;
    float noiseStr = sp2 * 0.012;
    distortedCoords = clamp(distortedCoords + noiseDisp * noiseStr, 0.002, 0.998);

    float4 distorted = tex2D(uImage0, distortedCoords);

    // =
    //色差分离（径向方向，从中心向外撕裂RGB）
    // =
    float2 caDir = normalize(centerUV + 0.001);
    float caStr = sp * 0.018 * (0.5 + edgeDist);
    float2 caOffset = caDir * caStr;
    distorted.r = tex2D(uImage0, clamp(distortedCoords + caOffset, 0.002, 0.998)).r;
    distorted.b = tex2D(uImage0, clamp(distortedCoords - caOffset * 0.65, 0.002, 0.998)).b;

    // =
    //去饱和（虚空侵蚀色彩）
    // =
    float lum = dot(distorted.rgb, float3(0.299, 0.587, 0.114));
    float desatStr = sp * 0.6;
    distorted.rgb = lerp(distorted.rgb, float3(lum, lum, lum), desatStr);

    // =
    //虚空侵蚀（暗色触手从屏幕边缘向中心蔓延）
    // =
    //侵蚀前沿位置：sp=0时在屏幕外，sp=1时逼近中心
    float corruptionEdge = lerp(1.4, 0.15, sp2);

    //多层噪声确定侵蚀边界
    //归一化极角到0-1避免atan2的±π接缝
    float polarAngle = (atan2(centerUV.y, centerUV.x) + 3.14159) / 6.28318;
    float2 cUV1 = frac(float2(polarAngle * 3.0 + uTime * 0.02, edgeDist * 0.5 + uTime * 0.015));
    float cNoise1 = tex2D(noiseTex, cUV1).r;
    float2 cUV2 = frac(float2(polarAngle * 5.0 - uTime * 0.03, edgeDist * 1.2 - uTime * 0.01));
    float cNoise2 = tex2D(noiseTex, cUV2).g;
    float corruptNoise = cNoise1 * 0.6 + cNoise2 * 0.4;

    //侵蚀因子：距边缘的距离超过前沿阈值时开始侵蚀
    float corruptThreshold = corruptionEdge + (corruptNoise - 0.5) * 0.4;
    float corruptFactor = saturate((edgeDist - corruptThreshold) / 0.25);
    corruptFactor *= corruptFactor;

    //侵蚀颜色（深黑带暗红脉络）
    float veinPulse = 0.3 + 0.7 * saturate(sin(uTime * 2.0 + corruptNoise * 6.28) * 0.5 + 0.5);
    float3 corruptColor = float3(0.02, 0.002, 0.005);
    float3 veinColor = float3(0.12, 0.01, 0.02) * veinPulse * corruptNoise;
    float3 corruption = corruptColor + veinColor;

    distorted.rgb = lerp(distorted.rgb, corruption, corruptFactor * sp);

    // =
    //暗角（视野急剧收窄）
    // =
    float vignette = edgeDist * edgeDist;
    float vignetteStr = sp * 1.2;
    distorted.rgb *= saturate(1.0 - vignette * vignetteStr);

    // =
    //黑闪覆盖
    // =
    distorted.rgb *= saturate(1.0 - blackFlash);

    return distorted;
}

technique Technique1
{
    pass VoidSuctionPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
