// ============================================================================
// MechSky.fx 机械 Boss 战氛围天空
// 全程序化；uWarn/uFlash/uOverload 驱动警告层
// ============================================================================

float uTime;        // 秒（GlobalTimeWrappedHourly）
float uIntensity;   // 整体淡入淡出 0-1
float uAspectRatio; // 宽/高
float uWarn;        // 蓄力警告强度 0-1
float uFlash;       // 闪电强度 0-1（C#侧指数衰减）
float uFlashX;      // 闪电屏幕x位置 0-1（兼作本次电弧的形状种子）
float uOverload;    // 死亡过载强度 0-1

#define TAU 6.28318530
#define PI  3.14159265

// Hash / Noise

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

// 两八度轻量 fbm，用于次级云层与电弧扰动
float fbm2(float2 p)
{
    return vnoise(p) * 0.625 + vnoise(p * 2.1 + float2(3.7, 8.1)) * 0.375;
}

// 三八度 fbm，用于主云层
float fbm3(float2 p)
{
    return vnoise(p) * 0.55
         + vnoise(p * 2.07 + float2(3.7, 8.1)) * 0.30
         + vnoise(p * 4.13 - float2(1.3, 5.5)) * 0.15;
}

// 单层星辰，返回该 UV 处的亮度
float starLayer(float2 uv, float scale, float seed)
{
    float2 id  = floor(uv * scale);
    float2 sub = frac(uv * scale) - 0.5;
    float  h   = hash21(id + seed);
    if (h < 0.80) return 0.0;
    h = (h - 0.80) / 0.20;
    float2 off = (hash22(id + seed * 1.37) - 0.5) * 0.38;
    float  d   = length(sub - off);
    float  tw  = sin(uTime * (1.0 + h * 2.2) + hash11(h + seed) * TAU) * 0.15 + 0.85;
    return h * h * tw * smoothstep(0.09, 0.0, d);
}

// 单层余烬火星：调用方负责在传入坐标上叠加上升漂移
float emberLayer(float2 p0, float scale, float seed)
{
    float2 id  = floor(p0 * scale);
    float2 sub = frac(p0 * scale) - 0.5;
    float  h   = hash21(id + seed);
    if (h < 0.86) return 0.0;
    h = (h - 0.86) / 0.14;
    float2 off = (hash22(id + seed * 1.37) - 0.5) * 0.6;
    float  d   = length(sub - off);
    float  tw  = 0.55 + 0.45 * sin(uTime * (2.0 + h * 4.0) + h * 31.0);
    return h * tw * smoothstep(0.055, 0.0, d);
}

// 齿轮剪影遮罩（1=实体）。p 为已宽高比校正、平移到齿轮中心的坐标
float gearMask(float2 p, float radius, float teeth, float rot)
{
    float r = length(p);
    float a = atan2(p.y, p.x) + rot;
    float aa = radius * 0.05;

    // 方齿外缘：角向方波抬升外径
    float tooth  = step(0.50, frac(a * teeth / TAU));
    float outerR = radius * (0.86 + 0.14 * tooth);
    float body   = smoothstep(outerR + aa, outerR - aa, r);

    // 外环（挖去内孔）
    float ring = body * smoothstep(radius * 0.585 - aa, radius * 0.585 + aa, r);
    // 轮毂圆盘
    float hub = smoothstep(radius * 0.24 + aa, radius * 0.24 - aa, r);
    // 4根楔形轮辐连接轮毂与外环
    float sa = abs(frac(a * 4.0 / TAU) - 0.5);
    float spoke = step(sa, 0.10) * step(r, radius * 0.64);

    return saturate(max(ring, max(hub, spoke)));
}

// 主函数

