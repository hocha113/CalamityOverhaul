// =====================================================================
//  AurumArc.fx
//  真·黄金斩 —— 挥砍扇形刀光着色器
//  - vs_3_0 / ps_3_0, 配合程序化"扇形顶点网格"使用
//  - UV.x = 弧向绝对进度 (0 = 起手位, 1 = 完整挥砍终点)
//  - UV.y = 径向 (0 = 外缘/剑尖扫过的圆弧, 1 = 内缘/近身)
//
//  关键参数:
//      SweepT  : 当前已扫过的弧向比例 — 网格只生成到 SweepT,
//                刀锋热区(白热前沿)始终贴着 u≈SweepT 处
//      FadeOut : 挥砍结束后的消散进度 (1=完整可见, 0=完全散尽)
//                消散从拖尾侧(u 小处)开始, 被噪声撕成碎金
// =====================================================================

float4x4 WorldViewProjection;

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
float SweepT;           // [0,1] 当前已扫过比例
float FadeOut;          // [0,1] 消散进度 (1 = 不消散)
float HeatBoost;        // 刀锋前沿白热强度 (推荐 1.0 ~ 2.0)
float RimIntensity;     // 外缘亮线强度 (推荐 0.8 ~ 1.6)

float4 LeadColor;       // 前沿白热色 (255,245,205)
float4 GoldColor;       // 高亮金 (255,200,80)
float4 AmberColor;      // 主体琥珀 (250,140,35)
float4 TailColor;       // 拖尾暗琥珀 (150,70,15)

struct VSInput
{
    float3 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput MainVS(VSInput input)
{
    VSOutput o;
    o.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    o.Color = input.Color;
    o.TexCoord = input.TexCoord;
    return o;
}

float4 MainPS(VSOutput input) : COLOR0
{
    float u = saturate(input.TexCoord.x);   // 弧向绝对进度
    float v = saturate(input.TexCoord.y);   // 径向: 0 外缘, 1 内缘

    // 相对于当前刀锋位置的"追迹坐标": 1 = 紧贴刀锋, 0 = 起手拖尾
    float rel = saturate(u / max(SweepT, 0.0001));

    // ---- 噪声 ----
    // 沿弧向拉伸的流动噪声(主体质感)
    float n1 = tex2D(NoiseSampler, float2(u * 3.6 - TotalTime * 1.0, v * 0.8 + 0.2)).r;
    // 高频丝状条纹
    float n2 = tex2D(NoiseSampler, float2(u * 7.5 - TotalTime * 2.2, v * 0.3 + 0.6)).r;
    // 溶解噪声
    float n3 = tex2D(NoiseSampler, float2(u * 2.2 + TotalTime * 0.25, v * 1.3)).r;

    // ---- 主体颜色: 拖尾暗琥珀 → 琥珀 → 金(靠近刀锋) ----
    float3 col = lerp(TailColor.rgb, AmberColor.rgb, smoothstep(0.0, 0.45, rel));
    col = lerp(col, GoldColor.rgb, smoothstep(0.45, 0.88, rel));

    // ---- 刀锋前沿白热区 ----
    float lead = smoothstep(0.62, 1.0, rel);
    col += LeadColor.rgb * lead * HeatBoost * (1.0 - v * 0.55);

    // ---- 外缘亮线 (剑尖扫过的圆弧 = 最亮的切割轨迹) ----
    float rim = pow(saturate(1.0 - v / 0.16), 2.2);
    rim *= 0.45 + 0.55 * rel;   // 越靠近刀锋, 外缘越炽
    col += LeadColor.rgb * rim * RimIntensity;

    // ---- 流动条纹 ----
    float streak = smoothstep(0.45, 0.86, n1) * 0.6 + smoothstep(0.58, 0.94, n2) * 0.5;
    streak *= (0.35 + 0.65 * rel) * (1.0 - v * 0.5);
    col += GoldColor.rgb * streak * 0.65;

    // ---- alpha ----
    // 径向: 外缘实, 向内缘羽化; 内缘被噪声撕碎
    float innerEdge = v + (n1 - 0.5) * 0.22;
    float alpha = smoothstep(1.05, 0.55, innerEdge);
    alpha = max(alpha, rim * 0.85);

    // 弧向: 拖尾自然变淡
    alpha *= 0.30 + 0.70 * smoothstep(0.0, 0.55, rel);

    // ---- 消散: 从拖尾侧开始被噪声蚕食 ----
    float cut = (1.0 - FadeOut) * 1.30;
    alpha *= smoothstep(cut - 0.05, cut + 0.30, u + (n3 - 0.5) * 0.24);

    alpha = saturate(alpha) * input.Color.a;

    // 前沿与外缘颜色增益 > alpha 增益 → 半加法白热感
    float glowAlpha = saturate(alpha + (lead * 0.30 + rim * 0.22) * FadeOut);

    return float4(col * alpha + LeadColor.rgb * (lead * HeatBoost * 0.20 + rim * 0.15) * FadeOut, glowAlpha);
}

technique Technique1
{
    pass MainPass
    {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
}
