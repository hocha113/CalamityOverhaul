sampler uImage0 : register(s0);
texture uNoise;
sampler2D noiseTex = sampler_state { texture = <uNoise>; magfilter = LINEAR; minfilter = LINEAR; AddressU = wrap; AddressV = wrap; };
float uTime;
float uProgress;
float uIntensity;
float uSweep; //0~1 横扫进度

float4 SkullBeamPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2.0 - 1.0;
    float beamX = lerp(-1.2, 1.2, uSweep);
    float core = exp(-pow((p.x - beamX) * 18.0, 2.0));
    float halo = exp(-pow((p.x - beamX) * 6.0, 2.0)) * 0.45;
    float n = tex2D(noiseTex, float2(p.y * 2.0 + uTime * 3.0, p.x * 0.5)).r;
    float arc = core * (0.7 + 0.3 * n);
    float3 col = lerp(float3(1.0, 0.35, 0.1), float3(1.0, 0.95, 0.7), core);
    float a = (arc + halo) * uIntensity * uProgress;
    return float4(col * a, a);
}

technique Technique1
{
    pass PrimeSkullBeamPass
    {
        PixelShader = compile ps_3_0 SkullBeamPS();
    }
}
