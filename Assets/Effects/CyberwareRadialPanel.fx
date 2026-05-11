// ============================================================================
// CyberwareRadialPanel.fx 义体技能雷达专属背板着色器
// 与 HackRamArc / SHPCCoreOrb 的"数据网格 / 能量核心"语言区分，
// 这里走"植入义体接口"风格：
//   - 整体偏冷蓝青带电路绿光，区别于 RAM HUD 的纯青
//   - 内圈中央是带旋转辐条的虹膜（光圈），表达"接口正在张开"
//   - 中圈是六边形铺底 + 数据扫描光带的环形背板（低 alpha，仅作底纹）
//   - 外圈是细碎的拨码刻度 + 缓慢旋转
//
// 设计原则：本着色器只渲染雷达的"框架与底纹"（背景铺底 / 中心虹膜 / 外圈
// 拨码刻度 / 内外缘描边）。扇区填充、悬停高亮、图标、状态文字、悬停信息面
// 板等动态绘制完整保留在 CPU 层（CyberwareSkillRadialUI.Draw 内）。
// 因此 quad 应在 CPU 扇区绘制之前先行 Draw，让 CPU 内容自然叠加在上方。
// ============================================================================
// 参数说明：
//   uResolution     绘制 quad 的像素尺寸
//   uCenter         雷达圆心在 quad 内的像素坐标
//   uInnerR         扇区内弧半径
//   uOuterR         扇区外弧半径
//   uDeadZoneR      中心死区半径（虹膜直径基准）
//   uDecoOuterR     外圈刻度装饰的最大半径
//   uTime           动画驱动时间（秒）
//   uAlpha          全局 alpha
//   uOpenProgress   展开进度（0~1），驱动入场动画
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uOpenProgress;
float2 uResolution;
float2 uCenter;
float uInnerR;
float uOuterR;
float uDeadZoneR;
float uDecoOuterR;

//================== 工具函数 ==================

//六边形 SDF
float hexDist(float2 p, float s) {
    p = abs(p);
    float c = dot(p, normalize(float2(1.0, 1.732)));
    c = max(c, p.x);
    return c - s;
}

//六边形网格：返回到所在六角中心的距离
float hexGrid(float2 p, float size) {
    float2 r = float2(1.0, 1.732);
    float2 h = r * 0.5;
    float2 a = fmod(p, r) - h;
    float2 b = fmod(p + h, r) - h;
    float2 gv = dot(a, a) < dot(b, b) ? a : b;
    return hexDist(gv, size * 0.5);
}

float wrapPi(float a) {
    a = fmod(a + 3.14159265, 6.28318530);
    if (a < 0) a += 6.28318530;
    return a - 3.14159265;
}

