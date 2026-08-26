// ============================================================================
//HalibutItemPanel.fx 比目鱼介绍面板背景，深海鉴:水面在顶,面板体是水下
//AlphaBlend 预乘 alpha 输出;色板内置,与 SeaDialogueBox.fx / HalibutTheme 同源
//身份=有机深海(缓、曲线、光柱):顶沿海面波光 + 斜射焦散光柱 + 两翼缓升气泡 +
//底部深渊沉黑;一切运动缓慢有机,与 SHPC 的快速机械扫线拉开
//构图纪律:tooltip 满铺文字,护字罩内去细节去运动;光柱算"光"留半强度
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;       //面板体不透明度(近 1,要盖得住原生蓝框)
float2 uResolution; //含 edgePad 外扩的画布尺寸
float uEdgePad;

#define PI 3.14159265
#define TAU 6.28318530

float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

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

//色板(SeaDialogueBox 同源)
static const float3 COL_VOID    = float3(0.004, 0.012, 0.022); //深渊黑蓝
static const float3 COL_DEEP    = float3(0.012, 0.038, 0.060); //深海主基调
static const float3 COL_MID     = float3(0.030, 0.110, 0.150); //中层冷蓝
static const float3 COL_TEAL    = float3(0.060, 0.260, 0.310); //近水面青绿
static const float3 COL_GLOW    = float3(0.300, 0.780, 0.980); //生物冷光
static const float3 COL_CAUSTIC = float3(0.620, 0.940, 1.000); //焦散高光
static const float3 COL_GOLD    = float3(1.000, 0.780, 0.380); //暖金点缀(HalibutTheme.Accent)

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //====圆角矩形 SDF + 水蚀有机边缘====
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 10.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;
    float edgeNoise = fbm4(pixelPos * 0.022 + float2(uTime * 0.010, 0.0));
    panelSDF += (edgeNoise - 0.5) * 2.8;

    if (panelSDF > uEdgePad + 8.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime;
    float u = pixelPos.x / uResolution.x;

    //====面板体:水下体积,上浅(近水面青)下深(深渊黑)====
    float3 bg = lerp(COL_MID * 0.8, COL_VOID, smoothstep(0.0, 0.72, uv.y));
    bg = lerp(bg, COL_DEEP, 0.35);
    bg += COL_TEAL * (1.0 - uv.y) * (1.0 - uv.y) * 0.16; //顶部浅水青

    //护字静默罩:离边框带越深越安静
    float calm = 1.0 - smoothstep(-34.0, -10.0, panelSDF);
    float live = lerp(1.0, 0.32, calm);

    //水体悬浮颗粒(海雪):极缓下沉的細粒
    float snow = valueNoise(float2(pixelPos.x * 0.7, pixelPos.y * 0.7 - t * 2.2));
    bg += COL_MID * smoothstep(0.86, 0.97, snow) * 0.35 * live;

    //水体流动:低频洋流明暗,缓慢横漂
    float2 flowUV = uv * float2(2.4, 2.0) + float2(t * 0.014, -t * 0.004);
    float flow = valueNoise(flowUV) * 0.62 + valueNoise(flowUV * 2.3 + 5.1) * 0.38;
    bg += COL_MID * (flow - 0.42) * 0.22 * live;

    //边缘吸暗:靠近边框处水体沉向深渊色
    float innerDist = max(-panelSDF, 0.0);
    float sink = exp(-innerDist * 0.055);
    bg = lerp(bg, COL_VOID, sink * 0.30);

    //====斜射焦散光柱(有机身份主笔):两道自顶缘斜落,缓慢摆动====
    //光柱算"光"不算细节,护字罩内留 55%
    float shaftLive = lerp(1.0, 0.55, calm);
    float shaft = 0.0;
    for (int si = 0; si < 2; si++) {
        float fi = (float)si;
        float baseX = 0.26 + fi * 0.34;
        float swayX = baseX + sin(t * 0.22 + fi * 2.6) * 0.035;
        float slant = 0.16 + fi * 0.05;
        float sd = abs((uv.x - swayX) - uv.y * slant);
        float width = 0.035 + fi * 0.02;
        float body = exp(-sd * sd / (width * width));
        //光强随深度衰减,且随时间呼吸
        float breathe = 0.75 + 0.25 * sin(t * 0.4 + fi * 1.9);
        shaft += body * pow(1.0 - uv.y, 1.6) * breathe;
    }
    bg += COL_GLOW * shaft * 0.11 * shaftLive;
    bg += COL_CAUSTIC * shaft * shaft * 0.05 * shaftLive;

    //====两翼缓升气泡:哈希相位小亮环,升到顶淡出====
    float flank = smoothstep(0.20, 0.85, abs(uv.x - 0.5) * 2.0);
    float bubbles = 0.0;
    for (int bi = 0; bi < 6; bi++) {
        float fb = (float)bi;
        float speed = 0.045 + hash11(fb * 1.71) * 0.035;
        float ph = frac(t * speed + hash11(fb * 3.13));
        float bx = hash11(fb * 5.37);
        bx = bx < 0.5 ? 0.04 + bx * 0.36 : 0.60 + (bx - 0.5) * 0.72;
        float wob = sin(t * 0.9 + fb * 2.2) * 0.012; //上升途中轻摆
        float2 bp = float2((bx + wob) * innerSize.x, (1.0 - ph) * innerSize.y) + innerMin;
        float dB = length(pixelPos - bp);
        float r = 1.1 + hash11(fb * 9.73) * 1.5;
        float ring = exp(-pow(dB - r, 2.0) * 1.3);
        //出生淡入,近顶淡出
        bubbles += ring * smoothstep(0.0, 0.12, ph) * smoothstep(1.0, 0.82, ph);
    }
    bg += COL_GLOW * bubbles * 0.30 * lerp(1.0, 0.5, calm) * max(flank, 0.35);
    bg = max(bg, 0.0);

    //====框线:冷光折光,顶部近水面亮,角部一点暖金====
    float rimLine = exp(-panelSDF * panelSDF * 0.30);
    float topNear = 1.0 - smoothstep(0.0, 0.5, uv.y);
    float2 cornerD = halfSize - abs(pixelPos - center);
    float cornerNear = exp(-(cornerD.x + cornerD.y) * 0.045);
    float3 rimCol = lerp(COL_GLOW * 0.8, COL_CAUSTIC, topNear * 0.5);
    rimCol = lerp(rimCol, COL_GOLD, cornerNear * 0.45); //暖金角部点缀
    float rimA = rimLine * (0.30 + topNear * 0.26 + cornerNear * 0.18);

    //====顶沿海面波光:框顶上方一线缓波焦散,水面在面板之上====
    float endTaper = smoothstep(0.0, 0.06, u) * smoothstep(1.0, 0.94, u);
    float surfY = innerMin.y - 3.0
        + sin(u * TAU * 1.9 + t * 0.5) * 1.2
        + (valueNoise(float2(u * 6.0 - t * 0.06, 3.3)) - 0.5) * 2.4;
    float dyS = pixelPos.y - surfY;
    float surfLine = exp(-dyS * dyS * 0.20);
    float caustic = valueNoise(float2(u * 16.0 + t * 0.25, 7.7));
    float3 surfCol = lerp(COL_GLOW, COL_CAUSTIC, smoothstep(0.55, 0.9, caustic) * 0.8);
    //水面下的漫光往下渗一段
    float underGlow = smoothstep(-1.5, 1.5, dyS) * (1.0 - smoothstep(2.0, 26.0, dyS));
    float surfA = (surfLine * 0.78 + underGlow * 0.10) * endTaper;

    //====预乘 over 合成(后→前:面板体→框线→海面波光)====
    float bodyA = edgeAlpha * uAlpha;
    rimA *= uAlpha;
    surfA *= uAlpha;

    float3 C = bg * bodyA;
    float A = bodyA;
    C = rimCol * rimA + C * (1.0 - rimA);
    A = rimA + A * (1.0 - rimA);
    C = surfCol * surfA + C * (1.0 - surfA);
    A = surfA + A * (1.0 - surfA);

    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass HalibutItemPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
