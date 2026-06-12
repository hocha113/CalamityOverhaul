sampler uImage0 : register(s0);
texture uNoise;
sampler2D noiseTex = sampler_state { texture = <uNoise>; magfilter = LINEAR; minfilter = LINEAR; AddressU = wrap; AddressV = wrap; };
float uTime;
float uProgress;
float uIntensity;

float4 ArcChainPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2.0 - 1.0;
    float cross = min(abs(p.x), abs(p.y));
    float pulse = frac(cross * 8.0 - uTime * 6.0);
    float bolt = smoothstep(0.35, 0.5, pulse) * smoothstep(0.65, 0.5, pulse);
    float n = tex2D(noiseTex, p * 3.0 + uTime * 0.5).r;
    float mask = exp(-cross * cross * 12.0) * (0.5 + 0.5 * n);
    float3 col = lerp(float3(0.4, 0.7, 1.0), float3(1.0, 1.0, 1.0), bolt);
    float a = mask * bolt * uIntensity * uProgress;
    return float4(col * a, a);
}

technique Technique1
{
    pass PrimeArcChainPass
    {
        PixelShader = compile ps_3_0 ArcChainPS();
    }
}
