sampler uImage0 : register(s0);
float uTime;
float uStretch;
float uIntensity;
float2 texelSize;

float4 AfterimagePS(float2 uv : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 texColor = tex2D(uImage0, uv);
    if (texColor.a < 0.04) return float4(0, 0, 0, 0);
    float smear = tex2D(uImage0, uv + float2(texelSize.x * uStretch * 8.0, 0)).a;
    float trail = lerp(texColor.a, smear, uStretch * 0.5);
    float3 col = lerp(texColor.rgb, float3(1.0, 0.45, 0.15), uStretch * 0.35) * vertexColor.rgb;
    float a = trail * vertexColor.a * uIntensity;
    return float4(col * a, a);
}

technique Technique1
{
    pass PrimeAfterimagePass
    {
        PixelShader = compile ps_3_0 AfterimagePS();
    }
}
