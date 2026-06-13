// ============================================================================
// NotifBadge.fx 任务书通知红点
// AlphaBlend；ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float2 uResolution;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 center = coords - 0.5;
    float dist = length(center) * 2.0;

    //圆形遮罩
    float circle = 1.0 - smoothstep(0.40, 0.50, dist);
    if (circle < 0.001)
        return float4(0, 0, 0, 0);

    //脉冲
    float pulse = sin(uTime * 2.5) * 0.5 + 0.5;

    //球面明暗（中心亮边缘暗 + 左上偏光）
    float shade = 1.0 - dist * dist * 0.7;
    float2 hlOff = center + float2(0.08, 0.10);
    float hl = saturate(1.0 - length(hlOff) * 5.0);

    //色彩
    float3 deep = float3(0.75, 0.10, 0.06);
    float3 bright = float3(1.0, 0.35, 0.22);
    float3 hot = float3(1.0, 0.88, 0.75);

    float3 col = lerp(deep, bright, shade);
    col += hot * hl * 0.55;

    //菲涅尔边缘暖光
    float rim = dist * dist * dist;
    col = lerp(col, float3(1.0, 0.45, 0.25), rim * 0.5);

    //呼吸
    col *= 0.93 + pulse * 0.07;

    //外发光
    float glow = smoothstep(0.50, 0.35, dist) * (1.0 - circle);
    float glowA = glow * (0.25 + pulse * 0.12);

    float a = saturate(circle + glowA * 0.5);
    float3 finalC = col * circle + float3(1.0, 0.3, 0.15) * glowA;

    return float4(finalC * a, a);
}

technique Technique1
{
    pass NotifBadgePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
