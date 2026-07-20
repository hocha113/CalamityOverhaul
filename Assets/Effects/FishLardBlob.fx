// ============================================================================
//FishLardBlob.fx 斑驳油渍程序化油体（飞行液滴/附着油渍双态一体）
//quad UV 0..1，p=uv*2-1；p.x 沿 CPU 侧主轴（飞行=速度方向，附着=表面切向）
//uDown=世界向下在该坐标系的单位向量，sag 流淌变形与上部反光带均以它定向
//层次：暗油体（近不透明）→内部下淌油纹→上部油黄反光带→薄膜虹彩（极低强度点缀）
//  →燃烧焦化+热边；全笛卡尔噪声采样，无极角，无接缝问题
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uTear;   //0..1 尾部收尖量（飞行速度感），附着态为0
float uSag;    //0..1 附着流淌进度：重心下移+下侧摊宽+上缘收窄+底缘垂滴瘤
float uBurn;   //0..1 燃烧焦化：油色转焦黑+边缘热边
float uIrid;   //0..1 薄膜虹彩强度，附着成膜后淡入，燃烧归零
float uFade;   //整体不透明度
float2 uDown;  //世界向下在形体坐标系(x=主轴,y=副轴)的单位向量

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

static const float3 ColDeep = float3(0.050, 0.040, 0.026);  //暗褐近黑油底
static const float3 ColMid = float3(0.140, 0.104, 0.050);   //油褐中间调
static const float3 ColSheen = float3(0.44, 0.35, 0.16);    //油黄反光（低亮非白）
static const float3 ColChar = float3(0.030, 0.021, 0.015);  //焦黑
static const float3 ColHeat = float3(0.98, 0.35, 0.07);     //燃烧热边暗橙

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
    float2 p = input.TexCoords * 2.0 - 1.0;
    float2 dn = uDown;
    float2 dPerp = float2(-dn.y, dn.x);

    //---- sag 流淌变形：下侧摊宽、上缘收窄、重心下移（uSag=0 时恒等） ----
    float s0 = dot(p, dn);
    float t0 = dot(p, dPerp);
    float downW = saturate(s0 * 1.4 + 0.3);
    float upW = saturate(-s0 * 1.6);
    float t1 = t0 / (1.0 + uSag * downW * 0.55);
    float s1 = s0 * (1.0 + uSag * upW * 0.38) - uSag * 0.10;
    float2 q = dPerp * t1 + dn * s1;

    //---- 尾部收尖（飞行液滴）：尾侧(q.x<0)纵向压细，头侧保持圆 ----
    float tailW = smoothstep(0.1, -0.7, q.x) * uTear;
    q.y *= 1.0 + tailW * 1.3;
    q.x *= 1.0 - uTear * 0.12;

    //---- 边缘：基础半径+缓慢下淌的流动噪声扰动+底缘垂滴瘤 ----
    float nEdge = tex2D(noiseSamp, p * 0.55 + uSeed * 3.7 + dn * (uTime * 0.045)).r;
    float lobeN = tex2D(noiseSamp, float2(t0 * 0.9 + uSeed * 7.0, uSeed * 13.0)).r;
    float edge = 0.62 + (nEdge - 0.5) * 0.14;
    edge += smoothstep(0.55, 0.92, lobeN) * uSag * saturate(s0 * 2.0) * 0.28;
    float r = length(q);
    float body = smoothstep(edge, edge - 0.09, r);
    if (body < 0.004)
        return float4(0, 0, 0, 0);

    //---- 内部油纹：沿世界向下缓慢流淌（叠一条恒定慢速兜住飞行态） ----
    float flow = tex2D(noiseSamp, p * 1.35 + float2(uSeed * 11.0, uSeed * 5.0)
        - dn * (uTime * 0.09) - float2(0, uTime * 0.02)).r;
    float3 col = lerp(ColDeep, ColMid, flow);

    //---- 上部反光带：油面光泽，靠上缘内侧的月牙区，被油纹打碎 ----
    float band = upW * body * smoothstep(edge - 0.36, edge - 0.10, r);
    col += ColSheen * band * (0.30 + 0.42 * flow);

    //---- 薄膜虹彩：hue 随噪声缓慢漂移的干涉色近似，极低强度点缀 ----
    float iridT = tex2D(noiseSamp, p * 0.8 - float2(uTime * 0.026, 0) + uSeed * 17.0).r;
    float3 irid = 0.5 + 0.5 * cos(6.2831 * (iridT + float3(0.0, 0.33, 0.67)));
    float iridMask = body * smoothstep(0.15, 0.75, flow) * (0.3 + 0.7 * upW);
    col += irid * (uIrid * 0.11) * iridMask;

    //---- 燃烧：焦化转黑+边缘热边闪烁 ----
    float flick = tex2D(noiseSamp, float2(uTime * 0.9 + uSeed * 23.0, uSeed)).r;
    col = lerp(col, ColChar, uBurn * (0.45 + 0.55 * flow));
    float rim = smoothstep(edge - 0.17, edge - 0.03, r) * body;
    col += ColHeat * rim * uBurn * (0.55 + 0.45 * flick);

    //---- 油渍脏边：边缘一圈更暗轮廓，燃烧时让位给热边 ----
    col *= 1.0 - smoothstep(edge - 0.13, edge - 0.02, r) * 0.35 * (1.0 - uBurn);

    float alpha = body * (0.86 + 0.10 * flow) * uFade;
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
