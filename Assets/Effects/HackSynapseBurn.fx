// ============================================================================
// HackSynapseBurn.fx 突触焚毁
// 采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;    // 效果进度 0→1
float intensity;   // 效果强度
float2 texelSize;  // 1/texWidth, 1/texHeight

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

// 伪Worley噪声——用于生成神经网络脉络
float worley(float2 uv, float scale)
{
    float2 id = floor(uv * scale);
    float2 fd = frac(uv * scale);
    float minDist = 1.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2(x, y);
            float2 pt = float2(
                hash(dot(id + neighbor, float2(127.1, 311.7))),
                hash(dot(id + neighbor, float2(269.5, 183.3)))
            );
            float2 diff = neighbor + pt - fd;
            float dist = length(diff);
            minDist = min(minDist, dist);
        }
    }
    return minDist;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    // 热扭曲偏移
    float heatPhase = uTime * 3.0 + coords.y * 20.0;
    float heatWarp = sin(heatPhase) * 0.003 * intensity;
    float2 sampleCoords = coords + float2(heatWarp, 0);

    // 行撕裂（间歇性水平偏移）
    float tearCycle = frac(uTime * 0.4);
    float tearWindow = smoothstep(0.0, 0.03, tearCycle) * smoothstep(0.08, 0.05, tearCycle);
    float row = floor(coords.y / (texelSize.y * 4.0));
    float rowRand = hash(row + floor(uTime * 8.0));
    sampleCoords.x += (rowRand - 0.5) * 0.015 * tearWindow * intensity;

    float4 texColor = tex2D(uImage0, sampleCoords);
    if (texColor.a < 0.01) return texColor;

    // 神经脉络纹理（Worley噪声反转 = 网络状）
    float nerve = 1.0 - worley(coords + float2(uTime * 0.05, 0), 8.0);
    nerve = pow(nerve, 3.0); // 锐化脉络
    // 脉冲波沿脉络行进
    float pulse = sin(uTime * 6.0 - coords.y * 30.0) * 0.5 + 0.5;
    nerve *= 0.5 + 0.5 * pulse;

    // 全局热力呼吸
    float breathe = sin(uTime * 2.0) * 0.15 + 0.85;

    // 颜色：深橙 → 亮红之间根据进度过渡
    float3 hotColor = lerp(float3(1.0, 0.4, 0.05), float3(1.0, 0.15, 0.0), progress);
    float3 coolColor = float3(0.8, 0.25, 0.0);

    // 叠加神经网络辉光
    float overlay = nerve * intensity * breathe;
    // 边缘发光增强
    float edgeGlow = saturate(1.0 - texColor.a * 4.0) * 0.5;
    overlay += edgeGlow * intensity * 0.3;

    float3 finalColor = lerp(texColor.rgb, hotColor, overlay * 0.6);
    // 脉络线上的亮点
    finalColor += hotColor * nerve * pulse * intensity * 0.4;
    // 整体受热发红
    finalColor = lerp(finalColor, coolColor, intensity * 0.08 * breathe);

    // 进度尾声闪烁衰减
    float fadeFlicker = 1.0;
    if (progress > 0.8)
    {
        float t = (progress - 0.8) / 0.2;
        fadeFlicker = 1.0 - t * 0.5 + sin(uTime * 20.0) * t * 0.3;
    }

    return float4(finalColor * fadeFlicker, texColor.a);
}

technique Technique1
{
    pass HackSynapseBurnPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
