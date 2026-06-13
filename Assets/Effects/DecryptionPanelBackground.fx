// ============================================================================
// DecryptionPanelBackground.fx 信号塔解密面板背景
// AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;
float uOpenProgress;//0~1，面板展开进度（影响能量注入强度）
float uPhase;      //0=加密锁定 1=解密中 2=解密完成（配色偏移）

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

//六边形距离场：返回 (到六边形边的距离, 格子ID)
float2 hexDist(float2 p, float cellSize) {
    float2 s = float2(1.0, 1.7320508);//sqrt(3)
    float2 h = s * 0.5 * cellSize;
    float2 a = fmod(p, s * cellSize) - h;
    float2 b = fmod(p + h, s * cellSize) - h;
    float2 gv = dot(a, a) < dot(b, b) ? a : b;
    //到六边形边的距离
    float d = max(
        abs(gv.x) * 0.8660254 + abs(gv.y) * 0.5,
        abs(gv.y)
    );
    //近似格子ID
    float2 id = p - gv;
    return float2(cellSize * 0.5 - d, id.x * 0.13 + id.y * 0.31);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //圆角矩形SDF（略带切角感）
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 6.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    if (panelSDF > uEdgePad + 2.0) return float4(0, 0, 0, 0);
    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (edgeAlpha < 0.01) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);

    //═══ 阶段配色（相位插值） ═══
    //阶段0：锈红/深棕 + 冷青辅助 + 警戒红
    //阶段1：橙黄 + 亮青 + 洋红
    //阶段2：祖母绿 + 亮金 + 冷青
    float3 baseColP0 = float3(0.058, 0.032, 0.020);
    float3 baseColP1 = float3(0.070, 0.045, 0.020);
    float3 baseColP2 = float3(0.024, 0.048, 0.034);
    float3 baseCol = lerp(
        lerp(baseColP0, baseColP1, saturate(uPhase)),
        baseColP2, saturate(uPhase - 1.0));

    float3 accent = lerp(
        lerp(float3(0.85, 0.30, 0.22), float3(1.00, 0.55, 0.10), saturate(uPhase)),
        float3(0.35, 1.00, 0.55), saturate(uPhase - 1.0));
    float3 cyber = lerp(
        lerp(float3(0.18, 0.55, 0.85), float3(0.35, 0.95, 1.00), saturate(uPhase)),
        float3(0.65, 1.00, 0.90), saturate(uPhase - 1.0));
    float3 neonHL = lerp(
        lerp(float3(0.95, 0.18, 0.55), float3(1.00, 0.35, 0.75), saturate(uPhase)),
        float3(0.70, 0.90, 1.00), saturate(uPhase - 1.0));

    //═══ 1. 拉丝金属基底（水平拉丝+大块脏污） ═══
    float brushH = valueNoise(pixelPos * float2(0.08, 0.35));
    float brushL = valueNoise(pixelPos * 0.02 + 311.0);
    float dirt = valueNoise(pixelPos * 0.011 - 73.0);
    float3 bg = baseCol * (0.55 + brushH * 0.55 + brushL * 0.35);
    bg *= 0.72 + dirt * 0.45;

    //锈斑：细粒度噪声相乘得到明暗斑点（克制使用）
    float rustSpot = valueNoise(pixelPos * 0.22) * valueNoise(pixelPos * 0.55 + 99.0);
    rustSpot = pow(saturate(rustSpot), 2.0);
    bg += accent * rustSpot * 0.09;

    //═══ 2. 金属板接缝（上下两道水平厚接缝 + 铆钉） ═══
    float seamDist = min(abs(uv.y - 0.18), abs(uv.y - 0.82)) * innerSize.y;
    float seam = 1.0 - smoothstep(0.0, 2.0, seamDist);
    bg -= baseCol * seam * 2.0;//接缝压暗
    bg += accent * seam * 0.15;//锈迹渗出

    //铆钉：沿接缝按固定间距排布
    float rivetPeriod = 42.0;
    float rivetX = frac((pixelPos.x - innerMin.x) / rivetPeriod) - 0.5;
    float rivetY18 = abs(pixelPos.y - (innerMin.y + innerSize.y * 0.18));
    float rivetY82 = abs(pixelPos.y - (innerMin.y + innerSize.y * 0.82));
    float rivetYmin = min(rivetY18, rivetY82);
    float rivetR = length(float2(rivetX * rivetPeriod, rivetYmin));
    float rivet = smoothstep(3.5, 2.5, rivetR);
    float rivetShine = smoothstep(2.2, 0.8, rivetR);
    bg -= float3(0.02, 0.012, 0.008) * rivet;//铆钉暗底
    bg += float3(0.20, 0.16, 0.11) * rivetShine;//铆钉亮面

    //═══ 3. 六边形能量栅格（冷青，仅作底纹微提示） ═══
    float2 hexPos = (pixelPos - center) + float2(0.0, uTime * 4.0);
    float2 hexR = hexDist(hexPos, 26.0);
    //仅渲染细线轮廓
    float hexEdge = smoothstep(0.9, 0.1, hexR.x);
    //格子节奏脉冲减弱
    float hexPhase = frac(uTime * 0.22 + hash11(hexR.y * 13.0));
    float hexPulse = smoothstep(0.0, 0.15, hexPhase) * smoothstep(0.45, 0.15, hexPhase);
    bg += cyber * hexEdge * (0.035 + 0.10 * hexPulse) * uOpenProgress;

    //═══ 5. 极淡水平CRT扫描线 ═══
    float scanY = frac(pixelPos.y * 0.3333);
    bg *= 0.96 + 0.04 * smoothstep(0.0, 0.25, scanY) * smoothstep(1.0, 0.75, scanY);

    //═══ 6. 单道缓慢全息扫描带（克制） ═══
    float swPhase = frac(uTime * 0.06);
    float swDist = uv.y - swPhase;
    if (swDist < -0.5) swDist += 1.0;
    if (swDist > 0.5) swDist -= 1.0;
    float swCore = exp(-abs(swDist) * 28.0);
    float swGlow = exp(-abs(swDist) * 6.0);
    bg += cyber * swCore * 0.22;
    bg += cyber * swGlow * 0.035;

    //仅保留边缘带遮罩供警戒斜纹使用
    float edgeBand = 1.0 - smoothstep(0.0, 14.0, -panelSDF);

    //═══ 9. 浮雕斜面 + 警戒斜纹 ═══
    float bevelW = 14.0;
    float bevelMask = saturate(-panelSDF / bevelW);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;
    float2 lightDir = normalize(float2(0.6, -0.8));
    float2 edgeN = normalize(pixelPos - center);
    float bevelLight = dot(edgeN, lightDir) * 0.5 + 0.5;
    float3 bevelHL = lerp(float3(0.004, 0.002, 0.001), accent * 0.45, bevelLight);
    bg += bevelHL * bevelMask * 0.9;
    float glint = bevelMask * pow(abs(bevelLight), 9.0);
    bg += accent * glint * 0.35;

    //警戒斜纹：仅阶段0附着在边缘，提示"锁定中"
    if (uPhase < 0.01) {
        float stripe = frac((pixelPos.x + pixelPos.y) * 0.07 - uTime * 0.4);
        float stripeMask = step(0.5, stripe) * edgeBand;
        bg += accent * stripeMask * 0.22;
    }

    //═══ 10. 暗角 ═══
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.42, 0.55), vig * float2(0.42, 0.55));
    bg *= saturate(1.0 - vigStr) * 0.45 + 0.55;

    //═══ 输出 ═══
    float fa = uAlpha * edgeAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass DecryptionPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
