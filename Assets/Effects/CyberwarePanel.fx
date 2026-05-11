// ============================================================================
// CyberwarePanel.fx —— 赛博义体管理面板专属背景着色器
// 主题：深红黑底 + 红色数据网格 + 中央人体能量光场 + CRT 行 + 内边柔光
// 输入参数：
//   uTime        累计时间
//   uAlpha       全局不透明度
//   uResolution  绘制矩形像素尺寸
//   uEdgePad     面板内缩边距
//   uBodyCenter  人体中心相对面板局部像素坐标（侧栏模式不使用）
//   uBodyRadius  人体能量光场半径，<=1 时退化为无中央光场
//   uMode        0=主面板（完整层）/ 1=侧栏（轻量层）
// 渲染方式：sb.Begin(Immediate, AlphaBlend, ..., effect)
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;
float2 uBodyCenter;
float uBodyRadius;
float uMode;

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
    float2 inSize = max(inMax - inMin, float2(1.0, 1.0));

    //内框SDF
    float sdf = min(min(px.x - inMin.x, inMax.x - px.x), min(px.y - inMin.y, inMax.y - px.y));
    if (sdf < -uEdgePad) return float4(0, 0, 0, 0);

    float2 uv = saturate((px - inMin) / inSize);
    bool isMain = uMode < 0.5;

    // ═══ 1. 深红黑底色，竖直渐变 ═══
    float3 col = lerp(float3(0.045, 0.012, 0.012), float3(0.022, 0.008, 0.012), uv.y);

    //fbm 雾化纹理（侧栏减弱）
    float n = fbm(uv * 3.2 + uTime * 0.06);
    col *= 0.85 + n * (isMain ? 0.30 : 0.18);
    col += float3(0.030, 0.008, 0.008) * (n - 0.5) * (isMain ? 1.0 : 0.5);

    // ═══ 2. 数据网格背景（细密点阵 + 中等方格） ═══
    if (isMain)
    {
        //细密点阵
        float2 dotUV = uv * float2(60.0, 36.0);
        float2 dotF = frac(dotUV);
        float dotMask = step(0.85, dotF.x) * step(0.85, dotF.y);
        col += float3(0.42, 0.10, 0.10) * dotMask * 0.20;

        //方格细线
        float2 grid = uv * float2(12.0, 7.0);
        float2 g = abs(frac(grid) - 0.5);
        float gridLine = step(0.46, max(g.x, g.y)) - step(0.49, max(g.x, g.y));
        col += float3(0.50, 0.12, 0.12) * gridLine * 0.22;

        //随机数据单元高亮（终端字符闪动感）
        float2 cellUV = uv * float2(36.0, 22.0);
        float2 cellId = floor(cellUV);
        float h = hash21(cellId + floor(uTime * 4.0));
        float cellLit = step(0.985, h);
        float2 cellF = frac(cellUV);
        float cellShape = step(0.18, cellF.x) * step(cellF.x, 0.82)
            * step(0.18, cellF.y) * step(cellF.y, 0.82);
        col += float3(1.00, 0.30, 0.30) * cellLit * cellShape * 0.55;
    }
    else
    {
        //侧栏：仅极淡的横向数据线，避免与列表项干扰
        float subtleGrid = abs(frac(uv.y * 14.0) - 0.5);
        float ln = step(0.46, subtleGrid) - step(0.49, subtleGrid);
        col += float3(0.30, 0.06, 0.06) * ln * 0.20;
    }

    // ═══ 3. 横向扫描带（缓慢自下向上扫过，仅主面板） ═══
    if (isMain)
    {
        float sweep = frac(uTime * 0.20 - uv.y * 1.1);
        float swG = exp(-abs(sweep - 0.5) * 16.0);
        col += float3(0.70, 0.18, 0.18) * swG * 0.55;
    }

    //细横扫描线（CRT风格，偶数行轻微变暗）
    float scan = frac(px.y * 0.5);
    col *= 0.92 + 0.08 * smoothstep(0.0, 0.30, scan) * smoothstep(1.0, 0.70, scan);

    // ═══ 4. 中央人体能量光场 ═══
    if (uBodyRadius > 1.0)
    {
        float2 bodyDelta = px - uBodyCenter;
        float bodyDist = length(bodyDelta);
        float gNorm = bodyDist / uBodyRadius;

        //柔和椭圆 halo（红+少量金）
        float halo = exp(-gNorm * gNorm * 1.6);
        col += float3(0.95, 0.25, 0.15) * halo * 0.32;
        col += float3(0.45, 0.30, 0.06) * halo * 0.10;

        //内圈较亮一层
        float inner = exp(-gNorm * gNorm * 5.0);
        col += float3(1.00, 0.30, 0.18) * inner * 0.22;

        //极坐标扫描指针（缓慢医疗扫描扇区）
        float ang = atan2(bodyDelta.y, bodyDelta.x);
        float scanA = frac(ang / 6.2832 + uTime * 0.15);
        float pointer = exp(-abs(scanA - 0.5) * 22.0);
        col += float3(1.00, 0.45, 0.30) * pointer * halo * 0.45;

        //同心定位环 0.55r、0.85r
        float ring1 = exp(-pow(abs(bodyDist - uBodyRadius * 0.55) / 1.4, 2.0));
        float ring2 = exp(-pow(abs(bodyDist - uBodyRadius * 0.85) / 1.4, 2.0));
        col += float3(0.80, 0.20, 0.20) * (ring1 + ring2) * 0.45;

        //径向数据射线
        float rays = 0.5 + 0.5 * sin(ang * 18.0 + uTime * 0.4);
        col += float3(0.55, 0.12, 0.12) * pow(rays, 12.0) * halo * 0.35;
    }

    // ═══ 5. 顶部色带高光 ═══
    col += float3(0.60, 0.15, 0.15) * (1.0 - smoothstep(0.0, 0.06, uv.y)) * (isMain ? 0.65 : 0.40);

    // ═══ 6. 边缘暗角 ═══
    float vig = saturate(sdf / (uEdgePad + 28.0));
    col *= 0.62 + 0.38 * vig;

    // ═══ 7. 内边线柔光 ═══
    float frameInner = smoothstep(uEdgePad + 6.0, uEdgePad + 4.0, sdf)
                     * smoothstep(uEdgePad + 2.0, uEdgePad + 4.0, sdf);
    col += float3(1.00, 0.28, 0.28) * frameInner * 0.55;

    //外边框柔光（向外扩散一小段）
    float frameGlow = smoothstep(uEdgePad + 12.0, 0.0, sdf);
    col += float3(0.60, 0.16, 0.16) * frameGlow * 0.30;

    float fa = uAlpha;
    return float4(col * fa, fa) * vertexColor;
}

technique Technique1
{
    pass CyberwarePanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
