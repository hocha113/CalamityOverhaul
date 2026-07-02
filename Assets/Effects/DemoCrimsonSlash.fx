// ============================================================================
//DemoCrimsonSlash.fx 绯红裂空斩刀光（双模式 + 三层异步复用）
//uMode=0 极坐标月牙带：phi=atan2 仅经 u=phi/span+0.5 做单调比较与 clamp 采样，
//  无 sin/cos/噪声直接消费原始极角，分支切线(±π)落在弧外 → 极角审计合规
//uMode=1 直线刀刃带：u 沿刃长，白热中脊、两缘渐暗
//
//边缘语言（锋利感的来源）：
//  热度坐标 h：0=锋利侧(弧=外凸缘剃刀线/直线=中脊)，1=拖尾暗侧
//  h=0 侧轮廓活跃期绝对光滑（噪声参差只随 uErode 激活），贴边一条白热剃刀细线+
//  高光渐变；有机破碎/笔刷撕裂全部属于 h=1 侧；溶解从 h=1 侧向剃刀线推进，
//  剃刀线保持干净到最后
//uFlash：完全张开瞬间全形过曝再速落（干脆感 pop 帧）
//uTailErode：彗星逻辑——前缘还在揭开时起笔端已开始蒸发
//预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;         //秒
float uMode;         //0=弧形月牙 1=直线刀刃
float uSweep;        //0..1 扫掠揭开进度
float uErode;        //0..1 生命期整体侵蚀
float uTailErode;    //0..1 起笔端定向蒸发（彗星尾）
float uFlash;        //0..1 全形白闪帧
float uFlowPhase;    //能量沿刃奔涌的累计相位
float uColorShift;   //0..1 亮红→深酒红整体压暗
float uOpacity;      //整体不透明度
float uFlip;         //+1/-1 挥动镜像
float uSeed;         //实例随机相位
float uArcSpan;      //弧总跨度(弧度，须<2π)，仅弧模式
float uThick;        //带厚度(p 空间尺度，0..~0.4)
float uFrontGlow;    //扫掠前缘白热强度
float uFarSel;       //远近半侧分层：0=整体 +1=仅近半 -1=仅远半(玩家身后层)
float uFarDim;       //远半侧压暗系数（空间纵深的明度线索）
float2 uFarDirLocal; //quad uv 空间中指向"远侧(屏幕上方)"的单位向量

float3 uColHot;      //白热核心
float3 uColBright;   //亮绯红
float3 uColDeep;     //深红
float3 uColDark;     //暗酒红描边

