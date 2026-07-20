// ============================================================================
//OniMacheteSlash.fx 鬼砍刀挥砍刀光（硫磺橙红 + 熔金调色）
//单模式极坐标月牙带：phi=atan2 仅经 u=phi/span+0.5 做单调比较与 clamp 采样，
//  无 sin/cos/噪声直接消费原始极角；atan2 的 ±π 切线映射到 uRaw≈0.5±π/span，
//  span<2π 时落在包络零区之外 → 极角审计合规（全部噪声输入为 uc/vc）
//
//边缘语言：h=0 外凸缘 = 熔金剃刀线（活跃期绝对光滑 + 白金高光），
//  h=1 内侧拖尾暗侧（硫火橙红 → 焦黑酒红，噪声撕裂全部属于这一侧）
//熔金脉络：中带内一层沿刃流动的金丝高光（重斩拍加强），读作刀身里掺的黄金被甩出来
//uSweep 扫掠揭开 + 前缘白热；uErode 生命期从暗侧向剃刀线侵蚀；
//uTailErode 起笔端定向蒸发；uFlash 完全张开瞬间全形过曝 1~2 帧
//预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //秒
float uSweep;     //0..1 扫掠揭开进度
float uErode;     //0..1 生命期整体侵蚀
float uTailErode; //0..1 起笔端定向蒸发
float uFlash;     //0..1 全形白闪帧
float uOpacity;   //整体不透明度
float uFlip;      //+1/-1 挥动镜像
float uSeed;      //实例随机相位
float uArcSpan;   //弧总跨度(弧度，须<2π)
float uThick;     //带厚度(p 空间尺度)
float uFrontGlow; //扫掠前缘白热强度
float uGoldVein;  //0..1 熔金脉络权重（重斩拍加强）

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

#define PI 3.14159265

//==== 调色（白金热核 / 熔金 / 硫火橙红 / 焦黑酒红）====
static const float3 ColHot = float3(1.60, 1.42, 1.02);
static const float3 ColGold = float3(1.18, 0.80, 0.24);
static const float3 ColBrim = float3(1.15, 0.30, 0.06);
static const float3 ColDark = float3(0.20, 0.045, 0.02);

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
    float2 p = (input.TexCoords - 0.5) * 2.0;
    p.y *= uFlip;

    float r = length(p);
    float phi = atan2(p.y, p.x);
    float uRaw = phi / uArcSpan + 0.5;   //仅单调比较与 clamp，不进任何周期函数
    float uc = saturate(uRaw);

    //厚度包络：峰值偏收笔端，两端收尖
    float env = sin(pow(uc, 1.75) * PI);
    float w = uThick * pow(max(env, 0.0), 0.72);
    if (w < 0.004)
        return float4(0, 0, 0, 0);

    //参差噪声（只骚扰暗侧与侵蚀期）
    float jag = tex2D(noiseSamp, float2(uc * 2.8 + uSeed, 0.23 + uSeed * 0.7)).r - 0.5;

    //v 径向位置：0=内(暗侧) 1=外(剃刀线)；h 热度坐标 0=锋利侧
    float outerR = 0.90 + jag * uErode * 0.12;
    float innerR = outerR - w;
    float v = (r - innerR) / w;
    if (v < -0.12 || v > 1.10)
        return float4(0, 0, 0, 0);
    float h = 1.0 - saturate(v);
    float vc = saturate(v);

    //---- 笔触主体：双八度沿刃拉丝 ----
    float flow = uTime * 0.24 + uSeed;
    float s1 = tex2D(noiseSamp, float2(uc * 1.5 - flow * 0.30, vc * 0.9 + uSeed)).r;
    float s2 = tex2D(noiseSamp, float2(uc * 4.2 + flow * 0.45 + 0.37, vc * 0.55 + 0.21)).r;
    float streak = s1 * 0.62 + s2 * 0.38;

    //---- 熔金脉络：中带里的细金丝（脊线提取），沿刃向收笔端奔涌 ----
    float veinN = tex2D(noiseSamp, float2(uc * 3.4 - flow * 0.8 + uSeed * 3.1, vc * 1.4 + 0.55)).r;
    float vein = smoothstep(0.10, 0.0, abs(veinN - 0.5)) * smoothstep(0.05, 0.30, h) * smoothstep(0.85, 0.45, h);

    //---- 扫掠揭开 + 前缘白热 ----
    float edge = uSweep * 1.08 - 0.04;
    float reveal = smoothstep(edge + 0.012, edge - 0.06, uRaw);
    float front = exp(-pow((uRaw - edge) / 0.05, 2.0)) * uFrontGlow;

    //---- 溶解：生命期整体（暗侧先死，剃刀线最后）+ 彗星尾定向蒸发 ----
    float eN = tex2D(noiseSamp, float2(uc * 2.4 + uSeed * 3.3, vc * 0.9 + uSeed)).r * 0.65
             + tex2D(noiseSamp, float2(uc * 5.6 - uTime * 0.12, vc * 1.7 + 0.41)).r * 0.35;
    float eTh = max(uErode * 1.18 - (1.0 - h) * 0.30, uTailErode * 1.35 - uc * 1.05);
    float survive = smoothstep(eTh - 0.02, eTh + 0.12, eN);
    float burn = smoothstep(eTh - 0.16, eTh - 0.02, eN) * (1.0 - survive);

    //---- alpha 合成：锋利侧紧致光滑；暗侧有机破碎 ----
    float aSharp = smoothstep(1.06, 0.95, v);
    float aDark = smoothstep(-0.08, 0.20, v + jag * (0.7 + uErode * 1.8) + (0.5 - streak) * 0.40);
    float tipFeather = smoothstep(0.0, 0.05, uc) * smoothstep(1.0, 0.95, uc);
    float alpha = aSharp * aDark * tipFeather * reveal * survive;

    //透密调制：笔触透密 + 白闪抬升下限
    alpha *= lerp(saturate(0.45 + streak * 0.95), 1.0, uFlash * 0.40);
    alpha = saturate(alpha) * uOpacity;

    //---- 色带（沿 h：熔金剃刀线 → 金橙渐变 → 硫火橙红 → 焦黑）----
    float3 col = lerp(ColHot, ColGold, smoothstep(0.02, 0.20, h));
    col = lerp(col, ColBrim, smoothstep(0.16, 0.52, h));
    col = lerp(col, ColDark, smoothstep(0.55, 1.0, h));

    //熔金剃刀细线：贴 h=0 一条白金高光
    float razor = exp(-pow(h / 0.08, 2.0));
    col += ColHot * razor * 1.10;

    //熔金脉络 + 笔触高光 + 燃边 + 前缘 + 白闪
    col += ColGold * vein * (0.9 + uGoldVein * 1.4);
    col += ColBrim * streak * (1.0 - h * 0.55) * 0.45;
    col += float3(1.30, 0.55, 0.16) * burn * 2.2;
    col += ColHot * front * 2.5;
    col = lerp(col, col + ColHot * 0.55, saturate(uFlash));

    //剃刀线/前缘/白闪在 alpha 之外再给增益 → 半加法辉光
    float glowA = saturate(alpha + (front * 0.55 + burn * 0.25 + razor * 0.16 + uFlash * 0.15)
        * uOpacity * reveal * tipFeather * survive);
    return float4(col * alpha + ColHot * (front * 0.32 + uFlash * 0.08)
        * uOpacity * tipFeather * reveal, glowA);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
