// ============================================================================
//HimayoPortraitAssembly.fx
//BlurTech: 樱瓣印记密度的可分离高斯模糊
//CompositeTech: 密度场阈值凝聚 + 后段噪声补全 + 绯红显现边
// ============================================================================

sampler uImage0 : register(s0);
sampler uMask : register(s1);
sampler uNoise : register(s2);

float2 uDelta;
float2 uTexelSize;
float uProgress;
float uTime;
float3 uEdgeColor;

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
    float4 portrait = tex2D(uImage0, coords);
    if (portrait.a <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    float density = tex2D(uMask, coords).r;
    float neighborDensity = max(
        max(tex2D(uMask, coords + float2(uTexelSize.x * 2.0, 0)).r,
            tex2D(uMask, coords - float2(uTexelSize.x * 2.0, 0)).r),
        max(tex2D(uMask, coords + float2(0, uTexelSize.y * 2.0)).r,
            tex2D(uMask, coords - float2(0, uTexelSize.y * 2.0)).r));
    density = max(density, neighborDensity * 0.82);

    float2 noiseUV = coords * float2(2.1, 3.7) + float2(uTime * 0.017, -uTime * 0.011);
    float noise = tex2D(uNoise, noiseUV).r;

    //抵达花瓣先写出局部轮廓，随后由上至下的噪声前沿补齐孔洞
    float sweepT = smoothstep(0.30, 0.96, uProgress);
    float sweepY = lerp(-0.08, 1.08, sweepT);
    float sweepJitter = (noise - 0.5) * 0.10 * (1.0 - sweepT);
    float sweepField = 1.0 - smoothstep(sweepY - 0.035, sweepY + 0.055, coords.y + sweepJitter);
    float field = max(density * 1.38, sweepField);

    float reveal = smoothstep(0.29, 0.47, field);
    float outer = smoothstep(0.17, 0.34, field);
    float inner = smoothstep(0.43, 0.60, field);
    float edge = saturate(outer - inner);
    float edgePulse = 0.86 + 0.14 * sin(uTime * 3.6 + coords.y * 31.0);

    float outputAlpha = portrait.a * reveal * vertexColor.a;
    float3 body = portrait.rgb * reveal * vertexColor.rgb;
    float3 glow = uEdgeColor * edge * edgePulse * portrait.a * vertexColor.a * 0.72;
    float glowAlpha = edge * portrait.a * vertexColor.a * 0.28;

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
