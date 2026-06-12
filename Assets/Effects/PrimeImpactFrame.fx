sampler uImage0 : register(s0);
float uIntensity;
float uProgress;

float4 ImpactFramePS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2.0 - 1.0;
    float vignette = smoothstep(0.2, 1.0, length(p));
    float flash = (1.0 - uProgress) * uIntensity;
    float contrast = lerp(1.0, 0.15, flash);
    float3 col = float3(contrast, contrast, contrast);
    float a = flash * vignette * 0.85;
    return float4(col, a);
}

technique Technique1
{
    pass PrimeImpactFramePass
    {
        PixelShader = compile ps_3_0 ImpactFramePS();
    }
}
