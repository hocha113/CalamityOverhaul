sampler uImage0 : register(s0);
texture uNoise;
sampler2D noiseTex = sampler_state { texture = <uNoise>; magfilter = LINEAR; minfilter = LINEAR; AddressU = wrap; AddressV = wrap; };
float uTime;
float uProgress;
float uIntensity;
float uOpacity;

float4 ChargeVortexPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0) return float4(0, 0, 0, 0);
    float theta = atan2(p.y, p.x);
    float twist = lerp(2.0, 5.0, uProgress);
    float arm = tex2D(noiseTex, float2(theta / 6.28318 * 5.0 + r * twist + uTime * 0.8, r - uTime * 0.5)).r;
    float envelope = smoothstep(1.0, 0.4, r) * smoothstep(0.0, 0.15, r);
    float core = pow(saturate(1.0 - r * 2.5), 4.0) * uProgress;
    float intensity = (arm * envelope + core) * uIntensity * uOpacity;
    float3 col = lerp(float3(1.0, 0.2, 0.05), float3(1.0, 0.9, 0.4), core);
    return float4(col * intensity, saturate(intensity));
}

technique Technique1
{
    pass PrimeChargeVortexPass
    {
        PixelShader = compile ps_3_0 ChargeVortexPS();
    }
}
