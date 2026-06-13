// ============================================================================
// ArcaneRealityTremor.fx 永恒奥秘之座现实震荡
// 爆心大型四边形 Additive；coreColor/edgeColor 复用金紫或青白
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float ringProgress;     //0~1+ 主环扩张进度
float ringThickness;    //主环厚度（归一化）
float fadeAlpha;        //整体淡出 0~1
float seed;             //随机种子
float3 coreColor;       //核心亮色
float3 edgeColor;       //外缘色

#define TAU 6.28318530

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159265) / TAU;

    // 噪声扰动
    float n1 = tex2D(noiseTex, float2(normAngle * 5.0 + seed, ringProgress * 1.7 + uTime * 0.6)).r;
    float n2 = tex2D(noiseTex, float2(normAngle * 9.0 - uTime * 1.1, seed + ringProgress)).g;
    float noiseDisp = (n1 * 0.6 + n2 * 0.4 - 0.5) * 0.09;
    float adjDist = dist + noiseDisp;

    // 主冲击环
    float ringDist = abs(adjDist - ringProgress);
    float ringMask = 1.0 - smoothstep(0.0, ringThickness, ringDist);
    float innerBias = smoothstep(ringProgress, ringProgress - ringThickness * 0.7, adjDist);
    float ringBrightness = ringMask * (0.7 + innerBias * 1.3);

    // 次级余波环
    float echoProgress = ringProgress * 0.72;
    float echoDist = abs(adjDist - echoProgress);
    float echoMask = (1.0 - smoothstep(0.0, ringThickness * 0.55, echoDist)) * 0.55;

    // 放射状龟裂纹
    //以角度为主轴的脊状噪声，随主环扩张被"拖出"
    float crackNoise = tex2D(noiseTex, float2(normAngle * 7.0 + seed * 11.0, adjDist * 1.6 - uTime * 0.25)).r;
    float crackRidge = 1.0 - abs(crackNoise * 2.0 - 1.0);
    float cracks = pow(crackRidge, 7.0);
    //龟裂只出现在主环内侧到中心之间，靠近环处最强
    float crackZone = smoothstep(ringProgress, ringProgress * 0.25, adjDist) * step(adjDist, ringProgress);
    float crackFade = smoothstep(0.0, 0.25, ringProgress) * (1.0 - smoothstep(0.7, 1.15, ringProgress));
    cracks *= crackZone * crackFade * 1.6;

    // 中心闪光
    float flash = pow(saturate(1.0 - ringProgress * 1.6), 2.0) * smoothstep(0.55, 0.0, dist) * 2.2;

    // 环上符纹
    float micro = frac(normAngle * 64.0 + seed);
    float microLine = smoothstep(0.04, 0.0, min(micro, 1.0 - micro));
    ringBrightness += microLine * ringMask * 0.3;

    // 颜色合成
    float3 ringColor = lerp(edgeColor, coreColor, innerBias) * ringBrightness;
    float3 echoColor = edgeColor * echoMask;
    float3 crackColor = lerp(coreColor, edgeColor, 0.35) * cracks;
    float3 flashColor = coreColor * flash;

    float3 finalColor = (ringColor + echoColor + crackColor + flashColor) * fadeAlpha;
    float alpha = saturate(ringBrightness + echoMask + cracks * 0.8 + flash) * fadeAlpha;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass ArcaneRealityTremorPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
