// ============================================================================
//OniMeiStand.fx 改铭台的台面质感,两个 technique:
//  TechCloth——解剑白布:织纹经纬+纵向折痕明暗+绫边压线(深红+金丝)+
//    底缘烛光暖染(低频摇曳)+两端布边噪声撕散,取代 CPU 平铺色块
//  TechWood——烙印木牌:手裁木板轮廓(SDF 边缘蛀蚀+缺角)+年轮/细鑢木纹+
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

// ============================ TechCloth ============================

float4 PSCloth(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float2 uv = coords;

    //====两端布边:噪声撕散,不给整齐的裁切线====
    float endN = valueNoise(float2(px.y * 0.06, uSeed * 3.1)) * 12.0;
    float endDist = min(px.x, uResolution.x - px.x);
    float endMask = smoothstep(2.0, 10.0 + endN, endDist);
    //上下缘 1.5px 软化
    float edgeDist = min(px.y, uResolution.y - px.y);
    float edgeMask = smoothstep(0.0, 1.8, edgeDist);
    float mask = endMask * edgeMask;
    if (mask <= 0.004) {
        return float4(0, 0, 0, 0);
    }

    //====布底:烛光自下,三段过渡揉成连续渐变====
    float3 clothTop = float3(0.170, 0.148, 0.132);
    float3 clothLow = float3(0.268, 0.222, 0.182);
    float3 col = lerp(clothTop, clothLow, uv.y * uv.y * (3.0 - 2.0 * uv.y));

    //====织纹:经纬十字细纹 + 布料杂色====
    float weaveX = sin(px.x * 1.85 + valueNoise(px * 0.11 + uSeed) * 2.4);
    float weaveY = sin(px.y * 1.85 + valueNoise(px * 0.11 + uSeed * 2.0) * 2.4);
    col *= 1.0 + (weaveX * weaveY) * 0.030;
    col *= 0.94 + fbm2(px * 0.16 + uSeed * 7.0) * 0.12;

    //====纵向折痕:低频折面明暗,折脊接一线高光,烛光里微息====
    float foldT = uv.x * 8.5 + uSeed;
    float fold = fbm2(float2(foldT, 0.37));
    float breath = 1.0 + sin(uTime * 0.9 + fold * 9.0) * 0.03;
    col *= (0.86 + fold * 0.24) * breath;
    float ridge = exp(-pow(frac(foldT * 0.5) - 0.5, 2.0) * 34.0);
    col += float3(0.10, 0.086, 0.070) * ridge * fold * 0.5;

    //====绫边:上下缘深红压线,内侧一根金丝====
    float selvTop = 1.0 - smoothstep(2.0, 4.5, px.y);
    float selvBot = smoothstep(uResolution.y - 4.5, uResolution.y - 2.0, px.y);
    float selv = max(selvTop, selvBot);
    col = lerp(col, uColDeep * 0.62, selv * 0.85);
    float goldTop = exp(-pow(px.y - 7.5, 2.0) * 0.55);
    float goldBot = exp(-pow(px.y - (uResolution.y - 7.5), 2.0) * 0.55);
    col += uColGoldDeep * (goldTop + goldBot) * 0.35;

    //====烛光暖染:底缘涌上的暖,低频摇曳;顶缘微沉====
    float flick = 0.86 + 0.10 * sin(uTime * 2.1) + 0.04 * sin(uTime * 7.3 + 1.7);
    col += uColCandle * exp(-(1.0 - uv.y) * 3.4) * 0.16 * flick;
    col *= 1.0 - (1.0 - uv.y) * 0.10;

    //====布上的旧墨渍:两三处极淡的洇痕(台子用过很多年)====
    float stain = fbm2(px * 0.021 + uSeed * 13.0);
    col *= 1.0 - smoothstep(0.62, 0.86, stain) * 0.14;

    float a = mask * 0.965;
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

technique TechCloth
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCloth();
    }
}

technique TechWood
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSWood();
    }
}
