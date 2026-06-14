// ============================================================================
//DropPodShockwave.fx 空降仓大气冲击波环
//UV 0~1 映射 -1~1；ps_3_0
// ============================================================================

float globalTime;
float shockwaveIntensity; //整体强度 0~1
float ringRadius;         //0~1 当前环半径占比
float ringThickness;      //环的厚度
float squishY;            //Y轴压缩比 <1 产生透视椭圆

texture uNoise;
sampler2D noiseTex = sampler_state
{
    texture = <uNoise>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct PixelInput
{
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

//FBM 噪声
float fbm(float2 uv)
{
    float value = 0.0;
    float amplitude = 0.5;
    float2 offset = float2(23.0, 41.0);
    for (int i = 0; i < 3; i++)
    {
        value += amplitude * tex2D(noiseTex, uv).r;
        uv = uv * 2.13 + offset;
        amplitude *= 0.5;
    }
    return value;
}

float4 PixelShaderFunction(PixelInput input) : COLOR0
{
    float2 uv = input.TexCoords;

    //UV -1~1，Y 透视压缩
    float2 centered = uv * 2.0 - 1.0;
    centered.y /= squishY;
    float dist = length(centered);

    //极角，用于噪声的环向采样
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    //FBM 扰动环边缘
    float noiseA = fbm(float2(normAngle * 3.5 + globalTime * 2.5, dist * 1.8));
    float noiseB = fbm(float2(normAngle * 6.0 - globalTime * 1.8, dist * 1.2 + 0.3));
    float noiseDisp = (noiseA * 0.55 + noiseB * 0.45 - 0.5) * 0.09;
    float adjDist = dist + noiseDisp;

    //主环遮罩
    float ringDist = abs(adjDist - ringRadius);
    //内层：锐利的核心
    float innerRing = 1.0 - smoothstep(0.0, ringThickness * 0.5, ringDist);
    //外层：柔和的辉光扩散
    float outerGlow = 1.0 - smoothstep(0.0, ringThickness * 2.5, ringDist);
    outerGlow *= outerGlow;
    //复合环遮罩
    float ringMask = innerRing * 0.7 + outerGlow * 0.3;

    //内侧激波前沿偏亮
    float innerBias = smoothstep(ringRadius, ringRadius - ringThickness * 0.7, adjDist);

    //环边缘 RGB 色散
    float chromaOffset = ringMask * 0.015 * shockwaveIntensity;
    float2 chromaDir = normalize(centered + 0.001);
    float noiseR = fbm(float2(normAngle * 3.5 + globalTime * 2.5, (dist + chromaOffset) * 1.8));
    float noiseG = fbm(float2(normAngle * 3.5 + globalTime * 2.5, dist * 1.8));
    float noiseB_val = fbm(float2(normAngle * 3.5 + globalTime * 2.5, (dist - chromaOffset) * 1.8));
    float3 chromaNoise = float3(noiseR, noiseG, noiseB_val);

    float3 coreWhite  = float3(0.95, 0.97, 1.00);
    float3 innerCyan  = float3(0.45, 0.80, 1.00);
    float3 outerBlue  = float3(0.20, 0.50, 0.90);
    float3 warmEdge   = float3(0.80, 0.55, 0.30);

    //灼烧强度冷暖插值(vertexColor.r)
    float3 baseRingColor = lerp(innerCyan, warmEdge, input.Color.r * 0.3);

    //核心 + 内层 + 外层分层着色
    float brightness = innerRing * (0.8 + innerBias * 1.2);
    float3 ringColor = lerp(baseRingColor, coreWhite, innerBias * 0.6) * brightness;

    //应用色散噪声调制
    ringColor *= lerp(float3(1, 1, 1), chromaNoise, chromaOffset * 15.0);

    //外层辉光颜色：偏蓝的柔和光晕
    float3 glowColor = outerBlue * outerGlow * 0.6;

    //环上脉冲纹理
    float pulseNoise = tex2D(noiseTex, float2(normAngle * 12.0 + globalTime * 3.0, ringRadius * 3.0)).r;
    float pulseMask = smoothstep(0.4, 0.7, pulseNoise) * innerRing * 0.35;
    float3 pulseColor = coreWhite * pulseMask;

    //边缘 Fresnel 辉光
    float fresnelFade = pow(outerGlow, 0.5) * (1.0 - innerRing) * 0.4;
    float3 fresnelColor = innerCyan * fresnelFade;

    //最终合成
    float3 finalColor = (ringColor + glowColor + pulseColor + fresnelColor) * shockwaveIntensity;
    float alpha = saturate(ringMask + outerGlow * 0.4 + pulseMask * 0.3) * shockwaveIntensity;

    return float4(finalColor * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DropPodShockwavePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
