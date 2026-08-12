// ============================================================================
//KikasaEaterRift.fx 鬼奴世界吞噬怪·血水裂隙
//空气中被扯开的一片竖直水面：暗渊内腔真遮挡地形、撕裂湿缘毛边、
//内腔血水下涌翻搅、缘口淌血偏重下端；出水横缝复用同一 pass（uDrip 压低）。
//quad UV.x 0..1=横越裂口（开合轴） UV.y 0..1=沿裂口长轴（1 端=淌血下端）
//预乘输出 + AlphaBlend；直线算术无分支无极角，噪声全走绑定贴图
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;       //本裂口相位种子
float uOpen;       //开合 0..1，允许弹性过冲 >1
float uFade;       //整体透明度 0..1
float uDrip;       //内腔下涌与缘口淌血权重：竖裂隙 1 / 水面横缝 0.35
float3 uColDark;   //暗渊
float3 uColDeep;   //深血
float3 uColMain;   //血红
float3 uColBright; //血沫亮
float3 uColAccent; //蚀骨紫点缀

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    //p.x -1..1 横越裂口，p.y -1..1 沿长轴（+1 = 淌血下端）
    float2 p = input.TexCoords * 2.0 - 1.0;

    //长轴端部收尖的梭形底廓
    float tip = saturate(1.0 - p.y * p.y);

    //撕裂缘扰动：低频大撕口 + 高频湿毛边（沿长轴采样，有限长条无接缝问题）
    float n1 = tex2D(noiseSamp, float2(p.y * 0.62 + uSeed, uTime * 0.055 + uSeed * 2.7)).r - 0.5;
    float n2 = tex2D(noiseSamp, float2(p.y * 2.3 - uTime * 0.085 + uSeed * 5.3, uSeed + 0.31)).r - 0.5;

    //半宽：开度 × 梭形 × 撕缘噪声，压进 quad 内留出缘光余量
    float halfW = (pow(tip, 0.58) + (n1 * 0.42 + n2 * 0.22) * tip) * uOpen * 0.58;
    halfW = max(halfW, 0.0);
    float d = abs(p.x) - halfW;

    //内腔遮罩：软边吸光暗体，开度极小时整体熄灭
    float inside = saturate(-d * 8.0) * step(0.015, uOpen);

    //内腔血水：主流沿长轴下涌（竖裂隙里是往下淌），副层慢翻搅
    float flowV = p.y * (0.5 + uDrip * 0.8) + uTime * (0.20 + uDrip * 0.28);
    float f1 = tex2D(noiseSamp, float2(p.x * 1.4 + uSeed * 3.1, flowV)).r;
    float f2 = tex2D(noiseSamp, float2(p.x * 2.9 - uSeed * 1.7, p.y * 1.5 + uTime * 0.10)).r;

    //离缘越深越黑——里头是另一处水底，不是发光门
    float depth = saturate(1.0 - abs(p.x) / max(halfW, 1e-4));
    float3 body = uColDark * (0.50 + f2 * 0.22);
    body = lerp(body, uColDeep, saturate(f1 * 1.1 - 0.25) * 0.55);
    //稀疏血涌亮斑 + 更稀的蚀紫掠影
    float churn = saturate(f1 * f2 * 1.8);
    body += uColMain * pow(churn, 3.0) * 0.28;
    body += uColAccent * pow(saturate(f2 * 1.4 - 0.55), 2.0) * 0.14;
    body *= 0.55 + 0.45 * depth;

    float aBody = inside * (0.86 + depth * 0.10);

    //湿缘双层：贴缘亮唇 + 内侧一线弯月沉痕（水膜卷边的读数）
    float rim = exp2(-abs(d) * 26.0) * tip;
    float rimGlow = exp2(-abs(d) * 9.0) * tip;
    float meniscus = exp2(-abs(d + 0.05) * 22.0) * tip;
    float3 rimCol = lerp(uColMain, uColBright, saturate(n2 + 0.5));
    //下端淌血加重
    float dripBias = 1.0 + saturate(p.y) * uDrip * 0.9;
    float openSat = saturate(uOpen);

    float3 color = body * aBody;
    color -= uColDark * meniscus * 0.35 * aBody;
    color += rimCol * (rim * 0.85 + rimGlow * 0.22) * dripBias * openSat;

    float alpha = saturate(aBody + (rim * 0.7 + rimGlow * 0.18) * openSat);
    color *= uFade;
    alpha *= uFade;
    return float4(color, alpha) * input.Color;
}

technique Technique1
{
    pass KikasaEaterRiftPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
