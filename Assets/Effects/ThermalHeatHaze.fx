// ============================================================================
// ThermalHeatHaze.fx 火力发电机热浪扭曲
// 多热源屏幕 UV 偏移累加；s0 场景 s1 噪声；ps_3_0
// ============================================================================

#define MAX_SOURCES 8

sampler2D screenTex : register(s0);

float4 sources[MAX_SOURCES];
int    sourceCount;
float2 screenSize;
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
    if (sourceCount <= 0)
        return tex2D(screenTex, coords);

    float aspect = screenSize.x / screenSize.y;
    float2 totalOffset = float2(0.0, 0.0);
    float  warmAccum   = 0.0;

    [unroll]
    for (int i = 0; i < MAX_SOURCES; i++)
    {
        if (i >= sourceCount)
            break;

        float4 src = sources[i];
        float2 center    = src.xy;
        float  intensity = src.z;
        float  radius    = src.w;

        float2 delta = coords - center;
        // 修正宽高比，使热浪以圆形扩散
        float2 corrected = float2(delta.x * aspect, delta.y);
        float  dist = length(corrected);

        // 超出影响半径直接跳过
        if (dist > radius)
            continue;

        // 热气上升偏置：发电机上方扭曲更强，下方衰减
        float verticalBias = saturate(1.0 - (coords.y - center.y) * 1.6);
        verticalBias = lerp(0.25, 1.0, verticalBias);

        float r2 = radius * radius * 0.28;
        float falloff = exp(-dist * dist / max(r2, 0.0001)) * verticalBias;

        // 噪声UV用 center 解相关，避免多个热源叠出莫尔条纹
        float2 noiseUV1 = coords * 4.0 + center * 7.13 + float2(globalTime * 0.55, -globalTime * 0.85);
        float wave1 = tex2D(noiseTex, noiseUV1).r - 0.5;

        float2 noiseUV2 = coords * 9.0 + center * 3.71 + float2(-globalTime * 1.10, globalTime * 0.70);
        float wave2 = tex2D(noiseTex, noiseUV2).g - 0.5;

        float wave = wave1 * 0.65 + wave2 * 0.35;

        float strength = intensity * 0.0065;
        // 主要垂直方向抖动（热气流），少量水平
        float2 baseOffset = float2(wave * 0.32, wave * 1.0) * strength * falloff;

        // 沿径向再叠加少量，模拟向外扩散的暖流
        float2 dir = normalize(delta + float2(0.0001, 0.0001));
        baseOffset += dir * wave * strength * falloff * 0.22;

        totalOffset += baseOffset;
        warmAccum   += falloff * intensity;
    }

    float2 distortedUV = clamp(coords + totalOffset, 0.001, 0.999);
    float4 color = tex2D(screenTex, distortedUV);

    // 暖色调微偏移，热源附近略显发红
    float warmShift = saturate(warmAccum) * 0.07;
    color.r += warmShift * 0.55;
    color.g += warmShift * 0.18;
    color.b -= warmShift * 0.10;

    return color;
}

technique Technique1
{
    pass ThermalHeatHazePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
