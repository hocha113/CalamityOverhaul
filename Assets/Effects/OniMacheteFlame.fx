// ============================================================================
//OniMacheteFlame.fx 鬼手硫火火鞘（缠臂燃烧的附着式火焰，"硫火压制"的具象化）
//几何：沿 FABRIK 手臂曲线的加宽 TriangleStrip；uv.x：0=肩 → 1=手；
//  uv.y 横跨条带，臂中线位置由顶点色 R 编码（几何端按世界向上偏置了两侧宽度，
//  火在上侧有更多空间 → 火向上飘的附着感）
//层次：贴臂根火带（实）→ 流动噪声火舌（沿臂流动+快速抖闪，根实尖稀）→
//  尖端噪声撕边 → 舌外一圈半透明黑烟
//状态耦合 uRage（躁动混合 0..1）：0=低矮沉稳硫磺火鞘；走高=火势变薄、
//  低频缺口断续熄灭露出骨臂；≈1=大面积熄灭，缺口处迸暗红危焰
//配色：根炽橙 → 硫磺橙红 → 深红 → 黑烟，无金无冷白无青蓝
//全笛卡尔条带坐标，无极角 → 接缝协议天然合规
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //每手随机相位
float uRage;   //0 忠仆(硫火健旺) → 1 躁动(火鞘将熄+暗红危焰)
float uGlow;   //攻击强度 0..1，火势更旺
float uFade;   //整体不透明度

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

static const float3 ColRoot = float3(1.30, 0.58, 0.10);   //根部炽橙（暖，非白）
static const float3 ColBrim = float3(1.12, 0.28, 0.05);   //硫磺橙红
static const float3 ColDeep = float3(0.40, 0.06, 0.025);  //深红
static const float3 ColSmoke = float3(0.05, 0.02, 0.015); //黑烟
static const float3 ColRageF = float3(0.78, 0.10, 0.04);  //躁动暗红危焰

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
    float u = input.TexCoords.x;
    float vCenter = input.Color.r;   //臂中线在 v 上的位置（上侧更宽 → 火向上）

    //归一化离臂距离：0=贴臂 → 1=本侧条带外缘
    float across = input.TexCoords.y < vCenter
        ? (vCenter - input.TexCoords.y) / max(vCenter, 0.001)
        : (input.TexCoords.y - vCenter) / max(1.0 - vCenter, 0.001);
    across = saturate(across);

    //---- 火焰健康度与熄灭缺口 ----
    float health = 1.0 - uRage * 0.78;
    //沿臂低频缺口噪声：健康度走低时阈值收紧 → 火鞘断续熄灭
    float gapN = tex2D(noiseSamp, float2(u * 1.9 + uSeed * 7.0, 0.23 + uSeed)).r;
    float gapTh = (1.0 - health) * 0.92 - 0.10;
    float lit = smoothstep(gapTh - 0.06, gapTh + 0.14, gapN);

    //---- 火舌高度场：双八度沿臂流动 + 快速抖闪 ----
    float h1 = tex2D(noiseSamp, float2(u * 2.7 - uTime * 1.6 + uSeed * 11.0, uSeed)).r;
    float h2 = tex2D(noiseSamp, float2(u * 6.2 - uTime * 2.9 + uSeed * 3.0, 0.61 + uSeed)).r;
    float flameH = 0.26 + (h1 * 0.66 + h2 * 0.34) * 0.72 * (0.72 + uGlow * 0.55);
    //压制走低火变矮
    flameH *= lerp(0.55, 1.0, health);

    //---- 两端收势：高度预算向端点塌缩，收口边界用噪声抖动，火舌自然碎断（无平滑断面） ----
    float endN = tex2D(noiseSamp, float2(u * 4.6 + uTime * 0.8 + uSeed * 9.0, 0.47)).r;
    float endEnv = smoothstep(0.0, 0.14 + endN * 0.12, u)
        * smoothstep(1.0, 0.80 - endN * 0.12, u);
    flameH *= 0.22 + 0.78 * endEnv;

    //---- 尖端撕边：扰动后的离臂距离，越离臂撕得越碎 ----
    float rag = tex2D(noiseSamp, float2(u * 8.5 - uTime * 3.1 + uSeed, across * 1.7 + 0.37)).r;
    float shape = across + (rag - 0.5) * (0.16 + across * 0.80);

    //火舌：根实尖稀
    float flame = smoothstep(flameH, flameH * 0.32, shape) * lit;
    //贴臂根火带：火"长在臂上"的锚（同吃端点包络，端头只余零星火种，不留平滑亮带断面）
    float root = pow(saturate(1.0 - across * 2.7), 2.0) * lit * (0.15 + 0.85 * endEnv);

    //---- 躁动危焰：熄灭缺口里迸出的暗红短舌（更急促） ----
    float rageN = tex2D(noiseSamp, float2(u * 4.3 - uTime * 4.1 + uSeed * 5.0, 0.83)).r;
    float rageTongue = smoothstep(0.60, 0.86, rageN) * (1.0 - lit) * uRage
        * smoothstep(0.60, 0.0, shape);

    //---- 舌外黑烟：火焰上缘一圈半透明烟 ----
    float smoke = (smoothstep(flameH * 1.38, flameH * 0.92, shape)
        - smoothstep(flameH * 0.92, flameH * 0.40, shape));
    smoke = saturate(smoke) * (0.45 + uRage * 0.35) * max(lit, uRage * 0.7);

    //---- 色程 ----
    float heat = saturate(flame + root * 0.55);
    float3 col = lerp(ColSmoke, ColDeep, smoothstep(0.04, 0.34, heat));
    col = lerp(col, ColBrim, smoothstep(0.30, 0.74, heat));
    col = lerp(col, ColRoot, saturate(root * 0.85));
    col = lerp(col, ColRageF, saturate(rageTongue * 1.25));
    col = lerp(col, ColSmoke, saturate(smoke * 1.6) * (1.0 - heat));

    float alpha = saturate(heat * (0.52 + root * 0.40) + rageTongue * 0.85 + smoke * 0.50);
    //端点保底归零（碎断主要由 flameH 塌缩完成，这里只兜住条带最末几像素）
    alpha *= smoothstep(0.0, 0.03, u) * smoothstep(1.0, 0.97, u) * uFade;

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
