// ============================================================================
//CybCourseBoot.fx 超梦接入加载屏（青色系，与世界内构造核心同一套语言）
//旧的金色 CybCourseLoading.fx 保留给旧网(OldNet)加载屏复用，勿动
//全程序化，无采样器；焦点=中央六角构造刻度盘，唯一暖点=进度前沿与心跳
// ============================================================================

float uTime;
float uProgress;     //0..1 加载进度
float uAspectRatio;

#define TAU 6.28318530
#define PI  3.14159265

//SHPC 色板：青为体，琥珀只作前沿/心跳
#define CYAN     float3(0.337, 0.863, 0.941)
#define CYAN_HI  float3(0.667, 0.961, 1.000)
#define AMBER    float3(1.000, 0.667, 0.235)

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

//SDF helpers
float hLine(float y, float yCurr, float ht) { return smoothstep(ht, 0.0, abs(yCurr - y)); }
float vLine(float x, float xCurr, float ht) { return smoothstep(ht, 0.0, abs(xCurr - x)); }

//正六边形距离度量(边界=R)
float hexDist(float2 p)
{
    p = abs(p);
    return max(p.x * 0.86602540 + p.y * 0.5, p.y);
}

float2 rot2(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

//两套方格 Voronoi 等价六角网格（与天空/入场揭示同族）
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
    float pick = step(dot(dB, dB), dot(dA, dA));
    local  = lerp(dA, dB, pick);
    cellId = lerp(iA, iB + float2(0.37, 0.41), pick);
}

float hexEdgeDist(float2 p)
{
    p = abs(p);
    return 0.86602540 - max(p.x * 0.86602540 + p.y * 0.5, p.y);
}

//Main

