// ============================================================================
// VoidColonyLoading.fx — 虚空聚落加载界面背景着色器
// 亚空间穿越风格：深邃虚空底色 + 缓慢旋转能量云环 + 星辰 + 径向裂隙光晕
// 视觉语言源自 VoidColonySky.fx，精简以适配加载界面低频帧率
// 全程序化生成，无外部纹理依赖
// ============================================================================

float uTime;
float uProgress;      //0..1 加载进度，驱动进度环与进度条
float uAspectRatio;   //屏幕宽高比 screenWidth/screenHeight

#define PI  3.14159265
#define TAU 6.28318530

#define RIFT_MID   float3(0.55, 0.08, 0.02)
#define RIFT_CORE  float3(0.82, 0.24, 0.06)
#define RIFT_HOT   float3(1.00, 0.62, 0.18)

// ======================== Hash / Noise ========================

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.11369, 0.13787));
    p3 += dot(p3, p3.yzx + 19.19);
    return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
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

//分形噪声，3层足够加载界面使用
float fbm3(float2 p)
{
    float v = vnoise(p) * 0.50;
    p = float2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8) * 2.0;
    v += vnoise(p) * 0.25;
    p = float2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8) * 2.0;
    v += vnoise(p) * 0.125;
    return v / 0.875;
}

//SDF 工具
float hLine(float y, float yCurr, float ht) { return smoothstep(ht, 0.0, abs(yCurr - y)); }
float vLine(float x, float xCurr, float ht) { return smoothstep(ht, 0.0, abs(xCurr - x)); }

// ======================== Star Field ========================

float starField(float2 uv, float scale, float time)
{
    float2 id = floor(uv * scale);
    float2 sub = frac(uv * scale);
    float2 starPos = hash22(id);
    float brightness = hash21(id + 137.0);
    if (brightness < 0.72)
        return 0.0;
    brightness = (brightness - 0.72) / 0.28;
    float d = length(sub - starPos);
    float star = smoothstep(0.050, 0.0, d) * brightness;
    float twinkle = sin(hash11(id.x * 31.0 + id.y * 57.0) * TAU + time * (1.5 + hash11(id.x * 13.0) * 2.5));
    star *= 0.55 + 0.45 * twinkle;
    return star;
}

// ======================== Main ========================

