// ============================================================================
//RadialBlur.fx 径向模糊
//采样 uImage0 屏幕；ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

float strength;
float2 center;
const int nsamples = 20;

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords.xy;
    
    uv -= center;
    float precompute = strength * (1.0 / float(nsamples - 1));
    float4 color = float4(0, 0, 0, 0);
    for (int i = 0; i < nsamples; i++)
    {
        color += tex2D(uImage0, uv * (1.0 + precompute * i) + center);
    }
    return color / nsamples;
}

technique Technique1
{
    pass Blur
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
