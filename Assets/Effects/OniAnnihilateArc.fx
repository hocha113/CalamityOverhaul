// ============================================================================
//OniAnnihilateArc.fx 鬼哭·灭世一闪：水墨巨弧（ArcTech）+ 泼墨罡气舌（BurstTech）
//
//ArcTech：以 OniFinaleBlade 的弧路径为底本，h/v 边缘语言不变（h=0 外凸缘剃刀线
//  活跃期绝对光滑，h=1 内侧拖尾暗侧允许有机破碎），内部全面水墨化：
//  1) 墨分五色：domain-warp 墨场软阶化成 4 档密度台阶（焦/浓/重/淡），台阶交界
//     压暗一线"积墨"（水彩边缘沉积），淡墨档更透明——uInkStep；
//  2) 飞白：v 向细密、uc 向拉长的干笔断丝阈值带，只骚扰暗侧中后段（h<0.2 刃侧
//     不碰），随侵蚀期加剧——uFeiBai；
//  3) 洇边：暗侧轮廓内缘外一圈低 alpha 羽化墨晕（墨吃进纸里），低频噪声定形、
//     C# 时间轴推动外渗——uBleed；
//  4) 散锋：起笔端按 v 切 4 条锋毫窄带，每带独立蒸发阈值偏移 + 带间刻槽，
//     彗星尾蒸发读作一支笔的毫毛分叉收笔——uSplitTail；
//  5) 内部墨纹：双层反向 domain-warp 大理石纹替代笔刷双八度拉丝（笔刷只留一层
//     弱纹理保住行笔方向感），uFlowPhase 驱动沿弧漂移。
//
//BurstTech：施展帧玩家身周的泼墨罡气舌——根部锚定羽化、尖端噪声撕裂的放射墨舌，
//  uDissolve 从根向尖散掉；舌体黑红近墨、缘部一线绯红燃边。
//
//极角审计：phi=atan2 仅经 uRaw=phi/uArcSpan+0.5 单调映射后 clamp/采样消费
//  （span<2π，±π 分支切线落在弧外）；全部噪声输入为 uc/vc/quad uv，
//  无裸 phi 进 sin/cos/噪声；BurstTech 纯 quad uv 无极角。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;           //秒
float uSweep;          //0..1 扫掠揭开进度
float uErode;          //0..1 生命期整体侵蚀
float uTailErode;      //0..1 起笔端定向蒸发（散锋载体）
float uFlash;          //0..1 全形白闪帧
float uFlowPhase;      //墨纹沿弧漂移的累计相位
float uColorShift;     //0..1 亮色→暗酒红整体压暗（余烬态）
float uOpacity;        //整体不透明度
float uFlip;           //+1/-1 挥动镜像
float uSeed;           //实例随机相位
float uArcSpan;        //弧总跨度(弧度，须<2π)
float uThick;          //带厚度(p 空间尺度)
float uFrontGlow;      //扫掠前缘白热强度
float uRazorTailWiden; //剃刀线向收笔端展宽强度

float uInkStep;        //0..1 墨分五色（密度阶化 + 积墨线）强度
float uFeiBai;         //0..1 飞白干笔断丝强度
float uBleed;          //0..1 洇边外渗进度
float uSplitTail;      //0..1 散锋分叉强度

float uDissolve;       //BurstTech：0..1 从根向尖散掉
float uIntensity;      //BurstTech：整体强度包络

float3 uColHot;        //白热核心
float3 uColBright;     //主亮绯红
float3 uColDeep;       //深红
float3 uColDark;       //暗酒红（墨底）

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

// ============================ ArcTech ============================

