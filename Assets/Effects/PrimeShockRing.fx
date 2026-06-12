sampler uImage0 : register(s0);
texture uNoise;
sampler2D noiseTex = sampler_state { texture = <uNoise>; magfilter = LINEAR; minfilter = LINEAR; AddressU = wrap; AddressV = wrap; };
float globalTime;
float shockwaveIntensity;
float ringRadius;
float ringThickness;
float squishY;

float4 ShockRingPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = (uv * 2.0 - 1.0);
    centered.y /= max(squishY, 0.2);
    float dist = length(centered);
    float edge = abs(dist - ringRadius);
    float ring = exp(-pow(edge / max(ringThickness, 0.02), 2.0));
    float n = tex2D(noiseTex, float2(uv.x + globalTime, uv.y - globalTime)).r;
    ring *= 0.7 + 0.3 * n;
    float3 col = lerp(float3(1.0, 0.4, 0.1), float3(1.0, 0.95, 0.7), ring);
    float a = ring * shockwaveIntensity;
    return float4(col * a, a);
}

technique Technique1
{
    pass PrimeShockRingPass
    {
        PixelShader = compile ps_3_0 ShockRingPS();
    }
}
