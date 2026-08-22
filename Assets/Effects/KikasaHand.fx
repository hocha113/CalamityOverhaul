// KikasaHand.fx 血湖鬼手 臂/掌/指爪条带
// 世界空间 TriangleStrip：uv.x 0=水面根 → 0.70 腕 → 0.84 掌根 → 1=爪尖；uv.y 横跨条带
// 顶点色 R=臂中线 v 位；G=爪面掩码（0 臂身 → 1 手面）
// 材质是血湖水凝成的臂：深血半透水体、顺臂向根下淌的流层、窄亮水膜边、
// 稀疏顺流湿亮；根部不撕散，与湖面融为一体（泡沫搅动、越根越实），
// 与焦黑枯手的分野：焦炭黑壳/龟裂烬红/烟根 ↔ 血水半透/水膜亮边/融水根。
// uGrip=绷紧：整臂提亮+水膜边收锐（张力表面）；uDrain=化水回收：自爪端向根噪声侵蚀+蚀缘泡沫
// 全笛卡尔条带坐标，无极角；直线算术+平贴 tex2D，无动态分支；预乘 alpha
// ps_3_0 / vs_3_0

float4x4 transformMatrix;
float uTime;      //秒
float uOpacity;   //整体透明度
float uGrip;      //绷紧强度 0..1
float uSeed;      //每场演出统一随机相位（网络同步）
float uFoam;      //泡沫活性：出水/入水时最烈
float uDrain;     //0..1 化水回收，自爪端啃向根

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

static const float3 WaterDeep = float3(0.140, 0.022, 0.032);  //深血水底
static const float3 WaterMid  = float3(0.330, 0.070, 0.082);  //血水中层
static const float3 FlowRed   = float3(0.930, 0.300, 0.270);  //流层血红（湖面同源）
static const float3 FoamCol   = float3(0.965, 0.520, 0.440);  //血沫水膜

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
    float claw = input.Color.g;      //手面掩码

    //归一化离臂距离 0=贴中线 → 1=本侧外缘
    float across = input.TexCoords.y < vCenter
        ? (vCenter - input.TexCoords.y) / max(vCenter, 0.001)
        : (input.TexCoords.y - vCenter) / max(1.0 - vCenter, 0.001);
    across = saturate(across);

    //---- 轮廓：水面张力的平滑缘，仅轻微湿噪；绷紧时边收锐 ----
    float edgeN = tex2D(noiseSamp, float2(u * 3.1 - uTime * 0.10 + uSeed, across * 0.8 + uSeed * 2.7)).r;
    float edge = 0.93 - uGrip * 0.03 - edgeN * 0.07 * (1.0 - claw * 0.6);
    float edgeFeather = 0.14 - uGrip * 0.05;
    float body = 1.0 - smoothstep(edge - edgeFeather, edge + 0.04, across);

    //---- 化水回收：自爪端向根被湖吃回去 ----
    float dn = tex2D(noiseSamp, float2(u * 2.4 + uSeed * 3.9, across * 1.3 + uSeed)).r;
    float drainEdge = 1.06 - uDrain * 1.22 + (dn - 0.5) * 0.14;
    float keep = 1.0 - smoothstep(drainEdge - 0.05, drainEdge + 0.05, u);
    float drainRim = exp(-(u - drainEdge) * (u - drainEdge) / 0.0035)
        * saturate(uDrain * 8.0);
    body *= keep;

    //---- 水体组织：深血底 + 顺臂向根下淌的流层 ----
    float tissue = tex2D(noiseSamp, float2(u * 2.6 + uSeed * 9.0, across * 1.2 - uTime * 0.03)).r;
    float3 col = lerp(WaterDeep, WaterMid, tissue);
    //流层：特征随时间向 u=0（根）移动，水顺着臂淌回湖里
    float flow1 = tex2D(noiseSamp, float2(u * 3.4 + uTime * 0.42 + uSeed * 5.1, across * 0.9)).r;
    float flow2 = tex2D(noiseSamp, float2(u * 6.8 + uTime * 0.27 + uSeed * 1.3, across * 2.2 + 0.41)).r;
    float streak = saturate(flow1 * 0.62 + flow2 * 0.48 - 0.30);
    col += FlowRed * streak * 0.34 * (1.0 - claw * 0.35);
    //稀疏湿亮顺流闪
    float glint = pow(saturate(flow2 * 1.12), 8.0);
    col += FoamCol * glint * 0.42;

    //---- 根部融水：不撕散，泡沫搅动+越根越实，臂是湖的延伸 ----
    float rootZone = 1.0 - smoothstep(0.02, 0.20, u);
    float rootFoamN = tex2D(noiseSamp, float2(u * 7.5 - uTime * 0.30 + uSeed * 6.2, across * 2.6)).r;
    col += FoamCol * rootZone * (0.22 + 0.55 * rootFoamN) * (0.5 + 0.5 * uFoam);
    body = saturate(body + rootZone * 0.35);

    //---- 手面：更亮的水膜，指尖聚光 ----
    float3 clawCol = WaterMid * 0.9 + FoamCol * 0.22
        + FoamCol * smoothstep(0.93, 1.0, u) * (0.25 + uGrip * 0.75);
    col = lerp(col, clawCol, claw);

    //---- 水膜亮边：窄亮 FOAM 边，暗景里读形靠它；绷紧提亮 ----
    float rim = smoothstep(edge - 0.20, edge - 0.02, across);
    col += FoamCol * rim * (0.30 + 0.30 * uGrip + 0.18 * uFoam);
    //蚀缘泡沫
    col += FoamCol * drainRim * 0.9;

    //绷紧整体轻提亮：表面张力
    col *= 1.0 + uGrip * 0.14;

    //血水半透：中线附近略实、边缘让光；根区更实
    float alpha = body * (0.62 + tissue * 0.14 + (1.0 - across) * 0.14 + rootZone * 0.10);
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