float4 ArcPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    p.y *= uFlip;

    float r = length(p);
    float phi = atan2(p.y, p.x);
    float uRaw = phi / uArcSpan + 0.5;
    float uc = saturate(uRaw);

    //厚度包络：峰值偏收笔端(~0.7)
    float env = sin(pow(uc, 1.85) * PI);
    float w = uThick * pow(max(env, 0.0), 0.72);
    if (w < 0.004)
        return float4(0, 0, 0, 0);

    //参差噪声（只允许骚扰暗侧与消散期）
    float jag1 = tex2D(noiseSamp, float2(uc * 2.6 + uSeed, 0.19 + uSeed * 0.7)).r - 0.5;
    float jag2 = tex2D(noiseSamp, float2(uc * 6.5 - uTime * 0.10 + uSeed, 0.71)).r - 0.5;
    float jag = jag1 * 0.055 + jag2 * 0.030;

    //v/h 坐标：v=1 外凸缘剃刀线，v→负 为内侧拖尾暗侧（洇边预留到 -0.46）
    float outerR = 0.90 + jag * uErode * 1.5;
    float innerR = outerR - w;
    float v = (r - innerR) / w;
    if (v < -0.46 || v > 1.12)
        return float4(0, 0, 0, 0);
    float h = 1.0 - saturate(v);
    float vc = saturate(v);

    //---- 内部墨场：双层反向 domain-warp 大理石纹 ----
    float2 ip = float2(uc * 2.2 + uSeed * 3.1, vc * 1.1 + uSeed);
    float2 warp = float2(tex2D(noiseSamp, ip * 0.55).r
        , tex2D(noiseSamp, ip * 0.55 + float2(0.31, 0.47)).r) - 0.5;
    float n1 = tex2D(noiseSamp, ip + warp * 0.35 + float2(-uFlowPhase * 0.25 - uTime * 0.03, 0.0)).r;
    float n2 = tex2D(noiseSamp, float2(uc * 5.1 + 0.37, vc * 2.3 + 0.13) + warp * 0.60
        + float2(uFlowPhase * 0.40 + uTime * 0.05, 0.0)).r;
    float ink = n1 * 0.62 + n2 * 0.38;

    //---- 墨分五色：软阶化 + 积墨线 ----
    float stepped = min(floor(ink * 4.0), 3.0) / 3.0;
    float inkq = lerp(ink, stepped, uInkStep * 0.65);
    float stepFrac = frac(ink * 4.0);
    float inkEdge = (1.0 - smoothstep(0.02, 0.12, min(stepFrac, 1.0 - stepFrac))) * uInkStep;

    //---- 笔刷弱纹理：行笔方向感 ----
    float4 b1 = tex2D(brushSamp, float2(uc * 1.30 - uFlowPhase - uTime * 0.06, vc));
    float streak = b1.r * b1.a;

    //---- 扫掠揭开 + 前缘光带 ----
    float edge = uSweep * 1.10 - 0.04;
    float reveal = smoothstep(edge + 0.012, edge - 0.055, uRaw);
    float front = exp(-pow((uRaw - edge) / 0.05, 2.0)) * uFrontGlow;

    //---- 散锋：起笔端 4 条锋毫、独立蒸发阈值 + 带间刻槽 ----
    float lane = min(floor(vc * 4.0), 3.0);
    float laneN = tex2D(noiseSamp, float2(lane * 0.25 + uSeed * 11.3, 0.53)).r - 0.5;
    float tailZone = 1.0 - smoothstep(0.05, 0.30, uc);
    float laneFrac = frac(vc * 4.0);
    float groove = 1.0 - smoothstep(0.05, 0.17, min(laneFrac, 1.0 - laneFrac));

    //---- 溶解：生命期整体（h=0 剃刀线最后死）+ 彗星尾定向蒸发（按锋毫拆带） ----
    float eN = tex2D(noiseSamp, float2(uc * 2.3 + uSeed * 3.1, vc * 0.85 + uSeed)).r * 0.65
             + tex2D(noiseSamp, float2(uc * 5.2 - uTime * 0.14, vc * 1.60 + 0.40)).r * 0.35;
    float eLife = uErode * 1.18 - (1.0 - h) * 0.30;
    float eTail = uTailErode * 1.35 - uc * 1.05 + laneN * 0.34 * uSplitTail * tailZone;
    float eTh = max(eLife, eTail);
    float survive = smoothstep(eTh - 0.02, eTh + 0.12, eN);
    float burn = smoothstep(eTh - 0.16, eTh - 0.02, eN) * (1.0 - survive);

    //---- alpha 合成 ----
    //锋利侧轮廓紧致光滑；暗侧轮廓被墨场撕出有机破碎
    float aSharp = smoothstep(1.06, 0.96, v);
    float aDark = smoothstep(-0.10, 0.18, v + jag * (0.8 + uErode * 2.0) + (0.5 - ink) * 0.40);
    float tipFeather = smoothstep(0.0, 0.05, uc) * smoothstep(1.0, 0.952, uc);

    float alpha = aSharp * aDark * tipFeather * reveal * survive;

    //飞白：uc 向拉长、v 向细密的干笔断丝，暗侧中后段，随侵蚀加剧
    float fbN = tex2D(noiseSamp, float2(uc * 1.8 + uSeed * 7.7, vc * 7.0 + uSeed * 2.3)).r * 0.72
              + tex2D(noiseSamp, float2(uc * 6.0 + uSeed, vc * 3.0 + 0.61)).r * 0.28;
    float fbZone = smoothstep(0.40, 0.72, uc) * smoothstep(0.20, 0.45, h);
    float feiBai = smoothstep(0.58, 0.46, fbN);
    alpha *= 1.0 - feiBai * fbZone * uFeiBai * 0.85;

    //散锋带间刻槽：随尾蒸发激活，锋毫读作分开的几缕
    alpha *= 1.0 - groove * tailZone * uSplitTail * saturate(uTailErode * 2.2) * 0.80;

    //墨密度透密：淡墨更透（水墨的呼吸），白闪帧抬下限
    alpha *= lerp(saturate(0.30 + inkq * 0.95 + streak * 0.22), 1.0, uFlash * 0.40);
    alpha = saturate(alpha) * uOpacity;

    //---- 洇边：暗侧轮廓外的低 alpha 羽化墨晕 ----
    float bleedN = tex2D(noiseSamp, float2(uc * 1.4 + uSeed * 5.9, 0.31 + uSeed)).r;
    float bleedBand = smoothstep(-0.44, -0.10, v) * (1.0 - smoothstep(-0.10, 0.06, v));
    float bleedA = bleedBand * (0.35 + bleedN * 0.65) * uBleed * 0.18
        * tipFeather * reveal * survive * uOpacity;

    //---- 色带 ----
    float widen = uRazorTailWiden * smoothstep(0.38, 0.97, uc);
    //刃侧渐变承袭原语言
    float3 col = lerp(uColHot, uColBright, smoothstep(0.02, 0.24 + widen * 0.16, h));
    //墨体：h 越深越吃墨场，浓淡台阶主导 + 绯红丝筋
    float3 inkCol = lerp(uColDark * 0.85, uColDeep, smoothstep(0.15, 0.62, inkq));
    inkCol += uColBright * smoothstep(0.72, 0.95, n2) * 0.35;
    col = lerp(col, inkCol, smoothstep(0.24, 0.60, h));
    //积墨线压暗（只在墨体区）
    col *= 1.0 - inkEdge * 0.22 * smoothstep(0.20, 0.55, h);
    //余烬压暗期
    col = lerp(col, lerp(uColDark, uColDeep * 0.55, h), uColorShift * 0.85);

    //剃刀细线：贴 h=0 轮廓白热高光，向收笔端展宽增亮
    float razor = exp(-pow(h / (0.075 * (1.0 + widen * 1.6)), 2.0));
    col += uColHot * razor * (1.15 + widen * 0.35) * (1.0 - uColorShift * 0.65);

    //燃边 + 前缘 + 全形白闪
    col += float3(1.25, 0.42, 0.18) * burn * 2.3;
    col += uColHot * front * 2.6;
    col = lerp(col, col + uColHot * 0.55, saturate(uFlash));

    //剃刀线/前缘/白闪在 alpha 之外再给增益 → 半加法辉光；洇边按自身 alpha 叠墨色
    float glowA = saturate(alpha + bleedA + (front * 0.55 + burn * 0.25 + razor * 0.18 + uFlash * 0.16)
        * uOpacity * reveal * tipFeather * survive);
    return float4(col * alpha + uColDark * 0.60 * bleedA
        + uColHot * (front * 0.35 + uFlash * 0.08) * uOpacity * tipFeather * reveal, glowA);
}

