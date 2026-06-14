// ============================================================================
//HalibutPanel.fx 比目鱼 UI 深海面板背板
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uDepth;       //基调深浅
float uAgitation;   //深渊不安，复苏比例驱动
float uContentDim;  //中央压暗，文字密集面板调大

#define PI 3.14159265
#define TAU 6.28318530

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
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
        p = p * 2.07 + float2(3.1, 7.4);
        a *= 0.5;
    }
    return v;
}

//色板（与 HalibutTheme 对应）
static const float3 COL_VOID    = float3(0.004, 0.012, 0.022);
static const float3 COL_DEEP    = float3(0.012, 0.038, 0.060);
static const float3 COL_MID     = float3(0.030, 0.110, 0.150);
static const float3 COL_TEAL    = float3(0.060, 0.260, 0.310);
static const float3 COL_GLOW    = float3(0.300, 0.780, 0.980);
static const float3 COL_CAUSTIC = float3(0.620, 0.940, 1.000);
static const float3 COL_DANGER  = float3(1.000, 0.300, 0.300);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float edgePad = 3.0;
    float2 innerMin = float2(edgePad, edgePad);
    float2 innerMax = uResolution - float2(edgePad, edgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //大圆角矩形SDF：柔和的水滴质感轮廓
    float cornerR = clamp(min(halfSize.x, halfSize.y) * 0.22, 7.0, 13.0);
    float2 dd = abs(pixelPos - center) - halfSize + cornerR;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (panelSDF > edgePad + 5.0) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime * 0.5;

    //1 纵向深渊渐变，uDepth越大整体越沉
    float vy = uv.y;
    float3 deepCol = lerp(COL_DEEP, COL_VOID, uDepth * 0.7);
    float3 midCol = lerp(COL_MID, COL_DEEP, uDepth * 0.6);
    float3 tealCol = lerp(COL_TEAL, COL_MID, uDepth * 0.7);
    float3 bg;
    if (vy < 0.45) {
        bg = lerp(deepCol * 0.55, deepCol, vy / 0.45);
    }
    else {
        float tb = (vy - 0.45) / 0.55;
        bg = lerp(deepCol, midCol, tb);
        bg += tealCol * pow(tb, 2.4) * 0.18;
    }

    //2 低频流场扰动
    float2 flowUV = float2(uv.x * 2.4 + t * 0.16, uv.y * 1.5 - t * 0.09);
    float flow = fbm3(flowUV);
    bg += midCol * (flow - 0.5) * 0.20;

    //3 底部焦散网纹
    float bottomBand = smoothstep(0.55, 1.0, uv.y) * (1.0 - uDepth * 0.55);
    if (bottomBand > 0.001) {
        float2 cu = float2(uv.x * 3.2, uv.y * 2.0);
        cu.x += sin(t * 0.7 + uv.y * 4.0) * 0.16;
        cu.y -= t * 0.22;
        float n1 = valueNoise(cu * 1.6);
        float n2 = valueNoise(cu * 1.6 + float2(7.3, 2.1));
        float caustic = pow(saturate(1.0 - abs(n1 - n2) * 4.5), 3.0);
        bg += COL_GLOW * caustic * bottomBand * 0.18;
    }

    //4 稀疏浮游冷光粒，缓慢上浮
    float gridSize = 46.0;
    float2 g = floor(pixelPos / gridSize);
    float s = hash21(g);
    float life = frac(s * 5.31 + t * (0.06 + s * 0.05));
    float2 p0 = (g + 0.5) * gridSize + (hash22(g) - 0.5) * (gridSize * 0.7);
    p0.y -= life * (gridSize * 1.2);
    p0.x += sin(life * TAU + s * 9.0) * 3.5;
    float dPart = length(pixelPos - p0);
    float core = (1.0 - smoothstep(0.0, 1.1 + s, dPart)) * step(0.76, s) * sin(life * PI);
    bg += COL_CAUSTIC * core * 0.7;
    bg += COL_GLOW * exp(-dPart * 0.4) * step(0.76, s) * sin(life * PI) * 0.14;

    //5 中央内容压暗，保证前景文字可读
    float2 vCen = abs(uv - 0.5) * 2.0;
    float centerMask = 1.0 - smoothstep(0.15, 0.85, max(vCen.x, vCen.y));
    bg *= lerp(1.0, 1.0 - uContentDim * 0.42, centerMask);
    bg = lerp(bg, bg * float3(0.80, 0.88, 1.00), centerMask * 0.35);

    //6 躁动红化：从底部渗入的不安红光，随uAgitation增强并脉动
    if (uAgitation > 0.01) {
        float unrest = uAgitation * (0.72 + 0.28 * sin(t * (2.0 + uAgitation * 5.0)));
        float fromBottom = smoothstep(0.35, 1.0, uv.y);
        bg += COL_DANGER * fromBottom * unrest * 0.16;
        bg = lerp(bg, bg * float3(1.10, 0.85, 0.85), uAgitation * 0.4);
    }

    //7 柔和双层描边：细亮缘线 + 内侧柔光带，水膜质感
    float innerDist = max(-panelSDF, 0.0);
    float rimPulse = 0.82 + sin(t * 1.0) * 0.18;
    float3 rimCol = lerp(COL_GLOW, COL_DANGER, uAgitation * 0.55);
    //缘线（柔化的细线，不做硬1px）
    float rimHard = 1.0 - smoothstep(0.4, 2.6, abs(innerDist - 1.2));
    bg += rimCol * rimHard * 0.38 * rimPulse;
    //内侧柔光带
    float rimSoft = exp(-(innerDist - 5.0) * (innerDist - 5.0) * 0.07);
    bg += rimCol * rimSoft * 0.16 * rimPulse;
    bg += midCol * exp(-innerDist * 0.12) * 0.40 * rimPulse;
    //边缘水纹微光：缓慢游移的波纹增辉
    float ripple = sin(uv.x * 5.0 - uv.y * 4.0 + t * 0.8) * 0.5 + 0.5;
    bg += COL_CAUSTIC * rimSoft * ripple * 0.08;

    //7.5 顶缘高光扫带：缓慢左右游移的微弱亮带，强化"水面透光"质感
    float topBand = exp(-innerDist * 0.30) * smoothstep(0.35, 0.0, uv.y);
    float sweep = exp(-pow((uv.x - (0.5 + sin(t * 0.5) * 0.32)) * 3.2, 2.0));
    bg += COL_CAUSTIC * topBand * sweep * 0.16;

    //8 暗角
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.5, 0.6), vig * float2(0.5, 0.6));
    bg *= saturate(1.0 - vigStr) * 0.3 + 0.7;

    float fa = uAlpha * edgeAlpha;
    float emitBoost = saturate((max(bg.r, max(bg.g, bg.b)) - 0.55) * 0.7);
    fa = saturate(fa + emitBoost * edgeAlpha * 0.15);
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass HalibutPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
