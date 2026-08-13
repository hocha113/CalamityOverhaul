// ============================================================================
//CybCourseEntryReveal.fx 超梦入场揭示
//中心向外六角消解波；AlphaBlend 预乘 alpha，全程序化
// ============================================================================

float uTime;
float uReveal;        //0完全覆盖 1完全揭示 >1消散尾声
float uAspectRatio;

#define TAU 6.28318530
#define PI  3.14159265

//Hash / Noise

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//Hex Grid
//
//用两套整数方格的 Voronoi 等价于六角网格：
//网格 A 中心：(i, j) * s
//网格 B 中心：(i + 0.5, j + 0.5) * s
//其中 s = (1, sqrt(3))，最近邻 6 个方向距离均为 1，构成正六边形

void hexCellInfo(float2 p, float scale, out float2 local, out float2 cellId)
{
    p *= scale;
    const float2 s = float2(1.0, 1.7320508);

    float2 iA = floor(p / s + 0.5);
    float2 iB = floor(p / s);

    float2 cA = iA * s;
    float2 cB = iB * s + s * 0.5;

    float2 dA = p - cA;
    float2 dB = p - cB;

    if (dot(dA, dA) < dot(dB, dB)) {
        local  = dA;
        cellId = iA;
    } else {
        local  = dB;
        cellId = iB + float2(0.37, 0.41);
    }
}

//单元内部到最近边的垂直距离 (中心 0.866 → 边 0)
float hexEdgeDist(float2 p)
{
    p = abs(p);
    return 0.86602540 - max(p.x * 0.86602540 + p.y * 0.5, p.y);
}

//Main

