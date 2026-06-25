// ============================================================================
//TzeentchSky.fx 奸奇（命运之羽 / 变幻之主）魔法领域天空
//全程序化，无外部纹理；双色（蓝/品红）翻腾的亚空间魔火、虹彩能量带、
//漂浮的奥术符文、缓慢睁开的注视之眼、奥术星尘与中心脉冲，全部移入 GPU
//取代旧的逐像素复合绘制（88 片迷雾贴图 + 16×32 符文叠绘 + 每两帧烟雾粒子）
// ============================================================================

float uTime;        //秒（GlobalTimeWrappedHourly）
float uIntensity;   //整体淡入淡出 0-1
float uAspectRatio; //宽/高

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

//两八度轻量 fbm，用于域扭曲偏移
float fbm2(float2 p)
{
    return vnoise(p) * 0.625 + vnoise(p * 2.1 + float2(3.7, 8.1)) * 0.375;
}

//三八度 fbm，用于主魔火密度场
float fbm3(float2 p)
{
    return vnoise(p) * 0.55
         + vnoise(p * 2.07 + float2(3.7, 8.1)) * 0.30
         + vnoise(p * 4.13 - float2(1.3, 5.5)) * 0.15;
}

//刚性旋转，连续无缝
float2 rot2(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

//单层奥术星尘，返回该 UV 处亮度（带色相相位 outPhase）
float moteLayer(float2 uv, float scale, float seed, out float outPhase)
{
    float2 id  = floor(uv * scale);
    float2 sub = frac(uv * scale) - 0.5;
    float  h   = hash21(id + seed);
    outPhase   = hash11(h + seed * 1.7);
    if (h < 0.86) return 0.0;
    h = (h - 0.86) / 0.14;
    float2 off = (hash22(id + seed * 1.37) - 0.5) * 0.44;
    float  d   = length(sub - off);
    //缓慢明灭，奸奇的星尘永不安分
    float  tw  = sin(uTime * (1.2 + h * 2.6) + outPhase * TAU) * 0.35 + 0.65;
    float core = smoothstep(0.085, 0.0, d);
    //十字星芒（仅对最亮的星）
    float spike = (exp(-abs(sub.x - off.x) * 70.0) + exp(-abs(sub.y - off.y) * 70.0))
                * smoothstep(0.30, 0.0, d) * 0.5;
    return (core + spike * h) * h * tw;
}

//奸奇调色板：依 f(0-1) 与全局相位在 蓝焰→青→炽核→品红→金 之间变幻
float3 tzeentchPalette(float f, float phase)
{
    float3 deepBlue = float3(0.06, 0.07, 0.42);   //靛蓝底
    float3 blueFire = float3(0.12, 0.45, 1.05);   //奸奇蓝焰
    float3 hotCore  = float3(0.95, 0.92, 1.05);   //炽白核
    float3 pinkFire = float3(1.05, 0.18, 0.78);   //品红魔火
    float3 gold     = float3(1.05, 0.72, 0.22);   //命运金

    //phase 让蓝/品红主导权缓慢互换，呈现"变幻无常"
    float warpHue = f + (phase - 0.5) * 0.30;
    warpHue = saturate(warpHue);

    float3 col = lerp(deepBlue, blueFire, smoothstep(0.10, 0.42, warpHue));
    col = lerp(col, hotCore, smoothstep(0.46, 0.62, warpHue));
    col = lerp(col, pinkFire, smoothstep(0.62, 0.82, warpHue));
    col = lerp(col, gold, smoothstep(0.86, 1.00, warpHue) * 0.6);
    return col;
}

//缓慢睁开的"注视之眼"：透镜状眼睑 + 发光虹膜 + 高光，纯笛卡尔无极角接缝
float3 watchingEye(float2 uvW, float2 center, float openness, float size, float3 iris)
{
    float2 e = (uvW - center) / size;
    //眼睑：上下抛物线包络，openness 控制睁闭
    float lid = openness * (1.0 - e.x * e.x);
    float inside = step(abs(e.x), 1.0) * step(abs(e.y), max(lid, 0.0));
    float edge = smoothstep(0.06, 0.0, abs(abs(e.y) - lid)) * step(abs(e.x), 1.0);

    //虹膜与瞳孔
    float r = length(float2(e.x, e.y * 1.6));
    float irisGlow = exp(-r * r * 4.5) * inside;
    float pupil = smoothstep(0.18, 0.10, r) * inside;
    float glint = exp(-length(e - float2(-0.18, -0.22 * openness)) * 14.0) * inside;

    float3 col = iris * irisGlow * 1.1;
    col += iris * edge * 0.6;            //发光眼眶
    col -= iris * pupil * 0.7;           //深色瞳孔（自调色减暗）
    col += float3(1.0, 1.0, 1.0) * glint * 0.9;
    return max(col, 0.0) * saturate(openness * 2.0);
}

//主函数

float4 PSTzeentchSky(float2 uv : TEXCOORD0) : COLOR0
{
    //宽高比校正 UV
    float2 uvW = float2(uv.x * uAspectRatio, uv.y);
    float  t   = uTime;

    //全局"变幻"相位：让整片天空在蓝相 / 品红相之间缓慢呼吸
    float phase = 0.5 + 0.5 * sin(t * 0.13);

    //=
    //Layer 1 — 亚空间底色：顶部近黑靛紫 → 底部稍暖的深品紫
    //=
    float3 topCol = float3(0.020, 0.012, 0.055);
    float3 botCol = float3(0.060, 0.020, 0.085);
    float3 col = lerp(topCol, botCol, pow(saturate(uv.y), 0.7));
    //中央轻微聚光，四角压暗，把注意力收束到画面中心
    float vignette = smoothstep(1.15, 0.25, length((uv - 0.5) * float2(uAspectRatio, 1.0)));

    //=
    //Layer 2 — 双色翻腾魔火（核心）：域扭曲 fbm，取代 88 片迷雾贴图
    //q/r 为流动偏移场，warp 为最终密度，再映射到奸奇调色板
    //=
    {
        float2 p = uvW * 2.3 + float2(0.0, t * 0.012);
        float2 q = float2(fbm2(p + t * 0.05),
                          fbm2(p + float2(5.2, 1.3) - t * 0.04));
        float2 r = float2(fbm2(p + q * 2.6 + float2(1.7, 9.2) + t * 0.035),
                          fbm2(p + q * 2.6 + float2(8.3, 2.8) - t * 0.030));
        float warp = fbm3(p + r * 2.7);

        //密度塑形：让魔火呈絮状而非铺满
        float dens = smoothstep(0.32, 0.95, warp);
        //局部色相再加一点扰动，使蓝/品红交错更碎
        float localPhase = phase + (r.x - 0.5) * 0.6;
        float3 fire = tzeentchPalette(warp, localPhase);
        col = lerp(col, col + fire, dens * (0.55 + 0.35 * vignette));

        //炽亮丝缕：warp 高值处提亮，做出魔火的"芯"
        float wisp = smoothstep(0.80, 1.0, warp);
        col += fire * wisp * 0.7;
    }

    //=
    //Layer 3 — 虹彩能量带：几条 sin 起伏的飘带（青 / 品红 / 金），取代横向涟漪
    //=
    {
        [unroll]
        for (int i = 0; i < 3; i++) {
            float fi = float(i);
            float yc = 0.26 + fi * 0.24;
            float w  = sin(uvW.x * 2.2 + t * (0.7 + fi * 0.25) + fi * 1.9) * 0.030
                     + sin(uvW.x * 4.9 - t * 0.55 + fi) * 0.013;
            float band = exp(-pow((uv.y - yc - w) * 22.0, 2.0));
            float3 bc = tzeentchPalette(frac(0.18 + fi * 0.33 + t * 0.05), phase);
            col += bc * band * 0.28;
        }
    }

    //=
    //Layer 4 — 漂浮奥术符文：网格散布的发光环形法阵（整数花瓣，极角无缝）
    //取代 16 枚符文贴图的 16×2 叠绘
    //=
    {
        float gscale = 2.6;
        float2 gp = uvW * gscale + float2(t * 0.02, -t * 0.015);
        float2 cid = floor(gp);
        float2 cf  = frac(gp) - 0.5;

        float present = step(0.62, hash21(cid * 1.31 + 7.0));
        if (present > 0.5) {
            //每个符文随机：位置抖动、尺寸、花瓣数、出现节律
            float2 jit = (hash22(cid + 3.3) - 0.5) * 0.5;
            float2 lp = cf - jit;
            float rad = length(lp);
            float ang = atan2(lp.y, lp.x);

            float petalsF = floor(3.0 + hash11(dot(cid, float2(12.9, 78.2))) * 4.0); //3..6
            float spin = t * (0.4 + hash11(cid.x * 0.7 + cid.y * 1.3) * 0.6)
                       * (hash11(cid.y * 2.1) > 0.5 ? 1.0 : -1.0);

            //出现节律：每个符文按自身随机周期淡入淡出
            float per = 4.0 + hash11(cid.x * 5.1 - cid.y * 2.7) * 5.0;
            float ph  = frac(t / per + hash11(dot(cid, float2(3.1, 7.7))));
            float appear = sin(ph * PI);              //0→1→0
            appear *= appear;

            float baseR = 0.16 + hash11(cid.x * 1.9 + cid.y) * 0.07;
            //外环：带整数花瓣调制（cos(N*ang) 中 N 为整数 → 无缝）
            float petal = 0.5 + 0.5 * cos(petalsF * ang + spin);
            float ringR = baseR * (0.86 + 0.14 * petal);
            float ring = exp(-pow((rad - ringR) * 34.0, 2.0));
            //内环
            float ring2 = exp(-pow((rad - baseR * 0.5) * 46.0, 2.0)) * 0.7;
            //中心核
            float core = exp(-rad * rad * 90.0);
            //放射符线（整数路数）
            float spokes = pow(0.5 + 0.5 * cos(petalsF * 2.0 * ang - spin * 1.5), 8.0)
                         * smoothstep(baseR * 1.05, 0.0, rad) * 0.6;

            float runeMask = (ring + ring2 + core + spokes) * appear;
            float3 rc = tzeentchPalette(frac(hash11(cid.x + cid.y * 3.7) + t * 0.08), phase);
            col += rc * runeMask * 0.85;
        }
    }

    //=
    //Layer 5 — 注视之眼：极少数、缓慢睁闭的奸奇之眼，呼应"我一直在看你"
    //=
    {
        [unroll]
        for (int k = 0; k < 2; k++) {
            float sk = float(k) * 23.7 + 4.1;
            //缓慢游走的位置
            float ex = 0.5 + sin(t * 0.05 + sk) * 0.32 * uAspectRatio;
            float ey = 0.30 + sin(t * 0.037 + sk * 1.7) * 0.16 + k * 0.18;
            //睁闭节律：长时间闭合，偶尔睁开
            float blink = frac(t * (0.045 + hash11(sk) * 0.02) + hash11(sk * 2.3));
            float openness = smoothstep(0.0, 0.18, blink) * smoothstep(0.55, 0.32, blink);
            openness = saturate(openness) * 0.5;     //最大半睁，保持隐晦
            float3 iris = (k == 0) ? float3(0.5, 0.95, 1.1) : float3(1.1, 0.55, 0.95);
            col += watchingEye(uvW, float2(ex, ey), openness, 0.13, iris) * 0.8;
        }
    }

    //=
    //Layer 6 — 奥术星尘：三层明灭星点，色相随相位漂移，取代烟雾粒子
    //=
    {
        float pA, pB, pC;
        float s1 = moteLayer(uv, 20.0, 0.0, pA);
        float s2 = moteLayer(uv, 40.0, 17.3, pB);
        float s3 = moteLayer(uv, 11.0, 6.8, pC);
        col += tzeentchPalette(frac(pA + t * 0.1), phase) * s1 * 0.5;
        col += tzeentchPalette(frac(pB + t * 0.1 + 0.4), phase) * s2 * 0.4;
        col += tzeentchPalette(frac(pC + t * 0.1 + 0.7), phase) * s3 * 0.7;
    }

    //=
    //Layer 7 — 中心脉冲与扩张涟漪：呼吸辉光 + 两道环，取代逐像素脉冲/涟漪
    //=
    {
        float2 c = (uv - float2(0.5, 0.42)) * float2(uAspectRatio, 1.0);
        float d = length(c);
        //呼吸辉光
        float breathe = 0.6 + 0.4 * sin(t * 0.7);
        col += tzeentchPalette(0.55, phase) * exp(-d * d * 3.2) * breathe * 0.22;

        //两道缓慢向外扩张的能量环
        [unroll]
        for (int m = 0; m < 2; m++) {
            float rp = frac(t * 0.10 + m * 0.5);
            float rr = rp * 0.9;
            float ring = exp(-pow((d - rr) * 7.0, 2.0)) * (1.0 - rp); //外扩渐隐
            col += tzeentchPalette(frac(0.3 + m * 0.4 + t * 0.05), phase) * ring * 0.30;
        }
    }

    //=
    //Layer 8 — 顶部冷紫电离薄纱 + 整体虹彩微光
    //=
    {
        float topVeil = pow(1.0 - saturate(uv.y), 2.4);
        col += float3(0.05, 0.02, 0.11) * topVeil;
        //极弱全局虹彩，售卖"变幻无常"
        col *= 1.0 + 0.05 * sin(t * 0.9 + uv.x * 3.0 + uv.y * 2.0);
    }

    col *= vignette * 0.5 + 0.5;     //四角轻压暗

    //=
    //输出：alpha = uIntensity，整体淡入淡出
    //=
    col *= uIntensity;
    return float4(saturate(col), uIntensity);
}

technique TzeentchSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSTzeentchSky();
    }
}
