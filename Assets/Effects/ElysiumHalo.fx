// ============================================================================
//ElysiumHalo.fx 天国极乐 UI 神圣光环
//采样 s0 + s1 噪声；HaloBackground/CenterPanel/SlotAura 三 Technique
//ps_3_0
// ============================================================================

float uTime;
float fadeAlpha;
float discipleRatio;     //已激活门徒比例 0~1
float rotationAngle;     //转盘旋转角(弧度)
float pulsePhase;        //脉冲相位
float hoverSector;       //悬停门徒 -1无 0~11

float outerR;            //外环半径(UV 0~0.5)
float discipleR;         //门徒轨道半径
float innerR;            //内环半径

float3 warmGold;         //暖金
float3 brightGold;       //亮金
float3 holyWhite;        //圣白

float3 slotColor;
float slotActive;
float slotHover;
float slotDragSource;
float slotDragTarget;
float slotPhase;

texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture   = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

//SpriteBatch自动绑定到s0
sampler baseSamp : register(s0);

#define PI    3.14159265
#define TAU   6.28318530

//柔光环高斯 SDF
float softRing(float d, float r, float sharpness)
{
    float delta = d - r;
    return exp(-sharpness * delta * delta);
}

struct PSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

//HaloBackground 主背景神圣光环
float4 HaloBackgroundPS(PSInput input) : COLOR0
{
    float2 uv   = input.TexCoords;
    float2 c    = uv - 0.5;
    float  dist = length(c);
    float  ang  = atan2(c.y, c.x);

    float3 col = 0;

    //=
    //1. 深层环境辉光 — 多层高斯径向渐变
    //=
    float glow1 = exp(-dist * dist * 14.0);         //紧凑亮核
    float glow2 = exp(-dist * dist * 5.0);           //中层扩散
    float glow3 = exp(-dist * dist * 2.0) * 0.3;     //远层微光
    col += holyWhite  * glow1 * 0.25;
    col += warmGold   * glow2 * 0.12;
    col += brightGold * glow3 * 0.06;

    //=
    //2. 体积圣光射线 — 12主光线 + 24次级
    //=
    float rayAng = ang - rotationAngle;

    //折叠到12扇区
    float sectorVal  = frac(rayAng / TAU * 12.0 + 0.5);
    float sectorDist = abs(sectorVal - 0.5) * 2.0;   //0=射线中心 1=扇区边缘

    //主光线：高斯截面
    float mainRay = exp(-sectorDist * sectorDist * 120.0);

    //径向衰减
    float rayAttenIn  = smoothstep(innerR * 0.6, innerR * 1.5, dist);
    float rayAttenOut = 1.0 - smoothstep(outerR * 0.85, outerR * 1.15, dist);
    float rayRadial   = rayAttenIn * rayAttenOut;

    //噪声体积调制
    float2 nUV1 = float2(dist * 6.0 + uTime * 0.12, ang * 0.318);
    float  rn1  = tex2D(noiseSamp, frac(nUV1)).r;
    float2 nUV2 = float2(dist * 3.0 - uTime * 0.08, ang * 0.637 + 0.5);
    float  rn2  = tex2D(noiseSamp, frac(nUV2)).g;
    float  nMod = 0.55 + (rn1 * 0.6 + rn2 * 0.4) * 0.45;

    float volumetricRay = mainRay * rayRadial * nMod;

    //24条次级射线
    float s24Val  = frac(rayAng / TAU * 24.0 + 0.5);
    float s24Dist = abs(s24Val - 0.5) * 2.0;
    float subRay  = exp(-s24Dist * s24Dist * 350.0) * 0.25;
    float subIn   = smoothstep(discipleR * 0.5, discipleR, dist);
    float subOut  = 1.0 - smoothstep(outerR * 0.7, outerR * 1.0, dist);
    volumetricRay += subRay * subIn * subOut * nMod;

    //射线随门徒数增强
    float rayPower = 0.3 + discipleRatio * 0.7;
    float3 rayCol  = lerp(warmGold, holyWhite, volumetricRay * 0.6);
    col += rayCol * volumetricRay * rayPower * 0.65;

    //悬停扇区高亮(uniform上禁if：hoverSector<0时权重归零)
    float hoverOn = step(0.0, hoverSector);
    float hAng   = (hoverSector + 0.5) * TAU / 12.0 - PI * 0.5 + rotationAngle;
    float hDelta = ang - hAng;
    hDelta = hDelta - floor((hDelta + PI) / TAU) * TAU;
    float hGlow  = exp(-hDelta * hDelta * 80.0) * rayRadial * 0.5;
    col += holyWhite * hGlow * hoverOn;

    //=
    //3. 同心光环群 — 环形结构层次
    //=
    //外环（主边界）
    float outerPulse = 0.82 + 0.18 * sin(pulsePhase * 1.5);
    col += brightGold * softRing(dist, outerR, 2500.0) * 1.0 * outerPulse;
    col += warmGold   * softRing(dist, outerR, 400.0)  * 0.35 * outerPulse;
    col += warmGold   * softRing(dist, outerR, 100.0)  * 0.12;

    //门徒轨道环
    float discPulse = 0.75 + 0.25 * sin(pulsePhase);
    col += warmGold   * softRing(dist, discipleR, 1200.0) * 0.7 * discPulse;
    col += brightGold * softRing(dist, discipleR, 250.0)  * 0.2;

    //内环（圣所边界）
    float innerPulse = 0.85 + 0.15 * sin(pulsePhase * 2.0);
    col += holyWhite  * softRing(dist, innerR, 3000.0) * 0.9 * innerPulse;
    col += brightGold * softRing(dist, innerR, 500.0)  * 0.4;
    col += warmGold   * softRing(dist, innerR, 120.0)  * 0.15;

    //装饰环
    float deco1R = lerp(outerR, discipleR, 0.5);
    col += warmGold * softRing(dist, deco1R, 6000.0) * 0.3;
    float deco2R = lerp(discipleR, innerR, 0.5);
    col += warmGold * softRing(dist, deco2R, 6000.0) * 0.25;

    //微装饰环(外环两侧)
    col += brightGold * softRing(dist, outerR * 1.04, 8000.0) * 0.2;
    col += brightGold * softRing(dist, outerR * 0.96, 8000.0) * 0.2;

    //=
    //4. 玫瑰窗几何骨架 — 12径向分割 + 拱形纹饰
    //=
    //12条分割线
    float divLine = exp(-sectorDist * sectorDist * 4000.0);
    float divIn   = smoothstep(innerR * 1.2, innerR * 1.6, dist);
    float divOut  = smoothstep(outerR, outerR * 0.92, dist);
    col += warmGold * divLine * divIn * divOut * 0.45;

    //拱形纹饰 — 每个扇区内弧形装饰
    float archMidR  = lerp(discipleR, outerR, 0.55);
    float archSpan  = (outerR - discipleR) * 0.3;
    float archProf  = 1.0 - sectorDist * sectorDist * 4.0;
    archProf = max(archProf, 0.0);
    float archTarget = archMidR + archSpan * archProf;
    float archLine  = softRing(dist, archTarget, 8000.0);
    float archMask  = smoothstep(0.92, 0.6, sectorDist);
    col += brightGold * archLine * archMask * 0.3;

    //内层拱形
    float arch2Mid    = lerp(innerR, discipleR, 0.5);
    float arch2Span   = (discipleR - innerR) * 0.25;
    float arch2Target = arch2Mid + arch2Span * archProf;
    float arch2Line   = softRing(dist, arch2Target, 10000.0);
    col += warmGold * arch2Line * archMask * 0.2;

    //=
    //5. 十字光芒（镜头耀斑）
    //=
    float crossInt = exp(-dist * 10.0) * 0.6;
    float crossH   = exp(-c.y * c.y * 1200.0);
    float crossV   = exp(-c.x * c.x * 1200.0);
    col += holyWhite * (crossH + crossV) * crossInt;

    //对角线(45度，较弱)
    float2 rot45   = float2(c.x * 0.7071 + c.y * 0.7071,
                            -c.x * 0.7071 + c.y * 0.7071);
    float crossD1  = exp(-rot45.y * rot45.y * 2000.0);
    float crossD2  = exp(-rot45.x * rot45.x * 2000.0);
    float diagInt  = exp(-dist * 14.0) * 0.25;
    col += holyWhite * (crossD1 + crossD2) * diagInt;

    //=
    //6. 圣光能量波纹 — 同心扩散
    //=
    float wSpeed = uTime * 0.4;

    float w1 = sin(dist * 80.0 - wSpeed * 8.0) * 0.5 + 0.5;
    w1 = pow(w1, 12.0);
    float wMask    = smoothstep(outerR * 0.95, innerR, dist);
    float wMaskIn  = smoothstep(innerR * 0.3, innerR * 0.8, dist);
    float waveMask = wMask * wMaskIn;
    float dRatio   = 0.4 + discipleRatio * 0.6;
    col += warmGold * w1 * waveMask * 0.12 * dRatio;

    float w2 = sin(dist * 60.0 - wSpeed * 6.0 + 1.5) * 0.5 + 0.5;
    w2 = pow(w2, 12.0);
    col += brightGold * w2 * waveMask * 0.08 * dRatio;

    //=
    //7. 微光粒子 — 噪声驱动闪烁
    //=
    float2 shimUV  = float2(ang * 1.5 + uTime * 0.03, dist * 10.0 - uTime * 0.06);
    float  shimmer = tex2D(noiseSamp, frac(shimUV)).r;
    shimmer = pow(shimmer, 6.0);
    float shimMask = smoothstep(outerR * 1.1, innerR * 0.7, dist);
    shimMask *= smoothstep(0.01, innerR * 0.3, dist);
    col += holyWhite * shimmer * shimMask * 0.2;

    //=
    //外围柔和衰减
    //=
    float edgeFade = 1.0 - smoothstep(outerR * 1.0, outerR * 1.25, dist);
    col *= edgeFade;

    col *= fadeAlpha;
    return float4(col, 1.0);
}