texture uBrushTex;
sampler brushSamp = sampler_state
{
    texture = <uBrushTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = clamp;
};

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
    float2 p0 = (input.TexCoords - 0.5) * 2.0;   //未镜像坐标，供远近半侧判定
    float2 p = p0;
    p.y *= uFlip;

    //远近半侧分层：farW=1 远侧(屏幕上方) 0 近侧，边界羽化避免接缝
    float farW = smoothstep(-0.15, 0.15, dot(p0, uFarDirLocal));
    float passMul = 1.0;
    float dimMul = 1.0;
    if (uFarSel > 0.5)
    {
        passMul = 1.0 - farW;         //近半侧 pass
    }
    else if (uFarSel < -0.5)
    {
        passMul = farW;               //远半侧 pass（玩家身后）
        dimMul = uFarDim;
    }
    if (passMul < 0.004)
        return float4(0, 0, 0, 0);

    float isArc = uMode < 0.5 ? 1.0 : 0.0;

    //---- 模式坐标 ----
    float r = length(p);
    float phi = atan2(p.y, p.x);
    float uRawArc = phi / uArcSpan + 0.5;
    float uRawLine = input.TexCoords.x;
    float uRaw = lerp(uRawLine, uRawArc, isArc);
    float uc = saturate(uRaw);

    //厚度包络：弧峰值偏收笔端(~0.7)，直线近中央，两端收尖
    float envPow = lerp(1.35, 1.85, isArc);
    float env = sin(pow(uc, envPow) * PI);
    float w = uThick * pow(max(env, 0.0), 0.72);
    if (w < 0.004)
        return float4(0, 0, 0, 0);

    //参差噪声（只允许骚扰 h=1 暗侧与消散期）
    float jag1 = tex2D(noiseSamp, float2(uc * 2.6 + uSeed, 0.19 + uSeed * 0.7)).r - 0.5;
    float jag2 = tex2D(noiseSamp, float2(uc * 6.5 - uTime * 0.10 + uSeed, 0.71)).r - 0.5;
    float jag = jag1 * 0.055 + jag2 * 0.030;

    //---- v/h 坐标：v 径向位置，h 热度坐标(0=锋利侧 1=暗侧) ----
    float v;
    float h;
    if (uMode < 0.5)
    {
        //弧：外凸缘为剃刀线。外缘半径活跃期光滑，参差仅随侵蚀激活
        float outerR = 0.90 + jag * uErode * 1.5;
        float innerR = outerR - w;
        v = (r - innerR) / w;           //0=内(拖尾暗侧) 1=外(剃刀线)
        if (v < -0.30 || v > 1.12)
            return float4(0, 0, 0, 0);
        h = 1.0 - saturate(v);
    }
    else
    {
        //直线：中脊为锋利侧，两缘渐暗；缘部参差轻度+侵蚀期加剧
        v = abs(p.y) / w + jag * (0.5 + uErode * 1.8);
        if (v > 1.12)
            return float4(0, 0, 0, 0);
        h = saturate(v);
    }
    float vc = saturate(v);

    //---- 笔刷主体：双八度 + 沿刃奔涌相位 ----
    float flow = uTime * 0.30 + uSeed;
    float4 b1 = tex2D(brushSamp, float2(uc * 1.30 - flow * 0.22 - uFlowPhase, vc));
    float4 b2 = tex2D(brushSamp, float2(uc * 3.10 + flow * 0.40 + uFlowPhase * 0.6 + 0.37, vc * 0.62 + 0.20));
    float streak = b1.r * b1.a * 0.85 + b2.r * b2.a * 0.55;

    //---- 扫掠揭开 + 前缘光带 ----
    float edge = uSweep * 1.10 - 0.04;
    float reveal = smoothstep(edge + 0.012, edge - 0.055, uRaw);
    float front = exp(-pow((uRaw - edge) / 0.05, 2.0)) * uFrontGlow;

    //---- 溶解：生命期整体（从暗侧向剃刀线推进）+ 彗星尾定向蒸发 ----
    float eN = tex2D(noiseSamp, float2(uc * 2.3 + uSeed * 3.1, vc * 0.85 + uSeed)).r * 0.65
             + tex2D(noiseSamp, float2(uc * 5.2 - uTime * 0.14, vc * 1.60 + 0.40)).r * 0.35;
    float eLife = uErode * 1.18 - (1.0 - h) * 0.30;   //h=0 剃刀线最后死
    float eTail = uTailErode * 1.35 - uc * 1.05;      //uc=0 起笔端先蒸发
    float eTh = max(eLife, eTail);
    float survive = smoothstep(eTh - 0.02, eTh + 0.12, eN);
    float burn = smoothstep(eTh - 0.16, eTh - 0.02, eN) * (1.0 - survive);

    //---- alpha 合成 ----
    //锋利侧轮廓：紧致光滑，无笔刷撕裂
    float aSharp;
    //暗侧轮廓：有机破碎（笔刷撕裂 + 参差）
    float aDark;
    if (uMode < 0.5)
    {
        aSharp = smoothstep(1.06, 0.96, v);
        aDark = smoothstep(-0.10, 0.18, v + jag * (0.8 + uErode * 2.0) + (0.5 - streak) * 0.42);
    }
    else
    {
        aSharp = smoothstep(1.06, 0.90, v);
        aDark = 1.0;
    }
    float tipFeather = smoothstep(0.0, 0.05, uc) * smoothstep(1.0, 0.952, uc);

    float alpha = aSharp * aDark * tipFeather * reveal * survive;
    //笔刷透密调制：白闪帧轻抬下限即可，避免整面被拉到全不透明糊掉笔触细节
    alpha *= lerp(saturate(0.42 + streak * 0.95), 1.0, uFlash * 0.40);
    alpha = saturate(alpha) * uOpacity * passMul;

    //---- 色带（沿 h：剃刀线 → 高光渐变 → 主体 → 暗描边） ----
    float3 col = lerp(uColHot, uColBright, smoothstep(0.02, 0.24, h));
    col = lerp(col, uColDeep, smoothstep(0.30, 0.66, h));
    col = lerp(col, uColDark, smoothstep(0.62, 1.0, h));
    col = lerp(col, lerp(uColDark, uColDeep * 0.55, h), uColorShift * 0.85);

    //剃刀细线：贴 h=0 轮廓一条白热高光，活跃期恒亮，压暗期淡出
    float razor = exp(-pow(h / 0.075, 2.0));
    col += uColHot * razor * 1.15 * (1.0 - uColorShift * 0.65);

    //笔刷高光 + 燃边 + 前缘 + 全形白闪
    //白闪只做适度提亮增益（不整面覆盖成纯白），笔刷streak/色带在闪光期依然可辨——
    //这是"干脆感"与"细节质感"的平衡点：闪光是"提亮一拍"而不是"擦掉重画"
    col += uColBright * streak * (1.0 - h * 0.55) * 0.50;
    col += float3(1.25, 0.42, 0.18) * burn * 2.3;
    col += uColHot * front * 2.6;
    col = lerp(col, col + uColHot * 0.55, saturate(uFlash));

    //远半侧压暗（空间纵深线索），略偏冷偏暗
    col *= dimMul;

    //剃刀线/前缘/白闪在 alpha 之外再给增益 → 半加法辉光
    float glowA = saturate(alpha + (front * 0.55 + burn * 0.25 + razor * 0.18 + uFlash * 0.16)
        * uOpacity * reveal * tipFeather * survive * passMul);
    return float4(col * alpha + uColHot * (front * 0.35 + uFlash * 0.08) * uOpacity * tipFeather * reveal * passMul * dimMul, glowA);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
