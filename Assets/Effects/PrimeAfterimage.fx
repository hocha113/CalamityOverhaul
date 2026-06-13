// ============================================================================
// PrimeAfterimage.fx 速度门控热残影
// 采样 uImage0；Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uFade;  //0=最新残影 → 1=最旧残影
float uHeat;  //0~1 热度（按当前速度门控）
float uSeed;  //实例扰动

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 tex = tex2D(uImage0, coords);
    if (tex.a < 0.04)
    {
        return float4(0, 0, 0, 0);
    }

    //噪声溶蚀：越旧的残影被咬掉越多，余下碎片如冷却的余烬
    float n = hash21(floor(coords * 46.0) + uSeed * 7.31);
    float threshold = uFade * 0.82;
    float erosion = smoothstep(threshold, threshold + 0.22, n + 0.16);

    //热衰变色带：白热 → 橙 → 深红
    float3 hot = lerp(float3(1.0, 0.88, 0.62), float3(1.0, 0.44, 0.13), saturate(uFade * 1.5));
    hot = lerp(hot, float3(0.60, 0.10, 0.04), saturate((uFade - 0.55) * 2.2));

    float strength = pow(saturate(1.0 - uFade), 1.35) * uHeat;
    float a = tex.a * erosion * strength;

    return float4(hot * a * vertexColor.rgb, a * vertexColor.a);
}

technique Technique1
{
    pass PrimeAfterimagePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
