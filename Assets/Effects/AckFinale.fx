// ============================================================================
// AckFinale.fx  谢幕辉光场（绘于结尾标志背后的方形 quad）
// 中央泛光 + 外扩柔环 + 向心汇聚的光点；纯径向距离，无角度采样故无接缝
// AlphaBlend 预乘 alpha，作为暗底之上的加性辉光
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uIntensity;  //0-1 谢幕强度，驱动整体亮度
float3 uAccent;

#define PI 3.14159265
#define TAU 6.28318530

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 d = uv - 0.5;
    float r = length(d) * 2.0;
    float t = uTime;

    float glow = 0.0;

    //中央泛光，轻微脉动
    float pulse = 0.85 + 0.15 * sin(t * 1.8);
    glow += exp(-r * r * 5.0) * 1.1 * pulse;
    glow += exp(-r * 3.2) * 0.35;

    //外扩柔环
    for (int i = 0; i < 3; i++) {
        float ph = frac(t * 0.18 + i * 0.333);
        float ring = exp(-pow((r - ph * 1.1) * 9.0, 2.0)) * (1.0 - ph);
        glow += ring * 0.30;
    }

    //向心汇聚光点（笛卡尔位置，无角度）
    float motes = 0.0;
    [unroll]
    for (int m = 0; m < 16; m++) {
        float2 seed = float2(m, 3.7);
        float s = hash21(seed);
        float2 dir = hash22(seed + 9.1) - 0.5;
        float life = frac(s + t * (0.12 + s * 0.12));
        float2 start = dir * (1.5 + s * 0.6);
        float2 pos = lerp(start, float2(0.0, 0.0), life);
        float dd = length(d * 2.0 - pos);
        motes += (1.0 - smoothstep(0.0, 0.05, dd)) * (1.0 - life) * (0.4 + s * 0.6);
    }
    glow += motes * 0.45;

    float a = saturate(glow) * uIntensity * uAlpha;
    float3 col = uAccent * (0.6 + glow * 0.8);
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(glow - 0.7));

    return float4(col * a, a) * vertexColor;
}

technique Technique1
{
    pass AckFinalePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
