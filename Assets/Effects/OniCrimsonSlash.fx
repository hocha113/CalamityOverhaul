// ============================================================================
//OniCrimsonSlash.fx 绯红裂空斩刀光（双模式 + 三层异步复用 + 可调水墨材质）
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
//uTailErode：彗星逻辑，前缘还在揭开时起笔端已开始蒸发
//
//水墨件套（自 OniAnnihilateArc 降档移植，uInk=0 时与原光润能量完全一致）：
//  uInk 主权重：domain-warp 墨场替换双八度拉丝、墨分五色密度台阶 + 积墨线、
//    淡墨透密呼吸、暗侧吃墨调色、辉光增益收敛、笔刷滚动降速（墨落纸不流动）；
//  uFeiBai 飞白干笔断丝（只骚扰暗侧中后段）；uBleed 洇边墨晕外渗（双模式）；
//  uSplitTail 散锋：起笔端 4 条锋毫独立蒸发 + 带间刻槽，收笔读作笔毫分叉。
//  全部噪声输入为 uc/vc/quad uv，无裸极角进噪声 → 极角审计合规
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
float uRazorTailWiden; //剃刀线向收笔端展宽强度（0=恒定宽；>0 外弧白热高光向末端加粗）

float uInk;          //0..1 水墨主权重（0=原光润能量，1=全墨相）
float uFeiBai;       //0..1 飞白干笔断丝强度
float uBleed;        //0..1 洇边外渗进度
float uSplitTail;    //0..1 散锋分叉强度

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
        //弧：外凸缘为剃刀线。外缘半径活跃期光滑，参差仅随侵蚀激活；
        //暗侧裁剪放宽到 -0.46 给洇边墨晕留出羽化空间
        float outerR = 0.90 + jag * uErode * 1.5;
        float innerR = outerR - w;
        v = (r - innerR) / w;           //0=内(拖尾暗侧) 1=外(剃刀线)
        if (v < -0.46 || v > 1.12)
            return float4(0, 0, 0, 0);
        h = 1.0 - saturate(v);
    }
    else
    {
        //直线：中脊为锋利侧，两缘渐暗；缘部参差轻度+侵蚀期加剧；
        //外侧裁剪放宽到 1.42 给洇边墨晕留出羽化空间
        v = abs(p.y) / w + jag * (0.5 + uErode * 1.8);
        if (v > 1.42)
            return float4(0, 0, 0, 0);
        h = saturate(v);
    }
    float vc = saturate(v);

    //---- 笔刷主体：双八度 + 沿刃奔涌相位（墨相下滚动降速，墨落在纸上不流动） ----
    float flow = uTime * 0.30 * (1.0 - uInk * 0.85) + uSeed;
    float4 b1 = tex2D(brushSamp, float2(uc * 1.30 - flow * 0.22 - uFlowPhase, vc));
    float4 b2 = tex2D(brushSamp, float2(uc * 3.10 + flow * 0.40 + uFlowPhase * 0.6 + 0.37, vc * 0.62 + 0.20));
    float streak = b1.r * b1.a * 0.85 + b2.r * b2.a * 0.55;

    //---- 内部墨场：双层反向 domain-warp 大理石纹（墨相内部纹理主体） ----
    float2 ip = float2(uc * 2.2 + uSeed * 3.1, vc * 1.1 + uSeed);
    float2 warp = float2(tex2D(noiseSamp, ip * 0.55).r
        , tex2D(noiseSamp, ip * 0.55 + float2(0.31, 0.47)).r) - 0.5;
    float n1 = tex2D(noiseSamp, ip + warp * 0.35 + float2(-uFlowPhase * 0.25 - uTime * 0.03, 0.0)).r;
    float n2 = tex2D(noiseSamp, float2(uc * 5.1 + 0.37, vc * 2.3 + 0.13) + warp * 0.60
        + float2(uFlowPhase * 0.40 + uTime * 0.05, 0.0)).r;
    float ink = n1 * 0.62 + n2 * 0.38;

    //---- 墨分五色：软阶化成 4 档密度台阶 + 台阶交界压一线"积墨" ----
    float stepped = min(floor(ink * 4.0), 3.0) / 3.0;
    float inkq = lerp(ink, stepped, uInk * 0.65);
    float stepFrac = frac(ink * 4.0);
    float inkEdge = (1.0 - smoothstep(0.02, 0.12, min(stepFrac, 1.0 - stepFrac))) * uInk;

    //---- 扫掠揭开 + 前缘光带 ----
    float edge = uSweep * 1.10 - 0.04;
    float reveal = smoothstep(edge + 0.012, edge - 0.055, uRaw);
    float front = exp(-pow((uRaw - edge) / 0.05, 2.0)) * uFrontGlow;

    //---- 散锋：起笔端按 v 切 4 条锋毫、独立蒸发阈值偏移 + 带间刻槽 ----
    float lane = min(floor(vc * 4.0), 3.0);
    float laneN = tex2D(noiseSamp, float2(lane * 0.25 + uSeed * 11.3, 0.53)).r - 0.5;
    float tailZone = 1.0 - smoothstep(0.05, 0.30, uc);
    float laneFrac = frac(vc * 4.0);
    float groove = 1.0 - smoothstep(0.05, 0.17, min(laneFrac, 1.0 - laneFrac));

    //---- 溶解：生命期整体（从暗侧向剃刀线推进）+ 彗星尾定向蒸发（按锋毫拆带） ----
    float eN = tex2D(noiseSamp, float2(uc * 2.3 + uSeed * 3.1, vc * 0.85 + uSeed)).r * 0.65
             + tex2D(noiseSamp, float2(uc * 5.2 - uTime * 0.14, vc * 1.60 + 0.40)).r * 0.35;
    float eLife = uErode * 1.18 - (1.0 - h) * 0.30;   //h=0 剃刀线最后死
    float eTail = uTailErode * 1.35 - uc * 1.05       //uc=0 起笔端先蒸发
        + laneN * 0.34 * uSplitTail * tailZone;       //锋毫逐带分叉收笔
    float eTh = max(eLife, eTail);
    float survive = smoothstep(eTh - 0.02, eTh + 0.12, eN);
    float burn = smoothstep(eTh - 0.16, eTh - 0.02, eN) * (1.0 - survive);

    //---- alpha 合成 ----
    //锋利侧轮廓：紧致光滑，无笔刷撕裂
    float aSharp;
    //暗侧轮廓：有机破碎（墨相下由墨场撕裂，光润下由笔刷撕裂）
    float aDark;
    float tear = lerp(streak, ink, uInk);
    if (uMode < 0.5)
    {
        aSharp = smoothstep(1.06, 0.96, v);
        aDark = smoothstep(-0.10, 0.18, v + jag * (0.8 + uErode * 2.0) + (0.5 - tear) * 0.42);
    }
    else
    {
        aSharp = smoothstep(1.06, 0.90, v);
        aDark = 1.0;
    }
    float tipFeather = smoothstep(0.0, 0.05, uc) * smoothstep(1.0, 0.952, uc);

    float alpha = aSharp * aDark * tipFeather * reveal * survive;

    //飞白：uc 向拉长、v 向细密的干笔断丝，只骚扰暗侧中后段（h<0.2 刃侧不碰）
    float fbN = tex2D(noiseSamp, float2(uc * 1.8 + uSeed * 7.7, vc * 7.0 + uSeed * 2.3)).r * 0.72
              + tex2D(noiseSamp, float2(uc * 6.0 + uSeed, vc * 3.0 + 0.61)).r * 0.28;
    float fbZone = smoothstep(0.40, 0.72, uc) * smoothstep(0.20, 0.45, h);
    float feiBai = smoothstep(0.58, 0.46, fbN);
    alpha *= 1.0 - feiBai * fbZone * uFeiBai * 0.85;

    //散锋带间刻槽：随尾蒸发激活，锋毫读作分开的几缕
    alpha *= 1.0 - groove * tailZone * uSplitTail * saturate(uTailErode * 2.2) * 0.80;

    //透密调制：光润走笔刷透密，墨相走密度透密（淡墨更透，水墨的呼吸），白闪帧轻抬下限；
    //下限压低让暗墨档更多透出背景，避免读作实心黑块
    float floorGloss = saturate(0.42 + streak * 0.95);
    float floorInk = saturate(0.25 + inkq * 0.95 + streak * 0.22);
    alpha *= lerp(lerp(floorGloss, floorInk, uInk), 1.0, uFlash * 0.40);
    alpha = saturate(alpha) * uOpacity * passMul;

    //---- 洇边：暗侧轮廓外的低 alpha 羽化墨晕（墨吃进纸里），低频噪声定形 ----
    float bleedN = tex2D(noiseSamp, float2(uc * 1.4 + uSeed * 5.9, 0.31 + uSeed)).r;
    float bleedBand;
    if (uMode < 0.5)
        bleedBand = smoothstep(-0.44, -0.10, v) * (1.0 - smoothstep(-0.10, 0.06, v));
    else
        bleedBand = smoothstep(0.98, 1.10, v) * (1.0 - smoothstep(1.10, 1.40, v));
    float bleedA = bleedBand * (0.35 + bleedN * 0.65) * uBleed * 0.18
        * tipFeather * reveal * survive * uOpacity * passMul;

    //---- 色带（沿 h：剃刀线 → 高光渐变 → 主体 → 暗描边） ----
    //末端展宽：h 是相对带厚的比例坐标，带在收笔端本就收窄，按比例展宽读作
    //"白热渐渐吃满末端带宽"（力量积聚在收势），绝对像素宽不会爆
    float widen = uRazorTailWiden * smoothstep(0.38, 0.97, uc);
    float3 col = lerp(uColHot, uColBright, smoothstep(0.02, 0.24 + widen * 0.16, h));
    col = lerp(col, uColDeep, smoothstep(0.30, 0.66, h));
    col = lerp(col, uColDark, smoothstep(0.62, 1.0, h));

    //墨体：h 越深越吃墨场，浓淡台阶主导 + 绯红丝筋；积墨线只压墨体区。
    //暗端抬到深酒红以上（近黑墨块在明亮天空背景上过闷），混入上限压到 0.88 保留底层红色透光
    float3 inkCol = lerp(uColDark * 1.75, uColDeep * 1.05, smoothstep(0.10, 0.58, inkq));
    inkCol += uColBright * smoothstep(0.72, 0.95, n2) * 0.42;
    col = lerp(col, inkCol, smoothstep(0.28, 0.66, h) * uInk * 0.88);
    col *= 1.0 - inkEdge * 0.14 * smoothstep(0.20, 0.55, h);

    col = lerp(col, lerp(uColDark, uColDeep * 0.55, h), uColorShift * 0.85);

    //剃刀细线：贴 h=0 轮廓一条白热高光，活跃期恒亮，压暗期淡出，向收笔端展宽增亮
    float razor = exp(-pow(h / (0.075 * (1.0 + widen * 1.6)), 2.0));
    col += uColHot * razor * (1.15 + widen * 0.35) * (1.0 - uColorShift * 0.65);

    //笔刷高光 + 燃边 + 前缘 + 全形白闪
    //白闪只做适度提亮增益（不整面覆盖成纯白），笔刷streak/色带在闪光期依然可辨
    //这是"干脆感"与"细节质感"的平衡点：闪光是"提亮一拍"而不是"擦掉重画"
    //墨相下笔刷高光大幅收敛（只留行笔方向感），刀光读作纸上的颜料而非发光体
    col += uColBright * streak * (1.0 - h * 0.55) * 0.50 * (1.0 - uInk * 0.72);
    col += float3(1.25, 0.42, 0.18) * burn * 2.3;
    col += uColHot * front * 2.6;
    col = lerp(col, col + uColHot * 0.55, saturate(uFlash));

    //远半侧压暗（空间纵深线索），略偏冷偏暗
    col *= dimMul;

    //剃刀线/前缘/白闪在 alpha 之外再给增益 → 半加法辉光（墨相收敛）；洇边按自身 alpha 叠墨色
    float glowA = saturate(alpha + bleedA + (front * 0.55 + burn * 0.25 + razor * 0.18 + uFlash * 0.16)
        * (1.0 - uInk * 0.45) * uOpacity * reveal * tipFeather * survive * passMul);
    return float4(col * alpha + uColDark * 0.60 * bleedA * dimMul
        + uColHot * (front * 0.35 + uFlash * 0.08) * (1.0 - uInk * 0.35)
        * uOpacity * tipFeather * reveal * passMul * dimMul, glowA);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
