// ============================================================================
//KikasaItemPanel.fx 鬼伞介绍面板背景，伞下水鏡:整面落雨的湿玻璃 + 伞盖弧 + 溺月 + 底沿积水
//AlphaBlend 预乘 alpha 输出;uCol* 由 CPU 传 KikasaHudTheme 血湖⇄鬼雨插值色,
//面板随领域形态整体浸染(血湖暖红/鬼雨冷青),基材混入青灰保持湿冷(与鬼切纸面暖红拉开)
//身份主笔=雨:快速细竖雨丝(ShenyoMenuLake TechRain 已验形态——当年被否的是慢宽飘带,
//快速细雨是主菜单接受过的;y 坐标混入 x 分量防横向条带)+ 底沿落水溅圈
//构图纪律:tooltip 满铺文字,护字罩内雨衰减不熄灭,其余细节深处全静默
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;       //面板体不透明度(近 1,要盖得住原生蓝框)
float2 uResolution; //含 edgePad 外扩的画布尺寸
float uEdgePad;
float3 uColVoid;    //近黑底
float3 uColDeep;    //深水
float3 uColRain;    //主强调(血红⇄雨青)
float3 uColMoon;    //亮光(血沫暖光⇄溺月惨白)

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

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //====圆角矩形 SDF + 水蚀边缘:水把轮廓磨圆====
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 9.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;
    float edgeNoise = fbm4(pixelPos * 0.024 + float2(0.0, uTime * 0.012));
    panelSDF += (edgeNoise - 0.5) * 2.4;

    if (panelSDF > uEdgePad + 8.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime;
    float u = pixelPos.x / uResolution.x;

    //====面板体:水膜(基材由主题 uniform 推导,随血湖⇄鬼雨浸染)====
    float3 mist = uColVoid * 1.9 + uColDeep * 0.34; //顶部潮雾
    float3 voidC = uColVoid * 0.9 + uColDeep * 0.10; //最深处仍有材质
    float3 bg = lerp(mist, voidC, smoothstep(0.0, 0.38, uv.y));
    bg += uColDeep * (1.0 - uv.y) * 0.16;
    bg += uColRain * pow(uv.y, 3.0) * 0.085; //底部积水泛光

    //护字静默罩:离边框带越深越安静(tooltip 满铺文字,深处去细节去运动,不去光)
    float calm = 1.0 - smoothstep(-34.0, -10.0, panelSDF);
    float live = lerp(1.0, 0.32, calm);

    //玻璃面凝露颗粒
    float grain = valueNoise(pixelPos * 0.9) * 0.55 + valueNoise(pixelPos * 0.23) * 0.45;
    bg += (grain - 0.5) * 0.040 * live;

    //水汽:低频云状明暗,水膜厚薄不均
    float2 vaporUV = uv * float2(2.6, 2.4) + float2(t * 0.006, -t * 0.004);
    float vapor = valueNoise(vaporUV) * 0.62 + valueNoise(vaporUV * 2.3 + 5.1) * 0.38;
    bg += uColRain * (vapor - 0.42) * 0.12 * live;

    //边缘吸水:靠近边框处水膜向深水晕开
    float innerDist = max(-panelSDF, 0.0);
    float soak = exp(-innerDist * 0.06);
    bg = lerp(bg, uColDeep * 0.8, soak * 0.30);

    //两翼水痕缓流:高频竖纹极慢下滑,只在边带存活
    float streak = valueNoise(float2(uv.x * 26.0, uv.y * 2.2 - t * 0.05));
    bg += uColRain * (streak - 0.5) * 0.13 * live;

    //暗角 + 凝露微闪
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.46, 0.55), vig * float2(0.46, 0.55));
    bg *= saturate(1.0 - vigStr) * 0.14 + 0.86;
    float dust = hash21(pixelPos + t * 25.0) * 0.04;
    bg *= 1.0 - dust * 0.5;
    bg = max(bg, 0.0);

    //====水膜折光框线:上缘受月照亮====
    float rimLine = exp(-panelSDF * panelSDF * 0.30);
    float topNear = 1.0 - smoothstep(0.0, 0.5, uv.y);
    float3 rimCol = lerp(uColRain * 1.4, uColMoon, topNear * 0.55);
    float rimA = rimLine * (0.30 + topNear * 0.26);

    //====顶沿伞盖弧:宽扁半椭圆罩着面板顶缘,签名元素====
    //椭圆距离按短轴换算像素:顶点线宽准,两端变粗交给 endFade 收掉
    float2 canopyC = float2(uResolution.x * 0.42, innerMin.y + 3.0);
    float2 cdev = (pixelPos - canopyC) / float2(halfSize.x * 0.60, 15.0);
    float ringPx = (length(cdev) - 1.0) * 15.0;
    float endFade = smoothstep(1.0, 0.72, abs(cdev.x));
    float upperHalf = step(pixelPos.y, canopyC.y + 1.5);
    float canopyLine = exp(-ringPx * ringPx * 0.40) * endFade * upperHalf;
    //伞面在弧下含一口极淡的水光
    float canopyFill = (1.0 - smoothstep(0.45, 1.0, length(cdev))) * upperHalf * 0.06;
    float sway = 0.85 + 0.15 * sin(t * 0.9);
    float3 canopyCol = lerp(uColRain * 1.3, uColMoon, 0.5);
    float canopyA = (canopyLine * 0.55 + canopyFill) * sway;
    //顶针一点
    float dTip = length(pixelPos - (canopyC - float2(0.0, 16.0)));
    canopyA += exp(-dTip * dTip * 0.30) * 0.32 * sway;

    //====溺月:小半轮泡在右上顶沿背后====
    float2 moonC = float2(uResolution.x * 0.86, innerMin.y + 6.0);
    float dMoon = length(pixelPos - moonC);
    float breath = sin(t * 0.4) * 0.5 + 0.5;
    float moonR = 15.0;
    float moonDisc = 1.0 - smoothstep(moonR - 2.2, moonR + 2.2, dMoon);
    float moonHalo = exp(-max(dMoon - moonR, 0.0) * (0.14 - breath * 0.03));
    float moonTex = valueNoise(pixelPos * 0.09 + 7.3);
    float3 moonCol = lerp(uColRain, uColMoon, 0.62 + breath * 0.22) * (0.72 + moonTex * 0.4);
    float bandDisc = 1.0 - smoothstep(innerMin.y + 12.0, innerMin.y + 34.0, pixelPos.y);
    float bandHalo = 1.0 - smoothstep(innerMin.y + 16.0, innerMin.y + 64.0, pixelPos.y);
    float moonA = moonDisc * 0.95 * bandDisc + moonHalo * (0.20 + breath * 0.08) * bandHalo;

    //====底沿积水线:框底一线缓波,承住整个面板====
    float endTaper = smoothstep(0.0, 0.06, u) * smoothstep(1.0, 0.94, u);
    float waveY = innerMax.y + 4.0
        + sin(u * TAU * 1.7 + t * 0.7) * 1.0
        + (valueNoise(float2(u * 5.0 + t * 0.05, 2.7)) - 0.5) * 2.0;
    float dyW = pixelPos.y - waveY;
    float waterLine = exp(-dyW * dyW * 0.24);
    float pool = smoothstep(-1.5, 1.5, dyW) * (1.0 - smoothstep(2.0, uEdgePad * 0.8, dyW));
    float shimmer = valueNoise(float2(u * 14.0 - t * 0.20, 8.8));
    float3 waterCol = lerp(uColRain, uColMoon, smoothstep(0.60, 0.90, shimmer) * 0.65);
    float waterA = (waterLine * 0.88 + pool * 0.26) * endTaper;

    //====预乘 over 合成(后→前:溺月→面板体→框线→伞盖弧→积水线)====
    float bodyA = edgeAlpha * uAlpha;
    moonA *= uAlpha;
    rimA *= uAlpha;
    canopyA *= uAlpha;
    waterA *= uAlpha;

    float3 C = moonCol * moonA;
    float A = moonA;
    C = bg * bodyA + C * (1.0 - bodyA);
    A = bodyA + A * (1.0 - bodyA);
    C = rimCol * rimA + C * (1.0 - rimA);
    A = rimA + A * (1.0 - rimA);
    C = canopyCol * canopyA + C * (1.0 - canopyA);
    A = canopyA + A * (1.0 - canopyA);
    C = waterCol * waterA + C * (1.0 - waterA);
    A = waterA + A * (1.0 - waterA);

    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass KikasaItemPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
