// ============================================================================
//SulfurSeaSky.fx 硫磺海 / 酸雨之主 老公爵 战斗氛围天空
//全程序化，无外部纹理；uBurst/uBurstX 驱动硫酸爆发闪光
//取代旧的逐像素复合绘制（毒雾/气泡/腐蚀/波纹），全部移入 GPU
// ============================================================================

float uTime;        //秒（GlobalTimeWrappedHourly）
float uIntensity;   //整体淡入淡出 0-1
float uAspectRatio; //宽/高
float uBurst;       //硫酸爆发强度 0-1（C#侧指数衰减）
float uBurstX;      //爆发屏幕x位置 0-1

#define TAU 6.28318530
#define PI  3.14159265

//Hash / Noise

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

//两八度轻量 fbm，用于腐蚀斑与次级雾
float fbm2(float2 p)
{
    return vnoise(p) * 0.625 + vnoise(p * 2.1 + float2(3.7, 8.1)) * 0.375;
}

//三八度 fbm，用于主毒雾层
float fbm3(float2 p)
{
    return vnoise(p) * 0.55
         + vnoise(p * 2.07 + float2(3.7, 8.1)) * 0.30
         + vnoise(p * 4.13 - float2(1.3, 5.5)) * 0.15;
}

//单层酸雨雨丝：在已宽高比校正并轻微倾斜的坐标上取竖向雨柱
//返回该像素处的雨丝亮度
float rainLayer(float2 p, float colScale, float vScale, float speed, float seed)
{
    float colf = p.x * colScale;
    float id   = floor(colf);
    float fx   = frac(colf) - 0.5;
    float rnd  = hash11(id * 1.73 + seed);
    //向下平移雨柱（- 号 = 下落）
    float yy   = p.y * vScale - uTime * speed * (0.7 + rnd * 0.7) + rnd * 31.0;
    float seg  = frac(yy);
    //亮头在下（下落前缘），尾迹向上拖；seg 随屏幕下移而增大，故亮度在 seg→1 处最强
    float dash = exp(-(1.0 - seg) * 5.2);
    float thin = exp(-fx * fx * 70.0);     //横向收窄
    return step(0.34, rnd) * thin * dash;
}

//单层上浮酸液气泡：在向上滚动的笛卡尔网格上画细环 + 淡填充
//调用方负责在传入坐标上叠加 uTime 上浮位移
float bubbleLayer(float2 p, float scale, float seed)
{
    float2 id  = floor(p * scale);
    float2 sub = frac(p * scale) - 0.5;
    float  h   = hash21(id + seed);
    if (h < 0.80) return 0.0;
    h = (h - 0.80) / 0.20;
    float2 off = (hash22(id + seed * 1.37) - 0.5) * 0.5;
    float  d   = length(sub - off);
    float  rad = 0.16 + 0.18 * h;
    float  ring = smoothstep(0.045, 0.0, abs(d - rad));  //细气泡轮廓
    float  fill = smoothstep(rad, 0.0, d) * 0.22;        //淡内填充
    float  wob  = 0.78 + 0.22 * sin(uTime * 2.0 + h * 30.0);
    return (ring + fill) * h * wob;
}

//主函数

