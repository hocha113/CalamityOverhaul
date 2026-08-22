// ============================================================================
//KikasaPanorama.fx 湖心景：鬼伞主界面的全屏血湖夜景剖面。
//上带夜空（竖向层次+两层缓涌云带+鬼雨斜雨丝）、左岸礁（恶犬的立足处，
//噪声起伏的近黑岩体）、水线（泡沫缝线亮度吃 uVigor 湖力、uWisp 鬼火沿线
//燃起金焰舌，金=鬼火身份色不随雨浸染）、水下（深浅双频横流+近水焦散+
//烬萤浮游 uLightGate=湖藏填充率）、干湖（uDry 龟裂湖床，与湖畔村图同配方）。
//uRain 在血湖族与鬼雨族之间整画浸染。色板与 KikasaScene/KikasaHudTheme 同源。
//屏角轻渐晕收住画面。预乘输出。s0=白像素 s1=PerlinNoise（实测值域 0.22~0.776，
//高分位阈值一律 ≤0.74）。全笛卡尔无 atan2，直线算术无动态分支
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;         //秒
float uAlpha;        //整体透明度
float2 uResolution;  //quad 像素尺寸
float uWaterY;       //水面 uv.y（0.40=满水，0.88=湖空）
float uDry;          //0~1 干涸度（龟裂强度，1-RiseT）
float uRain;         //0~1 鬼雨浸染
float uStir;         //0~1 水面活性
float uVigor;        //0~1 湖力（水线缝光与浅水辉随它走）
float uWisp;         //0~1 鬼火燃势（水线金焰带）
float uLightGate;    //0~1 烬萤稠度（湖藏填充率）

//====== 血湖族（暮红，与 KikasaScene 同源） ======
static const float3 SKY_TOP_B   = float3(0.052, 0.008, 0.013);
static const float3 SKY_MID_B   = float3(0.270, 0.042, 0.040);
static const float3 HORIZON_B   = float3(0.560, 0.118, 0.055);
static const float3 CLOUD_DK_B  = float3(0.085, 0.012, 0.018);
static const float3 CLOUD_RIM_B = float3(0.640, 0.160, 0.070);
static const float3 EMBER_B     = float3(0.950, 0.340, 0.140);
static const float3 FOG_B       = float3(0.150, 0.026, 0.026);
static const float3 WATER_HI_B  = float3(0.150, 0.028, 0.038);
static const float3 WATER_LO_B  = float3(0.046, 0.008, 0.013);
static const float3 TINT_B      = float3(0.930, 0.300, 0.270);
static const float3 FOAM_B      = float3(0.965, 0.520, 0.440);
static const float3 BED_B       = float3(0.108, 0.052, 0.040);
static const float3 SHORE_B     = float3(0.034, 0.008, 0.012);
//====== 鬼雨族（墨青，禁红禁暖） ======
static const float3 SKY_TOP_R   = float3(0.020, 0.026, 0.032);
static const float3 SKY_MID_R   = float3(0.085, 0.105, 0.115);
static const float3 HORIZON_R   = float3(0.225, 0.262, 0.268);
static const float3 CLOUD_DK_R  = float3(0.050, 0.060, 0.070);
static const float3 CLOUD_RIM_R = float3(0.180, 0.212, 0.218);
static const float3 EMBER_R     = float3(0.620, 0.670, 0.680);
static const float3 FOG_R       = float3(0.140, 0.170, 0.180);
static const float3 WATER_HI_R  = float3(0.055, 0.072, 0.082);
static const float3 WATER_LO_R  = float3(0.020, 0.027, 0.034);
static const float3 TINT_R      = float3(0.300, 0.345, 0.355);
static const float3 FOAM_R      = float3(0.620, 0.670, 0.680);
static const float3 BED_R       = float3(0.060, 0.072, 0.078);
static const float3 SHORE_R     = float3(0.014, 0.020, 0.026);
//====== 鬼火金（身份色，不随雨浸染，与 KikasaWisp CPU 色同源） ======
static const float3 GOLD_CORE = float3(1.000, 0.925, 0.660);
static const float3 GOLD_BODY = float3(1.000, 0.730, 0.260);
static const float3 GOLD_TIP  = float3(0.847, 0.424, 0.118);

