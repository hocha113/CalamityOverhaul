// ============================================================================
//ForestPanel.fx 森林魔法面板
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;
float uNightMode; //0暖阳翠绿 1冷月幽蓝

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

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //═══ 圆角矩形SDF ═══
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 5.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    if (panelSDF > uEdgePad + 2.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (edgeAlpha < 0.01) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float nm = uNightMode;

    //═══ 1. 苔藓渐变底色（三段式） ═══
    float3 warmTop = float3(0.048, 0.058, 0.022);
    float3 warmMid = float3(0.032, 0.045, 0.016);
    float3 warmBot = float3(0.022, 0.028, 0.010);
    float3 coolTop = float3(0.022, 0.048, 0.058);
    float3 coolMid = float3(0.016, 0.032, 0.048);
    float3 coolBot = float3(0.010, 0.020, 0.032);

    float3 topC = lerp(warmTop, coolTop, nm);
    float3 midC = lerp(warmMid, coolMid, nm);
    float3 botC = lerp(warmBot, coolBot, nm);
    float3 bg = uv.y < 0.5
        ? lerp(topC, midC, uv.y * 2.0)
        : lerp(midC, botC, (uv.y - 0.5) * 2.0);

    //═══ 2. 树皮苔藓纹理 ═══
    float barkV = valueNoise(pixelPos * float2(0.03, 0.14));
    float mossP = valueNoise(pixelPos * 0.022 + 300.0);
    float texBlend = barkV * 0.55 + mossP * 0.45;
    bg *= 0.70 + texBlend * 0.60;

    float3 warmTint = float3(0.014, 0.010, -0.004);
    float3 coolTint = float3(-0.004, 0.006, 0.016);
    bg += lerp(warmTint, coolTint, nm) * (texBlend - 0.5);

    //═══ 3. 菌丝脉络网络（ridge noise + 动态脉冲） ═══
    float2 veinUV1 = pixelPos * 0.016 + float2(uTime * 0.12, uTime * 0.06);
    float2 veinUV2 = pixelPos * 0.032 + float2(-uTime * 0.08, uTime * 0.10) + 50.0;
    float ridge1 = 1.0 - abs(valueNoise(veinUV1) * 2.0 - 1.0);
    float ridge2 = 1.0 - abs(valueNoise(veinUV2) * 2.0 - 1.0);
    float veinPattern = ridge1 * ridge1 * 0.6 + ridge2 * ridge2 * 0.4;
    float veinPulse = sin(uTime * 0.7 + veinPattern * 6.28) * 0.25 + 0.75;

    float3 warmVein = float3(0.10, 0.24, 0.06);
    float3 coolVein = float3(0.04, 0.18, 0.26);
    bg += lerp(warmVein, coolVein, nm) * veinPattern * veinPulse * 0.38;

    //═══ 4. 萤火微粒（5个漂浮光点） ═══
    for (int fi = 0; fi < 5; fi++) {
        float2 flyBase = float2(
            hash11(fi * 3.7 + 1.0) * innerSize.x + innerMin.x,
            hash11(fi * 5.3 + 2.0) * innerSize.y + innerMin.y
        );
        flyBase.x += sin(uTime * (0.25 + hash11(fi * 1.1) * 0.35) + fi * 2.1) * 28.0;
        flyBase.y += cos(uTime * (0.18 + hash11(fi * 2.3) * 0.25) + fi * 1.7) * 22.0;

        float flyDist = length(pixelPos - flyBase);
        float flyPhase = sin(uTime * (1.0 + fi * 0.18) + fi * 1.57) * 0.5 + 0.5;
        float flyGlow = exp(-flyDist * 0.10) * flyPhase;
        float flyCore = exp(-flyDist * 0.40) * flyPhase;

        float3 warmFly = float3(0.22, 0.28, 0.04);
        float3 coolFly = float3(0.06, 0.22, 0.30);
        bg += lerp(warmFly, coolFly, nm) * flyGlow * 0.22;
        bg += lerp(warmFly, coolFly, nm) * flyCore * 0.55;
    }

    //═══ 5. 角落符文环（同心环 + 呼吸脉冲） ═══
    float2 rune[4] = {
        innerMin + float2(22.0, 22.0),
        float2(innerMax.x - 22.0, innerMin.y + 22.0),
        float2(innerMin.x + 22.0, innerMax.y - 22.0),
        innerMax - float2(22.0, 22.0)
    };
    float3 warmRune = float3(0.14, 0.24, 0.06);
    float3 coolRune = float3(0.05, 0.20, 0.32);

    for (int ri = 0; ri < 4; ri++) {
        float rDist = length(pixelPos - rune[ri]);
        float rPulse = sin(uTime * (0.65 + ri * 0.12) + ri * 1.57) * 0.3 + 0.7;

        float ring1 = exp(-pow(abs(rDist - 15.0), 2.0) * 0.25) * rPulse;
        float ring2 = exp(-pow(abs(rDist - 9.0), 2.0) * 0.40) * rPulse;
        float rCore = exp(-rDist * 0.16) * rPulse;
        float rGlow = exp(-rDist * 0.035) * rPulse;

        bg += lerp(warmRune, coolRune, nm) * (ring1 * 0.40 + ring2 * 0.28 + rCore * 0.60 + rGlow * 0.12);
    }

    //═══ 6. 有机浮雕边缘 ═══
    float bevelW = 10.0;
    float bevelMask = saturate(-panelSDF / bevelW);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;

    float2 lightDir = normalize(float2(0.6, -0.7));
    float2 edgeN = normalize(pixelPos - center);
    float bevelLight = dot(edgeN, lightDir) * 0.5 + 0.5;

    float3 warmHL = float3(0.10, 0.14, 0.04);
    float3 coolHL = float3(0.04, 0.12, 0.20);
    float3 warmSH = float3(0.006, 0.010, 0.003);
    float3 coolSH = float3(0.003, 0.007, 0.014);

    bg += lerp(lerp(warmSH, coolSH, nm), lerp(warmHL, coolHL, nm), bevelLight) * bevelMask * 0.80;

    float glint = bevelMask * pow(abs(bevelLight), 7.0);
    float3 warmGlint = float3(0.16, 0.22, 0.06);
    float3 coolGlint = float3(0.06, 0.18, 0.32);
    bg += lerp(warmGlint, coolGlint, nm) * glint * 0.30;

    float grooveDist = abs(-panelSDF - bevelW);
    float groove = exp(-grooveDist * grooveDist * 0.12) * 0.15;
    bg -= groove * float3(0.025, 0.030, 0.012);

    //═══ 7. 风拂扫描光带 ═══
    float swPhase = frac(uTime * 0.038);
    float swDist = uv.y - swPhase;
    if (swDist < -0.5) swDist += 1.0;
    if (swDist > 0.5) swDist -= 1.0;
    float swCore = exp(-abs(swDist) * 20.0);
    float swGlow = exp(-abs(swDist) * 5.5);

    float3 warmSweep = float3(0.07, 0.12, 0.03);
    float3 coolSweep = float3(0.03, 0.09, 0.16);
    bg += lerp(warmSweep, coolSweep, nm) * swCore * 0.55;
    bg += lerp(warmSweep, coolSweep, nm) * swGlow * 0.08;

    //风拂增强脉络可见度
    bg += lerp(warmVein, coolVein, nm) * veinPattern * swGlow * 0.14;

    //═══ 8. 斑驳树光（移动光斑） ═══
    float2 dappleUV = pixelPos * 0.009 + float2(uTime * 0.05, uTime * 0.03);
    float dapple = valueNoise(dappleUV);
    float dappleMask = smoothstep(0.42, 0.68, dapple);

    float3 warmDapple = float3(0.05, 0.07, 0.02);
    float3 coolDapple = float3(0.02, 0.05, 0.08);
    bg += lerp(warmDapple, coolDapple, nm) * dappleMask * 0.28;

    //═══ 9. 木板接缝 ═══
    float seamY = frac(pixelPos.y / 85.0);
    float seamDark = 1.0 - smoothstep(0.0, 0.015, seamY);
    bg -= float3(0.022, 0.028, 0.010) * seamDark * 0.42;
    float seamHL = smoothstep(0.015, 0.04, seamY) * (1.0 - smoothstep(0.04, 0.07, seamY));
    float3 warmSeamHL = float3(0.012, 0.016, 0.005);
    float3 coolSeamHL = float3(0.005, 0.013, 0.022);
    bg += lerp(warmSeamHL, coolSeamHL, nm) * seamHL * 0.32;

    //═══ 10. 暗角 ═══
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.42, 0.52), vig * float2(0.42, 0.52));
    bg *= saturate(1.0 - vigStr) * 0.35 + 0.65;

    //═══ 11. 顶部树冠光 ═══
    float topRef = 1.0 - smoothstep(0.0, 0.14, uv.y);
    float3 warmTopRef = float3(0.018, 0.028, 0.008);
    float3 coolTopRef = float3(0.008, 0.022, 0.038);
    bg += lerp(warmTopRef, coolTopRef, nm) * topRef * 0.45;

    //═══ 输出 ═══
    float fa = uAlpha * edgeAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass ForestPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