float4 PSCybCourseBoot(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float aspect = uAspectRatio;

    //=
    //Layer 0 — 虚空径向底色：中央微亮的午夜青蓝 → 四角更深
    //=
    float2 vc = uv - 0.5;
    float vr = length(vc * float2(1.35, 1.0));
    float3 deep = float3(0.003, 0.007, 0.014);
    float3 mid  = float3(0.010, 0.024, 0.040);
    float3 col  = lerp(mid, deep, smoothstep(0.0, 0.85, vr));

    //=
    //Layer 1 — 微弱数据雾（缓慢流动）
    //=
    {
        float2 nuv = uv * float2(2.5 * aspect, 2.5) + float2(t * 0.04, t * 0.018);
        float n = vnoise(nuv) * 0.6 + vnoise(nuv * 2.3 - t * 0.02) * 0.4;
        col += CYAN * (n - 0.5) * 0.020;
    }

    //=
    //Layer 2 — 底部六角编译带（正在铺装的地基，极淡）
    //=
    {
        float2 hp = float2(uv.x * aspect, uv.y);
        float2 cLocal, cId;
        hexCellInfo(hp, 11.0, cLocal, cId);
        float rnd = hash21(cId);
        float rnd2 = hash21(cId + float2(7.13, 1.71));
        float band = smoothstep(0.68, 0.96, uv.y);
        float gridLine = smoothstep(0.09, 0.0, hexEdgeDist(cLocal));

        float resident = step(0.62, rnd) * step(rnd, 0.85);
        col += CYAN * gridLine * resident * band * 0.045;

        //少数单元编译点亮一瞬
        float compiling = step(0.90, rnd);
        float ph = frac(t * 0.10 + rnd2 * 5.0);
        float env = smoothstep(0.0, 0.05, ph) * exp(-max(ph - 0.05, 0.0) * 7.0);
        col += CYAN_HI * gridLine * compiling * env * band * 0.30;
    }

    //=
    //Layer 3 — HUD 骨架：左主栅线 + 顶/底横线（DrawMenu 文字对齐这套框架）
    //=
    col += CYAN * vLine(0.020, uv.x, 0.0011) * 0.55;
    col += CYAN * hLine(0.115, uv.y, 0.0011)
                * step(0.020, uv.x) * step(uv.x, 0.980) * 0.32;
    col += CYAN * hLine(0.880, uv.y, 0.0010)
                * step(0.020, uv.x) * step(uv.x, 0.980) * 0.26;

    //顶横线左端节点点
    {
        float dx = (uv.x - 0.020) * aspect;
        float dy = (uv.y - 0.115);
        float d = length(float2(dx, dy));
        col += CYAN_HI * smoothstep(0.0040, 0.0, d) * 0.85;
    }

    //=
    //Layer 4 — 四角L形角标
    //=
    {
        float cS = 0.038, cT = 0.0022, cM = 0.026;
        float corner = 0.0;
        corner += hLine(cM,         uv.y, cT) * step(cM,        uv.x) * step(uv.x, cM + cS);
        corner += vLine(cM,         uv.x, cT) * step(cM,        uv.y) * step(uv.y, cM + cS);
        corner += hLine(cM,         uv.y, cT) * step(1.0-cM-cS, uv.x) * step(uv.x, 1.0-cM);
        corner += vLine(1.0-cM,     uv.x, cT) * step(cM,        uv.y) * step(uv.y, cM + cS);
        corner += hLine(1.0-cM,     uv.y, cT) * step(cM,        uv.x) * step(uv.x, cM + cS);
        corner += vLine(cM,         uv.x, cT) * step(1.0-cM-cS, uv.y) * step(uv.y, 1.0-cM);
        corner += hLine(1.0-cM,     uv.y, cT) * step(1.0-cM-cS, uv.x) * step(uv.x, 1.0-cM);
        corner += vLine(1.0-cM,     uv.x, cT) * step(1.0-cM-cS, uv.y) * step(uv.y, 1.0-cM);
        col += CYAN * saturate(corner) * 0.50;
    }

    //=
    //Layer 5 — 中央六角构造刻度盘（与世界内地平线构造核心同形）
    //自外向内：六角外框(慢转+心跳) → 刻度环 → 进度弧 → 内环
    //=
    float2 cc = float2(0.500, 0.510);
    float2 rel = uv - cc;
    rel.x *= aspect;
    float R  = length(rel);
    float A  = atan2(rel.y, rel.x);
    float a01 = (A + PI) / TAU;             //0..1（进度填充与整数刻度专用）

    //中心冷晕
    {
        float halo = exp(-R * 7.0);
        col += CYAN * halo * 0.035;
    }

    //六角外框：缓慢自转；每 ~6.5s 一次琥珀心跳（与天空核心同拍）
    {
        float beat = frac(t * 0.1538);
        float pulse = exp(-beat * 6.0);
        float2 pr = rot2(rel, t * 0.06);
        float hd = hexDist(pr);
        float frame = smoothstep(0.0016, 0.0, abs(hd - 0.172));
        col += CYAN * frame * 0.60;
        col += AMBER * frame * pulse * 0.35;
        //框内顶点连线的弱结构辉光
        float shell = smoothstep(0.176, 0.150, hd) * smoothstep(0.120, 0.150, hd);
        col += CYAN * shell * 0.05;
    }

    //刻度环：60小 / 12大（整数倍角，无接缝）
    {
        float tk    = abs(frac(a01 * 60.0) - 0.5) * 2.0;
        float bigTk = abs(frac(a01 * 12.0) - 0.5) * 2.0;
        float tickArea    = smoothstep(0.0048, 0.0, abs(R - 0.146));
        float bigTickArea = smoothstep(0.0090, 0.0, abs(R - 0.142));
        col += CYAN * smoothstep(0.94, 1.0, tk)    * tickArea    * 0.40;
        col += CYAN * smoothstep(0.96, 1.0, bigTk) * bigTickArea * 0.75;
    }

    //进度弧（顶部起点顺时针填充；前沿琥珀）
    float progR = 0.122;
    {
        float ang = frac(a01 + 0.25);
        float arcDist = abs(R - progR);
        float onRing  = smoothstep(0.0040, 0.0, arcDist);
        float fill    = step(ang, uProgress);
        col += CYAN * onRing * fill * 0.95;
        col += CYAN * smoothstep(0.012, 0.004, arcDist) * fill * 0.16;
        float lead = smoothstep(0.018, 0.0, abs(ang - uProgress)) * step(ang, uProgress);
        col += AMBER * onRing * lead * 1.20;
        //弧底轨道
        col += CYAN * onRing * 0.07;
    }

    //内引导环（包住中央百分比文字）
    col += CYAN * smoothstep(0.0008, 0.0, abs(R - 0.078)) * 0.45;

    //缓慢旋转的扫描臂（唯一持续运动元素）
    {
        float sweepAng = -t * 1.30;
        float relAng = A - sweepAng;
        relAng = relAng - TAU * floor((relAng + PI) / TAU);
        float wedge = smoothstep(0.55, 0.0, abs(relAng));
        wedge = pow(wedge, 2.6);
        float radial = smoothstep(0.160, 0.083, R)
                     * smoothstep(0.073, 0.098, R);
        col += CYAN * wedge * radial * 0.22;
        //扫描臂前沿在刻度环上点亮一颗游标
        float onTick = smoothstep(0.0040, 0.0, abs(R - 0.146));
        col += CYAN_HI * onTick * smoothstep(0.40, 0.0, abs(relAng)) * 0.55;
    }

    //=
    //Layer 6 — 底部进度条
    //=
    {
        float barY = 0.928;
        float barHalf = 0.0070;
        float barX0 = 0.026;
        float barX1 = 0.974;
        float bx = (uv.x - barX0) / (barX1 - barX0);
        float barDist = abs(uv.y - barY);
        float onBar = step(0.0, bx) * step(bx, 1.0);

        //深色轨道
        col += CYAN * smoothstep(barHalf + 0.0030, barHalf, barDist) * 0.05 * onBar;

        //填充（step 门控替代动态分支）
        float inFill = step(bx, uProgress) * step(0.0, bx);
        float fill = smoothstep(barHalf, 0.0, barDist) * inFill;
        float vy = (uv.y - (barY - barHalf)) / (2.0 * barHalf);
        float topHi = smoothstep(0.40, 0.0, vy);
        col = lerp(col, CYAN * 0.9, fill * 0.92);
        col += CYAN_HI * fill * topHi * 0.20;
        col += AMBER * exp(-max(uProgress - bx, 0.0) * 30.0) * fill * 0.50;

        //上下边缘细线
        col += CYAN * smoothstep(0.0010, 0.0, abs(barDist - barHalf)) * onBar * 0.38;
        //左右端帽
        col += CYAN * vLine(barX0, uv.x, 0.0008)
                    * smoothstep(0.012, 0.0, barDist) * 0.60;
        col += CYAN * vLine(barX1, uv.x, 0.0008)
                    * smoothstep(0.012, 0.0, barDist) * 0.60;
    }

    //=
    //Layer 7 — 胶片颗粒 + 暗角
    //=
    {
        float grain = hash21(uv * 1234.5 + frac(t * 7.3));
        col += (grain - 0.5) * 0.012;
    }
    {
        float2 v = uv - 0.5;
        float vig = 1.0 - dot(v, v) * 1.30;
        col *= saturate(vig * 0.55 + 0.62);
    }

    return float4(saturate(col), 1.0);
}

technique CybCourseBoot
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCybCourseBoot();
    }
}
