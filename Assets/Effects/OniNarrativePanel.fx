// ============================================================================
//OniNarrativePanel.fx 鬼切叙事面板背景，墨染和纸 + 顶沿注连墨绸 + 绯月 + 拔刀开合
//AlphaBlend 预乘 alpha 输出;调色板与绯红裂空斩共享(uCol* 由 CPU 传入,保证同源)
//构图纪律:签名装饰全部住在边框带(edgePad 区),面板内部保持墨黑静场护住文字
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;       //面板体不透明度(CPU 侧已做快速上斜,避免"半透明面板"长时间存在)
float2 uResolution; //含 edgePad 外扩的画布尺寸
float uEdgePad;
float uReveal;      //0~1 开合编舞:0.10 前中央刀光沿 X 生长,0.62 前上下剖开,之后定格
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
//基材亮度抬到可见档:暗夜和纸是"暗但有物",不是黑洞
static const float3 COL_INK  = float3(0.092, 0.046, 0.056); //暖酒红灰纸面
static const float3 COL_VOID = float3(0.048, 0.024, 0.031); //最深处仍保有材质

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //====圆角矩形 SDF + 墨缘侵蚀:轮廓像毛笔收出来的,不是直线====
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 7.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;
    float edgeNoise = fbm4(pixelPos * 0.030 + float2(0.0, uTime * 0.015));
    panelSDF += (edgeNoise - 0.5) * 4.2;

    if (panelSDF > uEdgePad + 10.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime;

    //====拔刀编舞:刀光生长 → 上下剖开====
    float lineGrow = saturate(uReveal / 0.10);
    float unfold = saturate((uReveal - 0.10) / 0.52);
    unfold = unfold * (2.0 - unfold); //easeOut
    float dyC = abs(pixelPos.y - center.y);
    float frontier = unfold * (halfSize.y + uEdgePad + 8.0);
    float openMask = 1.0 - smoothstep(frontier - 2.0, frontier + 2.0, dyC);

    //====面板体:墨染和纸====
    //纵向:顶部渗入一线酒红天光(逢魔),中段沉到最深
    float3 bg = lerp(COL_INK, COL_VOID, smoothstep(0.0, 0.55, uv.y));
    bg += uColDark * (1.0 - uv.y) * 0.20;
    bg += uColDark * pow(uv.y, 3.0) * 0.20; //底部墨沉淀

    //文字区静默罩:护字=去细节去运动,不是去光。中央只轻微压平
    float vCenter = abs(uv.y - 0.5) * 2.0;
    float textMask = 1.0 - smoothstep(0.10, 0.62, vCenter);

    //纸纤维颗粒:两档 value noise
    float grain = valueNoise(pixelPos * 0.9) * 0.55 + valueNoise(pixelPos * 0.23) * 0.45;
    bg += (grain - 0.5) * 0.055 * lerp(1.0, 0.55, textMask);

    //洇墨:低频云状斑驳,宣纸吸墨不均(材质主笔,双向呼吸:有的地方吸得深,有的地方浅)
    //两阶手工 value noise,省 ps_3_0 常量寄存器
    float2 washUV = uv * float2(3.0, 2.2) + float2(t * 0.008, -t * 0.005);
    float inkWash = valueNoise(washUV) * 0.62 + valueNoise(washUV * 2.3 + 5.1) * 0.38;
    bg += uColDark * (inkWash - 0.38) * 0.24 * lerp(1.0, 0.5, textMask);

    bg *= lerp(1.0, 0.94, textMask);

    //边缘吃墨:靠近边框处纸面向暗酒红晕开
    float innerDist = max(-panelSDF, 0.0);
    float wash = exp(-innerDist * 0.055);
    bg = lerp(bg, uColDark * 0.55, wash * 0.34);

    //一缕极缓的墨流(大尺度 fbm,左右两翼,中央只衰减不扑灭，文字列横向够不到两翼)
    float2 flowUV = float2(uv.x * 2.6, uv.y * 1.4 - t * 0.03);
    float inkFlow = fbm4(flowUV * 1.3);
    float flank = smoothstep(0.24, 0.95, abs(uv.x - 0.5) * 2.0);
    bg += uColDark * inkFlow * flank * lerp(1.0, 0.45, textMask) * 0.30;

    //远山脊线:底部两层淡墨剪影,呼应鬼域天空的山脊几何,给内部一个景深锚点
    float ridgeFarH = 0.74 + (valueNoise(float2(uv.x * 3.1 + 2.7, 4.2)) - 0.5) * 0.08
        + (valueNoise(float2(uv.x * 7.3, 11.0)) - 0.5) * 0.025;
    float ridgeNearH = 0.85 + (valueNoise(float2(uv.x * 2.2 - 1.3 + t * 0.004, 9.1)) - 0.5) * 0.10
        + (valueNoise(float2(uv.x * 6.1, 3.3)) - 0.5) * 0.03;
    float ridgeFar = smoothstep(ridgeFarH, ridgeFarH + 0.02, uv.y);
    float ridgeNear = smoothstep(ridgeNearH, ridgeNearH + 0.02, uv.y);
    float ridgeSoft = lerp(1.0, 0.55, textMask);
    bg += uColDark * ridgeFar * 0.16 * ridgeSoft;
    bg += uColDark * ridgeNear * 0.22 * ridgeSoft;

    //暗角 + 细颗粒
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.48, 0.58), vig * float2(0.48, 0.58));
    bg *= saturate(1.0 - vigStr) * 0.16 + 0.84;
    float dust = hash21(pixelPos + t * 25.0) * 0.04;
    bg *= 1.0 - dust * 0.5;
    bg = max(bg, 0.0); //洇墨的负向呼吸不许把纸面掏穿

    //====墨笔边框:框线本体在 SDF=0,角部晕开绯红(手工收笔的感觉)====
    float rimLine = exp(-panelSDF * panelSDF * 0.30);
    float2 cornerD = halfSize - abs(pixelPos - center);
    float cornerNear = exp(-(cornerD.x + cornerD.y) * 0.040);
    float3 rimCol = lerp(uColDark * 1.7, uColBright, cornerNear * 0.85);
    float rimA = rimLine * (0.42 + cornerNear * 0.50);

    //====绯月:半轮从顶沿背后升起(画在面板体之下,只在轮廓之外可见)====
    float2 moonC = float2(uResolution.x * 0.80, innerMin.y + 6.0);
    float dMoon = length(pixelPos - moonC);
    float breath = sin(t * 0.5) * 0.5 + 0.5;
    float moonR = 20.0;
    float moonDisc = 1.0 - smoothstep(moonR - 1.5, moonR + 1.5, dMoon);
    float moonHalo = exp(-max(dMoon - moonR, 0.0) * (0.11 - breath * 0.03));
    float moonTex = valueNoise(pixelPos * 0.11 + 7.3);
    float3 moonCol = lerp(uColDeep, uColBright, 0.30 + breath * 0.28) * (0.72 + moonTex * 0.5);
    moonCol += uColHot * pow(saturate(1.0 - dMoon / moonR), 3.0) * 0.10;
    //月盘卡在沿上,光晕允许向右上角内部渗一段，给内部一个光源方向
    float bandDisc = 1.0 - smoothstep(innerMin.y + 10.0, innerMin.y + 34.0, pixelPos.y);
    float bandHalo = 1.0 - smoothstep(innerMin.y + 16.0, innerMin.y + 80.0, pixelPos.y);
    float moonA = moonDisc * 0.92 * bandDisc + moonHalo * (0.22 + breath * 0.10) * bandHalo;

    //====注连墨绸:横陈在顶沿之上的流动黑红绸,中央微垂====
    float u = pixelPos.x / uResolution.x;
    float sag = sin(u * PI) * 3.4;
    float drift = (fbm4(float2(u * 2.3 + t * 0.05, 8.5)) - 0.5) * 3.0;
    float ribbonBaseY = innerMin.y - 6.0 + sag + drift;
    float flow = fbm4(float2(u * 5.0 - t * 0.16, 0.35));
    float halfW = 3.0 + flow * 2.6;
    float dyR = abs(pixelPos.y - ribbonBaseY);
    float ribbonMask = 1.0 - smoothstep(halfW - 1.2, halfW + 1.0, dyR);
    float endTaper = smoothstep(0.0, 0.06, u) * smoothstep(1.0, 0.94, u);
    ribbonMask *= endTaper;
    float ink = fbm4(float2(u * 9.0 - t * 0.28, dyR * 0.18 + 3.7));
    float3 ribbonCol = lerp(uColDark, uColDeep, ink);
    ribbonCol = lerp(ribbonCol, uColBright, smoothstep(0.62, 0.92, ink) * 0.80);
    float filament = exp(-pow(dyR - (flow - 0.5) * 2.0, 2.0) * 0.9) * smoothstep(0.74, 0.95, ink);
    ribbonCol += uColHot * filament * 0.45;
    float ribbonA = ribbonMask * 0.92;

    //====拔刀白热刀光:开合前沿的两条剖开线 + 初始中线====
    float dxC = abs(pixelPos.x - center.x);
    float lineHalfLen = lineGrow * (halfSize.x + uEdgePad);
    float lineMaskX = 1.0 - smoothstep(lineHalfLen - 8.0, lineHalfLen, dxC);
    float cutStrength = 1.0 - smoothstep(0.55, 0.95, uReveal);
    float frontGlow = exp(-pow(dyC - frontier, 2.0) * 0.10);
    float frontCore = exp(-pow(dyC - frontier, 2.0) * 0.55);
    float cutA = saturate(frontGlow * 0.55 + frontCore * 0.75) * lineMaskX * cutStrength;
    cutA *= saturate(uReveal * 8.0); //刀光独立于面板淡入,起手就亮
    float3 cutCol = lerp(uColBright, uColHot, frontCore);

    //====预乘 over 合成(后→前:绯月→面板体→框线→墨绸→刀光)====
    float bodyA = edgeAlpha * openMask * uAlpha;
    moonA *= openMask * uAlpha;
    rimA *= openMask * uAlpha;
    ribbonA *= openMask * uAlpha;

    float3 C = moonCol * moonA;
    float A = moonA;
    C = bg * bodyA + C * (1.0 - bodyA);
    A = bodyA + A * (1.0 - bodyA);
    C = rimCol * rimA + C * (1.0 - rimA);
    A = rimA + A * (1.0 - rimA);
    C = ribbonCol * ribbonA + C * (1.0 - ribbonA);
    A = ribbonA + A * (1.0 - ribbonA);
    C = cutCol * cutA + C * (1.0 - cutA);
    A = cutA + A * (1.0 - cutA);

    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass OniNarrativePanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
