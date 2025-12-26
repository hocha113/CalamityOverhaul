sampler uImage0 : register(s0);

float strength;
float2 center;
const int nsamples = 20;
// 很基础的径向模糊
float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords.xy;
    
    uv -= center;
    float precompute = strength * (1.0 / float(nsamples - 1));
    
    float4 color = (0, 0, 0, 0);
    for (int i = 0; i < nsamples; i++)
    {
        float scale = 1.0 + (float(i) * precompute);
        color += tex2D(uImage0, uv * scale + center);
    }
    color /= float(nsamples);
    
    
    return color;
}
technique Technique1
{
    pass Blur
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}