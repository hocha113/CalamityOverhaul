// ============================================================================
//OniVigorInk.fx 气力墨脉，书法"一"字横笔作气力计:
//宣纸底痕(上限轮廓,淡灰断裂干笔) + 湿墨主体(uFill 截断,洇墨/纤维/暗红血线)
//+ 飞白(低气力加剧,避开前沿保住读数边界) + 墨锋前沿(恢复时洇进,消耗时利落回切)
//+ 消耗残痕(uFill~uTrailFill 间的绯红湿迹,随脉冲蒸散成断丝) + 回满白热收笔扫光
//轮廓噪声吃恒定 uSeed,时间只驱动湿光/内部流动/墨锋，笔形每帧稳定,读数才可信
//AlphaBlend 预乘 alpha 输出;色板由 CPU 传入与 OnikiriUITheme 同源
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;   //quad 像素尺寸
float uFill;          //0~1 当前气力(显示值)
float uTrailFill;     //>= uFill,消耗残痕右缘
float uFlow;          //显示值变化速度,+恢复/-消耗,约 -1~1
float uSpendPulse;    //0~1 消耗脉冲
float uGainPulse;     //0~1 补气脉冲
float uFullPulse;     //0~1 回满收笔脉冲
float uSeed;          //形状种子(会话内恒定)
float3 uColInk;       //墨黑
float3 uColPaper;     //纸白
float3 uColDeep;      //深红
float3 uColBright;    //亮绯红
float3 uColHot;       //白热

#define PI 3.14159265

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.13 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

