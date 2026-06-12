sampler uImage0 : register(s0);
float uTime;
float uProgress;
float uIntensity;
float uMode; //0=线 1=扇 2=环

float4 TelegraphPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    float scan = frac(p.x * 12.0 - uTime * 4.0);
    float scanBand = smoothstep(0.42, 0.5, scan) * smoothstep(0.58, 0.5, scan);

    float mask = 0.0;
    if (uMode < 0.5)
        mask = smoothstep(0.92, 0.0, abs(p.y)) * smoothstep(-1.0, -0.2, p.x);
    else if (uMode < 1.5)
        mask = smoothstep(0.75, 0.0, abs(atan2(p.y, p.x))) * smoothstep(0.2, 1.0, p.x);
    else
        mask = smoothstep(0.02, 0.0, abs(r - lerp(0.55, 0.85, uProgress))) * smoothstep(1.0, 0.7, r);

    float pulse = 0.55 + 0.45 * sin(uTime * 8.0);
    float3 col = lerp(float3(1.0, 0.25, 0.08), float3(1.0, 0.85, 0.25), pulse);
    float a = mask * scanBand * uIntensity * (0.4 + 0.6 * uProgress);
    return float4(col * a, a);
}

technique Technique1
{
    pass PrimeTelegraphPass
    {
        PixelShader = compile ps_3_0 TelegraphPS();
    }
}
