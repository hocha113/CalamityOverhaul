// ============================================================================
//DropPodHeatHaze.fx 空降仓屏幕热浪扭曲
//全屏采样 screenTex 场景；ps_3_0
// ============================================================================

sampler2D screenTex : register(s0); //当前场景画面

float2 screenSize;       //屏幕像素尺寸
float2 hazeCenter;       //热源中心 归一化 0~1
float  hazeIntensity;    //扭曲强度 0~1
float  globalTime;

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

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    //如果强度极低直接返回原色，避免不必要的采样
    if (hazeIntensity < 0.005)
        return tex2D(screenTex, coords);

    //热源到像素距离
    float2 delta = coords - hazeCenter;
    float aspect = screenSize.x / screenSize.y;
    float2 corrected = float2(delta.x * aspect, delta.y);
    float dist = length(corrected);

    //近强远弱衰减，偏上方更强
    float verticalBias = saturate(1.0 - (coords.y - hazeCenter.y) * 1.5);
    verticalBias = lerp(0.3, 1.0, verticalBias);
    float falloff = exp(-dist * dist / (0.15 * hazeIntensity + 0.02)) * verticalBias;

    //多层噪声波纹
    float2 noiseUV1 = coords * 3.0 + float2(globalTime * 0.8, globalTime * 0.5);
    float wave1 = tex2D(noiseTex, noiseUV1).r - 0.5;

    //中频热浪
    float2 noiseUV2 = coords * 6.0 + float2(-globalTime * 1.2, globalTime * 0.9);
    float wave2 = tex2D(noiseTex, noiseUV2).g - 0.5;

    //高频闪烁
    float2 noiseUV3 = coords * 12.0 + float2(globalTime * 2.0, -globalTime * 0.7);
    float wave3 = tex2D(noiseTex, noiseUV3).r - 0.5;

    float combinedWave = wave1 * 0.5 + wave2 * 0.35 + wave3 * 0.15;

    //径向同心波纹
    float radialWave = sin(dist * 40.0 - globalTime * 6.0) * 0.3;
    radialWave *= exp(-dist * 5.0);

    //UV 偏移
    float distortionStrength = hazeIntensity * 0.008;
    float2 offset = float2(
        (combinedWave + radialWave) * distortionStrength * falloff,
        (combinedWave * 0.7 + radialWave * 0.5) * distortionStrength * falloff
    );

    //径向扩散分量
    float2 radialDir = normalize(delta + 0.0001);
    offset += radialDir * combinedWave * distortionStrength * falloff * 0.3;

    //扭曲采样
    float2 distortedUV = coords + offset;
    distortedUV = clamp(distortedUV, 0.001, 0.999);
    float4 color = tex2D(screenTex, distortedUV);

    //近处略偏暖
    float warmShift = falloff * hazeIntensity * 0.08;
    color.r += warmShift * 0.5;
    color.g += warmShift * 0.2;
    color.b -= warmShift * 0.1;

    return color;
}

technique Technique1
{
    pass DropPodHeatHazePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
