// ============================================================================
//KikasaNarrativePanel.fx 鬼雨叙事面板背景——湿墨冷青水膜 + 溺月 + 顶沿雨丝 + 底沿积水线 + 水幕开合
//AlphaBlend 预乘 alpha 输出;色板与 KikasaSky.fx 鬼雨异化态同源(uCol* 由 CPU 传入,禁红禁暖)
//构图纪律:签名装饰全部住在边框带(edgePad 区),面板内部保持湿墨静场护住文字
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;       //面板体不透明度(CPU 侧已做快速上斜,避免"半透明面板"长时间存在)
float2 uResolution; //含 edgePad 外扩的画布尺寸
float uEdgePad;
float uReveal;      //0~1 开合编舞:0.10 前中线水光沿 X 生长,0.62 前上下拉开水幕,之后定格
float3 uColVoid;    //近黑沉云
float3 uColDeep;    //墨青深底
float3 uColRain;    //雨青
float3 uColMoon;    //溺月惨白

#define PI 3.14159265
#define TAU 6.28318530

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

float fbm4(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * valueNoise(p);
        p = p * 2.11 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

//色板(仅底色本地,冷青系全部来自 uniform)
//基材亮度抬到可见档:雨夜水膜是"暗但有物",不是黑洞
static const float3 COL_MIST = float3(0.052, 0.064, 0.070); //顶部潮雾墨青
static const float3 COL_VOID = float3(0.026, 0.033, 0.038); //最深处仍保有材质

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //====圆角矩形 SDF + 水蚀边缘:水把轮廓磨圆,侵蚀比墨缘更温和====
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 10.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;
    float edgeNoise = fbm4(pixelPos * 0.024 + float2(0.0, uTime * 0.012));
    panelSDF += (edgeNoise - 0.5) * 2.6;

    if (panelSDF > uEdgePad + 10.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime;
    float u = pixelPos.x / uResolution.x;

    //====水幕编舞:中线水光生长 → 上下拉开====
    float lineGrow = saturate(uReveal / 0.10);
    float unfold = saturate((uReveal - 0.10) / 0.52);
    unfold = unfold * (2.0 - unfold); //easeOut
    float dyC = abs(pixelPos.y - center.y);
    float frontier = unfold * (halfSize.y + uEdgePad + 8.0);
    float openMask = 1.0 - smoothstep(frontier - 2.0, frontier + 2.0, dyC);

    //====面板体:雨夜水膜====
    //纵向:顶部一层溺月潮雾,中段沉到最深,底部积水泛起微光
    float3 bg = lerp(COL_MIST, COL_VOID, smoothstep(0.0, 0.55, uv.y));
    bg += uColDeep * (1.0 - uv.y) * 0.22;
    bg += uColRain * pow(uv.y, 3.0) * 0.10; //底部积水泛光

    //文字区静默罩:护字=去细节去运动,不是去光。中央只轻微压平
    float vCenter = abs(uv.y - 0.5) * 2.0;
    float textMask = 1.0 - smoothstep(0.10, 0.62, vCenter);

    //玻璃面凝露颗粒:两档 value noise
    float grain = valueNoise(pixelPos * 0.9) * 0.55 + valueNoise(pixelPos * 0.23) * 0.45;
    bg += (grain - 0.5) * 0.045 * lerp(1.0, 0.55, textMask);

    //水汽:低频云状明暗,水膜厚薄不均(材质主笔,双向呼吸)
    float2 vaporUV = uv * float2(2.8, 2.0) + float2(t * 0.006, -t * 0.004);
    float vapor = valueNoise(vaporUV) * 0.62 + valueNoise(vaporUV * 2.3 + 5.1) * 0.38;
    bg += uColRain * (vapor - 0.42) * 0.14 * lerp(1.0, 0.5, textMask);

    bg *= lerp(1.0, 0.94, textMask);

    //边缘吸水:靠近边框处水膜向墨青晕开
    float innerDist = max(-panelSDF, 0.0);
    float soak = exp(-innerDist * 0.055);
    bg = lerp(bg, uColDeep * 0.75, soak * 0.32);

    //水痕缓流:两翼高频竖纹极慢下滑(雨水沿玻璃淌落),中央只衰减不扑灭
    float flank = smoothstep(0.24, 0.95, abs(uv.x - 0.5) * 2.0);
    float streak = valueNoise(float2(uv.x * 30.0, uv.y * 2.4 - t * 0.05));
    bg += uColRain * (streak - 0.5) * flank * lerp(1.0, 0.4, textMask) * 0.15;

    //远景雨幡:两翼一层斜落的冷灰雨柱剪影,给内部一个纵深锚点
    float shaftN = valueNoise(float2((uv.x + uv.y * 0.14) * 6.5 + t * 0.010, 0.43));
    float shaft = smoothstep(0.60, 0.86, shaftN);
    bg += uColRain * shaft * flank * lerp(1.0, 0.5, textMask) * 0.10;

    //暗角 + 凝露微闪
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.48, 0.58), vig * float2(0.48, 0.58));
    bg *= saturate(1.0 - vigStr) * 0.16 + 0.84;
    float dust = hash21(pixelPos + t * 25.0) * 0.04;
    bg *= 1.0 - dust * 0.5;
    bg = max(bg, 0.0); //水汽的负向呼吸不许把水膜掏穿

    //====水膜折光框线:框线本体在 SDF=0,上缘受溺月照亮向下衰减====
    float rimLine = exp(-panelSDF * panelSDF * 0.30);
    float topNear = 1.0 - smoothstep(0.0, 0.5, uv.y);
    float3 rimCol = lerp(uColRain * 1.5, uColMoon, topNear * 0.6);
    float rimA = rimLine * (0.34 + topNear * 0.30);

    //====溺月:苍白半轮泡在顶沿背后(画在面板体之下,只在轮廓之外可见)====
    float2 moonC = float2(uResolution.x * 0.22, innerMin.y + 6.0);
    float dMoon = length(pixelPos - moonC);
    float breath = sin(t * 0.4) * 0.5 + 0.5;
    float moonR = 20.0;
    float moonDisc = 1.0 - smoothstep(moonR - 2.5, moonR + 2.5, dMoon); //溺在水里,盘缘发涨
    float moonHalo = exp(-max(dMoon - moonR, 0.0) * (0.13 - breath * 0.03));
    float moonTex = valueNoise(pixelPos * 0.09 + 7.3);
    float3 moonCol = lerp(uColRain, uColMoon, 0.55 + breath * 0.20) * (0.66 + moonTex * 0.4);
    //月盘卡在沿上,湿光允许向顶部内侧渗一段——给内部一个光源方向
    float bandDisc = 1.0 - smoothstep(innerMin.y + 10.0, innerMin.y + 34.0, pixelPos.y);
    float bandHalo = 1.0 - smoothstep(innerMin.y + 16.0, innerMin.y + 80.0, pixelPos.y);
    float moonA = moonDisc * 0.85 * bandDisc + moonHalo * (0.18 + breath * 0.08) * bandHalo;

    //====顶沿雨丝:细密斜落的雨,住在顶沿边框带,落到框沿溅开即止====
    //下落速率对齐 CPU 雨丝粒子(约 160px/s),背景雨与前景雨不打架
    float slant = pixelPos.x + pixelPos.y * 0.34;
    float rainN = valueNoise(float2(slant * 0.16, pixelPos.y * 0.016 - t * 2.6));
    float rainCol01 = smoothstep(0.66, 0.92, rainN);
    //纵向包络:画布顶淡入,过框沿后迅速消失
    float aboveTop = 1.0 - smoothstep(innerMin.y + 2.0, innerMin.y + 7.0, pixelPos.y);
    float canvasIn = smoothstep(0.0, uEdgePad * 0.5, pixelPos.y);
    float endTaper = smoothstep(0.0, 0.06, u) * smoothstep(1.0, 0.94, u);
    float rainA = rainCol01 * aboveTop * canvasIn * endTaper * 0.62;
    //雨落到框沿:沿线一线溅起的湿光,随雨相位闪动
    float splash = exp(-pow(pixelPos.y - innerMin.y, 2.0) * 0.10) * rainCol01 * endTaper;
    float3 rainStreakCol = lerp(uColRain, uColMoon, rainCol01 * 0.55);
    rainA = saturate(rainA + splash * 0.5);

    //====底沿积水线:框底下方一线缓波,承住整个面板====
    float waveY = innerMax.y + 5.0
        + sin(u * TAU * 1.6 + t * 0.7) * 1.1
        + (valueNoise(float2(u * 5.0 + t * 0.05, 2.7)) - 0.5) * 2.2;
    float dyW = pixelPos.y - waveY;
    float waterLine = exp(-dyW * dyW * 0.22);
    //线下浅浅一层积水,向下淡出
    float pool = smoothstep(-1.5, 1.5, dyW) * (1.0 - smoothstep(2.0, uEdgePad * 0.8, dyW));
    float shimmer = valueNoise(float2(u * 14.0 - t * 0.20, 8.8));
    float3 waterCol = lerp(uColRain, uColMoon, smoothstep(0.60, 0.90, shimmer) * 0.65);
    float waterA = (waterLine * 0.72 + pool * 0.20) * endTaper;

    //====水幕前沿:拉开处一道湿亮的水线,边缘挂着不匀的水珠====
    float dxC = abs(pixelPos.x - center.x);
    float lineHalfLen = lineGrow * (halfSize.x + uEdgePad);
    float lineMaskX = 1.0 - smoothstep(lineHalfLen - 8.0, lineHalfLen, dxC);
    float cutStrength = 1.0 - smoothstep(0.55, 0.95, uReveal);
    float dropletN = 0.72 + 0.28 * valueNoise(float2(pixelPos.x * 0.24, frontier * 0.11));
    float frontGlow = exp(-pow(dyC - frontier, 2.0) * 0.06);
    float frontCore = exp(-pow(dyC - frontier, 2.0) * 0.34) * dropletN;
    float cutA = saturate(frontGlow * 0.42 + frontCore * 0.62) * lineMaskX * cutStrength;
    cutA *= saturate(uReveal * 8.0); //水线独立于面板淡入,起手就亮
    float3 cutCol = lerp(uColRain * 1.3, uColMoon, frontCore);

    //====预乘 over 合成(后→前:溺月→面板体→框线→雨丝→积水线→水幕前沿)====
    float bodyA = edgeAlpha * openMask * uAlpha;
    moonA *= openMask * uAlpha;
    rimA *= openMask * uAlpha;
    rainA *= openMask * uAlpha;
    waterA *= openMask * uAlpha;

    float3 C = moonCol * moonA;
    float A = moonA;
    C = bg * bodyA + C * (1.0 - bodyA);
    A = bodyA + A * (1.0 - bodyA);
    C = rimCol * rimA + C * (1.0 - rimA);
    A = rimA + A * (1.0 - rimA);
    C = rainStreakCol * rainA + C * (1.0 - rainA);
    A = rainA + A * (1.0 - rainA);
    C = waterCol * waterA + C * (1.0 - waterA);
    A = waterA + A * (1.0 - waterA);
    C = cutCol * cutA + C * (1.0 - cutA);
    A = cutA + A * (1.0 - cutA);

    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass KikasaNarrativePanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
