// ============================================================================
//OniInkWipe.fx 双屏换乘的墨扫转场:
//一笔浓墨自行进方向扫入盖屏,再顺势扫出揭开新屏——盖/揭两道前沿共用毛边噪声,
//前沿带笔锋亮线与飞白破口,墨体内有顺笔向的刷丝与纸理;
//uProgress 0~0.45 盖屏,0.52~1 揭屏,当中一拍全盖(屏体在此交接)
//uDir=+1 自东(右)扫入(东去改铭台),-1 自西扫入;AlphaBlend 预乘输出
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uProgress;      //0~1 换乘进度
float uDir;           //+1 东去 / -1 西回
float uSeed;
float2 uResolution;
float3 uColInk;
float3 uColDark;
float3 uColDeep;
float3 uColBright;
float3 uColHot;

#define PI 3.14159265

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

float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.13 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //s: 距入边归一距离(入边=行进方向那一侧)
    float s = uDir > 0.0 ? 1.0 - coords.x : coords.x;
    float aspect = uResolution.x / max(uResolution.y, 1.0);

    //盖/揭前沿(平滑推进,越界余量吃掉毛边)
    float cover = smoothstep(0.0, 0.45, uProgress) * 1.10;
    float reveal = smoothstep(0.52, 1.0, uProgress) * 1.10;

    //毛边:低频摆 + 高频纤维;揭沿换相位,别与盖沿同形
    float edgeLo = (valueNoise(float2(coords.y * 5.0 + uSeed * 7.0, uSeed)) - 0.5) * 0.11;
    float edgeHi = (valueNoise(float2(coords.y * 60.0, uSeed * 3.0)) - 0.5) * 0.028;
    float edgeC = edgeLo + edgeHi;
    float edgeR = (valueNoise(float2(coords.y * 5.0 + uSeed * 13.0, uSeed + 40.0)) - 0.5) * 0.10
        + (valueNoise(float2(coords.y * 52.0, uSeed * 5.0)) - 0.5) * 0.026;

    float coverM = 1.0 - smoothstep(cover + edgeC - 0.022, cover + edgeC + 0.022, s);
    float revealM = 1.0 - smoothstep(reveal + edgeR - 0.020, reveal + edgeR + 0.020, s);
    float mask = coverM * (1.0 - revealM);
    if (mask <= 0.003) {
        return float4(0, 0, 0, 0);
    }

    //====墨体:顺笔向刷丝(沿 x 拉长的噪声) + 纸理颗粒 + 深红洇斑====
    float2 pud = float2(coords.x * aspect, coords.y);
    float streak = valueNoise(float2(pud.x * 2.2 - uTime * 0.10, pud.y * 26.0 + uSeed));
    float grain = valueNoise(pud * 90.0 + uSeed * 17.0);
    float blotch = fbm3(pud * 2.6 + uSeed * 3.1);
    float3 body = lerp(uColInk * 0.86, uColDark * 0.9, saturate(blotch * 0.8));
    body = lerp(body, uColDeep * 0.55, smoothstep(0.62, 0.9, blotch) * 0.5);
    body *= 0.90 + (streak - 0.5) * 0.22 + (grain - 0.5) * 0.07;

    //飞白破口:靠近两道前沿处,刷丝干裂露底
    float nearCover = 1.0 - smoothstep(0.0, 0.16, cover + edgeC - s);
    float nearReveal = 1.0 - smoothstep(0.0, 0.13, s - (reveal + edgeR));
    float dry = smoothstep(0.52, 0.80, streak) * max(nearCover * 0.85, nearReveal * 0.9);
    float bodyA = mask * 0.985 * (1.0 - dry);

    //====笔锋:盖沿一线白热压绯红,揭沿一线深红余温====
    float dC = abs(s - (cover + edgeC));
    float frontA = exp(-dC * dC * 2600.0) * step(0.002, cover) * (1.0 - smoothstep(0.92, 1.06, cover));
    float3 frontCol = lerp(uColBright, uColHot, 0.45);
    float dR = abs(s - (reveal + edgeR));
    float tailA = exp(-dR * dR * 3200.0) * step(0.002, reveal) * (1.0 - smoothstep(0.92, 1.06, reveal)) * 0.55;

    //盖沿前方的先行墨点(溅出的碎笔)
    float splat = smoothstep(0.78, 0.96, valueNoise(float2(coords.y * 34.0 + uSeed * 23.0, floor((s - cover) * 60.0))));
    float splatA = splat * exp(-max(s - cover, 0.0) * 26.0) * step(s, cover + 0.10) * (1.0 - coverM) * 0.8;

    //====预乘合成====
    float3 C = body * bodyA;
    float A = bodyA;
    C += uColInk * splatA * 0.9;
    A = splatA + A * (1.0 - splatA);
    C += frontCol * frontA * mask;
    C += uColDeep * tailA * mask;

    return float4(C, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniInkWipePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
