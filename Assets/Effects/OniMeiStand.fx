// ============================================================================
//OniMeiStand.fx 改铭台的台面质感,两个 technique:
//  TechLacquer：刀掛黑漆底板:SDF 长板轮廓(边沿细噪蚀)+漆下木理+
//    urushi 承光带与缓移漆光+蒔絵金尘低闪+上缘金压线衬绯线+
//    端头断口沉色+底缘烛光暖染,鬼切的黑绯配色落在台面上
//  TechWood：烙印木牌:手裁木板轮廓(SDF 边缘蛀蚀+缺角)+年轮/细鑢木纹+
//    漆色纵深+焦边炭圈(烛下微燃)+穿绳孔+缓移油光,取代矩形盒
//全笛卡尔坐标无极角;恒定 uSeed 形状稳定;预乘 alpha 配 AlphaBlend;
//色板 CPU 传入与 OnikiriUITheme 同源
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;   //quad 像素尺寸
float uSeed;
float3 uColInk;       //墨黑
float3 uColPaper;     //纸白
float3 uColDeep;      //深红
float3 uColDark;      //暗酒红
float3 uColCandle;    //烛焰暖
float3 uColGold;      //金象嵌亮
float3 uColGoldDeep;  //金象嵌暗
float3 uColBurnDim;   //焚烧暗橙

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

//双八度,布纹/木纹共用
float fbm2(float2 p) {
    return valueNoise(p) * 0.62 + valueNoise(p * 2.31 + float2(3.7, 7.1)) * 0.38;
}

// ============================ TechLacquer ============================

float4 PSLacquer(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float2 uv = coords;

    //====板形 SDF:低圆角长板,边沿细噪蚀(手作漆器,不给尺规直角)====
    float wob = (fbm2(px * 0.05 + uSeed * 5.0) - 0.5) * 3.2;
    float2 center = uResolution * 0.5;
    float2 halfS = center - float2(3.0, 2.5);
    float2 d2 = abs(px - center) - halfS + wob;
    float sdf = length(max(d2, 0.0)) + min(max(d2.x, d2.y), 0.0) - 4.0;
    float body = 1.0 - smoothstep(-0.9, 0.9, sdf);
    if (body <= 0.004) {
        return float4(0, 0, 0, 0);
    }

    //====漆底:近墨深漆微透红,顶面最沉,下缘承烛光回暖====
    float3 lacqTop = uColInk * 0.52;
    float3 lacqBot = lerp(uColInk, uColDark, 0.85);
    float3 col = lerp(lacqTop, lacqBot, smoothstep(0.0, 1.0, uv.y));

    //====漆下木理:横走的极淡纹,漆厚处若隐====
    float grain = valueNoise(px * float2(0.012, 0.30) + uSeed * 7.0);
    col *= 1.0 + (grain - 0.5) * 0.06;

    //====urushi 漆光:上缘承光带缓呼吸 + 一条极缓横移的窄光====
    float breath = 0.9 + 0.1 * sin(uTime * 0.8 + uSeed);
    col += uColPaper * exp(-pow(px.y - uResolution.y * 0.18, 2.0) * 0.006) * 0.030 * breath;
    float sheenT = frac(uTime * 0.02 + uSeed * 0.7);
    float sheenX = sheenT * (uResolution.x + 260.0) - 130.0;
    col += uColPaper * exp(-pow((px.x - sheenX) * 0.012, 2.0)) * exp(-uv.y * 2.2) * 0.05;

    //====蒔絵金尘:稀疏细点散在中带,烛光里低闪====
    float dust = valueNoise(px * 0.9 + uSeed * 23.0);
    float dustGate = smoothstep(0.955, 0.985, dust)
        * smoothstep(0.10, 0.32, uv.y) * (1.0 - smoothstep(0.72, 0.95, uv.y));
    float tw = 0.6 + 0.4 * sin(uTime * 2.3 + px.x * 0.7 + px.y);
    col += uColGold * dustGate * 0.5 * tw;

    //====上缘金压线,内衬一线绯红(台的绫边记忆,换了材质仍是这家的规矩)====
    float goldLine = exp(-pow(px.y - 2.6, 2.0) * 0.45);
    col += uColGold * goldLine * 0.5 + uColGoldDeep * goldLine * 0.35;
    col = lerp(col, uColDeep * 0.72, exp(-pow(px.y - 6.5, 2.0) * 0.35) * 0.55);

    //====端头断口:两端漆色深沉(端面吃不到光)====
    float endDist = min(px.x, uResolution.x - px.x);
    col *= lerp(0.72, 1.0, smoothstep(1.0, 14.0, endDist));

    //====底缘烛光暖染(低频摇曳) + 边缘炭沉====
    float flick = 0.86 + 0.10 * sin(uTime * 2.1) + 0.04 * sin(uTime * 7.3 + 1.7);
    col += uColCandle * exp(-(1.0 - uv.y) * 5.0) * 0.10 * flick;
    col += uColBurnDim * exp(-(1.0 - uv.y) * 9.0) * 0.05 * flick;
    float edgeT = 1.0 - smoothstep(-5.5, -0.5, sdf);
    col = lerp(col, uColInk * 0.42, edgeT * 0.5);

    float a = body * 0.985;
    return float4(col * a, a) * uAlpha * vertexColor;
}

