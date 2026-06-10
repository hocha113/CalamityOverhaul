// =====================================================================
//  AurumImpact.fx
//  真·黄金斩 —— 命中爆点着色器
//  - ps_3_0, 由 SpriteBatch (Immediate) 调用
//  - 绘制时把任意贴图(仅取其 UV 0~1 范围)拉到目标尺寸, 形状全程序化:
//      1) 球形高亮核心: 高斯衰减光球, 命中瞬间膨胀 + 快速熄灭
//      2) 扩散能量环: 半径随 Progress 外扩, 厚度收窄,
//         环半径被角向噪声扰动 → 有机的能量环
//      3) 环内残光: 环扫过的区域留下淡淡的金色余晖
//
//  输出为预乘 alpha, 业务侧使用 BlendState.AlphaBlend
//  (颜色增益 > alpha 增益, 自带半加法发光)
// =====================================================================

sampler2D MainSampler : register(s0);   // 占位, 仅提供 UV

texture NoiseTexture;
sampler2D NoiseSampler = sampler_state
{
    Texture = (NoiseTexture);
    AddressU = Wrap;
    AddressV = Wrap;
    MagFilter = Linear;
    MinFilter = Linear;
    MipFilter = Linear;
};

float TotalTime;        // 时间累积(秒)
float RingRadius;       // [0,1] 能量环当前半径(以半幅为单位)
float RingThickness;    // 环厚度 (推荐 0.04 ~ 0.20)
float RingIntensity;    // 环强度
float SphereRadius;     // [0,1] 球形高亮半径
float SphereIntensity;  // 球形高亮强度

float4 CoreColor;       // 核心白金 (255,248,225)
float4 RingColor;       // 环金 (255,205,95)
float4 EmberColor;      // 外围橙 (255,135,35)

float4 MainPS(float4 vertexColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;        // [-1,1] 居中坐标
    float dist = length(p);
    float ang = atan2(p.y, p.x) / 6.2831853 + 0.5;   // [0,1] 角向

    // ---- 角向噪声: 扰动环半径与亮度 ----
    float n = tex2D(NoiseSampler, float2(ang * 2.0 + TotalTime * 0.06, RingRadius * 0.45 + 0.2)).r;
    float n2 = tex2D(NoiseSampler, float2(ang * 5.0 - TotalTime * 0.12, 0.7)).r;

    // ---- 1) 球形高亮核心 ----
    float sphere = exp(-pow(dist / max(SphereRadius, 0.001), 2.0) * 3.2) * SphereIntensity;

    // ---- 2) 扩散能量环 ----
    float rr = RingRadius * (0.90 + 0.18 * n);
    float ring = exp(-pow((dist - rr) / max(RingThickness, 0.005), 2.0));
    ring *= 0.72 + 0.42 * n2;
    ring *= RingIntensity;

    // ---- 3) 环内残光 ----
    float wake = smoothstep(rr, rr * 0.25, dist) * 0.22 * RingIntensity;

    // ---- 合成 ----
    float3 col = CoreColor.rgb * sphere
               + lerp(RingColor.rgb, EmberColor.rgb, saturate(dist * 1.15)) * (ring + wake);

    float alpha = saturate(sphere * 0.85 + ring * 0.65 + wake * 0.8);
    alpha *= vertexColor.a;

    return float4(col * vertexColor.a, alpha);
}

technique Technique1
{
    pass MainPass
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}