//CenterPanel 中心面板暗底+发光边框
float4 CenterPanelPS(PSInput input) : COLOR0
{
    float2 uv   = input.TexCoords;
    float2 c    = uv - 0.5;
    float  dist = length(c);

    float3 premulCol   = 0;
    float  totalAlpha  = 0;

    //暗色背景圆
    float bgMask  = smoothstep(0.5, 0.35, dist);
    float bgAlpha = bgMask * 0.92;
    float3 bgCol  = float3(0.06, 0.055, 0.04);
    premulCol  += bgCol * bgAlpha;
    totalAlpha  = bgAlpha;

    //发光边框(预乘：高色彩低alpha贡献 → additive效果)
    float border      = softRing(dist, 0.42, 600.0);
    float borderSharp = softRing(dist, 0.42, 3000.0);
    float borderPulse = 0.8 + 0.2 * sin(pulsePhase * 1.5);
    float3 brdCol  = warmGold * border * 0.5 * borderPulse
                   + brightGold * borderSharp * 0.8 * borderPulse;
    premulCol  += brdCol;
    totalAlpha  = max(totalAlpha, border * 0.15);

    //内部微弱辉光纹理
    float innerPat = sin(dist * 40.0 + uTime * 0.8) * 0.5 + 0.5;
    innerPat = pow(innerPat, 8.0) * 0.06;
    premulCol += warmGold * innerPat * bgMask;

    //中心深层辉光
    float centerGlow = exp(-dist * dist * 25.0) * 0.08;
    premulCol += holyWhite * centerGlow;

    totalAlpha = saturate(totalAlpha);

    return float4(premulCol * fadeAlpha, totalAlpha * fadeAlpha);
}