float4 PSMechSky(float2 uv : TEXCOORD0) : COLOR0
{
    // 宽高比修正 UV，用于一切需要保持圆形/等距的效果
    float2 uvW = float2(uv.x * uAspectRatio, uv.y);
    float  t   = uTime;

    // 地平线权重（下半屏增强）
    float horizon = smoothstep(0.35, 1.0, uv.y);

    // 预计算阴云密度：云值供星光遮蔽、云层着色、闪电背光三处共用
    float2 cuv1 = float2(uvW.x * 1.7 + t * 0.028, uv.y * 3.1);
    float  c1   = fbm3(cuv1);
    float2 cuv2 = float2(uvW.x * 3.4 - t * 0.046, uv.y * 5.5 + 7.7);
    float  c2   = fbm2(cuv2);
    float  cloud = saturate(c1 * 0.78 + c2 * 0.46 - 0.36);
    cloud = cloud * cloud * (3.0 - 2.0 * cloud);

    // =
    // Layer 1 — 基础渐变：近黑钢灰顶 → 锈红熔炉地平线 + 中央炉光隆起
    // =
    float3 topCol = float3(0.013, 0.012, 0.017);
    float3 midCol = float3(0.038, 0.020, 0.019);
    float3 botCol = float3(0.110, 0.036, 0.019);
    float3 col = lerp(topCol, midCol, saturate(uv.y * 1.55));
    col = lerp(col, botCol, pow(saturate(uv.y), 2.3));

    float cx = uv.x - 0.5;
    col += float3(0.095, 0.028, 0.010) * exp(-cx * cx * 5.5) * pow(saturate(uv.y), 3.0);

    // =
    // Layer 2 — 稀疏星光（高空可见，被阴云遮蔽）
    // =
    float starMask = saturate(smoothstep(0.62, 0.06, uv.y) * (1.0 - cloud * 1.5));
    col += float3(0.62, 0.64, 0.78) * starLayer(uv, 26.0, 3.1) * 0.26 * starMask;
    col += float3(0.78, 0.74, 0.85) * starLayer(uv, 11.0, 8.7) * 0.34 * starMask;

    // =
    // Layer 3 — 巨型齿轮剪影 ×3：缓慢旋转的天穹机械，底缘受熔炉光暖染
    // =
    {
        // 大齿轮：右上，主宰天际
        float2 gp = uvW - float2(0.80 * uAspectRatio, 0.26);
        float  g  = gearMask(gp, 0.34, 14.0, t * 0.06);
        col = lerp(col, float3(0.016, 0.013, 0.016), g * 0.62);
        col += float3(0.085, 0.030, 0.012) * g * saturate(gp.y / 0.34 + 0.25) * 0.62;
    }
    {
        // 中齿轮：左侧中空
        float2 gp = uvW - float2(0.16 * uAspectRatio, 0.42);
        float  g  = gearMask(gp, 0.22, 10.0, -t * 0.11);
        col = lerp(col, float3(0.015, 0.013, 0.016), g * 0.52);
        col += float3(0.080, 0.028, 0.011) * g * saturate(gp.y / 0.22 + 0.25) * 0.52;
    }
    {
        // 小齿轮：与大齿轮啮合
        float2 gp = uvW - float2(0.60 * uAspectRatio, 0.47);
        float  g  = gearMask(gp, 0.13, 8.0, t * 0.23);
        col = lerp(col, float3(0.015, 0.012, 0.015), g * 0.46);
        col += float3(0.075, 0.026, 0.010) * g * saturate(gp.y / 0.13 + 0.25) * 0.46;
    }

    // =
    // Layer 4 — 双层滚动阴云：云底被熔炉光染暗橙红，警告时染色增强
    // =
    {
        float3 cloudDark  = float3(0.028, 0.022, 0.026);
        float3 cloudEmber = float3(0.145, 0.052, 0.020);
        float  emberAmt   = saturate(horizon * (0.55 + 0.45 * uWarn) + cloud * 0.10);
        float3 cloudCol   = lerp(cloudDark, cloudEmber, emberAmt);
        col = lerp(col, cloudCol, cloud * 0.85);
    }

    // =
    // Layer 5 — 探照灯光束 ×2：自地平线下方的光源向天空扫掠，
    // 常态暗琥珀白，警告时染红、摆幅增大（机械军团在搜索目标）
    // =
    {
        [unroll]
        for (int k = 0; k < 2; k++) {
            float fk  = float(k);
            float2 org = float2(lerp(0.20, 0.78, fk) * uAspectRatio, 1.10);
            float swing = sin(t * (0.55 + 0.13 * fk) + fk * 2.6);
            float ang   = swing * (0.42 + 0.10 * uWarn);
            float2 dir  = float2(sin(ang), -cos(ang));
            float2 d    = uvW - org;
            float along = dot(d, dir);
            float perp  = dot(d, float2(-dir.y, dir.x));
            // 光束宽度随距离扩张，近地平线处收束
            float w = 0.014 + max(along, 0.0) * 0.060;
            float beam = exp(-(perp * perp) / max(w * w, 1e-5))
                       * smoothstep(0.0, 0.18, along) * exp(-along * 1.1);
            float3 bc = lerp(float3(0.55, 0.42, 0.30), float3(0.85, 0.12, 0.06), saturate(uWarn * 1.2));
            col += bc * beam * (0.085 + 0.10 * uWarn);
        }
    }

    // =
    // Layer 6 — 上升余烬火星（双层视差，下半屏更密）
    // =
    col += float3(1.00, 0.40, 0.10) * emberLayer(float2(uvW.x, uv.y + t * 0.050), 22.0, 4.7)
         * 0.42 * smoothstep(0.10, 0.85, uv.y);
    col += float3(1.00, 0.55, 0.16) * emberLayer(float2(uvW.x * 0.8, uv.y + t * 0.030), 12.0, 9.3)
         * 0.30 * smoothstep(0.20, 0.95, uv.y);

    // =
    // Layer 7 — 警告地平线脉冲：Boss蓄力时整条地平线警报呼吸
    // =
    float breath = 0.5 + 0.5 * sin(t * 5.6);
    col += float3(0.335, 0.050, 0.020) * horizon * uWarn * (0.35 + 0.65 * breath);

    // =
    // Layer 8 — 闪电：冲刺/俯冲瞬间天空如闪雷亮起
    // 以电弧为中心的局部辉光（近亮远暗、高空亮近地弱），
    // 云层被背光照透是主角，避免全屏均匀爆白晃眼
    // =
    if (uFlash > 0.003) {
        float dxl = (uv.x - uFlashX) * uAspectRatio;
        float localGlow = exp(-dxl * dxl * 1.4);
        float lightAmt = uFlash * lerp(0.22, 1.0, localGlow) * lerp(1.0, 0.45, uv.y);
        col += float3(0.30, 0.36, 0.46) * lightAmt * 0.42;
        col += float3(0.26, 0.32, 0.46) * cloud * lightAmt * 0.85;
        col *= 1.0 + lightAmt * 0.14;

        // 电弧本体只在闪电前段可见，余辉阶段先行消失
        float boltVis = smoothstep(0.30, 0.72, uFlash);
        if (boltVis > 0.001) {
            float seed = uFlashX * 91.7;
            float hitY = 0.78;

            // 主干：x 随高度抖动折行（粗细两级扰动），顶端收拢于云中
            float amp = 0.10 * smoothstep(0.0, 0.25, uv.y);
            float jit = (vnoise(float2(uv.y * 9.0,  seed))       - 0.5) * amp
                      + (vnoise(float2(uv.y * 27.0, seed + 9.1)) - 0.5) * 0.024;
            float bx  = uFlashX + jit;
            float dxp = abs(uv.x - bx) * uAspectRatio;
            float core = exp(-dxp * dxp * 26000.0);
            float glow = exp(-dxp * dxp * 900.0);
            float vmask = smoothstep(hitY + 0.02, hitY - 0.02, uv.y);
            col += float3(0.75, 0.85, 1.05) * (core * 1.30 + glow * 0.28) * vmask * boltVis;

            // 两条斜出分支，向下衰减
            [unroll]
            for (int b = 0; b < 2; b++) {
                float fb = float(b);
                float fy = 0.26 + 0.22 * fb;
                float side = step(0.5, hash11(seed + fb * 3.3)) * 2.0 - 1.0;
                float t01 = (uv.y - fy) / 0.16;
                float bmask = step(0.0, t01) * step(t01, 1.0) * (1.0 - t01);
                float bxx = bx + side * (uv.y - fy) * (0.55 + 0.40 * hash11(seed + fb * 7.7))
                          + (vnoise(float2(uv.y * 31.0, seed + fb * 17.0)) - 0.5) * 0.02;
                float bd = abs(uv.x - bxx) * uAspectRatio;
                col += float3(0.60, 0.72, 0.95) * exp(-bd * bd * 9000.0) * bmask * boltVis * 0.80;
            }

            // 击中点光球
            float2 hd = float2((uv.x - bx) * uAspectRatio, uv.y - hitY);
            col += float3(0.70, 0.80, 1.00) * exp(-dot(hd, hd) * 220.0) * boltVis * 0.70;
        }
    }

    // =
    // Layer 9 — 过载电涌：死亡演出时横向撕裂带 + 全屏快速明暗抖动
    // =
    if (uOverload > 0.01) {
        float band = step(0.975, hash21(float2(floor(uv.y * 36.0), floor(t * 16.0))));
        col += float3(0.45, 0.10, 0.05) * band * uOverload * 0.80;
        col += float3(0.28, 0.04, 0.02) * horizon * uOverload * 0.60;
        col *= 1.0 + uOverload * 0.18 * sin(t * 46.0 + uv.y * 9.0);
    }

    // =
    // 输出：预乘 alpha，由 uIntensity 控制整体淡入淡出
    // =
    col *= uIntensity;
    return float4(saturate(col), uIntensity);
}

technique MechSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSMechSky();
    }
}
