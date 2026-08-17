// ============================================================================
//OniSigilBoard.fx 结印盘盘体,单 technique:
//  TechDisc——役鬼工位的圆漆盘:SDF 圆盘轮廓(边沿细噪蚀,旋出来的漆器不给尺规正圆)+
//    轆轤同心旋纹+urushi 承光带与巡缘漆光+蒔絵六芒暗纹与金尘低闪+
//    金压线环内衬绯线(与顶梁/台账同一家规矩)+三结印位鬼火眠焰(占位的鬼在漆下呼吸,
//    将醒转绯)+盘心浅凹与合鬼暖芯+底缘烛光暖染+边缘炭沉
//全笛卡尔坐标无极角;线宽按盘径折算,同一支盘画得了全幅工位也画得了吊坠微缩;
//恒定 uSeed 形状稳定;预乘 alpha 配 AlphaBlend;色板 CPU 传入与 OnikiriUITheme 同源
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;   //quad 像素尺寸(方形)
float uSeed;
float uDiscR;         //漆盘外半径(px)
float uStarR;         //六芒尖端半径(px,蒔絵暗纹的骨)
float uSlotR;         //结印位所在半径(px,眠焰锚)
float uRot;           //盘体摆角(吊坠随绳摆;只转纹样与眠焰,受光带留在屏幕朝向)
float3 uSlotLit;      //三槽占用 0/1(上/右下/左下)
float3 uSlotDanger;   //三槽将醒 0/1
float uComplete;      //三印齐 0/1,盘心暖芯
float3 uColInk;
float3 uColPaper;
float3 uColDeep;
float3 uColDark;
float3 uColCandle;
float3 uColGold;
float3 uColGoldDeep;
float3 uColBurnDim;
float3 uColGhost;     //鬼火亮青
float3 uColGhostDim;  //鬼火暗青

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
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

//双八度,边蚀/颗粒共用
float fbm2(float2 p) {
    return valueNoise(p) * 0.62 + valueNoise(p * 2.31 + float2(3.7, 7.1)) * 0.38;
}

//点到线段距离(蒔絵六芒的笔)
float segDist(float2 p, float2 a, float2 b) {
    float2 ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 0.0001));
    return length(p - a - ab * t);
}

// ============================ TechDisc ============================

