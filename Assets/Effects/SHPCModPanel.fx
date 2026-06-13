// ============================================================================
// SHPCModPanel.fx SHPC枪体改造面板背景
// AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //绘制矩形像素尺寸
float uEdgePad;      //面板内缩边距
float2 uGunCenter;   //枪体中心局部像素坐标
float uGunRadius;    //枪体光场作用半径

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

    // ═══ 1. 深青蓝底色，竖直渐变 ═══
    float3 col = lerp(float3(0.012, 0.040, 0.062), float3(0.005, 0.020, 0.034), uv.y);

    //fbm 大尺度雾化纹理
    float n = fbm(uv * 3.2 + uTime * 0.06);
    col *= 0.85 + n * 0.30;
    col += float3(0.010, 0.030, 0.045) * (n - 0.5);

    // ═══ 2. 数据网格背景（细密点阵 + 中等方格 + 高亮单元） ═══
    //细密点阵
    float2 dotUV = uv * float2(60.0, 36.0);
    float2 dotF = frac(dotUV);
    float dotMask = step(0.85, dotF.x) * step(0.85, dotF.y);
    col += float3(0.10, 0.40, 0.55) * dotMask * 0.18;

    //方格细线
    float2 grid = uv * float2(12.0, 7.0);
    float2 g = abs(frac(grid) - 0.5);
    float gridLine = step(0.46, max(g.x, g.y)) - step(0.49, max(g.x, g.y));
    col += float3(0.10, 0.45, 0.60) * gridLine * 0.20;

    //随机数据单元高亮（每秒切几次）
    float2 cellUV = uv * float2(36.0, 22.0);
    float2 cellId = floor(cellUV);
    float h = hash21(cellId + floor(uTime * 4.0));
    float cellLit = step(0.985, h);
    float2 cellF = frac(cellUV);
    float cellShape = step(0.18, cellF.x) * step(cellF.x, 0.82)
        * step(0.18, cellF.y) * step(cellF.y, 0.82);
    col += float3(0.30, 0.95, 1.10) * cellLit * cellShape * 0.55;

    // ═══ 3. 横向扫描带（缓慢自下向上扫过） ═══
    float sweep = frac(uTime * 0.20 - uv.y * 1.1);
    float swG = exp(-abs(sweep - 0.5) * 16.0);
    col += float3(0.10, 0.45, 0.60) * swG * 0.55;

    //细横扫描线（CRT风格，偶数行轻微变暗）
    float scan = frac(px.y * 0.5);
    col *= 0.92 + 0.08 * smoothstep(0.0, 0.30, scan) * smoothstep(1.0, 0.70, scan);

    // ═══ 4. 中央枪体能量光场 ═══
    float2 gunDelta = px - uGunCenter;
    float gunDist = length(gunDelta);
    if (uGunRadius > 1.0)
    {
        float gNorm = gunDist / uGunRadius;
        //柔和椭圆光晕
        float halo = exp(-gNorm * gNorm * 1.8);
        col += float3(0.20, 0.70, 0.95) * halo * 0.35;
        //内圈较亮一层
        float inner = exp(-gNorm * gNorm * 5.0);
        col += float3(0.30, 0.90, 1.10) * inner * 0.25;

        //极坐标扫描指针（只在halo强度内显示）
        float ang = atan2(gunDelta.y, gunDelta.x);
        float scanA = frac(ang / 6.2832 + uTime * 0.18);
        float pointer = exp(-abs(scanA - 0.5) * 22.0);
        col += float3(0.40, 1.00, 1.20) * pointer * halo * 0.40;

        //同心定位环
        float ring1 = exp(-pow(abs(gunDist - uGunRadius * 0.55) / 1.4, 2.0));
        float ring2 = exp(-pow(abs(gunDist - uGunRadius * 0.85) / 1.4, 2.0));
        col += float3(0.20, 0.65, 0.85) * (ring1 + ring2) * 0.45;

        //径向数据射线
        float rays = 0.5 + 0.5 * sin(ang * 18.0 + uTime * 0.4);
        col += float3(0.10, 0.40, 0.55) * pow(rays, 12.0) * halo * 0.35;
    }

    // ═══ 5. 顶部色带高光 ═══
    col += float3(0.12, 0.45, 0.60) * (1.0 - smoothstep(0.0, 0.06, uv.y)) * 0.7;

    // ═══ 6. 边缘暗角 ═══
    float vig = saturate(sdf / (uEdgePad + 28.0));
    col *= 0.62 + 0.38 * vig;

    // ═══ 7. 内边线柔光 ═══
    float frameInner = smoothstep(uEdgePad + 6.0, uEdgePad + 4.0, sdf)
                     * smoothstep(uEdgePad + 2.0, uEdgePad + 4.0, sdf);
    col += float3(0.40, 0.95, 1.10) * frameInner * 0.55;

    //外边框柔光（向外扩散一小段）
    float frameGlow = smoothstep(uEdgePad + 12.0, 0.0, sdf);
    col += float3(0.15, 0.55, 0.75) * frameGlow * 0.30;

    float fa = uAlpha;
    return float4(col * fa, fa) * vertexColor;
}

technique Technique1
{
    pass SHPCModPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