//满水位 uv.y：必须与 KikasaPanoramaTheme.WaterFullUv 一致
static const float WATER_FULL_Y = 0.40;

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float4 PSLake(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float aspect = uResolution.x / max(uResolution.y, 1.0);
    float ux = uv.x * aspect;

    //====== 双形态色板 ======
    float3 skyTop = lerp(SKY_TOP_B, SKY_TOP_R, uRain);
    float3 skyMid = lerp(SKY_MID_B, SKY_MID_R, uRain);
    float3 horizonC = lerp(HORIZON_B, HORIZON_R, uRain);
    float3 cloudDk = lerp(CLOUD_DK_B, CLOUD_DK_R, uRain);
    float3 cloudRim = lerp(CLOUD_RIM_B, CLOUD_RIM_R, uRain);
    float3 ember = lerp(EMBER_B, EMBER_R, uRain);
    float3 fog = lerp(FOG_B, FOG_R, uRain);
    float3 waterHi = lerp(WATER_HI_B, WATER_HI_R, uRain);
    float3 waterLo = lerp(WATER_LO_B, WATER_LO_R, uRain);
    float3 tint = lerp(TINT_B, TINT_R, uRain);
    float3 foam = lerp(FOAM_B, FOAM_R, uRain);
    float3 bed = lerp(BED_B, BED_R, uRain);
    float3 shore = lerp(SHORE_B, SHORE_R, uRain);

    //====== 夜空：竖向层次 + 满水线上方的地平辉 ======
    float3 col = lerp(skyTop, skyMid, smoothstep(0.0, WATER_FULL_Y * 1.25, uv.y));
    float hGlow = exp2(-abs(uv.y - WATER_FULL_Y) * 10.0);
    col = lerp(col, horizonC, hGlow * 0.55);

    //====== 凝血日/溺月：右上一轮噪蚀暗盘，画在云带之前让云吞它 ======
    float2 sunP = float2(ux - aspect * 0.74, uv.y - 0.15);
    float sunD = length(sunP);
    float sunEat = noiseTex(float2(sunP.x * 2.0 + 0.31, sunP.y * 2.0 + 0.77));
    float sunDisc = smoothstep(0.064, 0.050, sunD + (sunEat - 0.5) * 0.022);
    float sunHalo = exp2(-sunD * 22.0);
    float3 sunCol = lerp(HORIZON_B * 1.25, float3(0.56, 0.61, 0.62), uRain);
    col += sunCol * (sunDisc * 0.55 + sunHalo * 0.26);

    //====== 云带：两层反向缓涌 + 云底烬缘 ======
    float c0 = noiseTex(float2(ux * 0.30 + uTime * 0.0080, uv.y * 1.5 + 0.13));
    float c1 = noiseTex(float2(ux * 0.66 - uTime * 0.0121, uv.y * 2.3 + 0.57));
    float cloud = saturate((c0 * 0.62 + c1 * 0.38 - 0.42) * 2.6)
        * smoothstep(WATER_FULL_Y + 0.02, WATER_FULL_Y - 0.34, uv.y);
    col = lerp(col, cloudDk, cloud * 0.72);
    float rim = saturate((c0 - 0.52) * 5.0) * exp2(-abs(uv.y - WATER_FULL_Y + 0.10) * 12.0);
    col += cloudRim * rim * 0.16;

    //====== 鬼雨斜雨丝：只在雨浸染里落，一路落到水线 ======
    float rainN = noiseTex(float2(ux * 2.6 + uv.y * 0.9 + uTime * 0.02,
        uv.y * 5.5 - uTime * 0.55));
    float rainStreak = saturate((rainN - 0.60) * 7.0)
        * smoothstep(uWaterY + 0.01, uWaterY - 0.08, uv.y) * uRain;
    col += fog * rainStreak * 1.5;

    //====== 远场烬点（天空里缓漂的村火余烬） ======
    float mote = noiseTex(float2(ux * 2.7 + uTime * 0.006, uv.y * 3.1 + uTime * 0.030));
    float spark = saturate((mote - 0.72) * 10.0)
        * smoothstep(WATER_FULL_Y + 0.06, WATER_FULL_Y - 0.30, uv.y);
    col += ember * spark * 0.10;

    //====== 湖床（满水线以下的地）：泥色 + 龟裂随干涸，裂缝里透残血烬光 ======
    float bedT = smoothstep(WATER_FULL_Y - 0.005, WATER_FULL_Y + 0.03, uv.y);
    float3 bedCol = bed * (0.9 + 0.2 * noiseTex(float2(ux * 2.2, uv.y * 5.0)));
    float k0 = noiseTex(float2(ux * 2.6, uv.y * 7.5));
    float k1 = noiseTex(float2(ux * 5.1 + 3.7, uv.y * 13.0));
    float crack = exp2(-abs(k0 - 0.5) * 30.0) + exp2(-abs(k1 - 0.5) * 34.0);
    bedCol *= 1.0 - saturate(crack) * 0.45 * uDry;
    bedCol += ember * saturate(crack * 0.8 - 0.4) * uDry * 0.16;
    bedCol *= lerp(1.0, 0.55, smoothstep(WATER_FULL_Y, 1.0, uv.y));
    col = lerp(col, bedCol, bedT);

    //====== 血湖水体（uWaterY 以下淹没湖床） ======
    float rel = uv.y - uWaterY;
    float depth = saturate(rel / max(1.0 - uWaterY, 0.001));
    float3 water = lerp(waterHi, waterLo, depth);
    float f0 = noiseTex(float2(ux * 1.1 - uTime * 0.016, uv.y * 2.4));
    float f1 = noiseTex(float2(ux * 2.3 + uTime * 0.011, uv.y * 4.6 + 3.7));
    float flowAmp = 0.20 + 0.14 * uStir;
    water += tint * flowAmp * (f0 * 0.62 + f1 * 0.48) * (1.0 - depth * 0.55);
    //近水面焦散
    float caus = pow(noiseTex(float2(ux * 3.4 - uTime * 0.022, uv.y * 9.0)), 6.0)
        * exp2(-max(rel, 0.0) * 11.0);
    water += foam * caus * (0.26 + 0.18 * uVigor);
    //烬萤浮游：湖藏越满，水里漂的村火越稠，储物的呼吸读数
    float fly = noiseTex(float2(ux * 3.4 + uTime * 0.018, uv.y * 4.4 - uTime * 0.026));
    water += ember * saturate((fly - 0.72) * 9.0) * uLightGate * 0.9
        * (1.0 - depth * 0.4);
    //湖力浅水辉：湖力越满，浅水一层暖光越亮
    water += tint * exp2(-max(rel, 0.0) * 7.0) * uVigor * 0.10;

    //====== 泡沫缝线：亮度吃湖力，一条水线读出这汪湖还有多少力气 ======
    float wob = (noiseTex(float2(ux * 2.8 - uTime * 0.020, 0.71)) - 0.5)
        * (0.008 + 0.014 * uStir);
    float seamD = rel + wob;
    float seamBand = exp2(-abs(seamD) * (180.0 - 60.0 * saturate(uStir)));
    float flicker = noiseTex(float2(ux * 3.6 - uTime * 0.05, 0.41));
    float3 seam = foam * seamBand
        * (0.22 + 0.30 * flicker * uVigor + 0.30 * uVigor + 0.22 * uStir);

    float toWater = saturate(seamD * 240.0) * step(WATER_FULL_Y - 0.02, uv.y);
    col = lerp(col, water, toWater);
    col += seam * step(WATER_FULL_Y - 0.02, uv.y);

    //====== 鬼火金焰带：燃势起时水线上方撕出金焰舌，根实尖碎 ======
    float above = uWaterY - uv.y;
    float flameH = 0.030 + 0.028 * uWisp;
    float tongueN = noiseTex(float2(ux * 4.2 - uTime * 0.05, uv.y * 6.0 + uTime * 0.16));
    //阈值随高度抬升：贴线连成床，愈高愈碎（0.36~0.70 全在实测值域内）
    float tongue = saturate((tongueN - lerp(0.36, 0.70, saturate(above / flameH))) * 6.0)
        * saturate(above / 0.004) * step(0.0, above)
        * saturate(1.0 - above / flameH) * uWisp;
    float3 gold = lerp(GOLD_BODY, GOLD_TIP, saturate(above / flameH));
    col += gold * tongue * 0.85;
    col += GOLD_CORE * tongue * tongue * 0.35;
    //金焰在水下的映光
    col += GOLD_BODY * exp2(-max(rel, 0.0) * 16.0) * step(0.0, rel) * uWisp * 0.14;

    //====== 左岸礁：贴着满水线的岩架，右缘噪声撕、探进水下即隐，不做通底黑柱 ======
    float shoreProfile = WATER_FULL_Y - 0.014
        + (noiseTex(float2(ux * 1.9 + 0.37, 0.19)) - 0.5) * 0.02
        - smoothstep(0.30, 0.13, ux) * 0.012;
    float shoreEdgeN = (noiseTex(float2(uv.y * 3.7 + 0.53, 0.67)) - 0.5) * 0.06;
    float shoreMask = step(shoreProfile, uv.y)
        * smoothstep(0.30, 0.20, ux + shoreEdgeN)
        * saturate(1.0 - (uv.y - WATER_FULL_Y) / 0.14);
    float shoreShade = 0.85 + 0.3 * noiseTex(float2(ux * 3.3, uv.y * 6.0));
    col = lerp(col, shore * shoreShade, saturate(shoreMask));
    //岸缘一线受光
    float shoreEdge = exp2(-abs(uv.y - shoreProfile) * 130.0)
        * smoothstep(0.30, 0.20, ux + shoreEdgeN);
    col += tint * shoreEdge * 0.22;

    //====== 屏角渐晕：把画面收进夜里 ======
    float2 vc = uv - 0.5;
    float vig = saturate(dot(vc, vc) * 1.15);
    col *= 1.0 - vig * 0.38;

    float aOut = uAlpha;
    return float4(col * aOut, aOut);
}

technique TechLake {
    pass P0 {
        PixelShader = compile ps_3_0 PSLake();
    }
}
