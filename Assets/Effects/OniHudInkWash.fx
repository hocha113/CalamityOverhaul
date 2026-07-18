// ============================================================================
//OniHudInkWash.fx 底墨横扫——封印札 HUD 簇的画底:
//一笔自左(屏外)扫入的宽幅墨道,起笔按压、中段收细、尾部飞白加剧、出锋收笔。
//轮廓吃恒定 uSeed 不逐帧变形;时间只驱动湿光/危态青斑。
//uReveal 驱动"书写"入场:墨自左向右写出,写锋带一点绯热。
//全程笛卡尔坐标,无极坐标接缝风险。AlphaBlend 预乘 alpha 输出;
//色板由 CPU 传入与 OnikiriUITheme 同源;峰值透明度刻意压低,它是底图不是主角
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uReveal;        //0~1 书写进度(CPU 已缓动)
float uDanger;        //0~1 危态缓动,尾部渗鬼火青斑
float uSeed;          //形状种子(会话内恒定)
float2 uResolution;   //quad 像素尺寸
float3 uColInk;       //墨黑
float3 uColDark;      //暗酒红
float3 uColDeep;      //深红
float3 uColBright;    //亮绯红
float3 uColHot;       //白热
float3 uColGhost;     //鬼火青

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
    float H = uResolution.y;

    //====笔道参数域:自左缘(屏外)向右,尾端留出锋余量====
    float x1 = uResolution.x - 22.0;
    float u = saturate(px.x / x1);

    //中线:缓弧 + 向右下沉的斜势,书家横扫不是水平尺
    float center = H * 0.40
        + (u - 0.30) * H * 0.13
        + sin(u * 3.1 + uSeed * 7.0) * H * 0.030;

    //压力曲线:起笔按压顿点,中段渐收,尾部出锋
    float press = 0.90
        + 0.50 * exp(-pow((u - 0.08) * 5.5, 2.0));
    press *= 1.0 - 0.30 * smoothstep(0.25, 0.82, u);
    press *= 1.0 - smoothstep(0.86, 1.0, u) * 0.97;

    float halfH = H * 0.235 * press;
    //轮廓噪声:恒定种子,笔缘毛而不抖
    halfH *= 0.80 + 0.36 * valueNoise(float2(u * 11.0, uSeed * 13.0));
    halfH = max(halfH, 0.8);

    float dy = px.y - center;
    float d = abs(dy) - halfH;
    //大块蚀刻:fbm 在轮廓上咬出缺口,"一笔墨"不是"一块板"
    float carve = fbm3(px * float2(0.012, 0.045) + uSeed * 3.1);
    d += smoothstep(0.60, 0.92, carve) * 9.0;

    //远离笔道直接透明(洇晕余量之外)
    if (d > 30.0) {
        return float4(0, 0, 0, 0);
    }

    //====书写入场:墨锋自左向右推进,锋前无墨====
    float frontX = lerp(-30.0, uResolution.x + 30.0, uReveal);
    float wet = 1.0 - smoothstep(frontX - 3.0, frontX + 5.0, px.x);

    float strokeA = 1.0 - smoothstep(-2.0, 2.5, d);

    //====飞白:沿笔向拉长的干笔露白,越近尾越干====
    float fw = valueNoise(px * float2(0.016, 0.30) + uSeed * 11.0);
    float holes = smoothstep(0.46, 0.78, fw);
    float flyStr = 0.18 + 0.62 * smoothstep(0.30, 0.95, u);

    //====墨体:墨黑带暗红洇斑,下缘泛深红====
    float blotch = fbm3(float2(px.x * 0.020, px.y * 0.050) + float2(uTime * 0.010, uSeed));
    float3 ink = lerp(uColInk * 0.95, uColDark, saturate(blotch * 0.60 + 0.08));
    ink += uColDeep * saturate(dy / max(halfH, 1.0)) * 0.14;
    //湿墨光泽:极淡亮带缓缓掠过
    float sheenX = lerp(-40.0, uResolution.x + 40.0, frac(uTime * 0.05 + uSeed * 0.29));
    ink += uColHot * exp(-pow((px.x - sheenX) * 0.018, 2.0)) * 0.035;

    //====洇晕:笔缘外的暗晕,墨渗进纸纤维====
    float bleedA = exp(-max(d, 0.0) * 0.085) * (1.0 - strokeA) * wet * 0.20;

    //底图纪律:核心透明度压在 ~0.6
    float wetA = strokeA * wet * 0.60 * (1.0 - holes * flyStr);

    //====危态青斑:尾部飞白间渗出的鬼火,缓慢明灭====
    float gfN = valueNoise(px * float2(0.050, 0.11) + float2(uTime * 0.22, uSeed * 5.0));
    float gfFlick = 0.70 + 0.30 * sin(uTime * 4.6 + u * 21.0);
    float ghostA = smoothstep(0.72, 0.92, gfN)
        * smoothstep(0.45, 0.88, u) * strokeA * wet * uDanger * 0.30 * gfFlick;

    //====写锋:书写进行中锋头一点绯热,写完即隐====
    float tipLive = 1.0 - smoothstep(0.985, 1.0, uReveal);
    float tipA = exp(-pow((px.x - frontX) * 0.075, 2.0))
        * (1.0 - smoothstep(-1.0, 10.0, d)) * tipLive * 0.55;
    float3 tipCol = lerp(uColBright, uColHot, 0.45);

    //====预乘 over 合成(后→前)====
    float3 C = float3(0.0, 0.0, 0.0);
    float A = 0.0;
    OverLayer(C, A, uColDark, bleedA);
    OverLayer(C, A, ink, wetA);
    OverLayer(C, A, uColGhost, ghostA);
    OverLayer(C, A, tipCol, tipA);

    return float4(C, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniHudInkWashPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
