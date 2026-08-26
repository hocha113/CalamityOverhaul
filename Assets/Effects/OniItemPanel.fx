// ============================================================================
//OniItemPanel.fx 鬼切介绍面板背景，拔刀纸鉴:墨染和纸 + 顶沿刀痕 + 绯月 + 底部远山脊
//AlphaBlend 预乘 alpha 输出;uCol* 与绯红裂空斩共享(CPU 传 CrimsonSlashRenderer 色板)
//构图纪律:tooltip 满铺文字,护字比叙事面板更严——内区深处全静默,
//活跃细节只住边框带内侧一圈与顶/底沿
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;       //面板体不透明度(近 1,要盖得住原生蓝框)
float2 uResolution; //含 edgePad 外扩的画布尺寸
float uEdgePad;
float3 uColHot;     //白热
float3 uColBright;  //亮绯红
float3 uColDeep;    //深红
float3 uColDark;    //暗酒红

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

//色板(仅底色本地,红系全部来自 uniform)
static const float3 COL_INK  = float3(0.086, 0.043, 0.052); //暖酒红灰纸面
static const float3 COL_VOID = float3(0.044, 0.022, 0.028); //最深处仍保有材质

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //====圆角矩形 SDF + 墨缘侵蚀:轮廓像毛笔收出来的====
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 6.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;
    float edgeNoise = fbm4(pixelPos * 0.030 + float2(0.0, uTime * 0.015));
    panelSDF += (edgeNoise - 0.5) * 3.6;

    if (panelSDF > uEdgePad + 8.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime;
    float u = pixelPos.x / uResolution.x;

    //====面板体:墨染和纸====
    float3 bg = lerp(COL_INK, COL_VOID, smoothstep(0.0, 0.5, uv.y));
    bg += uColDark * (1.0 - uv.y) * 0.16;
    bg += uColDark * pow(uv.y, 3.0) * 0.16; //底部墨沉淀

    //护字静默罩:离边框带越深越安静(去细节去运动,不去光)
    float calm = 1.0 - smoothstep(-34.0, -10.0, panelSDF);
    float live = lerp(1.0, 0.32, calm);

    //纸纤维颗粒
    float grain = valueNoise(pixelPos * 0.9) * 0.55 + valueNoise(pixelPos * 0.23) * 0.45;
    bg += (grain - 0.5) * 0.048 * live;

    //洇墨:低频云状斑驳,宣纸吸墨不均
    float2 washUV = uv * float2(2.8, 2.6) + float2(t * 0.008, -t * 0.005);
    float inkWash = valueNoise(washUV) * 0.62 + valueNoise(washUV * 2.3 + 5.1) * 0.38;
    bg += uColDark * (inkWash - 0.38) * 0.20 * live;

    //边缘吃墨:靠近边框处纸面向暗酒红晕开
    float innerDist = max(-panelSDF, 0.0);
    float wash = exp(-innerDist * 0.06);
    bg = lerp(bg, uColDark * 0.55, wash * 0.32);

    //====底部远山脊线:两层淡墨剪影,鬼域天空的山脊几何====
    float ridgeFarH = 0.80 + (valueNoise(float2(uv.x * 3.1 + 2.7, 4.2)) - 0.5) * 0.06
        + (valueNoise(float2(uv.x * 7.3, 11.0)) - 0.5) * 0.02;
    float ridgeNearH = 0.90 + (valueNoise(float2(uv.x * 2.2 - 1.3 + t * 0.004, 9.1)) - 0.5) * 0.07
        + (valueNoise(float2(uv.x * 6.1, 3.3)) - 0.5) * 0.02;
    float ridgeFar = smoothstep(ridgeFarH, ridgeFarH + 0.02, uv.y);
    float ridgeNear = smoothstep(ridgeNearH, ridgeNearH + 0.02, uv.y);
    bg += uColDark * ridgeFar * 0.15;
    bg += uColDark * ridgeNear * 0.20;

    //暗角 + 细颗粒
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.46, 0.55), vig * float2(0.46, 0.55));
    bg *= saturate(1.0 - vigStr) * 0.14 + 0.86;
    float dust = hash21(pixelPos + t * 25.0) * 0.04;
    bg *= 1.0 - dust * 0.5;
    bg = max(bg, 0.0);

    //====墨笔边框:角部晕开绯红(手工收笔)====
    float rimLine = exp(-panelSDF * panelSDF * 0.30);
    float2 cornerD = halfSize - abs(pixelPos - center);
    float cornerNear = exp(-(cornerD.x + cornerD.y) * 0.045);
    float3 rimCol = lerp(uColDark * 1.7, uColBright, cornerNear * 0.85);
    float rimA = rimLine * (0.36 + cornerNear * 0.46);

    //====绯月:小半轮从右上顶沿背后升起====
    float2 moonC = float2(uResolution.x * 0.85, innerMin.y + 6.0);
    float dMoon = length(pixelPos - moonC);
    float breath = sin(t * 0.5) * 0.5 + 0.5;
    float moonR = 15.0;
    float moonDisc = 1.0 - smoothstep(moonR - 1.5, moonR + 1.5, dMoon);
    float moonHalo = exp(-max(dMoon - moonR, 0.0) * (0.12 - breath * 0.03));
    float moonTex = valueNoise(pixelPos * 0.11 + 7.3);
    float3 moonCol = lerp(uColDeep, uColBright, 0.34 + breath * 0.28) * (0.78 + moonTex * 0.5);
    moonCol += uColHot * pow(saturate(1.0 - dMoon / moonR), 3.0) * 0.12;
    float bandDisc = 1.0 - smoothstep(innerMin.y + 12.0, innerMin.y + 34.0, pixelPos.y);
    float bandHalo = 1.0 - smoothstep(innerMin.y + 16.0, innerMin.y + 64.0, pixelPos.y);
    float moonA = moonDisc * 0.95 * bandDisc + moonHalo * (0.24 + breath * 0.10) * bandHalo;

    //====顶沿刀痕:横贯顶缘的白热刀光,中段微弓,笔锋两端收,慢冷却脉动====
    float endTaper = smoothstep(0.0, 0.10, u) * smoothstep(1.0, 0.90, u);
    float bowY = innerMin.y + 1.0 - sin(u * PI) * 2.2;
    float dyCut = pixelPos.y - bowY;
    float prof = pow(max(sin(u * PI), 0.0), 0.55);
    float cutW = 0.9 + prof * 1.5;
    float cutMask = exp(-dyCut * dyCut / (cutW * cutW)) * endTaper;
    float hotCore = smoothstep(0.16, 0.50, u) * smoothstep(0.92, 0.55, u);
    float pulse = 0.82 + 0.18 * sin(t * 1.3 + u * 4.0);
    float3 cutCol = lerp(uColBright, uColHot, hotCore);
    float cutA = cutMask * (0.50 + prof * 0.42) * pulse;

    //====预乘 over 合成(后→前:绯月→面板体→框线→刀痕)====
    float bodyA = edgeAlpha * uAlpha;
    moonA *= uAlpha;
    rimA *= uAlpha;
    cutA *= uAlpha;

    float3 C = moonCol * moonA;
    float A = moonA;
    C = bg * bodyA + C * (1.0 - bodyA);
    A = bodyA + A * (1.0 - bodyA);
    C = rimCol * rimA + C * (1.0 - rimA);
    A = rimA + A * (1.0 - rimA);
    C = cutCol * cutA + C * (1.0 - cutA);
    A = cutA + A * (1.0 - cutA);

    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass OniItemPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
