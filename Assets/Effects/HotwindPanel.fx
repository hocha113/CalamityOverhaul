// ============================================================================
//HotwindPanel.fx 热风任务书面板
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;
float uNightMode; //0暖铜琥珀 1冷钢月蓝

//─── 噪声工具 ───
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

float fbm2(float2 p) {
    float v = 0.0;
    v += valueNoise(p) * 0.5;
    v += valueNoise(p * 2.03 + 17.0) * 0.25;
    v += valueNoise(p * 4.01 + 43.0) * 0.125;
    return v / 0.875;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;

    //═══ 面板SDF（圆角矩形） ═══
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;
    float2 d = abs(pixelPos - center) - halfSize;
    float cornerR = 6.0;
    float panelSDF = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - cornerR;

    if (panelSDF > uEdgePad + 2.0) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);

    //═══ Alpha遮罩（柔化边缘而非硬切） ═══
    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.5, panelSDF);
    if (edgeAlpha < 0.01) return float4(0, 0, 0, 0);

    //─── 夜间模式色彩插值 ───
    float nm = uNightMode;

    //═══ 1. 底色渐变（暖铜→冷钢） ═══
    float3 warmTop = float3(0.085, 0.042, 0.020);
    float3 warmBot = float3(0.038, 0.018, 0.010);
    float3 coolTop = float3(0.028, 0.042, 0.078);
    float3 coolBot = float3(0.012, 0.020, 0.045);

    float3 bgTop = lerp(warmTop, coolTop, nm);
    float3 bgBot = lerp(warmBot, coolBot, nm);
    float3 bg = lerp(bgTop, bgBot, uv.y);

    //═══ 2. 锻造金属噪声纹理（拉丝/锤痕质感） ═══
    float2 metalUV = pixelPos * 0.06;
    float grain1 = valueNoise(metalUV + float2(0, uTime * 0.08));
    float grain2 = valueNoise(metalUV * float2(3.5, 0.3) + 50.0);
    float grain3 = valueNoise(metalUV * 1.7 + 200.0);
    float brushed = grain1 * 0.45 + grain2 * 0.35 + grain3 * 0.20;

    bg *= 0.72 + brushed * 0.56;

    float3 warmTint = float3(0.020, 0.008, -0.005);
    float3 coolTint = float3(-0.005, 0.005, 0.018);
    bg += lerp(warmTint, coolTint, nm) * (brushed - 0.5);

    //═══ 3. 热能脉络网（有机流动纹路，非几何网格） ═══
    float2 veinUV = pixelPos * 0.025;
    float veinT = uTime * 0.4;

    float v1 = fbm2(veinUV + float2(veinT * 0.3, 0));
    float v2 = fbm2(veinUV * 1.3 + float2(0, veinT * 0.25) + 100.0);
    float veinPattern = abs(v1 - v2);
    float veinLine = 1.0 - smoothstep(0.0, 0.06, veinPattern);
    veinLine *= veinLine;

    float veinFlow = sin(v1 * 12.0 - uTime * 2.2) * 0.5 + 0.5;
    float veinPulse = veinLine * (0.35 + veinFlow * 0.65);

    float3 warmVein = float3(0.22, 0.08, 0.02);
    float3 coolVein = float3(0.04, 0.10, 0.22);
    bg += lerp(warmVein, coolVein, nm) * veinPulse * 0.65;

    float3 warmVeinGlow = float3(0.35, 0.15, 0.04);
    float3 coolVeinGlow = float3(0.06, 0.15, 0.35);
    bg += lerp(warmVeinGlow, coolVeinGlow, nm) * veinLine * veinFlow * 0.25;

    //═══ 4. 热力涟漪（从中心向外扩散的暖光波纹） ═══
    float2 rippleCenter = innerMin + innerSize * 0.5;
    float rippleDist = length((pixelPos - rippleCenter) * float2(1.0, 1.4)) * 0.008;
    float ripple1 = sin(rippleDist * 18.0 - uTime * 1.6) * 0.5 + 0.5;
    float ripple2 = sin(rippleDist * 12.0 + uTime * 1.1 + 2.0) * 0.5 + 0.5;
    float rippleFade = exp(-rippleDist * 0.6);
    float rippleV = ripple1 * ripple2 * rippleFade;

    float3 warmRipple = float3(0.08, 0.03, 0.01);
    float3 coolRipple = float3(0.015, 0.03, 0.07);
    bg += lerp(warmRipple, coolRipple, nm) * rippleV * 0.4;

    //═══ 5. 浮雕边缘光照（模拟光源从左上打下的斜面效果） ═══
    float bevelWidth = 12.0;
    float bevelSDF = -panelSDF;
    float bevelMask = saturate(bevelSDF / bevelWidth);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;

    float2 lightDir = normalize(float2(0.7, -0.6));
    float2 edgeNormal = normalize(pixelPos - center);
    float bevelLight = dot(edgeNormal, lightDir) * 0.5 + 0.5;

    float3 warmHL = float3(0.25, 0.15, 0.06);
    float3 coolHL = float3(0.08, 0.14, 0.25);
    float3 warmSH = float3(0.01, 0.005, 0.002);
    float3 coolSH = float3(0.004, 0.006, 0.015);

    float3 highlightC = lerp(warmHL, coolHL, nm);
    float3 shadowC = lerp(warmSH, coolSH, nm);

    bg += lerp(shadowC, highlightC, bevelLight) * bevelMask * 0.8;

    float edgeGlint = bevelMask * pow(bevelLight, 8.0);
    float3 warmGlint = float3(0.40, 0.25, 0.10);
    float3 coolGlint = float3(0.12, 0.20, 0.40);
    bg += lerp(warmGlint, coolGlint, nm) * edgeGlint * 0.35;

    //内凹分界线（面板内侧微妙的凹槽感）
    float grooveDist = abs(bevelSDF - bevelWidth);
    float groove = exp(-grooveDist * grooveDist * 0.15) * 0.15;
    bg -= groove * float3(0.03, 0.02, 0.01);

    //═══ 6. 扫掠暖光（从上到下缓慢移动的发光带） ═══
    float sweepPhase = frac(uTime * 0.055);
    float sweepDist = uv.y - sweepPhase;
    if (sweepDist < -0.5) sweepDist += 1.0;
    if (sweepDist > 0.5) sweepDist -= 1.0;
    float sweepCore = exp(-abs(sweepDist) * 35.0);
    float sweepGlow = exp(-abs(sweepDist) * 12.0);

    float3 warmSweep = float3(0.12, 0.06, 0.02);
    float3 coolSweep = float3(0.03, 0.06, 0.12);
    bg += lerp(warmSweep, coolSweep, nm) * sweepCore * 0.5;
    bg += lerp(warmSweep, coolSweep, nm) * sweepGlow * 0.12;

    //═══ 7. 热力扫描线（微妙的水平纹理，比CRT更有机） ═══
    float scanRaw = pixelPos.y * 0.25;
    float scanWave = sin(scanRaw) * 0.5 + 0.5;
    float scanLine = scanWave * scanWave;
    bg *= 0.90 + 0.10 * scanLine;

    float fineGrain = valueNoise(pixelPos * 0.4) * 0.08;
    bg *= 1.0 - fineGrain;

    //═══ 8. 微粒烬火（shader内的小光点，沿脉络缓慢飘动） ═══
    float emberAccum = 0.0;
    float3 emberColorAccum = float3(0, 0, 0);
    float2 eGrid = floor(pixelPos / 40.0);
    float eSeed = hash21(eGrid);
    float eLife = frac(eSeed * 7.13 + uTime * (0.15 + eSeed * 0.12));
    float2 eCenter = (eGrid + 0.5) * 40.0 + (float2(hash21(eGrid + 1.0), hash21(eGrid + 2.0)) - 0.5) * 30.0;
    eCenter.y -= eLife * 20.0;
    float eDist = length(pixelPos - eCenter);
    float eSize = 1.5 + eSeed * 1.5;
    float eBright = (1.0 - smoothstep(0.0, eSize, eDist));
    eBright *= sin(eLife * 3.14159) * step(0.6, eSeed);
    float3 warmEmber = float3(0.35, 0.15, 0.03);
    float3 coolEmber = float3(0.05, 0.12, 0.30);
    emberColorAccum = lerp(warmEmber, coolEmber, nm) * eBright;
    bg += emberColorAccum * 0.6;

    //第二层烬火
    float2 eGrid2 = floor(pixelPos / 55.0);
    float eSeed2 = hash21(eGrid2 + 77.0);
    float eLife2 = frac(eSeed2 * 11.37 + uTime * (0.10 + eSeed2 * 0.08));
    float2 eCenter2 = (eGrid2 + 0.5) * 55.0 + (float2(hash21(eGrid2 + 3.0), hash21(eGrid2 + 4.0)) - 0.5) * 40.0;
    eCenter2.y -= eLife2 * 30.0;
    float eDist2 = length(pixelPos - eCenter2);
    float eSize2 = 1.0 + eSeed2 * 2.0;
    float eBright2 = (1.0 - smoothstep(0.0, eSize2, eDist2));
    eBright2 *= sin(eLife2 * 3.14159) * step(0.55, eSeed2);
    bg += lerp(warmEmber * 0.7, coolEmber * 0.7, nm) * eBright2 * 0.4;

    //═══ 9. 角落暖光晕（四角微妙的聚焦光） ═══
    float2 corners[4] = {
        innerMin + float2(30.0, 30.0),
        float2(innerMax.x - 30.0, innerMin.y + 30.0),
        float2(innerMin.x + 30.0, innerMax.y - 30.0),
        innerMax - float2(30.0, 30.0)
    };
    float cornerPulse = sin(uTime * 1.2) * 0.3 + 0.7;
    float3 warmCorner = float3(0.12, 0.06, 0.02);
    float3 coolCorner = float3(0.02, 0.05, 0.12);

    for (int ci = 0; ci < 4; ci++) {
        float cDist = length(pixelPos - corners[ci]);
        float cGlow = exp(-cDist * 0.04) * cornerPulse;
        bg += lerp(warmCorner, coolCorner, nm) * cGlow * 0.18;
    }

    //═══ 10. 暗角（有机椭圆渐暗） ═══
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.5, 0.6), vig * float2(0.5, 0.6));
    bg *= saturate(1.0 - vigStr) * 0.40 + 0.60;

    //═══ 11. 面板分段线（铆钉槽感） ═══
    float segY = frac(pixelPos.y / 65.0);
    float segLine = 1.0 - smoothstep(0.0, 0.02, segY);
    bg -= float3(0.025, 0.015, 0.008) * segLine * 0.4;
    float segHighlight = smoothstep(0.02, 0.04, segY) * (1.0 - smoothstep(0.04, 0.08, segY));
    float3 warmSegHL = float3(0.03, 0.018, 0.008);
    float3 coolSegHL = float3(0.008, 0.015, 0.03);
    bg += lerp(warmSegHL, coolSegHL, nm) * segHighlight * 0.3;

    //═══ 12. 顶部反光 ═══
    float topRef = (1.0 - smoothstep(0.0, 0.15, uv.y));
    float3 warmTopRef = float3(0.04, 0.022, 0.010);
    float3 coolTopRef = float3(0.012, 0.020, 0.04);
    bg += lerp(warmTopRef, coolTopRef, nm) * topRef * 0.35;

    //═══ 组合输出 ═══
    float fa = uAlpha * edgeAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass HotwindPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
