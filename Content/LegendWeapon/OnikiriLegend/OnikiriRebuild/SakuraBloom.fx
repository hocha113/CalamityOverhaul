sampler Source : register(s0);
float2 SampleStep;

float4 Blur(float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Thirteen-tap Gaussian, sigma 2.5, paired into seven bilinear reads.
    float4 result = tex2D(Source, uv) * 0.1610033737;
    result += tex2D(Source, uv + SampleStep * 1.4402863507) * 0.2655372909;
    result += tex2D(Source, uv - SampleStep * 1.4402863507) * 0.2655372909;
    result += tex2D(Source, uv + SampleStep * 3.3635474597) * 0.1231336988;
    result += tex2D(Source, uv - SampleStep * 3.3635474597) * 0.1231336988;
    result += tex2D(Source, uv + SampleStep * 5.2931777789) * 0.0308273234;
    result += tex2D(Source, uv - SampleStep * 5.2931777789) * 0.0308273234;
    return result * color;
}

technique SakuraBloom
{
    pass Gaussian
    {
        PixelShader = compile ps_3_0 Blur();
    }
}
