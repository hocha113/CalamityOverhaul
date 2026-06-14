// ============================================================================
//BrimstoneDialogueBox.fx 硫磺火对话框背景
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;
float uInfernoPulse; //底部余热脉动

//工具函数
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

//色板
static const float3 COL_DEEP   = float3(0.020, 0.012, 0.014);//背景深黑,略带冷紫
static const float3 COL_VOID   = float3(0.008, 0.004, 0.006);
static const float3 COL_ASH    = float3(0.048, 0.022, 0.018);//灰褐
static const float3 COL_MAGMA  = float3(0.180, 0.055, 0.028);//暗红
static const float3 COL_EMBER  = float3(0.820, 0.330, 0.110);//余烬橙
static const float3 COL_FLAME  = float3(1.000, 0.640, 0.230);//火焰亮橙

//主片段
float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //圆角矩形SDF
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 8.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (panelSDF > uEdgePad + 6.0) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    //时间统一减速,整体呼吸慢下来
    float t = uTime * 0.55;

    //1 主渐变底:纵向三段(顶冷 中深 底暖),水平方向从边到中心略微变暗
    //制造中央"深渊"感,文字区最暗最干净
    float vy = uv.y;
    float3 bg;
    if (vy < 0.5) {
        bg = lerp(COL_DEEP, COL_VOID, vy * 2.0);
    }
    else {
        float tb = (vy - 0.5) * 2.0;
        bg = lerp(COL_VOID, COL_ASH * 0.65, tb);
        bg += COL_MAGMA * pow(tb, 2.2) * 0.35;
    }

    //水平方向边缘比中央略亮一点,让中央形成暗带
    float hx = abs(uv.x - 0.5) * 2.0;
    bg *= 0.88 + hx * 0.18;

    //2 底部缓慢升腾的暖雾:不再是显式火焰,只是加热的空气
    //用大尺度fbm,垂直衰减非常陡,文字区上方几乎无侵染
    float bottomBand = smoothstep(0.55, 1.0, uv.y);
    if (bottomBand > 0.001) {
        float2 huv = float2(uv.x * 2.2, (uv.y - 0.55) / 0.45 * 1.6);
        huv.y -= t * 0.35;
        float n = fbm4(huv * 1.2);
        float xFall = 1.0 - pow(abs(uv.x * 2.0 - 1.0), 1.6);
        float heat = n * bottomBand * xFall;
        heat = pow(saturate(heat), 1.5);

        float3 heatCol = lerp(COL_MAGMA * 0.75, COL_EMBER * 0.55, saturate(heat * 1.4));
        bg += heatCol * heat * (0.55 + uInfernoPulse * 0.25);

        float floorGlow = pow(saturate((uv.y - 0.82) / 0.18), 2.0);
        bg += COL_MAGMA * floorGlow * 0.40;
    }

    //3 稀疏熔岩裂纹:非常弱,仅在底部与边缘形成若隐若现的纹理
    float2 crackUV = (pixelPos + float2(0, t * 2.5)) * 0.012;
    float crackN = fbm4(crackUV);
    float crack = smoothstep(0.42, 0.5, crackN) * (1.0 - smoothstep(0.5, 0.58, crackN));
    float crackRegion = bottomBand + smoothstep(0.5, 1.0, hx);
    crack *= saturate(crackRegion) * 0.55;
    float crackBreath = sin(t * 0.9 + crackUV.x * 0.6) * 0.5 + 0.5;
    crack *= 0.5 + crackBreath * 0.5;
    bg += COL_MAGMA * crack * 0.8;
    bg += COL_EMBER * crack * crackBreath * 0.18;

    //4 火星点阵:小且稀疏的发光点,向上缓慢漂移
    //只用gridSize大的一层,避免叠加出"粒子墙"
    float gridSize = 52.0;
    float2 g = floor(pixelPos / gridSize);
    float s = hash21(g);
    float life = frac(s * 6.13 + t * (0.09 + s * 0.05));
    float2 p0 = (g + 0.5) * gridSize + (hash22(g) - 0.5) * (gridSize * 0.7);
    p0.y -= life * (gridSize * 1.1);
    p0.x += sin(life * TAU + s * 10.0) * 3.0;

    float dSpark = length(pixelPos - p0);
    float sparkSize = 1.1 + s * 0.9;
    float spark = 1.0 - smoothstep(0.0, sparkSize, dSpark);
    spark *= step(0.72, s);
    spark *= sin(life * PI);
    spark *= smoothstep(0.0, 0.4, uv.y) * (0.5 + bottomBand * 0.9);

    bg += COL_FLAME * spark * 0.9;
    float sparkGlow = exp(-dSpark * 0.9) * step(0.72, s) * sin(life * PI);
    bg += COL_EMBER * sparkGlow * 0.15;

    //5 中央文字区保护:额外压暗
    float vCenter = abs(uv.y - 0.5) * 2.0;
    float textMask = 1.0 - smoothstep(0.05, 0.5, vCenter);
    bg *= lerp(1.0, 0.68, textMask);
    bg = lerp(bg, bg * float3(0.82, 0.85, 0.95), textMask * 0.35);

    //6 脉动火焰内边:集中表达"硫磺"个性,其余区域干净
    float innerDist = max(-panelSDF, 0.0);
    float rimSoft = exp(-innerDist * 0.16);
    float rimLine = exp(-innerDist * innerDist * 0.5);
    float rimPulse = 0.8 + sin(t * 1.1) * 0.2;
    float topBias = smoothstep(0.0, 0.25, 1.0 - uv.y);
    float botBias = smoothstep(0.0, 0.25, uv.y);
    float biasWarm = botBias * 1.1 + topBias * 0.55;

    bg += COL_MAGMA * rimSoft * 0.55 * rimPulse;
    bg += COL_EMBER * rimLine * 0.45 * rimPulse * biasWarm;
    float crawl = sin(uv.x * 6.0 + uv.y * 6.0 + t * 0.7) * 0.5 + 0.5;
    bg += COL_EMBER * rimLine * crawl * 0.18 * biasWarm;

    //7 浮雕斜面
    float bevelW = 10.0;
    float bevelMask = saturate(-panelSDF / bevelW);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;
    float2 lightDir = normalize(float2(0.6, -0.75));
    float2 edgeN = normalize(pixelPos - center + 0.0001);
    float bevelLight = dot(edgeN, lightDir) * 0.5 + 0.5;
    bg += lerp(COL_VOID, COL_EMBER * 0.7, bevelLight) * bevelMask * 0.35;
    float glint = bevelMask * pow(saturate(bevelLight), 12.0);
    bg += COL_FLAME * glint * 0.25;
    float grooveD = abs(-panelSDF - bevelW);
    float groove = exp(-grooveD * grooveD * 0.18) * 0.15;
    bg -= float3(0.022, 0.014, 0.010) * groove;

    //8 暗角
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.50, 0.60), vig * float2(0.50, 0.60));
    bg *= saturate(1.0 - vigStr) * 0.30 + 0.70;

    //9 细颗粒胶片感
    float dust = hash21(pixelPos + t * 25.0) * 0.04;
    bg *= 1.0 - dust * 0.5;

    float fa = uAlpha * edgeAlpha;
    float emitBoost = saturate((max(bg.r, max(bg.g, bg.b)) - 0.55) * 0.7);
    fa = saturate(fa + emitBoost * edgeAlpha * 0.2);
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass BrimstoneDialogueBoxPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
