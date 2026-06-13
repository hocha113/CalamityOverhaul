// ============================================================================
// DivineSourceCrescent.fx 真·黄金斩新月剑气波
// UV.x 弧向 0下角 0.5弧顶 1上角，UV.y 径向 0外缘 1内缘；vs_3_0/ps_3_0 + s1 噪声
// 预乘 alpha，AlphaBlend One InvSrcAlpha
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
float Opacity;          //0~1 整体不透明度
float Dissolve;         //0~1 生命期溶解
float RimIntensity;     //外缘刃口强度
float StreakStrength;   //环向流动条纹强度
float FlowOffset;       //弧向噪声流动相位偏移

float4 RimColor;        //刃口白金
float4 GoldColor;       //高亮金
float4 OrangeColor;     //主体橙
float4 DeepColor;       //内缘深橙

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
    float u = saturate(input.TexCoord.x);   // 弧向: 0/1 = 两角, 0.5 = 弧顶
    float v = saturate(input.TexCoord.y);   // 径向: 0 = 外缘, 1 = 内缘
    float horn = abs(u - 0.5) * 2.0;        // 0 = 弧顶中央, 1 = 角尖

    // 噪声
    // n1: 沿弧向强拉伸 → 环向流动条纹(主体质感)
    float2 nUV1 = float2(u * 3.2 - TotalTime * 0.55 - FlowOffset, v * 0.42 + 0.13);
    float n1 = tex2D(NoiseSampler, nUV1).r;
    // n2: 较各向同性 → 内缘撕裂 / 整体溶解阈值
    float2 nUV2 = float2(u * 1.6 + TotalTime * 0.18, v * 0.9 - TotalTime * 0.30);
    float n2 = tex2D(NoiseSampler, nUV2).r;
    // n3: 高频细条纹(锐利的丝状高光)
    float2 nUV3 = float2(u * 6.5 - TotalTime * 1.1 - FlowOffset * 1.6, v * 0.22 + 0.55);
    float n3 = tex2D(NoiseSampler, nUV3).r;

    // 1) 外缘白金灼热刃口
    // 极窄高亮带, 中央最强、向两角自然衰减; 微噪声让刃口呼吸
    float rimBand = saturate(1.0 - v / (0.16 + n1 * 0.05));
    float rim = pow(rimBand, 2.4) * (1.0 - horn * 0.55);

    // 2) 主体颜色梯度 (外金 → 橙 → 内深橙)
    float3 col = lerp(GoldColor.rgb, OrangeColor.rgb, smoothstep(0.08, 0.52, v));
    col = lerp(col, DeepColor.rgb, smoothstep(0.50, 0.96, v));

    // 3) 环向流动条纹
    // 拉伸噪声锐化成丝 → 模拟剑气体内"能量沿弧流动"的纹理
    float streak = smoothstep(0.42, 0.85, n1) * 0.75 + smoothstep(0.55, 0.92, n3) * 0.55;
    streak *= StreakStrength * (1.0 - v * 0.45) * (1.0 - horn * 0.35);
    col += GoldColor.rgb * streak;

    // 4) 刃口颜色注入
    col += RimColor.rgb * rim * RimIntensity;

    // alpha 合成
    // 主体: 外实内虚; 内缘被 n2 撕成羽化碎边
    float innerEdge = v + (n2 - 0.5) * (0.20 + Dissolve * 0.50);
    float alpha = smoothstep(1.02, 0.58, innerEdge);

    // 外缘需要饱满 (rim 区域 alpha 提到接近 1)
    alpha = max(alpha, rimBand * 0.9);

    // 角尖羽化: 角尖最末端轻微透明, 避免硬切
    alpha *= 1.0 - pow(horn, 7.0) * 0.45;

    // 生命期溶解: 阈值切割 → 越到后期主体越碎
    float dissolveMask = smoothstep(Dissolve * 0.85 - 0.18, Dissolve * 0.85 + 0.22, n2 * 0.72 + (1.0 - v) * 0.42);
    alpha *= dissolveMask;

    alpha = saturate(alpha) * Opacity * input.Color.a;

    // 刃口与条纹的颜色增益大于 alpha 增益 → 半加法发光
    float glowAlpha = saturate(alpha + rim * 0.35 * Opacity);

    return float4(col * alpha + RimColor.rgb * rim * RimIntensity * 0.28 * Opacity, glowAlpha);
}

technique Technique1
{
    pass MainPass
    {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
}