float4 PSVoidColonyLoading(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float aspect = uAspectRatio;

    // ============================================================
    //Layer 0 — 深邃虚空底色（墨黑微带深紫血红，中心略亮）
    // ============================================================
    float2 vc = uv - 0.5;
    float vr = length(vc * float2(1.35, 1.0));
    float3 deep = float3(0.003, 0.001, 0.006);
    float3 midBg = float3(0.010, 0.003, 0.016);
    float3 col = lerp(midBg, deep, smoothstep(0.0, 0.95, vr));

    // ============================================================
    //Layer 1 — 极淡背景虚空雾（低频FBM缓慢流动，提供基底质感）
    // ============================================================
    {
        float2 nuv = uv * float2(1.8 * aspect, 1.8) + float2(t * 0.011, -t * 0.007);
        float n = fbm3(nuv);
        float3 fogTint = lerp(float3(0.08, 0.01, 0.12), float3(0.02, 0.01, 0.07), n);
        col += fogTint * (n - 0.4) * 0.10;
    }

    // ============================================================
    //Layer 2 — 星辰（远景暗淡 + 近景稍亮，被亮区压制）
    // ============================================================
    float2 c = (uv - 0.5) * float2(aspect, 1.0);
    float dist = length(c);
    float angle = atan2(c.y, c.x);

    float starsFar = starField(uv + 800.0, 115.0, t) * 0.40;
    float starsNear = starField(uv + 200.0, 52.0, t * 0.75) * 0.60;
    float starMask = smoothstep(0.06, 0.28, dist);
    col += float3(0.78, 0.82, 1.00) * starsFar * starMask;
    col += float3(1.00, 0.92, 0.80) * starsNear * starMask;

    // ============================================================
    //Layer 3 — 缓慢旋转能量云环（旋涡臂底板，低调）
    // ============================================================
    {
        float rot = t * 0.055;
        float arm1 = sin(angle * 3.0 - dist * 5.0 + rot) * 0.5 + 0.5;
        float arm2 = sin(angle * 2.0 + dist * 3.0 - rot * 1.3) * 0.5 + 0.5;
        float ring = pow(arm1, 2.8) * pow(arm2, 2.2);
        float2 rUV = float2(c.x * 1.25 + t * 0.018, c.y * 1.25 - t * 0.013);
        float rn = fbm3(rUV * 3.8);
        ring *= rn * smoothstep(0.08, 0.28, dist) * smoothstep(1.15, 0.26, dist);
        col += float3(0.16, 0.018, 0.035) * ring;
        col += float3(0.42, 0.07, 0.02) * ring * ring * 0.75;
    }

    // ============================================================
    //Layer 4 — 中央虚空燃烧核心（不规则形态，较 Sky 版更暗）
    // ============================================================
    {
        float pulse = 0.88 + 0.10 * sin(t * 3.4 + 0.55 * sin(t * 1.1));
        float2 fc = c * float2(1.50, 1.02);
        float2 wq = float2(
            fbm3(fc * 2.2 + float2(t * 0.28, 0.0)),
            fbm3(fc * 2.2 + float2(5.2, 1.3) - t * 0.22)
        );
        float2 fcW = fc + (wq - 0.5) * 0.40;
        float fRw = length(fcW);
        float burnN = fbm3(fcW * 3.2 - t * 0.48);
        burnN = burnN * burnN;
        float flameOut = exp(-fRw * fRw * 20.0) * (0.22 + 0.85 * burnN);
        float flameMid = exp(-fRw * fRw * 58.0) * (0.48 + 0.60 * burnN);
        float flameHot = exp(-fRw * fRw * 160.0) * (0.68 + 0.42 * burnN);
        float flameWhite = exp(-length(fc) * length(fc) * 420.0);
        col += float3(0.22, 0.025, 0.008) * flameOut * pulse;
        col += float3(0.72, 0.18, 0.04) * flameMid * pulse;
        col += float3(1.00, 0.52, 0.14) * flameHot * pulse;
        col += float3(1.00, 0.88, 0.68) * flameWhite * 1.15 * pulse;
    }

    // ============================================================
    //Layer 5 — 中央进度环（12点起点顺时针填充，三环结构）
    // ============================================================
    {
        float a01 = (angle + PI) / TAU;

        //外引导环（暗哑参考圆）
        float outerR = 0.175;
        col += float3(0.28, 0.04, 0.02) * smoothstep(0.0012, 0.0, abs(dist - outerR)) * 0.55;

        //进度弧（顺时针填充）
        float progR = 0.142;
        float ang = frac(a01 + 0.25);
        float arcDist = abs(dist - progR);
        float onRing = smoothstep(0.0048, 0.0, arcDist);
        float fill = step(ang, uProgress);
        col += RIFT_CORE * onRing * fill * 1.10;
        col += RIFT_CORE * smoothstep(0.016, 0.004, arcDist) * fill * 0.22;
        //弧前沿暖光
        float lead = smoothstep(0.022, 0.0, abs(ang - uProgress)) * step(ang, uProgress);
        col += RIFT_HOT * onRing * lead * 1.28;

        //缓慢旋转扫描臂（环带内淡扇形）
        float sweepAng = -t * 1.18;
        float relAng = angle - sweepAng;
        relAng = relAng - TAU * floor((relAng + PI) / TAU);
        float wedge = pow(smoothstep(0.55, 0.0, abs(relAng)), 2.5);
        float radial = smoothstep(outerR, progR + 0.006, dist) * smoothstep(progR - 0.006, progR + 0.022, dist);
        col += RIFT_HOT * wedge * radial * 0.28;

        //内引导环
        float innerR = 0.092;
        col += float3(0.22, 0.035, 0.015) * smoothstep(0.0009, 0.0, abs(dist - innerR)) * 0.50;
    }

    // ============================================================
    //Layer 6 — 底部进度条（轨道 + 填充段 + 前沿高亮）
    // ============================================================
    {
        float barY = 0.930;
        float barHalf = 0.0068;
        float barX0 = 0.030, barX1 = 0.970;
        float bx = (uv.x - barX0) / (barX1 - barX0);
        float barDist = abs(uv.y - barY);
        float onBar = step(0.0, bx) * step(bx, 1.0);
        //轨道底色
        col += float3(0.12, 0.015, 0.030) * smoothstep(barHalf + 0.0032, barHalf, barDist) * 0.10 * onBar;
        //填充段
        float filled = step(bx, uProgress) * onBar;
        col += RIFT_CORE * smoothstep(barHalf, 0.0, barDist) * filled * 0.92;
        col += RIFT_CORE * smoothstep(barHalf + 0.0032, 0.0, barDist) * filled * 0.18;
        //前沿亮点
        float barLead = smoothstep(0.020, 0.0, abs(bx - uProgress)) * onBar;
        col += RIFT_HOT * smoothstep(barHalf * 0.55, 0.0, barDist) * barLead * 1.05;
    }

    // ============================================================
    //Layer 7 — 四角L形装饰线框（深红，轻量）
    // ============================================================
    {
        float cS = 0.040, cT = 0.0018, cM = 0.022;
        float corner = 0.0;
        corner += hLine(cM,       uv.y, cT) * step(cM,       uv.x) * step(uv.x, cM + cS);
        corner += vLine(cM,       uv.x, cT) * step(cM,       uv.y) * step(uv.y, cM + cS);
        corner += hLine(cM,       uv.y, cT) * step(1.0-cM-cS, uv.x) * step(uv.x, 1.0-cM);
        corner += vLine(1.0-cM,   uv.x, cT) * step(cM,       uv.y) * step(uv.y, cM + cS);
        corner += hLine(1.0-cM,   uv.y, cT) * step(cM,       uv.x) * step(uv.x, cM + cS);
        corner += vLine(cM,       uv.x, cT) * step(1.0-cM-cS, uv.y) * step(uv.y, 1.0-cM);
        corner += hLine(1.0-cM,   uv.y, cT) * step(1.0-cM-cS, uv.x) * step(uv.x, 1.0-cM);
        corner += vLine(1.0-cM,   uv.x, cT) * step(1.0-cM-cS, uv.y) * step(uv.y, 1.0-cM);
        col += RIFT_MID * saturate(corner) * 0.72;
    }

    // ============================================================
    //Post — 暗角压制 + Gamma校正 + 微胶片颗粒
    // ============================================================
    float vignette = smoothstep(1.35, 0.38, dist);
    vignette = max(vignette, 0.08);
    col *= vignette;
    col = pow(max(col, 0.0), 1.14);
    col *= 0.80;
    float grain = hash21(uv + t * 0.013) * 0.024 - 0.012;
    col += grain;

    return float4(saturate(col), 1.0);
}

technique VoidColonyLoading
{
    pass
    {
        PixelShader = compile ps_3_0 PSVoidColonyLoading();
    }
}