float4 PSDisc(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float2 p = px - uResolution * 0.5;
    float r = length(p);

    //====盘形 SDF:边沿低频噪蚀====
    float wob = (fbm2(px * 0.045 + uSeed * 5.0) - 0.5) * max(uDiscR * 0.018, 1.6);
    float sdf = r - uDiscR + wob;
    float body = 1.0 - smoothstep(-0.9, 0.9, sdf);
    if (body <= 0.004) {
        return float4(0, 0, 0, 0);
    }
    float rN = saturate(r / uDiscR);

    //====漆底:心浅缘沉,微透红====
    float3 col = lerp(uColInk * 0.60, lerp(uColInk, uColDark, 0.82),
        smoothstep(0.10, 1.0, rN));

    //====盘心浅凹:心里沉一层,凹唇一线回光(车出来的凹面)====
    float dishDark = exp(-pow(r / max(uSlotR * 0.55, 1.0), 2.0));
    col *= 1.0 - dishDark * 0.10;
    float dishLip = exp(-pow((r - uSlotR * 0.62) / max(uDiscR * 0.012, 1.0), 2.0));
    col += uColPaper * dishLip * 0.020;

    //====轆轤旋纹:同心细纹随噪声游移,漆厚处若隐 + 细鑢颗粒====
    float latheWave = valueNoise(float2(r * 0.020, uSeed * 3.0)) * 9.0;
    float lathe = sin(r * 1.05 + latheWave + uSeed * 29.0);
    col *= 1.0 + lathe * 0.030;
    float grain = valueNoise(px * 0.55 + uSeed * 11.0);
    col *= 1.0 + (grain - 0.5) * 0.05;

    //====urushi 漆光:上带承光缓呼吸 + 巡缘光斑慢转====
    float breath = 0.9 + 0.1 * sin(uTime * 0.8 + uSeed);
    float topBand = exp(-pow((p.y + uDiscR * 0.58) / (uDiscR * 0.34), 2.0));
    col += uColPaper * topBand * 0.035 * breath;
    float2 swDir = float2(cos(uTime * 0.10 + uSeed), sin(uTime * 0.10 + uSeed));
    float toward = dot(p / max(r, 0.001), swDir);
    float rimBand = exp(-pow((rN - 0.87) / 0.055, 2.0));
    col += uColPaper * smoothstep(0.86, 1.0, toward) * rimBand * 0.085;

    //====纹样坐标:盘体摆角只带动蒔絵与眠焰(光照不跟着转)====
    float cs = cos(uRot);
    float sn = sin(uRot);
    float2 pr = float2(p.x * cs + p.y * sn, -p.x * sn + p.y * cs);

    //====蒔絵六芒暗纹:金粉沉在漆下,旧年结印的纹====
    float2 va0 = float2(0.0, -1.0) * uStarR;
    float2 va1 = float2(0.8660254, 0.5) * uStarR;
    float2 va2 = float2(-0.8660254, 0.5) * uStarR;
    float2 vb0 = float2(0.8660254, -0.5) * uStarR;
    float2 vb1 = float2(0.0, 1.0) * uStarR;
    float2 vb2 = float2(-0.8660254, -0.5) * uStarR;
    float dHex = segDist(pr, va0, va1);
    dHex = min(dHex, segDist(pr, va1, va2));
    dHex = min(dHex, segDist(pr, va2, va0));
    dHex = min(dHex, segDist(pr, vb0, vb1));
    dHex = min(dHex, segDist(pr, vb1, vb2));
    dHex = min(dHex, segDist(pr, vb2, vb0));
    float hexW = max(uDiscR * 0.016, 1.2);
    float hexT = exp(-pow(dHex / hexW, 2.0));
    col += uColGoldDeep * hexT * 0.20 + uColGold * hexT * 0.05;

    //====蒔絵金尘:稀疏细点,中带低闪====
    float dust = valueNoise(px * 0.9 + uSeed * 23.0);
    float dustGate = smoothstep(0.955, 0.985, dust)
        * smoothstep(0.28, 0.50, rN) * (1.0 - smoothstep(0.78, 0.94, rN));
    float tw = 0.6 + 0.4 * sin(uTime * 2.3 + px.x * 0.7 + px.y);
    col += uColGold * dustGate * 0.42 * tw;

    //====金压线环,内衬一线绯====
    float ringW = max(uDiscR * 0.006, 0.9);
    float goldRing = exp(-pow((r - uDiscR * 0.945) / ringW, 2.0));
    col += uColGold * goldRing * 0.40 + uColGoldDeep * goldRing * 0.30;
    float crimRing = exp(-pow((r - uDiscR * 0.905) / max(uDiscR * 0.005, 0.8), 2.0));
    col = lerp(col, uColDeep * 0.72, crimRing * 0.45);

    //====三结印位鬼火眠焰:占位的鬼在漆下呼吸,将醒时焰转绯脉更急====
    float2 s0 = float2(0.0, -1.0) * uSlotR;
    float2 s1 = float2(0.8660254, 0.5) * uSlotR;
    float2 s2 = float2(-0.8660254, 0.5) * uSlotR;
    float sig = max(uSlotR * 0.55, 4.0);
    float d0 = length(pr - s0);
    float d1 = length(pr - s1);
    float d2 = length(pr - s2);
    float br0 = 0.5 + 0.5 * sin(uTime * 1.25);
    float br1 = 0.5 + 0.5 * sin(uTime * 1.25 + 2.1);
    float br2 = 0.5 + 0.5 * sin(uTime * 1.25 + 4.2);
    float h0 = exp(-pow(d0 / sig, 2.0));
    float h1 = exp(-pow(d1 / sig, 2.0));
    float h2 = exp(-pow(d2 / sig, 2.0));
    col += uColGhostDim * (h0 * uSlotLit.x * (0.10 + 0.08 * br0)
        + h1 * uSlotLit.y * (0.10 + 0.08 * br1)
        + h2 * uSlotLit.z * (0.10 + 0.08 * br2));
    float sigC = sig * 0.45;
    col += uColGhost * (exp(-pow(d0 / sigC, 2.0)) * uSlotLit.x * br0
        + exp(-pow(d1 / sigC, 2.0)) * uSlotLit.y * br1
        + exp(-pow(d2 / sigC, 2.0)) * uSlotLit.z * br2) * 0.055;
    col += uColDeep * (h0 * uSlotDanger.x * (0.5 + 0.5 * sin(uTime * 3.2))
        + h1 * uSlotDanger.y * (0.5 + 0.5 * sin(uTime * 3.2 + 2.1))
        + h2 * uSlotDanger.z * (0.5 + 0.5 * sin(uTime * 3.2 + 4.2))) * 0.16;

    //====合鬼暖芯:三印齐时盘心一点暖光呼吸====
    col += uColCandle * uComplete * exp(-pow(r / max(uSlotR * 0.5, 2.0), 2.0))
        * (0.10 + 0.06 * sin(uTime * 1.8));

    //====底缘烛光暖染(烛在屏下) + 边缘炭沉====
    float flick = 0.86 + 0.10 * sin(uTime * 2.1) + 0.04 * sin(uTime * 7.3 + 1.7);
    float bottomT = saturate(p.y / uDiscR) * smoothstep(0.55, 1.0, rN);
    col += uColCandle * bottomT * 0.10 * flick;
    col += uColBurnDim * bottomT * bottomT * 0.05 * flick;
    float edgeT = 1.0 - smoothstep(-max(uDiscR * 0.02, 2.5), -0.5, sdf);
    col = lerp(col, uColInk * 0.42, edgeT * 0.5);

    float a = body * 0.985;
    return float4(col * a, a) * uAlpha * vertexColor;
}

technique TechDisc
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSDisc();
    }
}
