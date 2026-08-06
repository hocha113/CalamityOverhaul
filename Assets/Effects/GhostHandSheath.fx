// GhostHandSheath.fx 焦黑枯手 臂/掌/指爪条带
// 世界空间 TriangleStrip：uv.x 0=肩 → 0.70 腕 → 0.84 掌根 → 1=爪尖；uv.y 横跨条带
// 顶点色 R=臂中线 v 位（上侧加宽，烟向上散）；G=爪面掩码（0 焦肉 → 1 硬爪）
// 材质是焦炭枯尸：近实心炭壳、龟裂缝里透血烬红光、攥握时裂纹崩亮；
// 肩根重度撕散成烟（从虚空探出），爪尖锐利实心；轮廓带烬红勾边，暗背景可读
// 全笛卡尔条带坐标，无极角；直线算术 + 平贴 tex2D，无动态分支
// 预乘 alpha，配 BlendState.AlphaBlend
// ps_3_0 / vs_3_0

float4x4 transformMatrix;
float uTime;      //秒
float uOpacity;   //整体透明度：常驻≈0.92 → 退场 0
float uGrip;      //抓握强度 0..1，裂纹崩亮+轮廓收紧
float uSeed;      //个体随机相位
float uEmber;     //余烬活性：待机呼吸≈0.6，扑抓/攥握 >1

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

static const float3 ColChar0 = float3(0.028, 0.020, 0.018);  //炭底 #070505
static const float3 ColChar1 = float3(0.096, 0.068, 0.056);  //焦壳亮部 #181108
static const float3 ColAsh   = float3(0.30, 0.27, 0.25);     //灰烬斑
static const float3 ColMid   = float3(0.55, 0.10, 0.07);     //烬红 #8C1A12
static const float3 ColHot   = float3(0.95, 0.30, 0.10);     //亮烬 #F24D1A

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
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
    float u = input.TexCoords.x;
    float vCenter = input.Color.r;   //臂中线在 v 上的位置
    float claw = input.Color.g;      //爪面掩码

    //归一化离臂距离 0=贴中线 → 1=本侧外缘
    float across = input.TexCoords.y < vCenter
        ? (vCenter - input.TexCoords.y) / max(vCenter, 0.001)
        : (input.TexCoords.y - vCenter) / max(1.0 - vCenter, 0.001);
    across = saturate(across);

    //---- 轮廓：焦壳硬边带碎屑崩口，爪面几乎不蚀 ----
    float edgeN1 = tex2D(noiseSamp, float2(u * 2.6 - uTime * 0.12 + uSeed, across * 0.9 + uSeed * 3.1)).r;
    float edgeN2 = tex2D(noiseSamp, float2(u * 5.7 + uTime * 0.19 + uSeed * 7.0, 0.37 + uSeed)).r;
    float edgeSoft = (edgeN1 * 0.22 + edgeN2 * 0.10) * (1.0 - claw * 0.8);
    float edge = 0.90 - uGrip * 0.05 - edgeSoft;
    float body = 1.0 - smoothstep(edge - 0.13, edge + 0.05, across);

    //肩根撕散成烟：从虚空探出的断口
    float rootN = tex2D(noiseSamp, float2(u * 2.8 - uTime * 0.08 + uSeed * 1.7, across * 1.1 + uSeed * 5.0)).r;
    float rootErode = 1.0 - smoothstep(0.02, 0.30, u);
    body *= 1.0 - rootErode * (0.40 + 0.60 * rootN);

    //---- 焦壳组织：暗炭底 + 灰烬斑 ----
    float tissue = tex2D(noiseSamp, float2(u * 3.2 + uSeed * 11.0, across * 1.4 - uTime * 0.02)).r;
    float3 col = lerp(ColChar0, ColChar1, tissue);
    float ashN = tex2D(noiseSamp, float2(u * 8.5 + uSeed * 3.3, across * 3.6 + uSeed)).r;
    col = lerp(col, ColAsh, smoothstep(0.80, 0.97, ashN) * 0.42);

    //---- 龟裂余烬：脊线噪声成缝，缝里透血烬 ----
    float c1 = tex2D(noiseSamp, float2(u * 4.4 + uSeed * 2.1, across * 1.8 + uSeed * 9.0)).r;
    float c2 = tex2D(noiseSamp, float2(u * 9.2 - uTime * 0.035 + uSeed * 5.2, across * 3.4)).r;
    float ridge = 1.0 - abs(2.0 * (c1 * 0.62 + c2 * 0.38) - 1.0);
    float crack = smoothstep(0.70, 0.92, ridge);
    float smolder = uEmber * (0.55 + 0.45 * sin(uTime * 2.3 + u * 8.0 + uSeed * 6.0));
    float emberI = crack * saturate(smolder + uGrip * 1.2);
    col += ColMid * emberI * 0.85 + ColHot * emberI * emberI * 0.9;

    //---- 爪面：暗硬质，热集中在爪根裂缝与爪尖 ----
    float3 clawCol = float3(0.052, 0.038, 0.034) + ColMid * crack * 0.25
        + ColHot * smoothstep(0.93, 1.0, u) * (0.22 + uGrip * 0.85);
    col = lerp(col, clawCol, claw);

    //---- 烬红勾边：轮廓附近微光，暗背景也读得出剪影 ----
    float rim = smoothstep(edge - 0.30, edge - 0.04, across);
    col += ColMid * rim * (0.16 + 0.30 * saturate(smolder)) * (1.0 - claw * 0.5);

    //近实心炭壳，只有整体 uOpacity 控制隐显
    float alpha = body * (0.88 + tissue * 0.12);
    alpha = saturate(alpha * uOpacity);

    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 PixelShaderFunction();
    }
}
