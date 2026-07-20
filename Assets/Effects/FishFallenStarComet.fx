// ============================================================================
//FishFallenStarComet.fx 坠星彗尾条带（沿 oldPos 轨迹的 TriangleStrip）
//uv.x：0=星头 → 1=尾梢；uv.y：0..1 横跨条带，0.5=中线
//全笛卡尔条带坐标，无极角，无接缝风险
//
//层次：淡金热脊（仅头段中线，偏金禁冷白）→ 星金焰体 → 深蓝中段 → 夜空靛蓝暗尾；
//尾段被高频噪声蚀成离散星尘斑点，靛蓝暗纱垫底衬金，头亮尾灭。
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uFade;   //整体不透明度（出生淡入）

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

//淡金热芯（偏金禁冷白）/ 星金 / 深蓝 / 夜空靛蓝
static const float3 ColCore = float3(1.30, 1.08, 0.62);
static const float3 ColGold = float3(1.02, 0.70, 0.28);
static const float3 ColBlue = float3(0.22, 0.36, 0.85);
static const float3 ColNight = float3(0.05, 0.07, 0.18);

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
    float t = input.TexCoords.x;          //0 头 → 1 尾
    float y = input.TexCoords.y - 0.5;    //-0.5..0.5

    //轻微星流扰动：比火焰安静，越靠尾越散
    float wob = tex2D(noiseSamp, float2(t * 2.2 - uTime * 1.1 + uSeed * 7.0, uSeed)).r - 0.5;
    y += wob * 0.30 * t;

    float across = saturate(abs(y) * 2.0);   //0 中线 → 1 边缘
    float body = 1.0 - across;

    //沿带流动的星尘纹
    float flow = tex2D(noiseSamp, float2(t * 3.0 - uTime * 1.6 + uSeed * 11.0, y * 1.4 + 0.5 + uSeed)).r;

    //尾端蚀散：阈值随 t 收紧，尾梢碎成星尘
    float ragged = smoothstep(0.14 + t * 0.55, 0.80, body + (flow - 0.5) * (0.30 + t * 0.90));
    if (ragged < 0.004)
        return float4(0, 0, 0, 0);

    //强度包络：头亮尾灭
    float head = pow(saturate(1.0 - t), 1.6);

    //夜空暗纱：中后段半透明靛蓝残带，暗底托亮部
    float veil = (1.0 - t) * smoothstep(0.08, 0.40, t) * 0.50;

    //色程：星金 → 深蓝 → 夜空靛蓝（自尾向头读即深蓝→金）
    float3 col = lerp(ColGold, ColBlue, smoothstep(0.14, 0.50, t + (0.5 - flow) * 0.10));
    col = lerp(col, ColNight, smoothstep(0.48, 0.90, t));

    //淡金热脊：仅头段中线一条细热芯
    float core = pow(body, 6.0) * pow(saturate(1.0 - t), 2.4);
    col += ColCore * core * 1.15;

    //星尘斑点：中尾段高频噪声阈出的离散金点，缓慢闪动
    float speckN = tex2D(noiseSamp, float2(t * 9.0 + uSeed * 23.0 - uTime * 0.4, y * 6.0 + uSeed * 5.0)).r;
    float speck = smoothstep(0.78, 0.94, speckN) * smoothstep(0.30, 0.70, t) * ragged;
    col += ColGold * speck * 0.9;

    float alpha = saturate(head * 0.90 + veil) * ragged * uFade;
    return float4(col * alpha + ColCore * core * 0.18 * uFade, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
