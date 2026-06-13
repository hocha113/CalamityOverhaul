// ============================================================================
// HackContagion.fx 骇客时间病毒蔓延
// 采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;    // 蔓延进度 0→1
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

// 简化噪声用于腐蚀边界
float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 texColor = tex2D(uImage0, coords);
    if (texColor.a < 0.01) return texColor;

    // ==== 蔓延遮罩（从底部向上扩散）====
    // 使用噪声使边界不规则
    float noise1 = vnoise(coords * 6.0 + float2(uTime * 0.3, 0));
    float noise2 = vnoise(coords * 12.0 + float2(0, uTime * 0.5));
    float noiseMix = noise1 * 0.7 + noise2 * 0.3;

    // 蔓延线：从底部(y=1)向顶部(y=0)推进
    float spreadLine = 1.0 - progress * 1.3; // 超出使末期完全覆盖
    float spreadMask = smoothstep(spreadLine + 0.1, spreadLine - 0.05, coords.y + (noiseMix - 0.5) * 0.2);

    // ==== 毒素脉动纹理 ====
    float toxinPulse = sin(uTime * 4.0 + coords.y * 15.0 + coords.x * 8.0) * 0.5 + 0.5;
    toxinPulse *= sin(uTime * 2.5 + noiseMix * 10.0) * 0.3 + 0.7;

    // ==== 细胞状纹理（Voronoi风格） ====
    float2 cellUV = coords * 10.0;
    float2 cellId = floor(cellUV);
    float2 cellF = frac(cellUV);
    float minDist = 1.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 nb = float2(x, y);
            float2 pt = float2(
                hash(dot(cellId + nb, float2(127.1, 311.7)) + uTime * 0.2),
                hash(dot(cellId + nb, float2(269.5, 183.3)) + uTime * 0.15)
            );
            float d = length(nb + pt - cellF);
            minDist = min(minDist, d);
        }
    }
    float cellEdge = smoothstep(0.05, 0.15, minDist); // 细胞膜

    // ==== 颜色 ====
    // 健康区域保持原色
    // 感染区域：深绿+黑色腐蚀
    float3 virusGreen = float3(0.1, 0.85, 0.2);
    float3 virusDark = float3(0.02, 0.15, 0.0);
    float3 infectedColor = lerp(virusDark, virusGreen, cellEdge * toxinPulse);

    // 感染边界发绿光
    float borderGlow = smoothstep(0.0, 0.15, spreadMask) * smoothstep(0.3, 0.15, spreadMask);

    // 合成
    float3 finalColor = lerp(texColor.rgb, infectedColor, spreadMask * intensity * 0.75);
    // 边界明亮辉光
    finalColor += virusGreen * borderGlow * intensity * 0.6;
    // 脉动亮纹
    finalColor += virusGreen * toxinPulse * spreadMask * intensity * 0.15;

    // ==== 轮廓绿色描边 ====
    float a_r = tex2D(uImage0, coords + float2(texelSize.x * 2.0, 0)).a;
    float a_l = tex2D(uImage0, coords - float2(texelSize.x * 2.0, 0)).a;
    float a_u = tex2D(uImage0, coords + float2(0, texelSize.y * 2.0)).a;
    float a_d = tex2D(uImage0, coords - float2(0, texelSize.y * 2.0)).a;
    float edge = saturate(abs(texColor.a - a_r) + abs(texColor.a - a_l) + abs(texColor.a - a_u) + abs(texColor.a - a_d));
    // 感染区域绿描边，未感染区域淡描
    float edgeStr = lerp(0.2, 1.0, spreadMask);
    finalColor += virusGreen * edge * intensity * edgeStr * 0.8;

    return float4(finalColor, texColor.a);
}

technique Technique1
{
    pass HackContagionPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