//预乘 over 合成
void OverLayer(inout float3 C, inout float A, float3 c, float a) {
    C = c * a + C * (1.0 - a);
    A = a + A * (1.0 - a);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;

    //====笔道参数域:横笔自左向右,两端留白给洇边/飞墨====
    float padX = 10.0;
    float x0 = padX;
    float x1 = uResolution.x - padX;
    float midY = uResolution.y * 0.5;

    float ux = clamp(px.x, x0, x1);
    float u = (ux - x0) / (x1 - x0);

    //中线:轻微上弓 + 右端微翘，书家手笔不是直尺
    float center = midY - sin(u * PI) * 1.6 - (u - 0.5) * 1.8;
    //压力曲线:藏锋起笔顿点 / 中段收细 / 收笔按压 / 锋尖出锋
    float press = 0.62
        + 0.55 * exp(-pow((u - 0.05) * 9.0, 2.0))
        + 0.30 * exp(-pow((u - 0.90) * 11.0, 2.0));
    press *= 1.0 - smoothstep(0.955, 1.0, u) * 0.92;
    float halfH = uResolution.y * 0.155 * press;
    //轮廓噪声:恒定种子,不吃时间
    halfH *= 0.86 + 0.30 * valueNoise(float2(u * 24.0, uSeed * 17.0));
    halfH = max(halfH, 0.6);

    //圆头胶囊 SDF:两端圆帽免费(px.x 钳进 [x0,x1])
    float dy = px.y - center;
    float2 q = float2(px.x - ux, dy);
    float d = length(q) - halfH;

    //远离笔道直接透明(留出蒸散残丝的上方余量)
    if (d > 20.0) {
        return float4(0, 0, 0, 0);
    }

    float strokeA = 1.0 - smoothstep(-0.9, 0.9, d);

    //====墨锋前沿:恢复时缓慢爬动洇进,消耗时锐利====
    float fillX = lerp(x0, x1, uFill);
    float trailX = lerp(x0, x1, max(uTrailFill, uFill));
    //满/空两端收拢摆幅,免得锋尖闪烁或空札见墨
    float wobAmp = 5.0
        * smoothstep(0.004, 0.05, uFill)
        * (1.0 - smoothstep(0.94, 0.998, uFill))
        * (1.0 - saturate(-uFlow) * 0.6);
    float edgeN = valueNoise(float2(px.y * 0.33 + uSeed * 31.0,
        uTime * (0.30 + saturate(uFlow) * 1.8)));
    float frontX = fillX + (edgeN - 0.5) * wobAmp;

    float wet = 1.0 - smoothstep(-1.6, 1.6, px.x - frontX);
    float pastFront = smoothstep(0.0, 8.0, px.x - frontX);

    //====宣纸底痕:上限轮廓,淡灰断裂干笔(只住在墨未及处)====
    float ghostTex = fbm3(float2(px.x * 0.10, px.y * 0.50) + uSeed * 3.7);
    float ghostBreak = smoothstep(0.34, 0.72, ghostTex);
    float ghostA = (1.0 - smoothstep(-0.5, 1.4, d)) * ghostBreak * 0.34 * pastFront;
    float3 ghostCol = lerp(uColPaper, uColDeep, 0.30) * 0.72;

    //====湿墨主体====
    //基墨:墨黑带深红洇斑,骨中带血
    float blotch = fbm3(float2(px.x * 0.045, px.y * 0.10) + float2(uTime * 0.014, uSeed));
    float fiber = valueNoise(px * float2(0.55, 0.90)) * 0.6 + valueNoise(px * 0.22 + 7.7) * 0.4;
    float3 ink = lerp(uColInk * 0.92, uColDeep * 0.60, saturate(blotch * 0.62 + 0.10));
    ink *= 0.92 + (fiber - 0.5) * 0.18;
    //纵向:下缘沉影
    ink *= 1.06 - saturate(dy / (halfH * 2.0) + 0.5) * 0.14;

    //暗红血线:压在中线上方一线,缓慢呼吸
    float vein = exp(-pow(dy + halfH * 0.28, 2.0) * 0.55);
    float veinPulse = 0.65 + 0.35 * sin(uTime * 1.7 + u * 6.2832);
    ink += uColBright * vein * 0.16 * veinPulse;

    //湿墨光泽:一条极淡亮带缓缓掠过
    float sheenT = frac(uTime * 0.11 + uSeed * 0.37);
    float sheenX = lerp(x0 - 20.0, x1 + 20.0, sheenT);
    ink += uColHot * exp(-pow((px.x - sheenX) * 0.05, 2.0)) * 0.05;

    //低气力:墨色发灰发干
    float lowT = 1.0 - smoothstep(0.10, 0.30, uFill);
    ink = lerp(ink, dot(ink, float3(0.40, 0.35, 0.25)) * float3(1.0, 0.84, 0.80), lowT * 0.35);

    //飞白:沿笔向拉长的干笔露白,越旧越干、气力越低越干;前沿一段保持满墨护住读数边界
    float fw = valueNoise(float2(px.x * 0.055, px.y * 0.75) + uSeed * 11.0);
    float holes = 1.0 - smoothstep(0.40, 0.72, fw);
    float flyStr = saturate((1.0 - smoothstep(0.12, 0.42, uFill)) * 0.55
        + (1.0 - smoothstep(0.0, 0.45, u)) * 0.22);
    float frontClean = smoothstep(4.0, 18.0, frontX - px.x);
    float wetA = strokeA * wet * 0.96 * (1.0 - holes * flyStr * frontClean);

    //====外辉与纸白裱边:深红微光衬底,零线一圈淡纸光，墨悬在夜里也读得清====
    float glowA = exp(-max(d, 0.0) * 0.28) * (1.0 - strokeA)
        * wet * (0.20 + uGainPulse * 0.15 + uSpendPulse * 0.12);
    float rimA = exp(-d * d * 1.1) * wet * 0.28;
    float3 rimCol = lerp(uColPaper, uColHot, 0.25);

    //====消耗残痕:frontX~trailX 间的绯红湿迹,将散尽时整体转淡====
    float inTrail = smoothstep(-1.0, 2.0, px.x - frontX) * (1.0 - smoothstep(-2.0, 1.0, px.x - trailX));
    float trailBody = 1.0 - smoothstep(-0.6, 0.9, d + halfH * 0.25);
    float trailFade = saturate((trailX - fillX) / 24.0);
    float trailA = inTrail * trailBody * (0.30 + uSpendPulse * 0.45) * saturate(trailFade * 2.5);
    float3 trailCol = lerp(uColBright, uColDeep, 0.35);

    //蒸散断丝:残痕上方升腾的细红缕,只随消耗脉冲存在
    float wispN = valueNoise(float2(px.x * 0.45 + uSeed * 5.0, px.y * 0.22 - uTime * 2.6));
    float rise01 = saturate(-dy / (halfH * 3.0));
    float wispA = inTrail * smoothstep(0.58, 0.86, wispN) * uSpendPulse
        * (1.0 - rise01) * saturate(rise01 * 6.0) * 0.55;

    //====墨锋辉光:恢复/补气时前沿湿亮,消耗时一瞬绯闪====
    float frontBand = exp(-pow((px.x - frontX) * 0.14, 2.0));
    float frontA = frontBand * strokeA
        * (0.26 + saturate(uFlow) * 0.50 + uGainPulse * 0.70 + uSpendPulse * 0.40)
        * smoothstep(0.004, 0.03, uFill);
    float3 frontCol = lerp(uColBright, uColHot, 0.35 + uGainPulse * 0.40);

    //====回满收笔:白热扫光自起笔掠向锋尖====
    float sweepX = lerp(x1, x0, saturate(uFullPulse));
    float sweepA = exp(-pow((px.x - sweepX) * 0.09, 2.0)) * strokeA * wet * uFullPulse * 0.85;

    //====预乘 over 合成(后→前)====
    float3 C = float3(0.0, 0.0, 0.0);
    float A = 0.0;
    OverLayer(C, A, ghostCol, ghostA);
    OverLayer(C, A, uColDeep, glowA);
    OverLayer(C, A, ink, wetA);
    OverLayer(C, A, trailCol, trailA);
    OverLayer(C, A, rimCol, rimA);
    OverLayer(C, A, frontCol, frontA);
    OverLayer(C, A, uColHot, sweepA);
    OverLayer(C, A, uColBright, wispA);

    return float4(C, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniVigorInkPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