float4 PSSulfurSeaSky(float2 uv : TEXCOORD0) : COLOR0
{
    //宽高比校正 UV，用于一切需保持等距/圆形的效果
    float2 uvW = float2(uv.x * uAspectRatio, uv.y);
    float  t   = uTime;

    //地平线权重（下半屏增强），毒沼向下越浓
    float horizon = smoothstep(0.30, 1.0, uv.y);

    //=
    //Layer 1 — 基础渐变：顶部病态墨绿 → 中部毒绿 → 底部浑浊硫黄绿
    //=
    float3 topCol = float3(0.020, 0.045, 0.026);
    float3 midCol = float3(0.042, 0.090, 0.040);
    float3 botCol = float3(0.095, 0.150, 0.052);
    float3 col = lerp(topCol, midCol, saturate(uv.y * 1.5));
    col = lerp(col, botCol, pow(saturate(uv.y), 2.1));

    //=
    //Layer 2 — 被毒雾弥散的硫黄太阳：高空一团朦胧黄绿辉光（无锐边）
    //=
    {
        float2 sp = uvW - float2(0.62 * uAspectRatio, 0.30);
        float  sd = length(sp);
        col += float3(0.16, 0.20, 0.05) * exp(-sd * sd * 3.0);   //核心晕
        col += float3(0.10, 0.16, 0.045) * exp(-sd * 1.6) * 0.5; //外散辉
    }

    //=
    //Layer 3 — 滚动毒雾（双层 fbm，向地平线加浓）取代100枚毒雾贴图
    //=
    float2 cuv1 = float2(uvW.x * 1.5 + t * 0.026, uv.y * 2.8);
    float  c1   = fbm3(cuv1);
    float2 cuv2 = float2(uvW.x * 3.1 - t * 0.040, uv.y * 4.9 + 6.3);
    float  c2   = fbm2(cuv2);
    float  fog  = saturate(c1 * 0.80 + c2 * 0.42 - 0.34);
    fog = fog * fog * (3.0 - 2.0 * fog);
    {
        float3 fogDark = float3(0.030, 0.062, 0.030);
        float3 fogTox  = float3(0.085, 0.180, 0.070);
        float3 fogCol  = lerp(fogDark, fogTox, saturate(horizon * 0.6 + fog * 0.3));
        col = lerp(col, fogCol, fog * (0.40 + 0.45 * horizon));
    }

    //=
    //Layer 4 — 腐蚀斑：缓慢脉动的毒绿斑块，集中于中下部，取代腐蚀斑块粒子
    //=
    {
        float2 buv = uvW * float2(1.0, 1.25) + float2(t * 0.012, t * 0.006);
        float  blo = fbm2(buv * 1.7);
        blo = smoothstep(0.56, 0.92, blo);
        float pulse = 0.6 + 0.4 * sin(t * 0.8 + blo * 9.0);
        float zone  = smoothstep(0.20, 0.80, uv.y);
        col += float3(0.10, 0.20, 0.07) * blo * pulse * zone * 0.55;
    }

    //=
    //Layer 5 — 横向毒液波纹带：几条随 sin 起伏的硫绿薄带，取代逐段绘制的毒液线
    //=
    {
        [unroll]
        for (int i = 0; i < 3; i++) {
            float fi = float(i);
            float yc = 0.28 + fi * 0.20;
            float w  = sin(uvW.x * 2.4 + t * 1.2 + fi * 1.7) * 0.018
                     + sin(uvW.x * 5.1 - t * 0.7 + fi) * 0.008;
            float band = exp(-pow((uv.y - yc - w) * 20.0, 2.0));
            col += float3(0.08, 0.17, 0.06) * band * 0.55;
        }
    }

    //=
    //Layer 6 — 上浮酸液气泡（双层视差，下半屏更密）取代气泡粒子
    //+uTime => 屏幕上升
    //=
    {
        float wlo = smoothstep(0.10, 0.95, uv.y);   //下方更密，上升中渐隐
        col += float3(0.40, 0.85, 0.30)
             * bubbleLayer(float2(uvW.x, uv.y + t * 0.045), 11.0, 3.1) * 0.45 * wlo;
        col += float3(0.55, 0.95, 0.40)
             * bubbleLayer(float2(uvW.x * 0.8, uv.y + t * 0.028), 6.5, 8.7) * 0.32 * wlo;
    }

    //=
    //Layer 7 — 酸雨：倾斜下落的雨丝（双层视差）老公爵即酸雨之主，雨丝为主题核心
    //=
    {
        float2 rp = uvW;
        rp.x += uv.y * 0.16;                         //轻微倾斜
        float rain = rainLayer(rp, 150.0, 17.0, 1.30, 0.0) * 0.55  //近层：快、亮
                   + rainLayer(rp * 1.7 + 5.3, 240.0, 26.0, 0.95, 4.2) * 0.32; //远层：密、淡
        float rainFade = smoothstep(0.02, 0.30, uv.y);  //顶端淡入，避免硬边
        col += float3(0.45, 0.70, 0.22) * rain * rainFade * 0.9;
    }

    //=
    //Layer 8 — 地平线毒沼辉光
    //=
    col += float3(0.075, 0.150, 0.045) * horizon * 0.5;

    //=
    //Layer 9 — 硫酸爆发闪光：SpawnSulfuricBurst 触发，以爆心为中心的局部辉光 + 扩张冲击环
    //取代旧的环形酸性脉冲逐像素绘制
    //=
    if (uBurst > 0.003) {
        float2 c = float2((uv.x - uBurstX) * uAspectRatio, uv.y - 0.5);
        float  d = length(c);
        float glow = exp(-d * d * 5.0);
        col += float3(0.45, 0.95, 0.40) * glow * uBurst * 0.85;
        //冲击环随衰减扩张
        float rr = (1.0 - uBurst) * 0.65;
        float ring = exp(-pow((d - rr) * 8.0, 2.0));
        col += float3(0.65, 1.05, 0.45) * ring * uBurst * 0.55;
        col *= 1.0 + uBurst * 0.10;
    }

    //=
    //输出：预乘 alpha，由 uIntensity 控制整体淡入淡出
    //=
    col *= uIntensity;
    return float4(saturate(col), uIntensity);
}

technique SulfurSeaSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSSulfurSeaSky();
    }
}
