// ============================================================================
//OniDismember.fx 鬼切肢解定格：NPC 快照碎片绘制
//几何切割在 C# 端完成（凸多边形裁剪成碎片），本 shader 只负责：
//  快照采样（预乘 alpha，POINT 采样保像素风）；
//  定格冷灰：微去饱和 + 压暗，"斩过之后世界静止"的尸身质感；
//  断面辉光：像素到各切割线的距离场，绯红晕 + 白热芯，只染实体像素。
//碎片位移由顶点承担而 uv 保留原位，localPx 恒为"未切开时"的身体坐标，
//两半分离后断面辉光仍精确贴合各自的切口边缘。
//切口按批传入，运行时计数决定本批循环次数；单批容量只影响绘制批数，不限制总切口数。
//极角审计：无 atan2/theta 消费，全部距离场为线性 dot，无缝隙风险。
//预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

#define CUTS_PER_BATCH 16

float4x4 transformMatrix;
float uTime;         //秒
float2 uSnapSize;    //快照 RT 像素尺寸
float4 uCutLine[CUTS_PER_BATCH];  //xy=切点(快照中心像素系) zw=单位法线
float4 uCutGlow[CUTS_PER_BATCH];  //x=辉光强度 y=辉光半宽px
int uCutCount;       //本批有效切口数
float uDrawBase;     //1=身体与辉光，0=仅附加辉光
float uDesat;        //定格冷灰去饱和 0..1
float uDim;          //整体压暗系数
float3 uColHot;      //断面白热芯
float3 uColBright;   //断面绯红晕

texture uSnapTex;
sampler snapSamp = sampler_state
{
    texture = <uSnapTex>;
    magfilter = POINT;
    minfilter = POINT;
    mipfilter = POINT;
    AddressU = clamp;
    AddressV = clamp;
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
    //顶点色承载整体淡出（预乘惯例 rgba 同乘）
    float4 snap = tex2D(snapSamp, input.TexCoords) * input.Color;
    float2 localPx = (input.TexCoords - 0.5) * uSnapSize;

    //定格冷灰：先冷后加辉光，断面保持灼热
    float gray = dot(snap.rgb, float3(0.299, 0.587, 0.114));
    snap.rgb = lerp(snap.rgb, float3(gray, gray, gray), uDesat) * uDim;

    //断面辉光：到各切线的垂距高斯衰减，多刀取最亮
    float glow = 0.0;
    [loop]
    for (int i = 0; i < uCutCount; i++)
    {
        float d = abs(dot(localPx - uCutLine[i].xy, uCutLine[i].zw));
        float w = max(uCutGlow[i].y, 0.001);
        glow = max(glow, exp(-(d * d) / (w * w)) * uCutGlow[i].x);
    }

    //首批输出身体，后续批次 alpha=0，使预乘混合只叠加新一批辉光
    float3 glowColor = (uColBright * glow + uColHot * glow * glow) * snap.a;
    return float4(snap.rgb * uDrawBase + glowColor, snap.a * uDrawBase);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
