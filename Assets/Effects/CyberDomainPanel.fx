// ============================================================================
// CyberDomainPanel.fx 赛博空间领域控制面板背景
// AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //绘制矩形像素尺寸
float uEdgePad;      //面板内缩边距
float uLayer;        //当前领域层数 0..3 浮点过渡
float uIntensity;    //领域整体强度 0..1

float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * vnoise(p);
        p *= 2.05;
        a *= 0.5;
    }
    return v;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float2 inMin = float2(uEdgePad, uEdgePad);
    float2 inMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 inSize = inMax - inMin;

    //内框SDF
    float sdf = min(min(px.x - inMin.x, inMax.x - px.x), min(px.y - inMin.y, inMax.y - px.y));
    if (sdf < -uEdgePad) return float4(0, 0, 0, 0);

    float2 uv = saturate((px - inMin) / inSize);
    float2 uvc = uv - 0.5;
    uvc.x *= inSize.x / inSize.y;//保持横向比例下的极坐标
    float r = length(uvc);
    float a = atan2(uvc.y, uvc.x);

    //层数因子0..1
    float layerN = saturate(uLayer / 3.0);
    float instab = layerN * uIntensity;

    // ═══ A. 故障水平条带横向位移 ═══
    float gt = floor(uTime * (4.0 + layerN * 6.0));
    float bandY = hash11(gt * 13.37);
    float bandH = 0.005 + hash11(gt * 91.1) * 0.020 * (0.6 + instab);
    float inBand = step(0.55 - instab * 0.3, hash11(gt * 7.91))
        * smoothstep(0.0, 0.004, uv.y - (bandY - bandH))
        * smoothstep(0.0, 0.004, (bandY + bandH) - uv.y);
    float shift = (hash11(gt * 71.3) - 0.5) * (12.0 + instab * 28.0);
    px.x += shift * inBand;
    uv.x = saturate((px.x - inMin.x) / inSize.x);
    uvc.x = (uv.x - 0.5) * (inSize.x / inSize.y);
    r = length(uvc);
    a = atan2(uvc.y, uvc.x);

    // ═══ 1. 深红黑底 ═══
    float3 col = lerp(float3(0.060, 0.012, 0.018), float3(0.020, 0.004, 0.008), uv.y);

    //fbm 大尺度渍染
    float n = fbm(uv * 3.6 + uTime * 0.10);
    col *= 0.78 + n * 0.5;
    col += float3(0.020, 0.002, 0.004) * (n - 0.5);

    // ═══ 2. 黑墙故障环（多层同心环，随层数推进） ═══
    //每生效一层就出现一道故障环，最外环越靠边
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        float kf = (float)k;
        float ringTarget = 0.18 + kf * 0.13;
        //本层出现度0..1
        float layerAmt = saturate(uLayer - kf);
        if (layerAmt <= 0.001) continue;
        float wob = sin(a * (3.0 + kf * 2.0) + uTime * (0.6 + kf * 0.4)) * 0.012 * (0.4 + instab);
        wob += (vnoise(float2(a * 3.0 + uTime * 0.7, kf * 11.0)) - 0.5) * 0.020 * instab;
        float ringR = ringTarget + wob;
        float thick = 0.012 + 0.010 * layerAmt;
        float d = abs(r - ringR);
        float ringEdge = smoothstep(thick, thick * 0.30, d);
        float ringGlow = smoothstep(thick * 4.0, 0.0, d) * 0.55;
        float3 ringCol = lerp(float3(0.95, 0.18, 0.15), float3(1.0, 0.55, 0.30), kf / 2.0);
        col += ringCol * ringEdge * (0.85 * layerAmt);
        col += ringCol * ringGlow * (0.45 * layerAmt) * (0.6 + instab);

        //本环周向扫描热点
        float scanA = frac(a / 6.2832 + uTime * (0.10 + kf * 0.07) + kf * 0.33);
        float hot = exp(-abs(scanA - 0.5) * 18.0);
        col += ringCol * hot * smoothstep(thick * 2.0, 0.0, d) * 0.9 * layerAmt;
    }

    // ═══ 3. 中心黑墙圆盘（领域核心暗示） ═══
    float coreR = 0.10 + layerN * 0.025;
    float coreEdge = smoothstep(coreR + 0.012, coreR - 0.002, r);
    //核心黑色 + 红色脉冲心跳
    float pulse = 0.5 + 0.5 * sin(uTime * 3.0 + layerN * 1.4);
    col = lerp(col, float3(0.020, 0.002, 0.006), coreEdge * 0.85);
    col += float3(0.45, 0.05, 0.08) * smoothstep(coreR * 1.4, coreR, r)
         * (0.30 + 0.45 * pulse) * uIntensity;

    //核心放射射线
    float rays = 0.5 + 0.5 * sin(a * 24.0);
    col += float3(0.40, 0.06, 0.10) * pow(rays, 8.0)
         * smoothstep(0.45, 0.05, r) * 0.25 * uIntensity;

    // ═══ 4. 数据流方格颗粒 ═══
    float2 cellUV = uv * float2(48.0, 28.0);
    float2 cellId = floor(cellUV);
    float h = hash21(cellId + floor(uTime * 6.0));
    float cellLit = step(0.985 - 0.04 * instab, h);
    float2 cellF = frac(cellUV);
    float cellShape = step(0.15, cellF.x) * step(cellF.x, 0.85)
        * step(0.15, cellF.y) * step(cellF.y, 0.85);
    col += float3(1.0, 0.25, 0.18) * cellLit * cellShape * 0.7;

    // ═══ 5. 全屏RGB色散扫描线 ═══
    float scan = frac(px.y / 3.0);
    col *= 0.85 + 0.15 * smoothstep(0.0, 0.20, scan) * smoothstep(1.0, 0.80, scan);

    //横向扫描带
    float sweep = frac(uTime * 0.18 - uv.y);
    float swG = exp(-abs(sweep - 0.5) * 14.0);
    col += float3(0.20, 0.04, 0.06) * swG * 0.45;

    // ═══ 6. 故障块着色 ═══
    if (inBand > 0.01)
    {
        col.r += inBand * 0.45;
        col.gb += inBand * float2(0.05, 0.10);
        //RGB色散
        float disp = inBand * (0.06 + instab * 0.10);
        col.r += disp * 0.4;
        col.b -= disp * 0.2;
    }

    // ═══ 7. 边框暗角 ═══
    float vig = saturate(sdf / (uEdgePad + 22.0));
    col *= 0.55 + 0.45 * vig;

    // ═══ 8. 顶部高光带 ═══
    col += float3(0.10, 0.020, 0.025) * (1.0 - smoothstep(0.0, 0.10, uv.y)) * 0.6;

    //内边线
    float frameInner = smoothstep(uEdgePad + 6.0, uEdgePad + 4.0, sdf)
                     * smoothstep(uEdgePad + 2.0, uEdgePad + 4.0, sdf);
    col += float3(0.95, 0.20, 0.18) * frameInner * 0.6;

    //外边框柔光
    float frameGlow = smoothstep(uEdgePad + 10.0, 0.0, sdf);
    col += float3(0.55, 0.10, 0.10) * frameGlow * 0.35 * uIntensity;

    float fa = uAlpha;
    return float4(col * fa, fa) * vertexColor;
}

technique Technique1
{
    pass CyberDomainPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
