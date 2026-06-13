// ============================================================================
// HackOpticOverload.fx 骇客时间视觉过载
// 采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;    // 效果进度 0→1
float intensity;
float2 texelSize;

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    // 扭曲：从NPC中心向外的径向扭曲
    float2 center = float2(0.5, 0.5);
    float2 toCenter = coords - center;
    float dist = length(toCenter);
    float warpStr = sin(uTime * 5.0 + dist * 20.0) * 0.004 * intensity;
    float2 sampleCoords = coords + normalize(toCenter + 0.001) * warpStr;

    float4 texColor = tex2D(uImage0, sampleCoords);
    if (texColor.a < 0.01) return texColor;

    // ==== 白色过曝核心 ====
    // 初期强烈，随进度衰减
    float overexpose = (1.0 - progress) * intensity;
    overexpose *= 0.7 + sin(uTime * 12.0) * 0.3; // 闪烁

    // 径向光晕（中心更亮）
    float radial = 1.0 - saturate(dist * 1.8);
    radial = pow(radial, 1.5);

    // ==== 青白色调 ====
    float3 glareColor = float3(0.85, 0.95, 1.0);

    // ==== 频闪效果 ====
    float strobe = sin(uTime * 20.0) * 0.5 + 0.5;
    strobe = pow(strobe, 3.0);
    float strobeIntensity = lerp(0.15, 0.05, progress); // 随时间减弱

    // ==== 扫描干扰纹 ====
    float scanNoise = hash(floor(coords.y / (texelSize.y * 2.0)) + floor(uTime * 15.0));
    scanNoise = smoothstep(0.8, 1.0, scanNoise) * 0.2 * intensity * (1.0 - progress);

    // ==== 合成 ====
    float3 finalColor = texColor.rgb;
    // 过曝提亮
    finalColor = lerp(finalColor, float3(1.0, 1.0, 1.0), overexpose * 0.6 * radial);
    // 青白光晕
    finalColor += glareColor * radial * overexpose * 0.3;
    // 频闪
    finalColor += glareColor * strobe * strobeIntensity * intensity;
    // 扫描噪点
    finalColor += float3(scanNoise, scanNoise, scanNoise);

    // 边缘高光（被光照溢出的轮廓）
    float a_r = tex2D(uImage0, coords + float2(texelSize.x * 3.0, 0)).a;
    float a_l = tex2D(uImage0, coords - float2(texelSize.x * 3.0, 0)).a;
    float a_u = tex2D(uImage0, coords + float2(0, texelSize.y * 3.0)).a;
    float a_d = tex2D(uImage0, coords - float2(0, texelSize.y * 3.0)).a;
    float edge = saturate(abs(texColor.a - a_r) + abs(texColor.a - a_l) + abs(texColor.a - a_u) + abs(texColor.a - a_d));
    float edgeGlow = edge * (1.0 - progress) * intensity;
    finalColor += glareColor * edgeGlow * 1.2;

    // 末期正常化
    if (progress > 0.75)
    {
        float restore = (progress - 0.75) / 0.25;
        finalColor = lerp(finalColor, texColor.rgb, restore * 0.7);
    }

    return float4(saturate(finalColor), texColor.a);
}

technique Technique1
{
    pass HackOpticOverloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
