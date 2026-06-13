// ============================================================================
// DraedonPanel.fx 嘉登数据终端面板
// AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;
float uNightMode; //0冷蓝科技 1暖红警戒

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

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    // ═══ 圆角矩形SDF ═══
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 4.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    if (panelSDF > uEdgePad + 2.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (edgeAlpha < 0.01) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float nm = uNightMode;

    // ═══ 1. 渐变底色（三段式工业底色） ═══
    float3 coolTop = float3(0.038, 0.058, 0.100);
    float3 coolMid = float3(0.022, 0.038, 0.072);
    float3 coolBot = float3(0.012, 0.020, 0.045);
    float3 warmTop = float3(0.085, 0.038, 0.028);
    float3 warmMid = float3(0.052, 0.024, 0.020);
    float3 warmBot = float3(0.030, 0.014, 0.012);

    float3 topC = lerp(coolTop, warmTop, nm);
    float3 midC = lerp(coolMid, warmMid, nm);
    float3 botC = lerp(coolBot, warmBot, nm);
    float3 bg = uv.y < 0.5
        ? lerp(topC, midC, uv.y * 2.0)
        : lerp(midC, botC, (uv.y - 0.5) * 2.0);

    // ═══ 2. 拉丝金属纹理 ═══
    float brushH = valueNoise(pixelPos * float2(0.04, 0.16));
    float brushL = valueNoise(pixelPos * 0.025 + 200.0);
    float brushed = brushH * 0.6 + brushL * 0.4;
    bg *= 0.74 + brushed * 0.52;

    float3 coolTint = float3(-0.005, 0.006, 0.018);
    float3 warmTint = float3(0.016, 0.005, -0.004);
    bg += lerp(coolTint, warmTint, nm) * (brushed - 0.5);

    // ═══ 3. 蓝图网格（双层+交叉点高亮） ═══
    float gridSize = 44.0;
    float gx = abs(frac(pixelPos.x / gridSize) - 0.5) * 2.0;
    float gy = abs(frac(pixelPos.y / gridSize) - 0.5) * 2.0;
    float gridLineX = 1.0 - smoothstep(0.0, 0.025, 1.0 - gx);
    float gridLineY = 1.0 - smoothstep(0.0, 0.025, 1.0 - gy);
    float gridLine = max(gridLineX, gridLineY);
    float gridCross = gridLineX * gridLineY;

    float subSize = 11.0;
    float sgx = abs(frac(pixelPos.x / subSize) - 0.5) * 2.0;
    float sgy = abs(frac(pixelPos.y / subSize) - 0.5) * 2.0;
    float subLine = max(
        1.0 - smoothstep(0.0, 0.05, 1.0 - sgx),
        1.0 - smoothstep(0.0, 0.05, 1.0 - sgy)
    );

    float3 coolGrid = float3(0.06, 0.14, 0.28);
    float3 warmGrid = float3(0.24, 0.08, 0.04);
    float3 gridCol = lerp(coolGrid, warmGrid, nm);

    bg += gridCol * gridLine * 0.20;
    bg += gridCol * subLine * 0.05;
    bg += gridCol * gridCross * 0.28;

    // ═══ 4. 电路走线+流动脉冲（3条，带节点闪烁） ═══
    float circuitAccum = 0.0;
    float circuitPulse = 0.0;
    for (int ci = 0; ci < 3; ci++) {
        float lineY = innerMin.y + innerSize.y * (0.22 + ci * 0.28);
        float distY = abs(pixelPos.y - lineY);
        float lineMask = 1.0 - smoothstep(0.0, 2.0, distY);

        float flowDir = (ci == 1) ? -1.0 : 1.0;
        float normX = (pixelPos.x - innerMin.x) / innerSize.x;

        //多个脉冲光点沿线流动
        float pulse1 = exp(-pow(abs(frac(normX * 0.5 + flowDir * uTime * 0.10 + ci * 0.33) - 0.5) * 8.0, 2.0));
        float pulse2 = exp(-pow(abs(frac(normX * 0.5 + flowDir * uTime * 0.10 + ci * 0.33 + 0.5) - 0.5) * 10.0, 2.0));

        circuitAccum += lineMask;
        circuitPulse += lineMask * (pulse1 + pulse2 * 0.6);

        //线与网格交叉处的节点闪烁
        float nodeFlash = gridLineX * lineMask;
        float nodePhase = sin(uTime * (1.2 + ci * 0.3) + hash11(floor(pixelPos.x / gridSize) + ci * 7.0) * 6.28) * 0.5 + 0.5;
        circuitPulse += nodeFlash * nodePhase * 0.4;
    }
    float3 coolCircuit = float3(0.04, 0.10, 0.22);
    float3 warmCircuit = float3(0.18, 0.06, 0.03);
    float3 coolPulseC = float3(0.10, 0.25, 0.50);
    float3 warmPulseC = float3(0.42, 0.14, 0.06);

    bg += lerp(coolCircuit, warmCircuit, nm) * circuitAccum * 0.30;
    bg += lerp(coolPulseC, warmPulseC, nm) * circuitPulse * 0.45;

    // ═══ 5. 能量场涟漪（从中心缓慢扩散的同心波纹） ═══
    float2 rippleCenter = center;
    float rippleDist = length((pixelPos - rippleCenter) * float2(1.0, 1.3)) * 0.012;
    float rWave1 = sin(rippleDist * 22.0 - uTime * 1.4) * 0.5 + 0.5;
    float rWave2 = sin(rippleDist * 14.0 + uTime * 0.9 + 1.8) * 0.5 + 0.5;
    float rFade = exp(-rippleDist * 0.8);
    float ripple = rWave1 * rWave2 * rFade;

    float3 coolRipple = float3(0.012, 0.028, 0.060);
    float3 warmRipple = float3(0.048, 0.018, 0.010);
    bg += lerp(coolRipple, warmRipple, nm) * ripple * 0.35;

    //涟漪增强网格可见度
    bg += gridCol * gridLine * ripple * 0.08;

    // ═══ 6. 浮雕斜面边缘 ═══
    float bevelW = 12.0;
    float bevelMask = saturate(-panelSDF / bevelW);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;

    float2 lightDir = normalize(float2(0.65, -0.65));
    float2 edgeN = normalize(pixelPos - center);
    float bevelLight = dot(edgeN, lightDir) * 0.5 + 0.5;

    float3 coolHL = float3(0.08, 0.15, 0.28);
    float3 warmHL = float3(0.22, 0.10, 0.05);
    float3 coolSH = float3(0.004, 0.007, 0.015);
    float3 warmSH = float3(0.012, 0.006, 0.003);

    bg += lerp(lerp(coolSH, warmSH, nm), lerp(coolHL, warmHL, nm), bevelLight) * bevelMask * 0.85;

    float glint = bevelMask * pow(abs(bevelLight), 8.0);
    float3 coolGlint = float3(0.14, 0.22, 0.40);
    float3 warmGlint = float3(0.36, 0.16, 0.08);
    bg += lerp(coolGlint, warmGlint, nm) * glint * 0.32;

    float grooveDist = abs(-panelSDF - bevelW);
    float groove = exp(-grooveDist * grooveDist * 0.15) * 0.18;
    bg -= groove * float3(0.03, 0.02, 0.015);

    // ═══ 7. 全息扫描光带（宽光带扫过时照亮一切） ═══
    float swPhase = frac(uTime * 0.055);
    float swDist = uv.y - swPhase;
    if (swDist < -0.5) swDist += 1.0;
    if (swDist > 0.5) swDist -= 1.0;
    float swCore = exp(-abs(swDist) * 28.0);
    float swGlow = exp(-abs(swDist) * 8.0);

    float3 coolSweep = float3(0.06, 0.12, 0.24);
    float3 warmSweep = float3(0.18, 0.08, 0.04);
    bg += lerp(coolSweep, warmSweep, nm) * swCore * 0.70;
    bg += lerp(coolSweep, warmSweep, nm) * swGlow * 0.12;
    bg += gridCol * gridLine * swGlow * 0.22;
    bg += gridCol * gridCross * swCore * 0.35;

    // ═══ 8. CRT扫描线 ═══
    float scl = frac(pixelPos.y / 3.0);
    bg *= 0.88 + 0.12 * smoothstep(0.0, 0.2, scl) * smoothstep(1.0, 0.8, scl);

    // ═══ 9. 角落指示灯（呼吸脉冲） ═══
    float2 lamp[4] = {
        innerMin + float2(18.0, 18.0),
        float2(innerMax.x - 18.0, innerMin.y + 18.0),
        float2(innerMin.x + 18.0, innerMax.y - 18.0),
        innerMax - float2(18.0, 18.0)
    };
    float3 coolLamp = float3(0.06, 0.18, 0.40);
    float3 warmLamp = float3(0.35, 0.10, 0.04);

    for (int li = 0; li < 4; li++) {
        float lDist = length(pixelPos - lamp[li]);
        float lPulse = sin(uTime * (0.8 + li * 0.15) + li * 1.57) * 0.3 + 0.7;
        float lCore = exp(-lDist * 0.20) * lPulse;
        float lGlow = exp(-lDist * 0.04) * lPulse;
        bg += lerp(coolLamp, warmLamp, nm) * lCore * 0.65;
        bg += lerp(coolLamp, warmLamp, nm) * lGlow * 0.12;
    }

    // ═══ 10. 面板接缝 ═══
    float segY = frac(pixelPos.y / 65.0);
    float segDark = 1.0 - smoothstep(0.0, 0.02, segY);
    bg -= float3(0.03, 0.025, 0.015) * segDark * 0.5;
    float segHL = smoothstep(0.02, 0.045, segY) * (1.0 - smoothstep(0.045, 0.08, segY));
    float3 coolSegHL = float3(0.010, 0.018, 0.038);
    float3 warmSegHL = float3(0.030, 0.014, 0.006);
    bg += lerp(coolSegHL, warmSegHL, nm) * segHL * 0.35;

    // ═══ 11. 暗角 ═══
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.45, 0.55), vig * float2(0.45, 0.55));
    bg *= saturate(1.0 - vigStr) * 0.35 + 0.65;

    // ═══ 12. 顶部反光 ═══
    float topRef = 1.0 - smoothstep(0.0, 0.12, uv.y);
    float3 coolTopRef = float3(0.018, 0.028, 0.055);
    float3 warmTopRef = float3(0.040, 0.020, 0.010);
    bg += lerp(coolTopRef, warmTopRef, nm) * topRef * 0.40;

    // ═══ 输出 ═══
    float fa = uAlpha * edgeAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass DraedonPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
