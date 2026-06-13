// ============================================================================
// DivineSourceArc.fx 真·黄金斩挥砍扇形刀光
// UV.x 弧向 0起手 1终点，UV.y 径向 0外缘 1内缘；vs_3_0/ps_3_0 + s1 噪声
// AlphaBlend
// ============================================================================

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

float TotalTime;        //时间(秒)
float SweepT;           //0~1 已扫过弧向比例，刀锋热区贴 u≈SweepT
float FadeOut;          //0~1 消散进度 1完整可见
float HeatBoost;        //刀锋前沿白热强度
float RimIntensity;     //外缘亮线强度

float4 LeadColor;       //前沿白热
float4 GoldColor;       //高亮金
float4 AmberColor;       //主体琥珀
float4 TailColor;       //拖尾暗琥珀

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

    // 噪声
    // 沿弧向拉伸的流动噪声(主体质感)
    float n1 = tex2D(NoiseSampler, float2(u * 3.6 - TotalTime * 1.0, v * 0.8 + 0.2)).r;
    // 高频丝状条纹
    float n2 = tex2D(NoiseSampler, float2(u * 7.5 - TotalTime * 2.2, v * 0.3 + 0.6)).r;
    // 溶解噪声
    float n3 = tex2D(NoiseSampler, float2(u * 2.2 + TotalTime * 0.25, v * 1.3)).r;

    // 主体颜色: 拖尾暗琥珀 → 琥珀 → 金(靠近刀锋)
    float3 col = lerp(TailColor.rgb, AmberColor.rgb, smoothstep(0.0, 0.45, rel));
    col = lerp(col, GoldColor.rgb, smoothstep(0.45, 0.88, rel));

    // 刀锋前沿白热区
    float lead = smoothstep(0.62, 1.0, rel);
    col += LeadColor.rgb * lead * HeatBoost * (1.0 - v * 0.55);

    // 外缘亮线 (剑尖扫过的圆弧 = 最亮的切割轨迹)
    float rim = pow(saturate(1.0 - v / 0.16), 2.2);
    rim *= 0.45 + 0.55 * rel;   // 越靠近刀锋, 外缘越炽
    col += LeadColor.rgb * rim * RimIntensity;

    // 流动条纹
    float streak = smoothstep(0.45, 0.86, n1) * 0.6 + smoothstep(0.58, 0.94, n2) * 0.5;
    streak *= (0.35 + 0.65 * rel) * (1.0 - v * 0.5);
    col += GoldColor.rgb * streak * 0.65;

    // alpha
    // 径向: 外缘实, 向内缘羽化; 内缘被噪声撕碎
    float innerEdge = v + (n1 - 0.5) * 0.22;
    float alpha = smoothstep(1.05, 0.55, innerEdge);
    alpha = max(alpha, rim * 0.85);

    // 弧向: 拖尾自然变淡
    alpha *= 0.30 + 0.70 * smoothstep(0.0, 0.55, rel);

    // 消散: 从拖尾侧开始被噪声蚕食
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