float4 PSCybCourseEntryReveal(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float aspect = uAspectRatio;

    //修正宽高比，让六边形保持正六边形
    float2 p = uv - 0.5;
    p.x *= aspect;

    //六角格采样
    const float HEX_SCALE = 13.0;       //屏幕高度方向约 13 行单元
    float2 cLocal, cId;
    hexCellInfo(p, HEX_SCALE, cLocal, cId);

    //单元中心在缩放空间的位置（用于换算到 p-空间的实际半径）
    float2 cellCenterScaled = p * HEX_SCALE - cLocal;
    float  cellR = length(cellCenterScaled) / HEX_SCALE;

    float edgeD = hexEdgeDist(cLocal);                  //0 (边) → 0.866 (中心)
    float edgeProx = saturate(1.0 - edgeD / 0.86602540); //0 (中心) → 1 (边)

    //单元唯一随机量（用于颜色 / 抖动 / 闪烁）
    float rnd  = hash21(cId);
    float rnd2 = hash21(cId + float2(7.13, 1.71));

    //波前推进
    float maxR = 0.5 * sqrt(aspect * aspect + 1.0);    //屏幕角到中心的最大距离
    float waveR = uReveal * (maxR + 0.18);              //略微超出确保边角清空

    //每格独立小幅时序抖动，让波前不是完美圆环
    float jitter = (rnd - 0.5) * 0.055
                 + (rnd2 - 0.5) * 0.018 * sin(rnd2 * TAU + t * 0.7);
    float localT = waveR - cellR + jitter;

    //颜色（覆盖态：暗赛博蜂窝）
    float3 col = float3(0.0, 0.0, 0.0);

    //单元基色：极深蓝 → 微亮，按 rnd 略有差异
    float3 darkCell = lerp(float3(0.004, 0.010, 0.026),
                           float3(0.020, 0.045, 0.085), rnd);

    //边线：固有的青色蜂窝栅格（覆盖态可见；色相对齐 SHPC 青 #56DCF0）
    float gridLine = smoothstep(0.10, 0.0, edgeD);
    float3 gridColor = float3(0.07, 0.31, 0.48);
    darkCell += gridColor * gridLine * (0.45 + 0.45 * rnd);

    //单元中央随机刻印：极少数单元出现一颗"数据点"（偶发，非密集）
    if (rnd2 < 0.18) {
        float pulse = 0.7 + 0.3 * sin(t * (1.4 + rnd * 1.8) + rnd * TAU);
        float core = smoothstep(0.045, 0.0, length(cLocal));
        darkCell += float3(0.20, 0.55, 0.85) * core * pulse * 0.45;
    }

    //一些单元被微弱"扫描线"掠过：随机选取部分单元加暖青横纹
    if (rnd > 0.84) {
        float scan = sin(cLocal.y * 60.0 + t * 4.5 + rnd * TAU);
        scan = smoothstep(0.85, 1.0, scan);
        darkCell += float3(0.10, 0.45, 0.70) * scan * 0.18;
    }

    col = darkCell;

    //波前能量层
    //每格在波到达瞬间被点亮：边线 → 内核 → 衰减
    float burnW = 0.060;
    float burn  = exp(-pow(localT / burnW, 2.0));

    //边线在波前剧烈放电
    col += float3(0.35, 0.95, 1.20) * gridLine * burn * 1.35;

    //单元内核：白热闪点
    float core2 = smoothstep(0.18, 0.04, length(cLocal));
    col += float3(0.55, 1.05, 1.20) * core2 * burn * 0.95;

    //内核外的弥散光晕
    float halo = smoothstep(0.30, 0.08, length(cLocal));
    col += float3(0.20, 0.55, 0.80) * halo * burn * 0.55;

    //单元在被消解时短暂出现一条沿对角的能量裂纹（破壳感）
    float crack = abs(cLocal.x * 0.86602540 + cLocal.y * 0.5);
    crack = smoothstep(0.022, 0.0, crack);
    col += float3(0.50, 1.00, 1.10) * crack * burn * (0.60 + rnd * 0.4);

    //波前主环
    //全屏空间中以 waveR 为半径的高强度细环（横跨多格的整体波前）
    float pR = length(p);
    float ringDist = abs(pR - waveR);

    //仅在波正在推进时显示（避免 t=0 时屏幕中心点就高亮）
    float ringActive = step(0.001, uReveal) * (1.0 - smoothstep(1.05, 1.18, uReveal));

    //主环
    float ringMain = exp(-pow(ringDist / 0.020, 2.0));
    col += float3(0.55, 1.05, 1.25) * ringMain * 0.65 * ringActive;

    //辅环（更宽更弱，外缘羽化）
    float ringSoft = exp(-pow(ringDist / 0.060, 2.0));
    col += float3(0.20, 0.55, 0.80) * ringSoft * 0.32 * ringActive;

    //沿环的色散：前方深蓝、后方暖青（系列色内的冷暖差，不引入紫）
    float chro = exp(-pow(ringDist / 0.012, 2.0));
    if (pR > waveR) col += float3(0.08, 0.42, 0.80) * chro * 0.30 * ringActive; //前(尚未到的远端)
    else            col += float3(0.55, 1.05, 0.80) * chro * 0.20 * ringActive; //后(已扫过的近端)

    //中心余晕（仪式感）
    //揭示开始的瞬间，从原点喷发一次柔光，随 reveal 衰减
    float burst = exp(-pR * 5.0) * exp(-uReveal * 2.4);
    col += float3(0.40, 0.95, 1.15) * burst * 0.70;

    //入场最初一瞬：整屏轻微泛光，建立"系统通电"感
    float boot = exp(-uReveal * 9.0);
    col += float3(0.05, 0.15, 0.28) * boot;

    //体积雾抖动
    //微弱噪声层让覆盖区域呈现数字粒子感
    float n = vnoise(p * 6.0 + float2(t * 0.15, t * 0.08)) - 0.5;
    col += float3(0.04, 0.12, 0.20) * n * 0.18;

    //Alpha (覆盖态完全不透明，被波扫过后透明)
    float fadeW = 0.075;

    //基础 alpha：localT < 0 时为 1，> fadeW 时为 0
    float alpha = 1.0 - smoothstep(0.0, fadeW, localT);

    //燃烧瞬间提升 alpha，让发光被肉眼看到
    alpha = max(alpha, burn * 0.85);

    //波前主环也保留可见度（在已经透明的远端也能看到环）
    alpha = max(alpha, ringMain * 0.95 * ringActive);
    alpha = max(alpha, ringSoft * 0.40 * ringActive);

    //中心余晕保持可见
    alpha = max(alpha, burst * 0.45);

    //reveal > 1 时整体淡出
    alpha *= 1.0 - smoothstep(1.00, 1.18, uReveal);

    //最终：预乘 alpha 输出
    col = saturate(col);
    return float4(col * alpha, alpha);
}

technique CybCourseEntryReveal
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCybCourseEntryReveal();
    }
}
