// ============================================================================
// DivineSourceBladeGlow.fx 真·黄金斩剑身能量笼罩
// 采样 s0 剑身 + s1 噪声；Immediate AlphaBlend
// ps_3_0
// ============================================================================

sampler2D MainSampler : register(s0);

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
float GlowStrength;     //0~1.5 能量强度
float FlashBoost;       //0~1 顶点闪光
float2 TexelSize;       //1/宽 1/高
float2 BladeDir;        //贴图空间刃向单位向量

float4 OutlineColor;    //描边金
float4 EnergyColor;     //能量金
float4 FlashColor;      //闪光白金

float4 MainPS(float4 vertexColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(MainSampler, uv);
    if (src.a < 0.01)
    {
        discard;
    }

    // 1) 内描边: 8 邻域 alpha 最小值
    float2 d = TexelSize * 1.6;
    float aL  = tex2D(MainSampler, uv + float2(-d.x,  0  )).a;
    float aR  = tex2D(MainSampler, uv + float2( d.x,  0  )).a;
    float aT  = tex2D(MainSampler, uv + float2( 0,  -d.y)).a;
    float aB  = tex2D(MainSampler, uv + float2( 0,   d.y)).a;
    float aTL = tex2D(MainSampler, uv + float2(-d.x, -d.y)).a;
    float aTR = tex2D(MainSampler, uv + float2( d.x, -d.y)).a;
    float aBL = tex2D(MainSampler, uv + float2(-d.x,  d.y)).a;
    float aBR = tex2D(MainSampler, uv + float2( d.x,  d.y)).a;

    float minN = min(min(min(aL, aR), min(aT, aB)),
                     min(min(aTL, aTR), min(aBL, aBR)));
    float edgeMask = saturate(saturate(1.0 - minN) * src.a);
    edgeMask = pow(edgeMask, 0.6);

    // 2) 沿刃流动的金色能量
    float along = uv.x * BladeDir.x + uv.y * BladeDir.y;   // 沿刃标量坐标
    float cross = uv.x * -BladeDir.y + uv.y * BladeDir.x;  // 横刃标量坐标

    float n1 = tex2D(NoiseSampler, float2(along * 2.4 - TotalTime * 0.8, cross * 3.0 + TotalTime * 0.15)).r;
    float n2 = tex2D(NoiseSampler, float2(along * 1.1 + TotalTime * 0.3, cross * 1.5 - TotalTime * 0.4)).r;

    // 高频金丝: 沿刃方向流动的锐利亮线
    float thread = sin(along * 38.0 - TotalTime * 7.0 + n1 * 6.0);
    thread = pow(saturate(thread), 7.0);

    float energy = saturate(thread * 0.6 + smoothstep(0.55, 0.95, n1 * 0.65 + n2 * 0.5) * 0.7);
    energy *= src.a * (0.5 + edgeMask * 0.6);

    // 3) 合成
    float3 outCol = src.rgb;

    // 基础提亮(让金属在能量态自发光)
    outCol += src.rgb * 0.22 * GlowStrength;

    // 内描边金光
    outCol += OutlineColor.rgb * edgeMask * 0.85 * GlowStrength;

    // 流动能量
    outCol += EnergyColor.rgb * energy * 0.55 * GlowStrength;

    // 顶点闪光: 全刃白金点亮, 边缘更盛
    outCol += FlashColor.rgb * (0.45 + 0.75 * edgeMask) * FlashBoost;

    // 4) 软限亮
    float maxC = max(max(outCol.r, outCol.g), outCol.b);
    if (maxC > 0.96)
    {
        outCol = outCol * ((0.96 + (maxC - 0.96) * 0.22) / maxC);
    }

    // 5) 顶点色调制 + 预乘 alpha
    outCol *= vertexColor.rgb;
    float alpha = src.a * vertexColor.a;

    return float4(outCol * alpha, alpha);
}

technique Technique1
{
    pass MainPass
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}
