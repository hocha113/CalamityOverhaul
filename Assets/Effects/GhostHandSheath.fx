// GhostHandSheath.fx 鬼手血影臂/指条带
// 世界空间 TriangleStrip：uv.x 0=肩 → 1=指尖（主臂占 0~0.80，五指续 0.80~1.0）；
// uv.y 横跨条带，臂中线 v 位置由顶点色 R 编码（上侧加宽，烟向上散）
// 材质是凝血阴影：边缘噪声撕散成烟、血脉沿臂向指尖流动、抓握时脉络亮起+轮廓收紧
// 全笛卡尔条带坐标，无极角
// 预乘 alpha，配 BlendState.AlphaBlend
// ps_3_0 / vs_3_0

float4x4 transformMatrix;
float uTime;      //秒
float uOpacity;   //整体透明度，Lurking≈0.35 → Gripping≈0.9
float uGrip;      //抓握强度 0..1，脉络亮度+边缘收窄
float uSeed;      //个体随机相位

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

static const float3 ColDark = float3(0.031, 0.012, 0.020);  //暗底 #080305
static const float3 ColMid  = float3(0.549, 0.059, 0.078);  //深血红 #8C0F14
static const float3 ColHot  = float3(0.800, 0.118, 0.157);  //亮脉 #CC1E28

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

    //归一化离臂距离 0=贴中线 → 1=本侧外缘
    float across = input.TexCoords.y < vCenter
        ? (vCenter - input.TexCoords.y) / max(vCenter, 0.001)
        : (input.TexCoords.y - vCenter) / max(1.0 - vCenter, 0.001);
    across = saturate(across);

    //---- 边缘烟雾撕散：双八度噪声扰动轮廓 ----
    float edgeN1 = tex2D(noiseSamp, float2(u * 2.3 - uTime * 0.16 + uSeed, across * 0.9 + uSeed * 3.1)).r;
    float edgeN2 = tex2D(noiseSamp, float2(u * 5.1 + uTime * 0.23 + uSeed * 7.0, 0.37 + uSeed)).r;
    //抓握收窄，轮廓变实变紧
    float edge = 0.88 - uGrip * 0.14 - (edgeN1 * 0.30 + edgeN2 * 0.14);
    float body = 1.0 - smoothstep(edge - 0.26, edge + 0.06, across);

    //指尖段撕散加剧，末梢碎成烟而非干净截断
    float tipErode = smoothstep(0.74, 1.0, u);
    body *= 1.0 - tipErode * (0.30 + 0.70 * edgeN2) * 0.62;

    //---- 血脉：贴中线、沿臂向指尖流动，抓握时更急 ----
    float vein = tex2D(noiseSamp,
        float2(u * 2.2 - uTime * (0.26 + uGrip * 0.24) + uSeed * 0.013, across * 0.55 + uSeed * 5.3)).r;
    vein = pow(vein, 2.4);
    float veinMask = 1.0 - smoothstep(0.05, 0.78, across);
    float veinGlow = vein * veinMask * (0.55 + uGrip * 0.75);

    //暗色组织底，低频缓动
    float tissue = tex2D(noiseSamp, float2(u * 3.4 + uSeed * 11.0, across * 1.3 - uTime * 0.05)).r;

    //---- 色程：暗底 → 深血红 → 亮脉过热点 ----
    float3 col = lerp(ColDark, ColMid, saturate(veinGlow * 0.9 + tissue * 0.22));
    col += ColHot * smoothstep(0.55, 0.95, veinGlow) * (0.35 + uGrip * 0.9);

    float alpha = body * (0.40 + tissue * 0.26 + veinGlow * 0.46);
    //肩根淡入、指尖最末几像素兜底归零
    alpha *= smoothstep(0.0, 0.10, u) * (1.0 - smoothstep(0.965, 1.0, u));
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
