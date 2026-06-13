// ============================================================================
// CybCourseLoading.fx 超梦接入加载界面背景
// 全程序化，无外部纹理
// ============================================================================

float uTime;
float uProgress;     //0..1 加载进度
float uAspectRatio;

#define TAU 6.28318530
#define PI  3.14159265

#define GOLD float3(0.965, 0.773, 0.094)   //主色（HUD与文字强调）
#define WARM float3(1.000, 0.860, 0.420)   //暖橙高光（用于前沿/扫描臂）

// Hash / Noise

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

// Main

float4 PSCybCourseLoading(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float aspect = uAspectRatio;

    // =
    // Layer 0 — 深邃径向渐变底色
    // 中央略亮的午夜蓝→四角更深；制造"屏幕中心微微发光"的错觉
    // =
    float2 vc = uv - 0.5;
    float vr = length(vc * float2(1.35, 1.0));
    float3 deep = float3(0.004, 0.007, 0.018);
    float3 mid  = float3(0.014, 0.022, 0.046);
    float3 col  = lerp(mid, deep, smoothstep(0.0, 0.85, vr));

    // =
    // Layer 1 — 极淡架构网格（屏幕空间，非透视）
    // 仅在交叉处微亮一些，营造"工程图纸"基底感
    // =
    {
        float gx = abs(frac(uv.x * 14.0) - 0.5) * 2.0;
        float gy = abs(frac(uv.y * 9.0) - 0.5) * 2.0;
        float lineX = smoothstep(0.987, 1.0, gx);
        float lineY = smoothstep(0.987, 1.0, gy);
        col += GOLD * (lineX + lineY) * 0.018;
    }

    // =
    // Layer 2 — 微弱数据雾（两八度FBM，营造体积感与缓慢流动）
    // =
    {
        float2 nuv = uv * float2(2.5 * aspect, 2.5) + float2(t * 0.04, t * 0.018);
        float n = vnoise(nuv) * 0.6 + vnoise(nuv * 2.3 - t * 0.02) * 0.4;
        col += GOLD * (n - 0.5) * 0.022;
    }

    // =
    // Layer 3 — 顶部/底部水平基线 + 左侧主栅线（HUD骨架）
    // 单条主线 + 单点交点装饰，避免双线/多线堆叠
    // =
    col += GOLD * vLine(0.020, uv.x, 0.0011);                    //左主栅线
    col += GOLD * hLine(0.115, uv.y, 0.0011)
                * step(0.020, uv.x) * step(uv.x, 0.980) * 0.55;  //顶横线
    col += GOLD * hLine(0.880, uv.y, 0.0010)
                * step(0.020, uv.x) * step(uv.x, 0.980) * 0.45;  //底横线

    //顶横线左端节点点
    {
        float dx = (uv.x - 0.020) * aspect;
        float dy = (uv.y - 0.115);
        float d = length(float2(dx, dy));
        col += GOLD * smoothstep(0.0040, 0.0, d) * 1.10;
    }

    // =
    // Layer 4 — 四角L形角标（单层，精炼）
    // =
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
        col += GOLD * saturate(corner) * 0.85;
    }

    // =
    // Layer 5 — 中央全息雷达盘（焦点元素，放大）
    // 自外向内：外环+刻度 → 进度弧 → 内环  
    // 配合：缓慢扫描臂 + 中心淡晕
    // =
    float2 cc = float2(0.500, 0.510);
    float2 rel = uv - cc;
    rel.x *= aspect;
    float R  = length(rel);
    float A  = atan2(rel.y, rel.x);
    float a01 = (A + PI) / TAU;             //0..1

    //中心暖晕（建立焦点的"体积感"）
    {
        float halo = exp(-R * 7.0);
        col += WARM * halo * 0.045;
    }

    //外引导环
    float outerR = 0.155;
    col += GOLD * smoothstep(0.0009, 0.0, abs(R - outerR)) * 0.65;

    //外环刻度：60小 / 12大
    {
        float tk    = abs(frac(a01 * 60.0) - 0.5) * 2.0;
        float bigTk = abs(frac(a01 * 12.0) - 0.5) * 2.0;
        float tickArea    = smoothstep(0.0048, 0.0, abs(R - (outerR - 0.006)));
        float bigTickArea = smoothstep(0.0090, 0.0, abs(R - (outerR - 0.010)));
        col += GOLD * smoothstep(0.94, 1.0, tk)    * tickArea    * 0.55;
        col += GOLD * smoothstep(0.96, 1.0, bigTk) * bigTickArea * 0.95;
    }

    //进度弧（顶部起点顺时针填充）
    float progR = 0.122;
    {
        float ang = frac(a01 + 0.25);
        float arcDist = abs(R - progR);
        float onRing  = smoothstep(0.0040, 0.0, arcDist);
        float fill    = step(ang, uProgress);
        //主弧
        col += GOLD * onRing * fill * 1.05;
        //柔肩（向外晕染，营造发光质感）
        col += GOLD * smoothstep(0.012, 0.004, arcDist) * fill * 0.18;
        //前沿暖光
        float lead = smoothstep(0.018, 0.0, abs(ang - uProgress)) * step(ang, uProgress);
        col += WARM * onRing * lead * 1.20;
    }

    //内引导环（包住中央百分比文字）
    float innerR = 0.078;
    col += GOLD * smoothstep(0.0008, 0.0, abs(R - innerR)) * 0.55;

    //缓慢旋转的扫描臂（柔和扇形，唯一的运动元素）
    //角速度配合 _loadTime 步进（约1.2/sec real-time），约3秒一圈
    {
        float sweepAng = -t * 1.30;
        float relAng = A - sweepAng;
        relAng = relAng - TAU * floor((relAng + PI) / TAU);
        float wedge = smoothstep(0.55, 0.0, abs(relAng));
        wedge = pow(wedge, 2.6);
        //仅在外环以内、内环以外的环带显示
        float radial = smoothstep(outerR, innerR + 0.005, R)
                     * smoothstep(innerR - 0.005, innerR + 0.020, R);
        col += WARM * wedge * radial * 0.32;
        //扫描臂前沿在外环上点亮一颗游标
        float onOuter = smoothstep(0.0040, 0.0, abs(R - outerR));
        col += WARM * onOuter * smoothstep(0.40, 0.0, abs(relAng)) * 0.65;
    }

    // =
    // Layer 6 — 底部进度条（克制、单段平滑填充）
    // =
    {
        float barY = 0.928;
        float barHalf = 0.0070;
        float barX0 = 0.026;
        float barX1 = 0.974;
        float bx = (uv.x - barX0) / (barX1 - barX0);
        float barDist = abs(uv.y - barY);
        float onBar = step(0.0, bx) * step(bx, 1.0);

        //深色轨道
        col += GOLD * smoothstep(barHalf + 0.0030, barHalf, barDist) * 0.06 * onBar;

        //填充
        if (bx <= uProgress && bx >= 0.0)
        {
            float fill = smoothstep(barHalf, 0.0, barDist);
            //垂直渐变（顶部更亮，模拟发光面板）
            float vy = (uv.y - (barY - barHalf)) / (2.0 * barHalf);
            float topHi = smoothstep(0.40, 0.0, vy);
            col = lerp(col, GOLD, fill * 0.94);
            col += WARM * fill * topHi * 0.22;
            //前沿柔光
            col += WARM * exp(-(uProgress - bx) * 30.0) * fill * 0.55;
        }

        //上下边缘细线
        col += GOLD * smoothstep(0.0010, 0.0, abs(barDist - barHalf)) * onBar * 0.50;
        //左右端帽
        col += GOLD * vLine(barX0, uv.x, 0.0008)
                    * smoothstep(0.012, 0.0, barDist) * 0.75;
        col += GOLD * vLine(barX1, uv.x, 0.0008)
                    * smoothstep(0.012, 0.0, barDist) * 0.75;
    }

    // =
    // Layer 7 — 胶片颗粒（极弱，建立屏幕质感）
    // =
    {
        float grain = hash21(uv * 1234.5 + frac(t * 7.3));
        col += (grain - 0.5) * 0.014;
    }

    // =
    // Post — 暗角（柔和，强化中央焦点）
    // =
    {
        float2 v = uv - 0.5;
        float vig = 1.0 - dot(v, v) * 1.30;
        col *= saturate(vig * 0.55 + 0.62);
    }

    return float4(saturate(col), 1.0);
}

technique CybCourseLoading
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCybCourseLoading();
    }
}
