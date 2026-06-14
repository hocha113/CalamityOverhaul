// ============================================================================
//HackTimeScreen.fx 骇客时间屏幕后处理
//采样 uImage0 屏幕
// ============================================================================

sampler uImage0 : register(s0);

float intensity;
float uTime;
float vignetteStrength;
float tintStrength;
float2 uScreenSize; //屏幕像素尺寸，多 tap 采样

//================ 工具 ================

//近似sRGB线性化
float3 toLinear(float3 c) { return c * c; }
float3 toGamma(float3 c) { return sqrt(max(c, 0.0)); }

//简化ACES色调映射
float3 ACESFilm(float3 x) {
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

//哈希噪点
float hash12(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//软阈值亮度提取
float3 extractHighlights(float3 c, float threshold, float knee) {
    float lum = dot(c, float3(0.299, 0.587, 0.114));
    float soft = smoothstep(threshold - knee, threshold + knee, lum);
    return c * soft;
}

//================ 主通道 ================

float4 HackTimePass(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);
    if (intensity < 0.001)
        return original;

    float fx = intensity;
    float3 color = original.rgb;

    //========================================
    //1.边缘色差(只在外圈,中心保持锐利可读)
    //========================================
    float2 vc = coords - 0.5;
    float vDist2 = dot(vc, vc);
    float edgeMask = smoothstep(0.10, 0.42, vDist2);
    float caStrength = 0.0028 * fx * edgeMask;
    float2 caDir = normalize(vc + 0.0001) * caStrength;
    float r1 = tex2D(uImage0, coords + caDir * 1.2).r;
    float b1 = tex2D(uImage0, coords - caDir * 1.2).b;
    float g1 = tex2D(uImage0, coords + caDir * 0.2).g;
    color.r = lerp(color.r, r1, edgeMask * 0.75);
    color.g = lerp(color.g, g1, edgeMask * 0.30);
    color.b = lerp(color.b, b1, edgeMask * 0.75);

    //========================================
    //2.色调重映射(冷青双色分级,保留可读性)
    //========================================
    float3 lin = toLinear(color);
    float lum = dot(lin, float3(0.299, 0.587, 0.114));

    //暗部推向深青蓝,亮部推向冰青
    float3 shadow = float3(0.035, 0.080, 0.115);
    float3 mid    = float3(0.28,  0.55,  0.60);
    float3 hi     = float3(0.78,  0.98,  1.00);

    float3 graded;
    if (lum < 0.28) {
        graded = lerp(shadow, mid, saturate(lum / 0.28));
    } else {
        graded = lerp(mid, hi, saturate((lum - 0.28) / 0.72));
    }
    graded = graded * (lum + 0.10) * 1.05;

    float tintMix = 0.42 * fx * tintStrength;
    float3 toned = lerp(lin, graded, tintMix);

    //轻压饱和
    float tonedL = dot(toned, float3(0.299, 0.587, 0.114));
    toned = lerp(float3(tonedL, tonedL, tonedL), toned, 0.85);

    color = toned;

    //========================================
    //3.辉光(8方向柔光,全息质感,偏冷青)
    //========================================
    float2 bloomStep = 1.0 / max(uScreenSize, 1.0);
    float bloomRadius = 2.8;

    float3 hl = extractHighlights(color, 0.55, 0.22);

    float3 bloom = 0;
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2( 1.5,  0.0) * bloomStep * bloomRadius).rgb), 0.50, 0.25);
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2(-1.5,  0.0) * bloomStep * bloomRadius).rgb), 0.50, 0.25);
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2( 0.0,  1.5) * bloomStep * bloomRadius).rgb), 0.50, 0.25);
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2( 0.0, -1.5) * bloomStep * bloomRadius).rgb), 0.50, 0.25);
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2( 1.0,  1.0) * bloomStep * bloomRadius * 1.6).rgb), 0.55, 0.25);
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2(-1.0,  1.0) * bloomStep * bloomRadius * 1.6).rgb), 0.55, 0.25);
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2( 1.0, -1.0) * bloomStep * bloomRadius * 1.6).rgb), 0.55, 0.25);
    bloom += extractHighlights(toLinear(tex2D(uImage0, coords + float2(-1.0, -1.0) * bloomStep * bloomRadius * 1.6).rgb), 0.55, 0.25);
    bloom /= 8.0;

    float3 bloomTint = float3(0.35, 0.92, 1.00);
    bloom = bloom * bloomTint;
    color += bloom * fx * 0.42;
    color += hl * bloomTint * fx * 0.08;

    //========================================
    //4.ACES映射 + 回gamma空间
    //========================================
    float3 mapped = ACESFilm(color * 1.08);
    color = lerp(color, mapped, 0.75);
    color = toGamma(color);

    //========================================
    //5.数据薄雾(极弱,仅边缘微颗粒)
    //========================================
    {
        float n1 = hash12(floor(coords * uScreenSize * 0.5) + floor(uTime * 12.0));
        float n2 = hash12(floor(coords * uScreenSize * 0.18) + floor(uTime * 3.0));
        float dust = (n1 * 0.4 + n2 * 0.6 - 0.5);
        //缩小范围且压低幅度,让中心与中外区都保持干净
        float dustMask = smoothstep(0.18, 0.42, vDist2);
        color += float3(0.05, 0.12, 0.14) * dust * fx * 0.12 * dustMask;
    }

    //========================================
    //6.暗角(平滑,边缘带冷光勾边)
    //========================================
    float vignette = 1.0 - vDist2 * vignetteStrength * fx * 0.58;
    vignette = saturate(vignette);
    color *= lerp(1.0, vignette, 0.85);
    float edgeGlow = smoothstep(0.18, 0.52, vDist2);
    color += float3(0.02, 0.08, 0.10) * edgeGlow * fx * 0.65;

    //========================================
    //7.赛博边框HUD(多层级:外细框 + 四角L + 侧边数据带 + 跑马灯)
    //以纯几何方式构造,不含扫描线
    //========================================
    {
        float2 px = coords * uScreenSize;
        float2 rpx = uScreenSize - px;
        //距四边的最近像素距离
        float dLeft   = px.x;
        float dRight  = rpx.x;
        float dTop    = px.y;
        float dBottom = rpx.y;
        float dEdgeH = min(dLeft, dRight);
        float dEdgeV = min(dTop, dBottom);
        float dEdge = min(dEdgeH, dEdgeV);

        float breathe = 0.70 + 0.30 * sin(uTime * 1.3);
        float3 accent = float3(0.30, 0.95, 1.00);
        float3 accentDim = float3(0.12, 0.45, 0.55);

        //------- 7a.外层细框(距边22~24px) -------
        float outerLine = smoothstep(24.0, 23.2, dEdge) * smoothstep(21.2, 22.0, dEdge);
        color += accent * outerLine * fx * 0.55 * breathe;
        //外框外侧柔和辉光
        float outerGlow = exp(-max(dEdge - 22.0, 0.0) / 4.0) * smoothstep(32.0, 22.0, dEdge);
        color += accentDim * outerGlow * fx * 0.28;

        //------- 7b.内层虚线框(距边40px处) -------
        float innerBand = smoothstep(41.0, 40.2, dEdge) * smoothstep(38.6, 39.4, dEdge);
        //沿边方向切成虚线
        float stripePos;
        if (dLeft == dEdgeH) {
            stripePos = px.y;
        } else if (dRight == dEdgeH) {
            stripePos = px.y;
        } else if (dTop == dEdgeV) {
            stripePos = px.x;
        } else {
            stripePos = px.x;
        }
        //水平和垂直边各自用对应轴产生虚线
        float stripeH = frac((px.x + uTime * 20.0) / 14.0);
        float stripeV = frac((px.y + uTime * 20.0) / 14.0);
        //接近横边用stripeH,竖边用stripeV
        float stripeSel = step(dEdgeV, dEdgeH) * step(0.5, stripeH) + step(dEdgeH, dEdgeV) * step(0.5, stripeV);
        color += accent * innerBand * stripeSel * fx * 0.32;

        //------- 7c.四角L形锁定括号 -------
        float2 insetDc = float2(min(dLeft, dRight), min(dTop, dBottom)) - 16.0;
        float armThick = 1.6;
        float armLenH = 72.0;
        float armLenV = 98.0;
        float inX = step(0.0, insetDc.x);
        float inY = step(0.0, insetDc.y);
        float horizArm = step(insetDc.y, armThick) * step(insetDc.x, armLenH) * inX * inY;
        float vertArm  = step(insetDc.x, armThick) * step(insetDc.y, armLenV) * inX * inY;
        float lShape = max(horizArm, vertArm);
        color += accent * lShape * fx * 0.85 * breathe;
        //L末端点亮粗体(前几像素稍粗)
        float tipMask = step(insetDc.x, 5.0) * step(insetDc.y, 5.0) * inX * inY;
        color += accent * tipMask * fx * 0.7 * breathe;
        //L短距辉光
        float lGlow = exp(-max(min(insetDc.x, insetDc.y), 0.0) * 0.20)
                    * step(insetDc.x, armLenV) * step(insetDc.y, armLenV) * inX * inY;
        color += float3(0.05, 0.20, 0.26) * lGlow * fx * 0.30 * breathe;

        //------- 7d.顶部中央标题栏区块 -------
        //中心上方放一条宽200px、细条数据带
        {
            float centerDist = abs(px.x - uScreenSize.x * 0.5);
            //距顶端12~20px,宽度220px
            float topStripeBand = smoothstep(20.0, 19.2, dTop) * smoothstep(11.4, 12.2, dTop);
            float inCenter = step(centerDist, 110.0);
            //两端渐淡
            float fade = 1.0 - smoothstep(80.0, 110.0, centerDist);
            color += accent * topStripeBand * inCenter * fade * fx * 0.45;
            //带内流动数据光点
            float flow = frac((px.x - uTime * 50.0) * 0.04);
            float dotMask = step(0.88, flow);
            float inTopDot = smoothstep(18.0, 17.4, dTop) * smoothstep(13.0, 13.6, dTop);
            color += accent * dotMask * inTopDot * inCenter * fade * fx * 0.6;
        }

        //------- 7e.左下角运行状态柱 -------
        //左下距边40~42px处做一条竖向16格小柱
        {
            float cx = 42.0;
            float colX = abs(px.x - cx);
            float colInX = step(colX, 1.1);
            //距底100~260px范围,分格闪烁
            float yFromBottom = dBottom;
            float inCol = step(100.0, yFromBottom) * step(yFromBottom, 260.0);
            float cellY = floor((yFromBottom - 100.0) / 10.0);
            float cellOn = step(0.55, hash12(float2(cellY, floor(uTime * 4.0))));
            color += accent * colInX * inCol * cellOn * fx * 0.55;
        }

        //------- 7f.右上角运行时钟滴答点 -------
        {
            float2 clockCenter = float2(uScreenSize.x - 42.0, 42.0);
            float cd = length(px - clockCenter);
            //外环
            float ring = smoothstep(11.0, 10.3, cd) * smoothstep(9.0, 9.7, cd);
            color += accent * ring * fx * 0.6;
            //旋转指针
            float2 rel = px - clockCenter;
            float ang = atan2(rel.y, rel.x);
            float targetAng = uTime * 1.8;
            float angDiff = abs(atan2(sin(ang - targetAng), cos(ang - targetAng)));
            float handMask = smoothstep(0.18, 0.05, angDiff) * step(cd, 9.0) * step(2.0, cd);
            color += accent * handMask * fx * 0.55;
            //中心点
            color += accent * smoothstep(2.5, 1.5, cd) * fx * 0.7;
        }

        //------- 7g.底部中央进度条装饰 -------
        {
            float cxDist = abs(px.x - uScreenSize.x * 0.5);
            float inBottomBand = smoothstep(20.0, 19.2, dBottom) * smoothstep(11.4, 12.2, dBottom);
            float inW = step(cxDist, 160.0);
            float fadeB = 1.0 - smoothstep(130.0, 160.0, cxDist);
            //主条
            color += accentDim * inBottomBand * inW * fadeB * fx * 0.55;
            //扫描光(一个短亮段沿条移动)
            float sweep = frac(uTime * 0.35);
            float sweepX = (sweep - 0.5) * 320.0;
            float sweepDist = abs(px.x - (uScreenSize.x * 0.5 + sweepX));
            float sweepGlow = exp(-sweepDist / 14.0) * inBottomBand * inW * fadeB;
            color += accent * sweepGlow * fx * 0.9;
        }

        //------- 7h.上框中央微型标识(|||) -------
        {
            float cxDist = px.x - uScreenSize.x * 0.5;
            float inTop = smoothstep(29.0, 28.2, dTop) * smoothstep(24.8, 25.6, dTop);
            //三道短竖线,间距14px,居中
            float s = abs(frac(cxDist / 14.0 + 0.5) - 0.5);
            float inRange = step(abs(cxDist), 21.0);
            float dashes = step(s, 0.12) * inRange;
            color += accent * inTop * dashes * fx * 0.6;
        }
    }

    //========================================
    //8.对比度微提(保持读图)
    //========================================
    float3 pivot = float3(0.32, 0.36, 0.38);
    color = pivot + (color - pivot) * lerp(1.0, 1.15, fx * 0.45);

    //========================================
    //9.整体轻微压暗,强化沉浸但不刺眼
    //========================================
    color *= lerp(1.0, 0.82, fx * 0.65);

    //暗部提色,不让阴影变死黑
    float darkLift = smoothstep(0.18, 0.0, dot(color, float3(0.299, 0.587, 0.114)));
    color += float3(0.015, 0.035, 0.045) * darkLift * fx;

    color = saturate(color);
    return float4(color, original.a);
}

technique Technique1
{
    pass HackTimeScreenPass
    {
        PixelShader = compile ps_3_0 HackTimePass();
    }
}
