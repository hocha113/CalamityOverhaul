sampler uImage0 : register(s0);
float uTime;
float uIntensity;
float uProgress;
float uRotation;

float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

float4 HeatWakePS(float2 uv : TEXCOORD0) : COLOR0
{
    float axial = 1.0 - uv.x;
    float lat = (uv.y - 0.5) * 2.0;
    float lateral = exp(-lat * lat * 2.5);
    float axialFall = smoothstep(0.0, 0.1, axial) * pow(saturate(1.0 - axial), 1.4);
    float ripple = sin(axial * 30.0 + uTime * 12.0 + hash21(uv) * 6.0);
    float mag = abs(ripple) * lateral * axialFall * uIntensity * uProgress;
    float angle = atan2(lat, -0.3) + uRotation;
    return float4(frac(angle / 6.28318 + 0.5), mag, 0, lateral * axialFall * uProgress);
}

technique Technique1
{
    pass PrimeHeatWakePass
    {
        PixelShader = compile ps_3_0 HeatWakePS();
    }
}
