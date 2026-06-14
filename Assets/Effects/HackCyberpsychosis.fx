// ============================================================================
//HackCyberpsychosis.fx 骇客时间赛博精神病
//采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;
float intensity;
float2 texelSize;

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    //==== 故障位移 ====
    //周期性抽搐窗口
    float glitchCycle = sin(uTime * 3.0) * 0.5 + 0.5;
    float glitchWindow = smoothstep(0.7, 0.9, glitchCycle);

    //块状像素化偏移
    float blockSize = 6.0 + sin(uTime * 7.0) * 3.0;
    float2 blockCoord = floor(coords / (texelSize * blockSize)) * (texelSize * blockSize);
    float blockRand = hash21(blockCoord + floor(uTime * 8.0));

    float2 sampleCoords = coords;
    //水平块撕裂
    float hShift = (blockRand - 0.5) * 0.04 * glitchWindow * intensity;
    sampleCoords.x += hShift;

    //垂直抖动
    float vJitter = hash(floor(uTime * 20.0)) * 0.01 * intensity;
    sampleCoords.y += vJitter * glitchWindow;

    float4 texColor = tex2D(uImage0, sampleCoords);
    if (texColor.a < 0.01) return texColor;

    //==== 色散（红色强烈偏移） ====
    float aberration = 0.006 * intensity * (1.0 + glitchWindow * 2.0);
    float r = tex2D(uImage0, sampleCoords + float2(aberration, aberration * 0.5)).r;
    float g = tex2D(uImage0, sampleCoords).g;
    float b = tex2D(uImage0, sampleCoords - float2(aberration * 0.5, aberration)).b;
    float3 splitColor = float3(r, g, b);

    //==== 红色疯狂脉冲 ====
    float madPulse = sin(uTime * 8.0 + coords.y * 40.0) * 0.5 + 0.5;
    madPulse = pow(madPulse, 2.0);
    float3 madColor = float3(1.0, 0.05, 0.0);

    //==== 扫描线干扰 ====
    float scanLine = sin(coords.y / texelSize.y * 0.5 + uTime * 10.0) * 0.5 + 0.5;
    scanLine = smoothstep(0.3, 0.5, scanLine) * 0.15;

    //==== 边缘红色辉光 ====
    float a_r = tex2D(uImage0, coords + float2(texelSize.x * 3.0, 0)).a;
    float a_l = tex2D(uImage0, coords - float2(texelSize.x * 3.0, 0)).a;
    float a_u = tex2D(uImage0, coords + float2(0, texelSize.y * 3.0)).a;
    float a_d = tex2D(uImage0, coords - float2(0, texelSize.y * 3.0)).a;
    float edge = saturate(abs(texColor.a - a_r) + abs(texColor.a - a_l) + abs(texColor.a - a_u) + abs(texColor.a - a_d));

    //==== 合成 ====
    float3 finalColor = splitColor;
    //整体偏红
    finalColor.r = lerp(finalColor.r, 1.0, intensity * 0.35);
    finalColor.gb *= 1.0 - intensity * 0.4;
    //疯狂脉冲叠加
    finalColor += madColor * madPulse * intensity * 0.3;
    //扫描线暗纹
    finalColor *= 1.0 - scanLine * intensity;
    //边缘红光
    finalColor += madColor * edge * intensity * 1.5;

    //间歇性全屏红闪
    float redFlash = hash(floor(uTime * 12.0));
    redFlash = smoothstep(0.85, 0.95, redFlash);
    finalColor += madColor * redFlash * intensity * 0.4;

    return float4(finalColor, texColor.a);
}

technique Technique1
{
    pass HackCyberpsychosisPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