//SlotAura 门徒槽位光环
float4 SlotAuraPS(PSInput input) : COLOR0
{
    float2 uv   = input.TexCoords;
    float2 c    = uv - 0.5;
    float  dist = length(c);
    float  ang  = atan2(c.y, c.x);

    float3 col = 0;

    //uniform上禁if：四态全算，权重乘混合(源>目标>在职>空缺互斥)
    float wSrc = step(0.5, slotDragSource);
    float wTgt = (1.0 - wSrc) * step(0.5, slotDragTarget);
    float wAct = (1.0 - wSrc) * (1.0 - wTgt) * step(0.5, slotActive);
    float wEmpty = (1.0 - wSrc) * (1.0 - wTgt) * (1.0 - wAct);

    //拖动源：暗淡虚线框
    float dashedRing  = softRing(dist, 0.34, 3000.0);
    float dashPattern = step(0.5, frac(ang / TAU * 12.0));
    float3 srcCol = slotColor * dashedRing * dashPattern * 0.4
                  + slotColor * exp(-dist * dist * 10.0) * 0.08;

    //拖动目标：强烈脉冲高亮
    float tgtPulse  = 0.6 + 0.4 * sin(uTime * 5.0);
    float tgtRing   = softRing(dist, 0.32, 300.0);
    float tgtGlow   = exp(-dist * dist * 8.0);
    float3 tgtCol = lerp(slotColor, holyWhite, 0.3) * (tgtRing + tgtGlow * 0.3) * tgtPulse
                  + holyWhite * softRing(dist, 0.38, 2000.0) * tgtPulse;

    //在职槽位：身份色环 + 旋转弧光 + 边缘脉动线
    float auraRing = softRing(dist, 0.32, 400.0);
    float innerFill = exp(-dist * dist * 12.0) * 0.2;
    float arcAng  = ang + uTime * 1.5 + slotPhase;
    float arc     = pow(saturate(sin(arcAng * 3.0) * 0.5 + 0.5), 4.0);
    float arcRing = softRing(dist, 0.3, 600.0);
    float edgeLine  = softRing(dist, 0.36, 4000.0);
    float edgePulse = 0.7 + 0.3 * sin(slotPhase + uTime * 2.0);
    float3 actCol = slotColor * (auraRing * 0.6 + innerFill + arc * arcRing * 0.4 + edgeLine * edgePulse);

    //空槽位：微弱轮廓
    float3 emptyCol = warmGold * softRing(dist, 0.34, 3000.0) * 0.25;

    col = srcCol * wSrc + tgtCol * wTgt + actCol * wAct + emptyCol * wEmpty;

    //悬停叠加高亮(权重乘)
    float hoverGlow = exp(-dist * dist * 8.0) * 0.3;
    float hoverRing = softRing(dist, 0.35, 500.0) * 0.5;
    col += holyWhite * (hoverGlow + hoverRing) * step(0.5, slotHover);

    //圆外裁切
    float clipMask = 1.0 - smoothstep(0.38, 0.46, dist);
    col *= clipMask * fadeAlpha;

    return float4(col, 1.0);
}

technique HaloBackground
{
    pass P0
    {
        PixelShader = compile ps_3_0 HaloBackgroundPS();
    }
};

technique CenterPanel
{
    pass P0
    {
        PixelShader = compile ps_3_0 CenterPanelPS();
    }
};

technique SlotAura
{
    pass P0
    {
        PixelShader = compile ps_3_0 SlotAuraPS();
    }
};
