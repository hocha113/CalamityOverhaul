// ============================================================================
// CybCourseSky.fx 超梦沉浸空间天空
// 全程序化深空夜景；无外部纹理
// ============================================================================

float uTime;
float uIntensity;
float uAspectRatio;

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

// 两八度轻量 fbm，用于星云与极光扰动
float fbm2(float2 p)
{
    return vnoise(p) * 0.625 + vnoise(p * 2.1 + float2(3.7, 8.1)) * 0.375;
}

// 三八度 fbm，用于体积雾
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

// 距正六边形（中心 0,0，apothem 0.866）边的距离
float hexEdgeDist(float2 p)
{
    p = abs(p);
    return 0.86602540 - max(p.x * 0.86602540 + p.y * 0.5, p.y);
}

// 主函数

float4 PSCybCourseSky(float2 uv : TEXCOORD0) : COLOR0
{
    // 宽高比修正 UV，用于水平方向敏感的效果（极光波形、星云、栅格）
    float2 uvW = float2(uv.x * uAspectRatio, uv.y);
    float  t   = uTime * 0.065;

    // =
    // Layer 1 — 基础渐变天空
    // 顶部深空黑蓝 → 底部略亮的午夜蓝；高纬度叠淡淡冷紫晕染（梦境感）
    // =
    float3 topCol = float3(0.014, 0.020, 0.052);
    float3 botCol = float3(0.026, 0.048, 0.095);
    float3 col    = lerp(topCol, botCol, pow(saturate(uv.y), 0.5));

    {
        // 顶部冷紫染色（远空带电离感）
        float topVeil = pow(1.0 - saturate(uv.y), 2.2);
        col += float3(0.020, 0.012, 0.045) * topVeil;
    }

    // =
    // Layer 2 — 远景数字蜂窝栅格
    // 占据下半部分，向地平线汇聚，颜色极淡，营造"地表是数字基底"的暗示
    // =
    {
        // 仅在 uv.y > 0.40 区域显示，向下增强
        float gridArea = smoothstep(0.40, 0.92, uv.y);

        // 透视：随 y 增大，水平 z 越近，视密度变化
        // 用 1/(1.05 - uv.y) 形成轻微聚拢感（但不极端）
        float perspY = 1.0 / max(1.05 - uv.y, 0.06);
        float2 hp = float2(uvW.x * perspY * 1.6, perspY * 0.6 + t * 0.30);

        // 取 hex 单元局部位置
        const float2 sH = float2(1.0, 1.7320508);
        float2 iA = floor(hp / sH + 0.5);
        float2 iB = floor(hp / sH);
        float2 cA = iA * sH;
        float2 cB = iB * sH + sH * 0.5;
        float2 dA = hp - cA;
        float2 dB = hp - cB;
        float2 cLocal = dot(dA, dA) < dot(dB, dB) ? dA : dB;
        float2 cId    = dot(dA, dA) < dot(dB, dB) ? iA : (iB + 0.41);

        float ed = hexEdgeDist(cLocal);
        float gridLine = smoothstep(0.10, 0.0, ed);

        // 极淡冷青边线
        col += float3(0.030, 0.110, 0.220) * gridLine * gridArea * 0.42;

        // 偶发节点闪烁（极少数蜂窝单元会被 ping 一次）
        float pingRnd = hash21(cId);
        if (pingRnd > 0.965) {
            float pingT = frac(uTime * 0.45 + pingRnd * 13.7);
            float ping = exp(-pingT * 9.0);
            float core = smoothstep(0.06, 0.0, length(cLocal));
            col += float3(0.30, 0.85, 1.05) * core * ping * gridArea * 0.55;
        }
    }

    // =
    // Layer 3 — 极光带（保留原有，并轻微提色）
    // 第一条：青色调（y≈0.36）
    // 第二条：蓝紫调（y≈0.62）
    // =
    float w1 = sin(uvW.x * 2.4 + t * 1.15) * 0.032
             + sin(uvW.x * 5.3 - t * 0.72) * 0.014;
    float b1 = exp(-pow(abs(uv.y - 0.36 - w1) * 30.0, 1.7));
    col += float3(0.06, 0.30, 0.45) * b1 * 0.105;

    float w2 = sin(uvW.x * 1.9 - t * 0.88) * 0.028
             + sin(uvW.x * 4.7 + t * 0.58) * 0.011;
    float b2 = exp(-pow(abs(uv.y - 0.62 - w2) * 24.0, 1.5));
    col += float3(0.10, 0.14, 0.42) * b2 * 0.080;

    // =
    // Layer 4 — 星云薄雾
    // 极低不透明度 fbm 纹理，注入蓝调与冷紫层次感，不遮挡任何内容
    // =
    float2 nebUV = uvW * float2(0.65, 1.05) + float2(t * 0.022, 0.0);
    float  neb   = fbm2(nebUV * 1.25);
    col += float3(0.025, 0.07, 0.16) * neb * 0.075;

    // 一支极弱的冷紫云，只在画面上半部出现
    float upperVeil = smoothstep(0.55, 0.05, uv.y);
    float neb2 = fbm2(nebUV * 0.7 + float2(2.4, -1.1));
    col += float3(0.045, 0.020, 0.110) * neb2 * upperVeil * 0.085;

    // =
    // Layer 5 — 数据光柱（缓慢漂移的垂直霓虹脉冲）
    // 在屏幕水平方向上随机分布若干"光柱"，模拟远方上传/下载到天穹的数据通道
    // =
    {
        // 每个屏宽内约 6~8 根可能的光柱位置（随时间播放）
        float3 col5 = float3(0.0, 0.0, 0.0);
        const int NUM_BEAMS = 5;
        [unroll]
        for (int k = 0; k < NUM_BEAMS; k++) {
            float seed = float(k) * 17.31 + 3.7;
            // 缓慢左右漂移
            float xPos = frac(seed * 0.2718 + uTime * 0.0040 * (1.0 + frac(seed * 0.31)));
            // 在 x 方向上转换到非线性宽度（更窄）
            float dx = (uv.x - xPos);
            // 周期性脉冲：每 ~6s 一次，宽度短
            float beat = frac(uTime * (0.18 + frac(seed) * 0.12) + seed);
            float pulse = exp(-pow(beat - 0.10, 2.0) * 240.0);
            // 强度随高度衰减：从地平线向上变弱
            float vert = smoothstep(0.95, 0.10, uv.y);
            float beam = exp(-(dx * dx) * 1500.0) * pulse * vert;
            // 随机色调：青/蓝紫/品红 三选一
            float hue = frac(seed * 0.731);
            float3 bc = (hue < 0.55)
                ? float3(0.18, 0.85, 1.05)
                : ((hue < 0.85) ? float3(0.30, 0.30, 1.10)
                                : float3(0.95, 0.30, 0.85));
            col5 += bc * beam * 0.45;
        }
        col += col5;
    }

    // =
    // Layer 6 — 星辰（三层：细密 / 稠密 / 稀疏偏亮）
    // 星辰整体微微蓝白色调，避免暖色系干扰科幻氛围
    // =
    col += float3(0.76, 0.87, 1.00) * starLayer(uv, 22.0,  0.0) * 0.48;
    col += float3(0.70, 0.82, 1.00) * starLayer(uv, 50.0, 27.3) * 0.40;
    col += float3(0.86, 0.92, 1.00) * starLayer(uv,  9.0,  6.8) * 0.62;
    // 一层粉紫稀星，进一步强化梦境感
    col += float3(0.85, 0.65, 1.00) * starLayer(uv, 14.0, 41.7) * 0.30;

    // =
    // Layer 7 — 流星（极稀少，每个周期最多一条，划过短促弧线）
    // 用屏幕水平方向上的细长线 + exp 头部高亮
    // =
    {
        float cycle = floor(uTime * 0.12);                  // 每 ~8s 切换一次轨迹
        float seed = hash11(cycle * 1.7 + 5.3);
        float startX = lerp(-0.05, 1.05, seed);
        float startY = lerp(0.05, 0.55, hash11(cycle * 2.3 + 11.1));
        float dirX = lerp(-1.0, 1.0, hash11(cycle * 3.7 + 19.9)) * 1.6;
        float dirY = lerp(0.20, 0.55, hash11(cycle * 5.1 + 27.3));
        float life = frac(uTime * 0.12);                    // 0..1
        float head = clamp(life * 1.8 - 0.2, 0.0, 1.0);
        float2 hPos = float2(startX + dirX * head, startY + dirY * head);
        float2 dxy = (uv - hPos);
        dxy.x *= uAspectRatio;
        // 沿轨迹方向的尾迹
        float2 trailDir = normalize(float2(dirX * uAspectRatio, dirY));
        float along = dot(dxy, trailDir);
        float perp  = abs(dot(dxy, float2(-trailDir.y, trailDir.x)));
        float trail = exp(-perp * 800.0)
                    * smoothstep(0.0, 0.18, -along) * exp(along * 8.0);
        // 头部高亮
        float heads = exp(-length(dxy) * 220.0);
        // 仅在 life 中段显示
        float lifeMask = smoothstep(0.05, 0.10, life) * smoothstep(0.95, 0.85, life);
        // 仅一定概率的 cycle 才出现流星
        float chance = step(0.55, hash11(cycle * 7.3 + 3.7));
        col += float3(0.85, 0.95, 1.10) * (trail + heads) * lifeMask * chance * 0.85;
    }

    // =
    // Layer 8 — 地平线城市余光 + 缓慢扫描脉冲
    // 天空底部隐约可见的大气辉光，暗示地平线外的赛博都市
    // 配合一道极淡水平脉冲：每 ~9s 从地平线向上推进一次（仿若远方主机的呼吸）
    // =
    float horizon = smoothstep(0.42, 1.0, uv.y);
    col += float3(0.045, 0.135, 0.260) * horizon * 0.34;
    col += float3(0.090, 0.052, 0.020) * horizon * 0.10;

    {
        // 远方"主机呼吸"扫描脉冲：周期 9s，从 y=1.0 向上推进到 y=0.45
        float bpm = frac(uTime * 0.111);
        float scanY = lerp(1.02, 0.42, bpm);
        float pulseW = 0.020;
        float pulse = exp(-pow((uv.y - scanY) / pulseW, 2.0));
        // 仅在 bpm 启动一段时间内可见
        float live = smoothstep(0.05, 0.12, bpm) * smoothstep(0.95, 0.85, bpm);
        col += float3(0.18, 0.55, 0.85) * pulse * live * 0.55;
    }

    // =
    // 最终输出，淡入淡出由 uIntensity 控制
    // =
    col *= uIntensity;
    return float4(saturate(col), 1.0);
}

technique CybCourseSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCybCourseSky();
    }
}
