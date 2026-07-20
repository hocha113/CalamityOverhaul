// ============================================================================
//FishDrizzleFlame.fx 炼鱼硫火喷吐焰锥（根部锚定鱼嘴的附着式火焰射流）
//几何：沿喷射轴的加宽锥形 TriangleStrip；uv.x：0=鱼嘴根 → 1=远端；
//  uv.y 0..1 横跨锥体，0.5=射流中线；锥形展开在几何端完成，shader 在归一化空间工作
//层次：嘴根炽橙火核（唯一亮芯，极小）→ 硫磺红湍流焰体（沿轴外冲+中线蜿蜒）→
//  深红边缘 → 焰缘外黑烟圈（略越过焰尖）
//末端断法：高度场向 uLen 噪声塌缩+撕边，无平滑收口；uSputter 走高时低频缺口断续熄灭
//配色：炽橙根 → 硫磺橙红 → 深红 → 黑烟，无金无冷白，白色不驻留
//全笛卡尔条带坐标，无极角 → 接缝协议天然合规
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;    //秒
float uSeed;    //每鱼随机相位
float uLen;     //有效燃烧长度占条带比例 0..1，收势时向根塌缩
float uPower;   //喷射强度 0..1，点火抬升、燃尽走低
float uSputter; //熄火断续 0..1，走高时火流被低频缺口撕开
float uFade;    //整体不透明度

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

static const float3 ColRoot = float3(1.30, 0.58, 0.10);   //嘴根炽橙（暖，非白）
static const float3 ColBrim = float3(1.05, 0.26, 0.045);  //硫磺橙红
static const float3 ColDeep = float3(0.38, 0.055, 0.022); //深红
static const float3 ColSmoke = float3(0.045, 0.02, 0.014);//黑烟

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
    float vSigned = input.TexCoords.y * 2.0 - 1.0;

    //---- 射流中线蜿蜒：振幅随下游增大，根部锁死在嘴上（流体感非直梁） ----
    float meander = (tex2D(noiseSamp, float2(u * 1.4 - uTime * 2.6 + uSeed * 13.0, 0.31 + uSeed)).r - 0.5)
        * 0.62 * smoothstep(0.04, 0.55, u);
    float across = abs(vSigned - meander);

    //---- 湍流高度场：双八度沿轴外冲 ----
    float h1 = tex2D(noiseSamp, float2(u * 2.2 - uTime * 3.2 + uSeed * 11.0, uSeed)).r;
    float h2 = tex2D(noiseSamp, float2(u * 5.4 - uTime * 6.0 + uSeed * 3.0, 0.61 + uSeed)).r;
    float flameH0 = (0.34 + (h1 * 0.62 + h2 * 0.38) * 0.60) * (0.50 + uPower * 0.60);
    //嘴根收窄：贴嘴处焰体窄而实，下游展开
    flameH0 *= lerp(0.42, 1.0, smoothstep(0.0, 0.30, u));

    //---- 末端塌缩：收口边界噪声抖动，越过 uLen 燃尽（无平滑断面） ----
    float endN = tex2D(noiseSamp, float2(u * 4.0 + uTime * 1.4 + uSeed * 9.0, 0.47)).r;
    float endEnv = smoothstep(uLen, uLen * 0.55 - endN * 0.18, u);
    float flameH = flameH0 * endEnv;

    //---- 断续熄火缺口 ----
    float gapN = tex2D(noiseSamp, float2(u * 1.8 - uTime * 1.2 + uSeed * 7.0, 0.23 + uSeed)).r;
    float gapTh = uSputter * 0.85 - 0.12;
    float lit = smoothstep(gapTh - 0.05, gapTh + 0.16, gapN);

    //---- 撕边：扰动离心距，越离流心/越近末端撕得越碎 ----
    float rag = tex2D(noiseSamp, float2(u * 7.6 - uTime * 5.4 + uSeed, across * 1.9 + 0.37)).r;
    float shape = across + (rag - 0.5) * (0.14 + across * 0.72 + (1.0 - endEnv) * 0.55);

    //焰体：流心实 → 缘稀
    float flame = smoothstep(flameH, flameH * 0.30, shape) * lit;

    //嘴根火核：唯一亮芯，极小（u<~0.14 且贴流心）
    float root = pow(saturate(1.0 - u * 7.0), 2.2) * smoothstep(0.55, 0.10, across)
        * (0.35 + uPower * 0.65);

    //---- 焰缘黑烟：外圈一环，允许略越过焰尖（燃尽处烟先散） ----
    float envSmoke = smoothstep(saturate(uLen * 1.16 + 0.03), uLen * 0.48, u);
    float hSmoke = flameH0 * max(endEnv, envSmoke * 0.62);
    float smoke = saturate(smoothstep(hSmoke * 1.42, hSmoke * 0.90, shape)
        - smoothstep(hSmoke * 0.90, hSmoke * 0.38, shape));
    smoke *= (0.42 + uSputter * 0.30) * max(lit, 0.45);

    //---- 色程：暗外圈 → 饱和中层 → 极小热芯 ----
    float heat = saturate(flame + root * 0.55);
    float3 col = lerp(ColSmoke, ColDeep, smoothstep(0.03, 0.32, heat));
    col = lerp(col, ColBrim, smoothstep(0.28, 0.72, heat));
    col = lerp(col, ColRoot, saturate(root));
    col = lerp(col, ColSmoke, saturate(smoke * 1.55) * (1.0 - heat));

    //根区高频抖闪：热扰暗示，小振幅只调饱和层不提白
    float shimmer = tex2D(noiseSamp, float2(u * 9.0 - uTime * 7.5 + uSeed * 5.0, 0.83)).r;
    col *= 1.0 + (shimmer - 0.5) * 0.22 * (1.0 - u) * uPower;

    float alpha = saturate(heat * (0.55 + root * 0.38) + smoke * 0.46);
    //条带端点与横向外缘保底归零（碎断主要由 flameH 塌缩完成）
    alpha *= smoothstep(0.0, 0.025, u) * smoothstep(1.0, 0.985, u);
    alpha *= smoothstep(1.0, 0.86, across);
    alpha *= uFade;

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