// ============================ BurstTech ============================

float4 BurstPS(PSInput input) : COLOR0
{
    float ax = input.TexCoords.x;                 //0 根(玩家) → 1 尖
    float ay = (input.TexCoords.y - 0.5) * 2.0;   //-1..1 横向

    //宽度包络：根宽尖窄
    float widthEnv = lerp(1.0, 0.30, pow(ax, 1.3));
    float edge = abs(ay) / max(widthEnv, 0.05);
    if (edge > 1.30)
        return float4(0, 0, 0, 0);

    //轮廓撕裂 + 内部墨纹
    float n = tex2D(noiseSamp, float2(ax * 1.7 + uSeed * 9.1, ay * 0.6 + uSeed * 3.7)).r;
    float n2 = tex2D(noiseSamp, float2(ax * 3.6 - uTime * 0.25 + uSeed, ay * 1.3 + 0.41)).r;
    float body = 1.0 - smoothstep(0.50 + n * 0.50, 1.06, edge);

    //尖端撕裂舌
    float tipTear = smoothstep(0.72, 1.02, ax + (n2 - 0.5) * 0.40);
    //根部锚定羽化（贴玩家不糊脸）
    float rootIn = smoothstep(0.0, 0.10, ax);
    //从根向尖散掉
    float front2 = uDissolve * 1.35 - 0.18;
    float dissolve = smoothstep(front2 - 0.10, front2 + 0.12, ax + (n - 0.5) * 0.28);

    float alpha = body * (1.0 - tipTear) * rootIn * dissolve * uIntensity;
    alpha = saturate(alpha * (0.55 + n2 * 0.45)) * uOpacity;
    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    //舌体黑红近墨，绯红丝筋，缘部一线燃边
    float3 col = lerp(uColDark * 0.55, uColDeep * 0.85, smoothstep(0.30, 0.80, n));
    col += uColBright * smoothstep(0.70, 0.95, n2) * 0.40;
    float rim = smoothstep(0.66, 0.98, edge) * (1.0 - tipTear);
    col += uColBright * rim * 0.40;

    return float4(col * alpha, alpha);
}

technique ArcTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 ArcPS();
    }
}

technique BurstTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 BurstPS();
    }
}
