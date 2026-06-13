// ============================================================================
// HackShortCircuit.fx 骇客时间短路
// 采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;    // 0→1 (极短时间内走完)
float intensity;
float2 texelSize;

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    // 强烈水平行撕裂
    float row = floor(coords.y / (texelSize.y * 2.0));
    float rowRand = hash(row + floor(uTime * 30.0));
    float displacement = (rowRand - 0.5) * 0.06 * intensity;
    float2 sampleCoords = coords + float2(displacement, 0);

    // 垂直跳变偏移
    float vertJump = hash(floor(uTime * 25.0)) * 0.02 * intensity;
    sampleCoords.y += vertJump;

    float4 texColor = tex2D(uImage0, sampleCoords);
    if (texColor.a < 0.01) return texColor;

    // 色彩分离（RGB分裂）
    float aberration = 0.008 * intensity;
    float r = tex2D(uImage0, sampleCoords + float2(aberration, 0)).r;
    float g = tex2D(uImage0, sampleCoords).g;
    float b = tex2D(uImage0, sampleCoords - float2(aberration, 0)).b;
    float3 splitColor = float3(r, g, b);

    // 电弧纹理——锐利锯齿线
    float arc1 = abs(sin(coords.y * 80.0 + uTime * 40.0 + sin(coords.x * 15.0) * 5.0));
    arc1 = smoothstep(0.96, 1.0, arc1);
    float arc2 = abs(sin(coords.x * 60.0 + uTime * 35.0 + cos(coords.y * 20.0) * 4.0));
    arc2 = smoothstep(0.97, 1.0, arc2);
    float arcs = saturate(arc1 + arc2);

    // 电弧颜色：白蓝
    float3 arcColor = float3(0.7, 0.85, 1.0);

    // 全身过曝白闪
    float flash = sin(uTime * 50.0) * 0.5 + 0.5;
    flash = pow(flash, 4.0);

    // 合成
    float3 finalColor = splitColor;
    // 整体偏蓝白
    finalColor = lerp(finalColor, float3(0.6, 0.75, 1.0), intensity * 0.3);
    // 电弧叠加
    finalColor += arcColor * arcs * intensity * 1.5;
    // 白闪
    finalColor += float3(1.0, 1.0, 1.0) * flash * intensity * 0.6;

    // 边缘电弧辉光
    float a_r = tex2D(uImage0, sampleCoords + float2(texelSize.x * 2.0, 0)).a;
    float a_l = tex2D(uImage0, sampleCoords - float2(texelSize.x * 2.0, 0)).a;
    float a_u = tex2D(uImage0, sampleCoords + float2(0, texelSize.y * 2.0)).a;
    float a_d = tex2D(uImage0, sampleCoords - float2(0, texelSize.y * 2.0)).a;
    float edge = saturate(abs(texColor.a - a_r) + abs(texColor.a - a_l) + abs(texColor.a - a_u) + abs(texColor.a - a_d));
    finalColor += arcColor * edge * intensity * 2.0;

    return float4(finalColor, texColor.a);
}

technique Technique1
{
    pass HackShortCircuitPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
