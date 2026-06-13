// ============================================================================
// AbandonedPortalPanel.fx 废墟传送门控制台面板
// AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;   //边缘扩展像素，面板外发光
float uRepair;    //修复进度 0~1
float uState;     //0损毁 1修复中 2已修复
float uGlitch;    //故障强度 0~1

// ─── 噪声 ───
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
    v += valueNoise(p * 4.07 + 43.0) * 0.125;
    v += valueNoise(p * 8.11 + 79.0) * 0.0625;
    return v / 0.9375;
}

// 单一锐利电弧线：基于FBM畸变的水平光带
float arcLine(float2 p, float baseY, float seed, float t) {
    float warp = fbm2(p * 0.012 + float2(t * 0.3, seed)) - 0.5;
    float dy = abs(p.y - baseY - warp * 28.0);
    return exp(-dy * 0.20);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;

    // ═══ 圆角矩形SDF（带切角倒角） ═══
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 5.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    if (panelSDF > uEdgePad + 2.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.5, panelSDF);
    if (edgeAlpha < 0.005) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);

    // ═══ 状态主色：损毁=橙红警告 / 修复中=蓝橙混色 / 已修复=青蓝稳定 ═══
    float3 brokenAccent  = float3(0.95, 0.32, 0.10);   // 警示橙红
    float3 repairAccent  = float3(0.45, 0.85, 0.95);   // 启动青蓝
    float3 stableAccent  = float3(0.55, 0.90, 1.00);   // 稳定青白

    float3 accent = lerp(brokenAccent, repairAccent, saturate(uRepair));
    accent = lerp(accent, stableAccent, step(1.5, uState));

    // ═══ 1. 锈蚀金属底色（深色泛橄榄绿+棕色） ═══
    float3 baseTop = float3(0.052, 0.044, 0.038);
    float3 baseMid = float3(0.034, 0.030, 0.026);
    float3 baseBot = float3(0.020, 0.017, 0.014);

    float3 bg = uv.y < 0.5
        ? lerp(baseTop, baseMid, uv.y * 2.0)
        : lerp(baseMid, baseBot, (uv.y - 0.5) * 2.0);

    // ═══ 2. 拉丝金属 + 锈斑斑驳纹理 ═══
    float brushH = valueNoise(pixelPos * float2(0.06, 0.20));
    float brushV = valueNoise(pixelPos * float2(0.30, 0.04) + 50.0);
    float brushed = brushH * 0.55 + brushV * 0.45;
    bg *= 0.78 + brushed * 0.42;

    // 大尺度锈斑：低频FBM，偏暖色
    float rust = fbm2(pixelPos * 0.018 + float2(110.0, 220.0));
    rust = smoothstep(0.45, 0.95, rust);
    float3 rustColor = float3(0.085, 0.038, 0.018);
    bg += rustColor * rust * 0.55;

    // 微小颗粒/腐蚀颗粒
    float grain = valueNoise(pixelPos * 0.55) - 0.5;
    bg += grain * 0.025;

    // ═══ 3. 撕裂裂缝（损毁残留） ═══
    // 基于FBM变形的少量大裂纹
    float crackVal = fbm2(pixelPos * 0.03 + 12.7);
    float crack = abs(crackVal - 0.5);
    float crackLine = 1.0 - smoothstep(0.0, 0.018, crack);
    float crackBrokenWeight = 1.0 - saturate(uRepair * 0.8); // 修复后裂痕变淡
    bg -= float3(0.05, 0.03, 0.02) * crackLine * (0.6 + crackBrokenWeight * 0.4);

    // 裂缝中渗出的微弱能量光（修复时增强）
    float crackGlowMask = 1.0 - smoothstep(0.0, 0.04, crack);
    bg += accent * crackGlowMask * (0.05 + uRepair * 0.20);

    // ═══ 4. 网格底图（淡薄蓝图层） ═══
    float gridSize = 36.0;
    float gx = abs(frac(pixelPos.x / gridSize) - 0.5) * 2.0;
    float gy = abs(frac(pixelPos.y / gridSize) - 0.5) * 2.0;
    float gridLineX = 1.0 - smoothstep(0.0, 0.025, 1.0 - gx);
    float gridLineY = 1.0 - smoothstep(0.0, 0.025, 1.0 - gy);
    float gridLine = max(gridLineX, gridLineY);

    float subSize = 9.0;
    float sgx = abs(frac(pixelPos.x / subSize) - 0.5) * 2.0;
    float sgy = abs(frac(pixelPos.y / subSize) - 0.5) * 2.0;
    float subLine = max(
        1.0 - smoothstep(0.0, 0.05, 1.0 - sgx),
        1.0 - smoothstep(0.0, 0.05, 1.0 - sgy)
    );

    float3 gridDim = float3(0.04, 0.06, 0.08);
    bg += gridDim * gridLine * 0.18;
    bg += gridDim * subLine * 0.05;
    // 网格在裂缝处缺失（被腐蚀）
    bg -= gridDim * gridLine * crackGlowMask * 0.6;

    // ═══ 5. 修复进度电路脉冲（多条流动光带） ═══
    float circuitAccum = 0.0;
    [unroll]
    for (int ci = 0; ci < 3; ci++) {
        float lineSeed = float(ci) * 7.13;
        float lineY = innerMin.y + innerSize.y * (0.18 + ci * 0.30);
        float arc = arcLine(pixelPos, lineY, lineSeed, uTime);

        // 脉冲点沿x方向移动
        float flow = frac(pixelPos.x / innerSize.x * 0.6 + lineSeed * 0.3 - uTime * (0.18 + ci * 0.04));
        float pulseSpot = exp(-pow(abs(flow - 0.5) * 6.0, 2.0));
        circuitAccum += arc * (0.20 + pulseSpot * 0.85);
    }
    // 仅当修复推进时电路点亮
    float circuitWeight = smoothstep(0.0, 0.05, uRepair) * 0.9 + 0.05;
    bg += accent * circuitAccum * circuitWeight * 0.55;

    // 修复进度填充条：底部从左到右一条流动能量带
    float repairBarY = uv.y;
    float barBand = smoothstep(0.92, 0.965, repairBarY) * (1.0 - smoothstep(0.985, 1.0, repairBarY));
    float barFill = step(uv.x, uRepair);
    bg += accent * barBand * barFill * 0.55;
    bg += accent * barBand * (1.0 - barFill) * 0.05;

    // ═══ 6. 故障CRT扫描线 + 横向撕裂位移 ═══
    // 慢速垂直扫描
    float scanCRT = sin(pixelPos.y * 1.65) * 0.5 + 0.5;
    bg *= 0.90 + 0.10 * scanCRT;

    // 故障横条带：根据uGlitch随机出现的撕裂条
    float glitchBand = step(0.96, hash11(floor(pixelPos.y / 6.0) + floor(uTime * 9.0)));
    float glitchAmp = glitchBand * uGlitch * 0.7;
    bg += accent * glitchAmp * 0.18;

    // ═══ 7. 全息扫掠（缓慢从上往下扫一束亮带） ═══
    float swPhase = frac(uTime * 0.06);
    float swDist = uv.y - swPhase;
    if (swDist < -0.5) swDist += 1.0;
    if (swDist > 0.5)  swDist -= 1.0;
    float swCore = exp(-abs(swDist) * 32.0);
    float swGlow = exp(-abs(swDist) * 9.0);
    bg += accent * swCore * 0.35;
    bg += accent * swGlow * 0.06;
    bg += gridDim * gridLine * swGlow * 0.18;

    // ═══ 8. 浮雕斜面边缘（深刻金属感） ═══
    float bevelW = 12.0;
    float bevelMask = saturate(-panelSDF / bevelW);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;

    float2 lightDir = normalize(float2(0.65, -0.65));
    float2 edgeN = normalize(pixelPos - center + float2(0.0001, 0.0001));
    float bevelLight = dot(edgeN, lightDir) * 0.5 + 0.5;

    float3 hl = float3(0.18, 0.13, 0.08);
    float3 sh = float3(0.008, 0.006, 0.004);
    bg += lerp(sh, hl, bevelLight) * bevelMask * 0.85;

    float glint = bevelMask * pow(abs(bevelLight), 8.0);
    bg += float3(0.30, 0.22, 0.14) * glint * 0.30;

    // 内凹槽
    float grooveDist = abs(-panelSDF - bevelW);
    float groove = exp(-grooveDist * grooveDist * 0.12) * 0.18;
    bg -= groove * float3(0.04, 0.025, 0.018);

    // ═══ 9. 危险条纹边框（顶部警示斜条） ═══
    float topBandMask = (1.0 - smoothstep(0.0, 0.035, uv.y));
    float stripeT = sin((pixelPos.x - pixelPos.y - uTime * 26.0) * 0.4) * 0.5 + 0.5;
    float stripe = step(0.5, stripeT);
    float3 hazardA = float3(0.32, 0.20, 0.04); // 警黄
    float3 hazardB = float3(0.10, 0.06, 0.02); // 黑
    // 修复完成后转为青色边框
    float3 hazardCalm = accent * 0.18;
    float3 hazardFinal = lerp(lerp(hazardB, hazardA, stripe), hazardCalm, saturate(uState - 1.0));
    bg = lerp(bg, hazardFinal, topBandMask * 0.85);

    // ═══ 10. 角落警示灯（呼吸） ═══
    float2 lamp[4] = {
        innerMin + float2(20.0, 20.0),
        float2(innerMax.x - 20.0, innerMin.y + 20.0),
        float2(innerMin.x + 20.0, innerMax.y - 20.0),
        innerMax - float2(20.0, 20.0)
    };
    [unroll]
    for (int li = 0; li < 4; li++) {
        float lDist = length(pixelPos - lamp[li]);
        float lPulse = sin(uTime * (0.9 + li * 0.18) + li * 1.57) * 0.35 + 0.65;
        // 损毁时只有部分灯闪烁
        float aliveMask = step(uRepair * 4.0 - 0.5, float(li));
        // 当uState=0且uRepair=0时，aliveMask始终为1; 修复推进时灯逐个点亮
        float lAlive = lerp(1.0 - aliveMask, 1.0, saturate(uRepair));
        float lCore = exp(-lDist * 0.22) * lPulse;
        float lGlow = exp(-lDist * 0.045) * lPulse;
        bg += accent * lCore * 0.55 * lAlive;
        bg += accent * lGlow * 0.10 * lAlive;
    }

    // ═══ 11. 暗角 ═══
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.45, 0.55), vig * float2(0.45, 0.55));
    bg *= saturate(1.0 - vigStr) * 0.35 + 0.65;

    // ═══ 12. 顶部反光（仅在修复完成后明显） ═══
    float topRef = (1.0 - smoothstep(0.0, 0.18, uv.y)) * (0.20 + saturate(uState - 1.0) * 0.5);
    bg += accent * topRef * 0.10;

    // ═══ 输出 ═══
    float fa = uAlpha * edgeAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass AbandonedPortalPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