// ============================ TechWood ============================

float4 PSWood(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float pad = 9.0;
    float2 center = uResolution * 0.5;
    float2 halfS = center - pad;

    //====板形 SDF:圆角矩形,边缘手裁蛀蚀,右上缺一角====
    float wob = (fbm2(px * 0.055 + uSeed * 5.0) - 0.5) * 6.5;
    float2 d2 = abs(px - center) - halfS + wob;
    float sdf = length(max(d2, 0.0)) + min(max(d2.x, d2.y), 0.0) - 5.0;
    //右上缺角:一口圆凿去(圆内为正=去料)
    float2 chipC = float2(uResolution.x - pad - 4.0, pad + 3.0);
    sdf = max(sdf, 14.0 - length(px - chipC));

    float body = 1.0 - smoothstep(-0.9, 0.9, sdf);
    if (body <= 0.004) {
        return float4(0, 0, 0, 0);
    }

    //====木纹:横走年轮(低频波带) + 细鑢直纹====
    float ringWave = valueNoise(float2(px.x * 0.016, px.y * 0.10) + uSeed * 3.0) * 13.0;
    float ring = sin(px.y * 0.62 + ringWave + uSeed * 29.0);
    float grain = valueNoise(px * float2(0.028, 0.55) + uSeed * 11.0);

    //====漆色:暗红棕漆,下缘沉,年轮浮出深浅====
    float3 col = lerp(float3(0.215, 0.083, 0.068), float3(0.150, 0.058, 0.052), coords.y);
    col *= 1.0 + ring * 0.055 + (grain - 0.5) * 0.10;

    //====焦边:边缘炭黑一圈,再往里一线焚橙余温(烛下微燃)====
    float edgeT = 1.0 - smoothstep(-10.0, -1.0, sdf);
    col = lerp(col, uColInk * 0.55, edgeT * 0.72);
    float emberFlick = 0.75 + 0.25 * sin(uTime * 2.7 + px.x * 0.05 + uSeed * 9.0);
    float emberBand = exp(-pow(sdf + 6.5, 2.0) * 0.09);
    col += uColBurnDim * emberBand * 0.10 * emberFlick;

    //====穿绳孔:左上一眼,孔缘下侧接光====
    float2 holeC = float2(pad + 11.0, pad + 9.0);
    float holeD = length(px - holeC);
    float hole = 1.0 - smoothstep(3.4, 5.2, holeD);
    col = lerp(col, uColInk * 0.30, hole);
    col += uColCandle * exp(-pow(holeD - 5.6, 2.0) * 0.30) * saturate((px.y - holeC.y) / 5.0) * 0.20;

    //====油光:一条极缓的斜光带擦过漆面====
    float sheenT = frac(uTime * 0.028 + uSeed * 0.41);
    float sheenPos = (px.x + px.y * 0.42) / (uResolution.x + uResolution.y * 0.42);
    col += uColCandle * exp(-pow((sheenPos - sheenT) * 9.0, 2.0)) * 0.045;

    //====底缘烛光暖染====
    col += uColCandle * exp(-(uResolution.y - px.y) * 0.030) * 0.085;

    float a = body * 0.975 * (1.0 - hole * 0.85);
    return float4(col * a, a) * uAlpha * vertexColor;
}

technique TechLacquer
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSLacquer();
    }
}

technique TechWood
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSWood();
    }
}
