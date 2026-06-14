// ============================================================================
//CyberPanel.fx SHPC 赛博面板背景
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;

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

float2 hmod(float2 a, float2 b) { return a - b * floor(a / b); }

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;

    float panelSDF = min(
        min(pixelPos.x - innerMin.x, innerMax.x - pixelPos.x),
        min(pixelPos.y - innerMin.y, innerMax.y - pixelPos.y)
    );
    if (panelSDF < -(uEdgePad + 2.0)) return float4(0, 0, 0, 0);

    //═══ A. 故障位移块（shader独有：水平带内容错位+RGB色散） ═══
    float normY0 = saturate((pixelPos.y - innerMin.y) / innerSize.y);
    float glitchMask = 0.0;
    float gt = floor(uTime * 3.5);
    float gr = hash11(gt * 17.3);
    float gt2 = floor(uTime * 6.0);
    float gr2 = hash11(gt2 * 23.7);

    if (gr > 0.65)
    {
        float bc = hash11(gt * 31.7);
        float bw = 0.012 + hash11(gt * 53.1) * 0.025;
        float ib = smoothstep(0.0, 0.004, normY0 - (bc - bw))
                 * smoothstep(0.0, 0.004, (bc + bw) - normY0);
        float shift = (hash11(gt * 71.3) - 0.5) * 16.0;
        glitchMask = ib;
        pixelPos.x += shift * ib;
    }
    if (gr2 > 0.78)
    {
        float bc2 = hash11(gt2 * 47.1);
        float bw2 = 0.005 + hash11(gt2 * 67.3) * 0.010;
        float ib2 = smoothstep(0.0, 0.003, normY0 - (bc2 - bw2))
                  * smoothstep(0.0, 0.003, (bc2 + bw2) - normY0);
        float shift2 = (hash11(gt2 * 83.1) - 0.5) * 10.0;
        glitchMask = max(glitchMask, ib2);
        pixelPos.x += shift2 * ib2;
    }

    //═══ B. 六角网格 ═══
    float hexScale = 0.065;
    float2 hexUV = pixelPos;
    float distT = uTime * 0.35;
    float2 ds = pixelPos * 0.008;
    float dx = valueNoise(ds + float2(distT, 0.0)) - 0.5;
    float dy = valueNoise(ds + float2(0.0, distT * 0.7)) - 0.5;
    hexUV += float2(dx, dy) * 2.0;

    float2 hp = hexUV * hexScale;
    float2 hr = float2(1.0, 1.7320508);
    float2 hh = hr * 0.5;
    float2 ha = hmod(hp, hr) - hh;
    float2 hb = hmod(hp - hh, hr) - hh;
    float2 hg;
    if (dot(ha, ha) < dot(hb, hb))
        hg = ha;
    else
        hg = hb;

    float hexDist = max(abs(hg.x), abs(hg.y * 0.5773 + abs(hg.x) * 0.5));
    float2 cellId = hp - hg;
    float2 cellCenterPx = cellId / hexScale;
    float cellHash = hash21(cellId);
    float cellHash2 = hash21(cellId + float2(7.13, 3.71));

    float hexEdge = 1.0 - smoothstep(0.36, 0.44, hexDist);
    float hexLine = smoothstep(0.38, 0.42, hexDist) * (1.0 - smoothstep(0.42, 0.50, hexDist));

    //═══ C. 波浪推进式边缘（有节奏感，非随机） ═══
    float cellSDF = min(
        min(cellCenterPx.x - innerMin.x, innerMax.x - cellCenterPx.x),
        min(cellCenterPx.y - innerMin.y, innerMax.y - cellCenterPx.y)
    );

    float w1 = sin(cellCenterPx.x * 0.05 + uTime * 1.5);
    float w2 = sin(cellCenterPx.y * 0.07 - uTime * 1.0);
    float w3 = sin((cellCenterPx.x + cellCenterPx.y) * 0.04 + uTime * 0.8);
    float protrusion = 4.0 + (w1 + w2 * 0.7 + w3 * 0.5) * 5.5;

    float effectiveBound = cellSDF + protrusion;

    //═══ D. Alpha遮罩 ═══
    float cellAlpha;
    if (panelSDF > 18.0)
        cellAlpha = 1.0;
    else
    {
        cellAlpha = smoothstep(-2.0, 3.0, effectiveBound);
        cellAlpha = max(cellAlpha, smoothstep(10.0, 18.0, panelSDF));
    }
    if (cellAlpha < 0.01) return float4(0, 0, 0, 0);

    float2 innerCoords = saturate((pixelPos - innerMin) / innerSize);

    //═══ 1. 深暗紫色渐变 ═══
    float3 bg = lerp(float3(0.045, 0.018, 0.078), float3(0.020, 0.010, 0.048), innerCoords.y);

    //═══ 2. 噪声表面 ═══
    float sn1 = valueNoise(pixelPos * 0.08);
    float sn2 = valueNoise(pixelPos * 0.035 + 100.0);
    float sv = sn1 * 0.6 + sn2 * 0.4;
    bg *= 0.78 + sv * 0.44;
    bg += float3(0.010, -0.004, 0.016) * (sv - 0.5);

    //═══ 3. 六角网格线（紫色底+蓝色能量流） ═══
    float2 flowDir = normalize(float2(1.0, 0.8));
    float flowPhase = dot(pixelPos * 0.012, flowDir) - uTime * 1.8;
    float flowInt = sin(flowPhase) * 0.5 + 0.5;

    float lg = 0.85 + flowInt * 0.6;
    bg += float3(0.050, 0.020, 0.100) * hexLine * lg * 0.7;
    bg += float3(0.015, 0.050, 0.120) * hexLine * lg * flowInt;

    bg += float3(0.030, 0.012, 0.060) * step(0.55, cellHash) * 0.25 * hexEdge;
    bg += float3(0.015, 0.025, 0.085) * step(0.72, cellHash)
        * (sin(uTime * 1.8 + cellHash * 40.0) * 0.5 + 0.5) * hexEdge;

    //═══ 4. 能量脉冲传导（可见脉冲从角落沿六角网格传播） ═══
    float p1D = length((cellCenterPx - innerMin) * 0.01);
    float p1P = frac(uTime * 0.4) * 6.5;
    float p1B = exp(-(p1D - p1P) * (p1D - p1P) * 15.0);
    bg += float3(0.025, 0.08, 0.22) * p1B * hexLine * 3.0;
    bg += float3(0.015, 0.05, 0.14) * p1B * hexEdge * 0.6;

    float p2D = length((cellCenterPx - innerMax) * 0.01);
    float p2P = frac(uTime * 0.3 + 0.5) * 6.5;
    float p2B = exp(-(p2D - p2P) * (p2D - p2P) * 15.0);
    bg += float3(0.06, 0.025, 0.18) * p2B * hexLine * 2.5;
    bg += float3(0.04, 0.015, 0.10) * p2B * hexEdge * 0.5;

    //═══ 5. 数据流粒子（亮点沿六角边缘移动） ═══
    float cAngle = atan2(hg.y, hg.x);
    float pPhase = frac(cAngle / 6.2832 + uTime * 0.5 + cellHash);
    float pDot = 1.0 - smoothstep(0.0, 0.06, abs(pPhase - 0.5));
    bg += float3(0.06, 0.16, 0.32) * pDot * hexLine * step(0.45, cellHash) * 2.5;

    float pPhase2 = frac(cAngle / 6.2832 - uTime * 0.3 + cellHash2);
    float pDot2 = 1.0 - smoothstep(0.0, 0.05, abs(pPhase2 - 0.5));
    bg += float3(0.10, 0.03, 0.20) * pDot2 * hexLine * step(0.65, cellHash2) * 1.8;

    //═══ 6. 波浪边框辉光 ═══
    float bProx = exp(-abs(cellSDF) * 0.06);
    bg += float3(0.050, 0.020, 0.100) * hexLine * bProx * 1.5;
    bg += float3(0.012, 0.045, 0.120) * hexLine * bProx * flowInt;
    float eHL = smoothstep(3.0, 0.0, abs(effectiveBound)) * hexEdge;
    float waveT = (w1 + w2 * 0.7 + w3 * 0.5) / 4.4 + 0.5;
    bg += lerp(float3(0.045, 0.020, 0.120), float3(0.020, 0.060, 0.150), waveT) * eHL;

    //═══ 7. CRT扫描线 ═══
    float scl = frac(pixelPos.y / 3.0);
    bg *= 0.82 + 0.18 * smoothstep(0.0, 0.18, scl) * smoothstep(1.0, 0.82, scl);

    //═══ 8. 面板分段线 ═══
    float hSeg = frac(pixelPos.y / 60.0);
    bg *= 1.0 - (1.0 - smoothstep(0.0, 0.025, hSeg)) * 0.50;
    float hRef = smoothstep(0.025, 0.06, hSeg) * (1.0 - smoothstep(0.06, 0.10, hSeg));
    bg += float3(0.020, 0.012, 0.045) * hRef * 0.40;
    float vL1 = 1.0 - smoothstep(0.0, 0.005, abs(innerCoords.x - 0.28));
    float vL2 = 1.0 - smoothstep(0.0, 0.005, abs(innerCoords.x - 0.76));
    bg += float3(0.018, 0.012, 0.050) * (vL1 + vL2) * 0.35;

    //═══ 9. 全息扫掠光 ═══
    float swP = frac(uTime * 0.07);
    float swD = innerCoords.y - swP;
    if (swD < -0.5) swD += 1.0;
    if (swD > 0.5) swD -= 1.0;
    float swC = exp(-abs(swD) * 30.0);
    float swG = exp(-abs(swD) * 10.0);
    bg += float3(0.028, 0.015, 0.070) * swC * 0.6;
    bg += float3(0.010, 0.012, 0.035) * swG * 0.25;
    bg += float3(0.008, 0.025, 0.060) * swG * hexLine * 1.5;

    //═══ 10. 暗角 ═══
    float2 vig = innerCoords * 2.0 - 1.0;
    bg *= saturate(1.0 - dot(vig * float2(0.45, 0.55), vig * float2(0.45, 0.55))) * 0.35 + 0.65;

    //═══ 11. 故障块着色（RGB色散+亮块） ═══
    if (glitchMask > 0.01)
    {
        float bx = hash11(gt * 91.3 + gt2 * 13.7);
        float bw = 0.06 + hash11(gt * 113.7) * 0.12;
        float ib = step(bx, innerCoords.x) * step(innerCoords.x, bx + bw);
        float bv = ib * glitchMask;
        bg += float3(0.08, 0.02, 0.10) * bv;
        bg.r += glitchMask * 0.05;
        bg.b += glitchMask * 0.03;
        cellAlpha *= 1.0 - bv * 0.12;
    }

    //═══ 12. 顶部高光 ═══
    bg += float3(0.018, 0.010, 0.035) * (1.0 - smoothstep(0.0, 0.12, innerCoords.y)) * 0.3;

    float fa = uAlpha * cellAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass CyberPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
