// ============================================================================
//BlessingLantern.fx 引魂灯氛围层：焰室光晕 / 灯窗漏光 / 地面光池 / 升腾魂雾 / 上浮余烬
//画布契约（与 BlessingRenderer.LanternAmbientRect 同步改）：
//  quad 宽 = 2.6×灯宽，高 = 2.05×灯高，焰室中心落在 UV(0.5, 0.62)
//  p 空间常量：焰心 y=+0.24，灯顶 y=-0.27，地线 y=+0.71，灯半宽 x=0.385，横轴校正 0.891
//AlphaBlend 预乘 alpha，全亮色内容（低 alpha 近加法），暗部零覆盖
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uSeed;        //本盏灯相位种子
float uLit;         //燃焰占比 0..1（零燃焰时 C# 端传长明微焰值）
float uHover;       //悬停度 0..1
float uPulse;       //解锁腾起包络 1→0
float3 uAccent;     //主 accent（修罗紫红 / 死神苍银）
float3 uEmber;      //余烬亮色（描金 / 冷白）

#define FLAME_Y 0.24
#define TOP_Y (-0.27)
#define GROUND_Y 0.71
#define AX 0.891

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm2(float2 p) {
    float v = 0.0;
    float a = 0.55;
    for (int i = 0; i < 2; i++) {
        v += a * valueNoise(p);
        p = p * 2.13 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float2 q = float2(p.x * AX, p.y);   //像素等距空间（圆在此空间才是圆）
    float t = uTime;
    float seed = uSeed * 11.3;
    float lit = saturate(uLit);
    float glow = 0.28 + 0.72 * lit;
    float pulse = saturate(uPulse);
    float hover = saturate(uHover);

    //火光颤闪：与魂焰 shader 同族双频拍动，两层表现同呼吸
    float flicker = 0.86 + 0.14 * sin(t * 7.3 + seed * 3.0) * sin(t * 12.9 + seed * 7.1);

    float3 col = float3(0.0, 0.0, 0.0);

    //1 焰室光晕：紧核 + 宽晕，解锁腾起时撑大，悬停微涨
    float2 df = q - float2(0.0, FLAME_Y);
    float d = length(df);
    float rTight = 0.17 * (1.0 + 0.30 * pulse + 0.08 * hover);
    float rWide = 0.55 * (1.0 + 0.45 * pulse + 0.15 * hover);
    float tight = exp(-(d * d) / (rTight * rTight));
    float wide = exp(-(d * d) / (rWide * rWide));
    col += uEmber * tight * 0.60 * glow * flicker;
    col += uAccent * wide * 0.24 * glow * (0.9 + 0.1 * flicker);

    //2 灯窗漏光：焰高处一横条溢出灯骨的光，读作"光从骨条间挤出来"
    float spill = exp(-pow((p.y - FLAME_Y) / 0.11, 2.0)) * exp(-pow(p.x / 0.72, 2.0));
    //骨条影：与线稿骨条同位的两道竖暗缝，把漏光切成瓣
    float rib = 1.0 - 0.62 * exp(-pow((abs(p.x) - 0.22) / 0.05, 2.0));
    col += uEmber * spill * rib * 0.30 * glow * flicker;

    //3 地面光池：灯足下压扁的暖椭圆 + 一线接地亮痕
    float2 gp = float2(q.x / 0.50, (p.y - GROUND_Y) / 0.085);
    float pool = exp(-dot(gp, gp));
    col += uEmber * pool * 0.30 * glow * flicker;
    col += uAccent * pool * pool * 0.16 * glow;
    float contact = exp(-pow((p.y - GROUND_Y) / 0.020, 2.0)) * exp(-pow(p.x / 0.40, 2.0));
    col += uEmber * contact * 0.18 * glow * flicker;

    //4 升腾魂雾：灯顶之上一缕缓摆的雾带，密度走 fbm 上滚，燃焰越多越实
    float hr = saturate((TOP_Y - p.y) / 0.73);
    float crownGate = smoothstep(0.02, 0.18, hr) * pow(1.0 - hr, 1.4);
    float sway = (fbm2(float2(hr * 2.2 - t * 0.55, seed)) - 0.5) * 0.50 * hr;
    float ribbonW = lerp(0.07, 0.24, hr);
    float ribbon = 1.0 - smoothstep(0.0, ribbonW, abs(q.x - sway * 0.4));
    float den = fbm2(float2(q.x * 3.0 + seed, p.y * 2.0 + t * 0.85));
    den = smoothstep(0.22, 0.75, den);
    float plume = saturate(ribbon * den * crownGate) * lit * (1.0 + pulse * 1.8);
    col += uAccent * plume * 0.85;
    col += uEmber * plume * plume * 0.50;

    //5 上浮余烬微尘：两层视差自灯身升起，密度随燃焰与腾起
    float density = 0.28 + 0.50 * lit + 0.35 * pulse;
    float riseMask = (1.0 - smoothstep(0.22, 0.62, abs(q.x)))
        * smoothstep(GROUND_Y + 0.05, 0.20, p.y);
    for (int layer = 0; layer < 2; layer++) {
        float lf = (float)layer;
        float spd = 0.20 + 0.11 * lf + 0.25 * pulse;
        float grid = 0.170 - lf * 0.045;
        float2 mp = float2(q.x + sin(t * 0.6 + lf * 2.7 + seed) * 0.02, p.y + t * spd);
        float2 g = floor(mp / grid);
        float2 f = frac(mp / grid) - 0.5;
        float rnd = hash21(g + lf * 17.3 + seed);
        float2 c = (hash22(g + lf * 31.7) - 0.5) * 0.6;
        float dd = length(f - c);
        float mote = 1.0 - smoothstep(0.0, 0.06 + rnd * 0.07, dd);
        float life = frac(rnd * 5.7 + t * (0.10 + rnd * 0.12));
        mote *= sin(life * 3.14159265);
        mote *= step(1.0 - density * 0.30, rnd);
        col += uEmber * mote * riseMask * (0.40 + 0.22 * lf);
    }

    //6 细颗粒，压掉大渐变的塑料感
    col *= 1.0 - hash21(p * 913.0 + t) * 0.06;

    //亮度定覆盖：暗处零覆盖（不压暗背景），亮处低覆盖给一点"体"
    float lum = max(col.r, max(col.g, col.b));
    float a = saturate(lum) * 0.30 * uAlpha;
    return float4(col * uAlpha, a) * vertexColor;
}

technique Technique1
{
    pass BlessingLanternPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
