//=============================================================================
// NeutronRing.fx v2 — 中子星能量场着色器
// ps_3_0 — 吸积环结构 + 磁场辐射线 + 事件视界辉光 + 能量脉冲
//=============================================================================

sampler uImage0 : register(s0);

float3 uColor;
float uOpacity;
float uTime;
float cosine;
bool set;

#define PI  3.14159265
#define TAU 6.28318530

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 NeutronFieldPS(float2 uv : TEXCOORD0) : COLOR0
{
    // 坐标变换
    float2 centered = uv - 0.5;

    // 旋转矩阵
    float sinT = sin(uTime);
    float2x2 rotate = float2x2(cosine, -sinT, sinT, cosine);
    float2 rotated = mul(centered, rotate);
    float2 finalUV = rotated + 0.5;

    // 极坐标
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + PI) / TAU;

    // 基础纹理采样（旋转后）
    float4 baseTex = tex2D(uImage0, finalUV);
    float3 baseColor = baseTex.rgb;

    // ---- 吸积环结构 ----
    // 主环
    float ring1Pos = 0.25;
    float ring1 = exp(-pow(abs(dist - ring1Pos) * 10.0, 2.0));
    float ring1Noise = valueNoise(float2(normAngle * 4.0 + uTime * 0.3, dist * 8.0));
    ring1 *= (0.6 + ring1Noise * 0.4);

    // 次环
    float ring2Pos = 0.35;
    float ring2 = exp(-pow(abs(dist - ring2Pos) * 8.0, 2.0)) * 0.6;
    float ring2Noise = valueNoise(float2(normAngle * 6.0 - uTime * 0.2, dist * 5.0 + 0.5));
    ring2 *= (0.5 + ring2Noise * 0.5);

    // 光子环（极亮极窄）
    float photonRing = exp(-pow(abs(dist - 0.15) * 25.0, 2.0)) * 1.5;
    float photonFlicker = 0.7 + valueNoise(float2(normAngle * 8.0, uTime * 0.5)) * 0.3;
    photonRing *= photonFlicker;

    // ---- 磁场辐射线 ----
    float fieldLines = sin(angle * 6.0 + uTime * 1.5);
    fieldLines = pow(max(fieldLines, 0.0), 3.0) * 0.3;
    fieldLines *= smoothstep(0.4, 0.1, dist);

    // ---- 能量脉冲 ----
    float pulse = sin(dist * 25.0 - uTime * 6.0);
    pulse = max(pulse, 0.0) * 0.2;
    pulse *= exp(-dist * 5.0);

    // 综合能量场
    float energy = ring1 + ring2 + photonRing + fieldLines + pulse;

    // 颜色混合
    float3 energyColor = uColor * energy;
    float3 finalColor = baseColor * uColor * 0.3 + energyColor;

    // 事件视界暗区
    float horizonDark = smoothstep(0.05, 0.12, dist);
    finalColor *= horizonDark;

    // 透明度
    float alpha = uOpacity;
    alpha *= smoothstep(0.5, 0.3, dist);
    alpha -= uv.x * 0.3;
    alpha = saturate(alpha + energy * 0.2);

    // 全蓝高能模式
    if (set && any(finalColor))
    {
        finalColor = lerp(finalColor, float3(0.7, 0.85, 1.0), 0.6);
        alpha = saturate(alpha + 0.3);
    }

    return float4(finalColor, alpha);
}

technique Technique1
{
    pass NeutronRingPass
    {
        PixelShader = compile ps_3_0 NeutronFieldPS();
    }
}