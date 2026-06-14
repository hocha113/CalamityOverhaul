// ============================================================================
//HalibutHudGauge.fx 比目鱼HUD：深渊复苏流体压力柱
//玻璃压力管内的发光液体：真实液面波动 + 上升气泡 + 焦散微光 +
//70%/90% 蚀刻阈值线（液面越过时点亮）+ 临界沸腾与红化
//uFill=显示比例, uDanger=危险程度(0-1), uRate=复苏速度(驱动气泡密度)
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uFill;
float uDanger;
float uRate;
float uFlash;   //研究强化反馈闪光

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

static const float3 COL_VOID    = float3(0.004, 0.012, 0.022);
static const float3 COL_DEEP    = float3(0.012, 0.038, 0.060);
static const float3 COL_MID     = float3(0.030, 0.110, 0.150);
static const float3 COL_GLOW    = float3(0.300, 0.780, 0.980);
static const float3 COL_CAUSTIC = float3(0.620, 0.940, 1.000);
static const float3 COL_AMBER   = float3(1.000, 0.700, 0.250);
static const float3 COL_DANGER  = float3(1.000, 0.260, 0.260);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 res = uResolution;
    float2 p = coords * res;
    float t = uTime;

    //----- 胶囊管体SDF -----
    float pad = 3.0;
    float halfW = res.x * 0.5 - pad;
    float capR = halfW;
    float topY = pad + capR;
    float botY = res.y - pad - capR;
    float2 c = float2(res.x * 0.5, clamp(p.y, topY, botY));
    float tubeSDF = length(p - c) - capR;

    if (tubeSDF > 4.0) return float4(0, 0, 0, 0);
    float tubeMask = smoothstep(0.8, -0.8, tubeSDF);

    //内腔
    float innerSDF = tubeSDF + 2.6;
    float innerMask = smoothstep(0.5, -0.5, innerSDF);

    float3 col = COL_VOID * 0.8;   //空腔底色

    //----- 液面高度 -----
    float fillTop = lerp(res.y - pad - 3.0, pad + 3.0, saturate(uFill));
    //液面波动：基础正弦 + 噪声，危险时振幅与频率上升
    float waveAmp = 1.1 + uDanger * 2.6;
    float wave = sin(p.x * 0.55 + t * 3.2) * waveAmp * 0.6
               + (valueNoise(float2(p.x * 0.20, t * 1.6)) - 0.5) * waveAmp
               + sin(p.x * 1.7 - t * 7.0) * uDanger * 1.2;
    float surfaceY = fillTop + wave;
    float inFluid = smoothstep(-1.0, 1.0, p.y - surfaceY) * innerMask;

    //----- 液体本体 -----
    if (inFluid > 0.003) {
        //色带：底部深，向液面渐亮；危险度推向琥珀→赤红
        float depthT = saturate((p.y - surfaceY) / max(res.y - surfaceY, 8.0));
        float3 fluidCool = lerp(COL_GLOW, COL_MID, depthT * 0.8);
        float3 fluidHot = lerp(COL_DANGER, COL_AMBER, depthT * 0.6);
        float3 fluid = lerp(fluidCool, fluidHot, smoothstep(0.25, 0.95, uDanger));

        //内部流动
        float flow = valueNoise(p * 0.10 + float2(t * 0.25, -t * 0.55));
        fluid *= 0.70 + flow * 0.55;

        //上升气泡：两列网格，速度随复苏速率
        float bubbleSpeed = 0.35 + uRate * 1.4 + uDanger * 0.8;
        for (int layer = 0; layer < 2; layer++) {
            float lf = (float)layer;
            float grid = 13.0 + lf * 7.0;
            float colId = floor(p.x / grid);
            float s = hash21(float2(colId, lf * 17.0));
            float life = frac(s * 7.31 + t * bubbleSpeed * (0.5 + s * 0.7));
            float by = lerp(res.y - pad - 4.0, surfaceY + 3.0, life);
            float bx = (colId + 0.5) * grid + sin(life * TAU * 2.0 + s * 9.0) * 2.4;
            float bd = length(p - float2(bx, by));
            float bubble = (1.0 - smoothstep(0.4, 1.3 + s * 1.1, bd)) * step(0.30, s);
            fluid += COL_CAUSTIC * bubble * 0.45 * sin(life * PI);
        }

        //焦散微光
        float caus = pow(saturate(1.0 - abs(valueNoise(p * 0.16 + float2(0.0, -t * 0.4))
            - valueNoise(p * 0.16 + float2(5.3, -t * 0.4 + 2.1))) * 4.0), 3.0);
        fluid += COL_CAUSTIC * caus * 0.14;

        //临界：底部红光呼吸
        float bottomGlow = smoothstep(0.55, 1.0, (p.y - topY) / (botY - topY + 0.001));
        fluid += COL_DANGER * bottomGlow * uDanger * (0.5 + 0.5 * sin(t * (4.0 + uDanger * 6.0))) * 0.30;

        col = lerp(col, fluid, inFluid);
    }

    //----- 液面亮线与辉光 -----
    float surfDist = abs(p.y - surfaceY);
    float meniscus = exp(-surfDist * surfDist * 0.55) * innerMask * step(0.02, uFill);
    float3 surfCol = lerp(COL_CAUSTIC, COL_DANGER, uDanger * 0.7);
    col += surfCol * meniscus * (0.85 + uFlash * 0.8);
    col += surfCol * exp(-surfDist * 0.30) * innerMask * 0.16 * (1.0 + uFlash);

    //临界沸腾：液面上方溅起的小液滴
    if (uDanger > 0.45) {
        float dropId = floor(p.x / 6.0);
        float ds = hash21(float2(dropId, floor(t * 5.0)));
        float dLife = frac(t * 2.5 + ds * 5.0);
        float dy = surfaceY - dLife * 9.0 * uDanger;
        float dd = length(p - float2((dropId + 0.5) * 6.0, dy));
        float drop = (1.0 - smoothstep(0.2, 1.0, dd)) * step(0.72, ds) * (1.0 - dLife) * innerMask;
        col += surfCol * drop * 0.8;
    }

    //----- 空腔玻璃感 -----
    float emptyMask = (1.0 - inFluid) * innerMask;
    //左缘竖向高光
    float glassHl = exp(-pow(abs(p.x - (res.x * 0.5 - halfW * 0.55)), 2.0) * 0.18);
    col += COL_MID * glassHl * emptyMask * 0.55;
    col += COL_CAUSTIC * glassHl * emptyMask * 0.07;

    //----- 阈值蚀刻线：70% / 90% -----
    for (int th = 0; th < 2; th++) {
        float thVal = th == 0 ? 0.7 : 0.9;
        float thY = lerp(res.y - pad - 3.0, pad + 3.0, thVal);
        float thDist = abs(p.y - thY);
        float etch = exp(-thDist * thDist * 1.2) * innerMask;
        bool passed = uFill >= thVal;
        float3 thCol = th == 0 ? COL_AMBER : COL_DANGER;
        float lit = passed ? (0.75 + 0.25 * sin(t * 5.0)) : 0.22;
        col += thCol * etch * lit * 0.6;
    }

    //----- 管壁双层描边 -----
    float rimHard = 1.0 - smoothstep(0.4, 1.4, abs(tubeSDF + 0.6));
    float3 rimCol = lerp(COL_GLOW, COL_DANGER, uDanger * 0.6);
    col += rimCol * rimHard * (0.55 + uFlash * 0.5);
    float rimSoft = exp(-abs(tubeSDF + 4.5) * 0.7);
    col += rimCol * rimSoft * 0.13;

    //顶/底端帽小亮点
    float capDotT = exp(-length(p - float2(res.x * 0.5, pad + 1.5)) * 0.9);
    float capDotB = exp(-length(p - float2(res.x * 0.5, res.y - pad - 1.5)) * 0.9);
    col += rimCol * (capDotT + capDotB) * 0.8;

    float alpha = max(tubeMask * 0.94, rimHard);
    alpha = saturate(alpha + rimSoft * 0.25);
    float fa = alpha * uAlpha;
    return float4(col * uAlpha, fa) * vertexColor;
}

technique Technique1
{
    pass HalibutHudGaugePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