//================== 主像素着色 ==================

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vcol : COLOR0) : COLOR0
{
    float2 p = uv * uResolution;
    float2 d = p - uCenter;
    float r = length(d);
    float ang = atan2(d.y, d.x);

    //展开进度的整体淡入。早期阶段背板向中心收拢
    float expand = saturate(uOpenProgress);
    float ease = 1.0 - pow(1.0 - expand, 3.0);
    //半径乘以入场动画，让整圈从中心"绽放"
    float rNorm = r / max(uOuterR, 1.0);

    float3 outCol = float3(0.0, 0.0, 0.0);
    float outA = 0.0;

    //==================================================
    // 1) 中心虹膜：旋转辐条 + 同心薄环
    //==================================================
    if (r < uDeadZoneR * 1.15) {
        //半径归一化到死区
        float irisT = saturate(r / uDeadZoneR);
        //内部柔光背景
        float core = 1.0 - smoothstep(0.0, 1.0, irisT);
        float3 irisBg = float3(0.04, 0.18, 0.24) * core;
        outCol += irisBg;
        outA = max(outA, core * 0.55);

        //旋转辐条：6 根，缓慢自转
        float spokeAng = ang - uTime * 0.45;
        float spokes = abs(sin(spokeAng * 3.0));
        float spokeMask = smoothstep(0.85, 0.99, spokes) * core;
        outCol += float3(0.20, 0.85, 0.95) * spokeMask * 0.85;
        outA = max(outA, spokeMask * 0.85);

        //中心微脉冲点
        float dotR = 3.0 + sin(uTime * 2.6) * 0.6;
        float dotMask = 1.0 - smoothstep(dotR - 1.0, dotR + 1.5, r);
        outCol += float3(0.55, 1.0, 1.0) * dotMask * 0.95;
        outA = max(outA, dotMask);

        //死区边缘的薄环
        float edgeRing = 1.0 - smoothstep(0.4, 1.2, abs(r - uDeadZoneR));
        outCol += float3(0.18, 0.65, 0.78) * edgeRing * 0.55;
        outA = max(outA, edgeRing * 0.55);
    }

    //==================================================
    // 2) 扇区背板：内弧 ~ 外弧之间的环带背景
    //==================================================
    float ringMid = (uInnerR + uOuterR) * 0.5;
    float ringHalf = (uOuterR - uInnerR) * 0.5;
    float ringSDF = abs(r - ringMid) - ringHalf;
    float ringAA = 1.0 - smoothstep(0.0, 1.5, ringSDF);

    if (ringAA > 0.002) {
        //六边形底纹：低饱和、低 alpha，仅作"接口板底"的细节
        //CPU 扇区会以半透明覆盖在上方，所以这里保持非常克制
        float hexD = hexGrid(p * 0.55 + float2(uTime * 0.6, -uTime * 0.3), 5.0);
        float hexLine = smoothstep(-0.5, -1.2, hexD);
        float3 hexCol = float3(0.035, 0.085, 0.11) + float3(0.03, 0.13, 0.16) * hexLine;

        //缓慢旋转的扫描光带（保留，但减弱）
        float scanT = frac(uTime * 0.18);
        float scanAng = wrapPi(ang - scanT * 6.2831);
        float scanFall = exp(-pow(scanAng / 0.55, 2.0));
        hexCol += float3(0.08, 0.45, 0.52) * scanFall * 0.45;

        outCol = lerp(outCol, hexCol, ringAA);
        //底纹 alpha 故意压低，让 CPU 扇区清晰可读
        outA = max(outA, ringAA * 0.35);
    }

    //==================================================
    // 3) 内弧描边（"接口口径"）
    //==================================================
    {
        float innerEdge = 1.0 - smoothstep(0.4, 1.4, abs(r - uInnerR));
        float3 innerEdgeCol = float3(0.18, 0.78, 0.92);
        outCol += innerEdgeCol * innerEdge * 0.7;
        outA = max(outA, innerEdge * 0.7);
    }

    //==================================================
    // 4) 外弧描边 + 旋转拨码刻度
    //==================================================
    {
        float outerEdge = 1.0 - smoothstep(0.4, 1.4, abs(r - uOuterR));
        float3 outerEdgeCol = float3(0.16, 0.68, 0.82);
        outCol += outerEdgeCol * outerEdge * 0.75;
        outA = max(outA, outerEdge * 0.75);

        //更外圈的拨码刻度环
        if (r > uOuterR && r < uDecoOuterR + 2.0) {
            //拨码：12 个长刻度 + 12 个短刻度交替，整体缓慢旋转
            float tickAng = wrapPi(ang - uTime * 0.08);
            float tickPhase = tickAng / 6.28318530 * 24.0;
            float tickLocal = frac(tickPhase);
            float tickPick = step(0.85, tickLocal) + step(tickLocal, 0.15);
            //长刻度用偶数位
            float longTick = step(0.5, fmod(floor(tickPhase), 2.0));
            float tickInner = uOuterR + 2.0;
            float tickLen = lerp(3.0, 7.0, longTick);
            float tickOuter = tickInner + tickLen;
            float inTickBand = step(tickInner, r) * step(r, tickOuter);
            float tickWidth = smoothstep(0.018, 0.005,
                abs(tickLocal - round(tickLocal)) * 6.28318530 / 24.0);
            float tickVis = tickPick * inTickBand * tickWidth;
            float3 tickCol = lerp(float3(0.12, 0.45, 0.55), float3(0.30, 0.85, 0.95), longTick);
            outCol += tickCol * tickVis * 0.85;
            outA = max(outA, tickVis * 0.75);

            //外圈基础环线
            float baseRing = 1.0 - smoothstep(0.4, 1.4, abs(r - uDecoOuterR));
            outCol += float3(0.10, 0.32, 0.38) * baseRing * 0.55;
            outA = max(outA, baseRing * 0.45);
        }
    }

    //==================================================
    // 5) 全局展开淡入：以圆心向外的扩散波形
    // 把 rNorm > ease 的像素淡出，制造"自圆心向外绽放"的入场
    //==================================================
    {
        float reveal = smoothstep(ease - 0.2, ease + 0.05, rNorm);
        outA *= lerp(1.0, 0.0, reveal);
    }

    //==================================================
    // 6) 输出（预乘 alpha 以匹配 AlphaBlend）
    //==================================================
    float finalA = outA * uAlpha;
    return float4(outCol * finalA, finalA);
}

technique Technique1
{
    pass CyberwareRadialPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
