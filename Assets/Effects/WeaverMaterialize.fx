sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uProgress;
float uTime;
float2 uTextureSize;
float uScale;
float uRotation;
float uCenterY;
float uGroundY;
float3 uSoulColor;
float3 uEdgeColor;

float4 MaterializePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 source = tex2D(uImage0, coords);
    if (source.a <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    float progress = saturate(uProgress);
    float2 localPx = (coords - 0.5) * uTextureSize;

    float2 noiseUV1 = coords * float2(2.35, 3.20)
        + float2(uTime * 0.071, -uTime * 0.043);
    float2 noiseUV2 = coords * float2(5.10, 2.85)
        + float2(-uTime * 0.052, uTime * 0.064);
    float noise1 = tex2D(uNoise, noiseUV1).r;
    float noise2 = tex2D(uNoise, noiseUV2).r;

    //贴图刀轴由左下指向右上
    float2 bladeAxis = float2(0.663, -0.749);
    float axisPx = dot(localPx, bladeAxis);
    float crossPx = dot(localPx, float2(-bladeAxis.y, bladeAxis.x));
    float fiberWave = 0.5 + 0.5 * sin(crossPx * 0.23 + axisPx * 0.055
        - uTime * 4.2 + noise2 * 5.4);
    float fiber = smoothstep(0.79, 0.98, fiberWave);
    float axialPulse = 0.5 + 0.5 * sin(axisPx * 0.105 - uTime * 5.7 + noise1 * 3.1);

    float density = noise1 * 0.51 + noise2 * 0.27 + fiber * 0.17 + axialPulse * 0.05;
    float threshold = 1.03 - progress * 1.25;
    float outerReveal = smoothstep(threshold - 0.085, threshold + 0.045, density);
    float bodyReveal = smoothstep(threshold + 0.015, threshold + 0.145, density);

    //收尾还原原贴图
    float settle = smoothstep(0.86, 0.995, progress);
    bodyReveal = lerp(bodyReveal, 1.0, settle);
    outerReveal = lerp(outerReveal, 1.0, settle);
    float edge = saturate(outerReveal - bodyReveal);
    float soulFilaments = fiber * outerReveal * (1.0 - settle);
    float effectFade = 1.0 - smoothstep(0.86, 0.98, progress);

    float scaledRotationSin = sin(uRotation);
    float scaledRotationCos = cos(uRotation);
    float2 scaledLocalPx = localPx * uScale;
    float worldY = uCenterY
        + scaledLocalPx.x * scaledRotationSin
        + scaledLocalPx.y * scaledRotationCos;
    float groundMask = 1.0 - smoothstep(uGroundY - 1.0, uGroundY + 3.0, worldY);

    float revealAlpha = saturate(bodyReveal + edge * 0.46);
    float outputAlpha = source.a * revealAlpha * groundMask * vertexColor.a;
    float3 body = source.rgb * bodyReveal * groundMask * vertexColor.rgb;
    float3 glow = uEdgeColor * edge * 0.72;
    glow += uSoulColor * soulFilaments * (0.13 + noise1 * 0.22);
    glow *= source.a * groundMask * vertexColor.a * effectFade;

    return float4(body + glow, outputAlpha);
}

technique Technique1
{
    pass MaterializePass
    {
        PixelShader = compile ps_3_0 MaterializePS();
    }
}
