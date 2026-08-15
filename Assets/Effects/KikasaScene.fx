// ============================================================================
//KikasaScene.fx 湖畔村图：鬼伞主界面的活画。红黑穹顶 + 缓涌云带 + 两层程序化
//村落剪影（villageRow 改造自 KikasaDreamSky——民居/望楼/枯树抽签、出檐坡脊、
//窗火炊烟、记忆微颤；去视差改画内固定构图）+ 岸线以下的血湖水带
//（深浅双频横流/焦散/泡沫缝线/沸腾，与湖窗同族）+ 干湖龟裂湖床。
//uLightGate=湖藏填充率：空仓全村无灯，满仓灯火通明。
//uRain 在血湖族与鬼雨族（KikasaSky RAIN_* 同源）之间整画浸染。
//外形＝微噪蚀圆角画心；预乘输出。s0=白像素 s1=PerlinNoise
//TechCard：湿纸引导卡底（自旧 KikasaHud.fx 迁入，配方不变）。
//TechChime：掌中风铃 HUD 的玻璃铃身（铃内盛血湖，uWaterY 复用为液面充盈度）。
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;         //秒
float uAlpha;        //整体透明度
float2 uResolution;  //quad 像素尺寸
float uWaterY;       //水面 uv.y（0.63=满水贴岸线，>0.94=湖空）
float uDry;          //0~1 干涸度（龟裂强度，1-RiseT）
float uRain;         //0~1 鬼雨浸染
float uStir;         //0~1 水面活性
float uBoil;         //0~1 翻转沸腾
float uFlash;        //0~1 结算白闪
float uLightGate;    //0~1 村中窗火开度（湖藏填充率）
float uTear;         //TechCard 用：撕开揭示
float uSwing;        //TechChime 用：当前摆角（弧度，液面反向找平）

//====== 血湖族（暮红） ======
static const float3 SKY_TOP_B   = float3(0.052, 0.008, 0.013);
static const float3 SKY_MID_B   = float3(0.270, 0.042, 0.040);
static const float3 HORIZON_B   = float3(0.560, 0.118, 0.055);
static const float3 CLOUD_DK_B  = float3(0.085, 0.012, 0.018);
static const float3 CLOUD_RIM_B = float3(0.640, 0.160, 0.070);
static const float3 SIL_FAR_B   = float3(0.092, 0.022, 0.026);
static const float3 SIL_NEAR_B  = float3(0.030, 0.007, 0.011);
static const float3 EMBER_B     = float3(0.950, 0.340, 0.140);
static const float3 FOG_B       = float3(0.150, 0.026, 0.026);
static const float3 WATER_HI_B  = float3(0.150, 0.028, 0.038);
static const float3 WATER_LO_B  = float3(0.046, 0.008, 0.013);
static const float3 TINT_B      = float3(0.930, 0.300, 0.270);
static const float3 FOAM_B      = float3(0.965, 0.520, 0.440);
static const float3 BED_B       = float3(0.108, 0.052, 0.040);
//====== 鬼雨族（墨青，禁红禁暖） ======
static const float3 SKY_TOP_R   = float3(0.020, 0.026, 0.032);
static const float3 SKY_MID_R   = float3(0.085, 0.105, 0.115);
static const float3 HORIZON_R   = float3(0.225, 0.262, 0.268);
static const float3 CLOUD_DK_R  = float3(0.050, 0.060, 0.070);
static const float3 CLOUD_RIM_R = float3(0.180, 0.212, 0.218);
static const float3 SIL_FAR_R   = float3(0.052, 0.066, 0.074);
static const float3 SIL_NEAR_R  = float3(0.016, 0.022, 0.028);
static const float3 EMBER_R     = float3(0.620, 0.670, 0.680);
static const float3 FOG_R       = float3(0.140, 0.170, 0.180);
static const float3 WATER_HI_R  = float3(0.055, 0.072, 0.082);
static const float3 WATER_LO_R  = float3(0.020, 0.027, 0.034);
static const float3 TINT_R      = float3(0.300, 0.345, 0.355);
static const float3 FOAM_R      = float3(0.620, 0.670, 0.680);
static const float3 BED_R       = float3(0.060, 0.072, 0.078);
//湿纸
static const float3 FIBER_COL   = float3(0.880, 0.795, 0.690);
static const float3 PAPER_BASE  = float3(0.052, 0.028, 0.024);
//画内构图常量：岸线（湖的上缘）
static const float SHORE_Y = 0.63;

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float hash1(float cell, float seed) {
    return tex2D(uImage1, float2(cell * 0.0371 + seed * 0.1130, seed * 0.0713 + 0.317)).r;
}

//一排村落：连续起伏地面垫底，每格抽签——空地/枯树/望楼/民居；
//民居出檐坡脊，望楼窄高脊陡，枯树团冠噪声啃蚀；窗火受 lightGate 总闸。
//返回 x=剪影 y=窗火 z=炊烟（自 KikasaDreamSky.villageRow 改造）
float3 villageRow(float x, float y, float baseY, float rollAmp, float seedRow, float lightGate) {
    float cell = floor(x);
    float fx = frac(x) - 0.5;

    float gCont = baseY + (noiseTex(float2(x * 0.047 + seedRow * 0.31, 0.23)) - 0.5) * rollAmp;
    float gBase = baseY + (noiseTex(float2((cell + 0.5) * 0.047 + seedRow * 0.31, 0.23)) - 0.5) * rollAmp;

    float h1 = hash1(cell, seedRow);
    float h2 = hash1(cell, seedRow + 7.0);
    float h3 = hash1(cell, seedRow + 13.0);
    float h4 = hash1(cell, seedRow + 23.0);

    float sil = step(gCont, y);

    float isTree = step(0.14, h4) * step(h4, 0.30);
    float isTower = step(0.30, h4) * step(h4, 0.40);
    float isHut = step(0.40, h4);

    //民居：身比檐窄，脊线下垂、檐口外挑
    float hutH = 0.028 + h1 * 0.034;
    float hutW = 0.125 + h2 * 0.135;
    float eave = 0.045 + h2 * 0.045;
    float roofH = 0.016 + h1 * 0.016;
    float roofSpan = hutW + eave;
    float top = gBase - hutH;
    float rr = saturate(abs(fx) / roofSpan);
    float roofLine = top - roofH * (1.0 - pow(rr, 1.45));
    float hutSil = saturate(
        step(abs(fx), roofSpan) * step(roofLine, y) * step(y, top + 0.006)
        + step(abs(fx), hutW) * step(top, y) * step(y, gBase + 0.02));
    sil = saturate(sil + hutSil * isHut);

    //望楼：窄高一柱
    float twH = 0.065 + h1 * 0.045;
    float twW = 0.042 + h2 * 0.028;
    float twTop = gBase - twH;
    float twRr = saturate(abs(fx) / (twW + 0.028));
    float twRoofLine = twTop - 0.030 * (1.0 - pow(twRr, 1.3));
    float twSil = saturate(
        step(abs(fx), twW + 0.028) * step(twRoofLine, y) * step(y, twTop + 0.005)
        + step(abs(fx), twW) * step(twTop, y) * step(y, gBase + 0.02));
    sil = saturate(sil + twSil * isTower);

    //枯村之树：双团冠 + 细干，冠缘噪声啃蚀
    float trH = 0.030 + h1 * 0.026;
    float2 c1 = float2(fx, y - (gBase - trH)) * float2(1.0, 1.6);
    float2 c2 = float2(fx - 0.10 + h2 * 0.20, y - (gBase - trH - 0.012)) * float2(1.0, 1.6);
    float blob = smoothstep(0.085, 0.055, length(c1))
        + smoothstep(0.062, 0.040, length(c2));
    float eaten = step(0.34, noiseTex(float2(cell * 0.171 + fx * 1.3, y * 9.0 + seedRow)));
    float trunk = step(abs(fx), 0.008) * step(gBase - trH, y) * step(y, gBase);
    sil = saturate(sil + (saturate(blob) * eaten + trunk) * isTree);

    //窗火：民居三成一格小窗，望楼顶窗常明；lightGate=湖藏总闸
    float wx = fx - (h2 - 0.5) * hutW;
    float wy = y - (top + hutH * 0.55);
    float win = step(abs(wx), 0.020) * step(abs(wy), 0.009) * step(0.72, h1) * isHut;
    float twWin = step(abs(fx), 0.014) * step(abs(y - (twTop + 0.014)), 0.008) * isTower;
    //各户点灯有先后：hash 门槛低的先亮，湖藏越满亮的越多
    float lightOrder = step(1.0 - lightGate, h3);
    float flicker = 0.30 + 0.70 * noiseTex(float2(cell * 0.131, uTime * 0.067 + seedRow));
    float light = (win + twWin) * flicker * lightOrder;

    //炊烟：两成人家一缕
    float smokeGate = step(0.80, h3) * isHut;
    float rise = saturate((top - y) * 6.5);
    float sway = (noiseTex(float2(cell * 0.37, y * 2.2 - uTime * 0.05)) - 0.5) * 0.10 * rise;
    float sx = fx - (h3 - 0.5) * hutW * 0.8 - sway;
    float smoke = exp2(-abs(sx) * 130.0 * (1.35 - rise * 0.95)) * smokeGate
        * rise * saturate(1.0 - rise * 0.80)
        * noiseTex(float2(cell * 0.53, y * 3.0 - uTime * 0.11));

    return float3(saturate(sil), light, smoke);
}

//画心遮罩：微噪蚀圆角矩形（画在湿纸上，边不锋利也不烂）
float2 canvasMask(float2 uv) {
    float2 pc = (uv - 0.5) * uResolution;
    float2 halfSize = uResolution * 0.5 - 3.0;
    float2 q = abs(pc) - halfSize + 7.0;
    float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - 7.0;
    float j = noiseTex(uv * float2(3.1, 4.2)) * 0.6
        + noiseTex(uv * float2(7.3, 8.1)) * 0.4;
    d += (j - 0.5) * 2.6;
    float mask = 1.0 - smoothstep(-1.0, 1.0, d);
    return float2(mask, d);
}

float4 PSVista(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float2 mk = canvasMask(uv);
    float aspect = uResolution.x / max(uResolution.y, 1.0);
    float ux = uv.x * aspect;

    //====== 双形态色板 ======
    float3 skyTop = lerp(SKY_TOP_B, SKY_TOP_R, uRain);
    float3 skyMid = lerp(SKY_MID_B, SKY_MID_R, uRain);
    float3 horizonC = lerp(HORIZON_B, HORIZON_R, uRain);
    float3 cloudDk = lerp(CLOUD_DK_B, CLOUD_DK_R, uRain);
    float3 cloudRim = lerp(CLOUD_RIM_B, CLOUD_RIM_R, uRain);
    float3 silFar = lerp(SIL_FAR_B, SIL_FAR_R, uRain);
    float3 silNear = lerp(SIL_NEAR_B, SIL_NEAR_R, uRain);
    float3 ember = lerp(EMBER_B, EMBER_R, uRain);
    float3 fog = lerp(FOG_B, FOG_R, uRain);
    float3 waterHi = lerp(WATER_HI_B, WATER_HI_R, uRain);
    float3 waterLo = lerp(WATER_LO_B, WATER_LO_R, uRain);
    float3 tint = lerp(TINT_B, TINT_R, uRain);
    float3 foam = lerp(FOAM_B, FOAM_R, uRain);
    float3 bed = lerp(BED_B, BED_R, uRain);

    //====== 穹顶：竖向层次 + 地平烬光 ======
    float horizon = 0.46;
    float3 col = lerp(skyTop, skyMid, smoothstep(0.02, 0.58, uv.y));
    float hGlow = exp2(-abs(uv.y - horizon) * 9.5);
    col = lerp(col, horizonC, hGlow * 0.62);

    //====== 云带：两层反向缓涌 + 云底烬缘 ======
    float c0 = noiseTex(float2(ux * 0.33 + uTime * 0.0080, uv.y * 1.35 + 0.13));
    float c1 = noiseTex(float2(ux * 0.71 - uTime * 0.0121, uv.y * 2.10 + 0.57));
    float cloud = saturate((c0 * 0.62 + c1 * 0.38 - 0.42) * 2.6)
        * smoothstep(horizon + 0.02, horizon - 0.34, uv.y);
    col = lerp(col, cloudDk, cloud * 0.72);
    float rim = saturate((c0 - 0.52) * 5.0) * exp2(-abs(uv.y - horizon + 0.10) * 12.0);
    col += cloudRim * rim * 0.16;

    //====== 远村两排：记忆微颤，构图固定 ======
    float shiver = (noiseTex(float2(uTime * 0.05, 0.71)) - 0.5) * 0.006;
    float breathe = 0.82 + 0.18 * noiseTex(float2(uTime * 0.021, 0.29));
    float3 far = villageRow((ux + shiver) * 5.2 + 2.7, uv.y,
        horizon + 0.012, 0.030, 3.0, uLightGate * 0.65);
    float3 near = villageRow((ux - shiver * 1.6) * 3.1 + 1.3, uv.y,
        horizon + 0.062, 0.048, 11.0, uLightGate);

    col = lerp(col, silFar, far.x * 0.80 * breathe);
    col += fog * far.z * 0.55;
    float midFog = smoothstep(horizon + 0.005, horizon + 0.10, uv.y);
    col = lerp(col, fog, midFog * 0.30);
    col = lerp(col, silNear, near.x * 0.95);
    col += tint * near.z * 0.55;
    col += ember * (far.y * 0.24 + near.y * 0.50);

    //====== 地平烬雾 ======
    float fogBand = smoothstep(horizon - 0.01, horizon + 0.13, uv.y);
    col = lerp(col, fog, fogBand * 0.30);

    //====== 湖床（岸线以下）：干涸泥地 + 龟裂，门控走 lerp 无分支 ======
    float bedT = smoothstep(SHORE_Y - 0.004, SHORE_Y + 0.03, uv.y);
    float3 bedCol = bed * (0.9 + 0.2 * noiseTex(float2(ux * 2.2, uv.y * 5.0)));
    //龟裂：两个错频噪声的等值线交叠成裂网
    float k0 = noiseTex(float2(ux * 2.6, uv.y * 7.5));
    float k1 = noiseTex(float2(ux * 5.1 + 3.7, uv.y * 13.0));
    float crack = exp2(-abs(k0 - 0.5) * 30.0) + exp2(-abs(k1 - 0.5) * 34.0);
    bedCol *= 1.0 - saturate(crack) * 0.45 * uDry;
    //岸缘往画底渐深
    bedCol *= lerp(1.0, 0.55, smoothstep(SHORE_Y, 1.0, uv.y));
    col = lerp(col, bedCol, bedT);

    //====== 血湖水带（uWaterY 以下淹没湖床） ======
    float rel = uv.y - uWaterY;
    float depth = saturate(rel / max(1.0 - uWaterY, 0.001));
    float3 water = lerp(waterHi, waterLo, depth);
    float f0 = noiseTex(float2(ux * 1.1 - uTime * 0.016, uv.y * 2.4));
    float f1 = noiseTex(float2(ux * 2.3 + uTime * 0.011, uv.y * 4.6 + 3.7));
    float flowAmp = 0.15 + 0.10 * uStir + 0.14 * uBoil;
    water += tint * flowAmp * (f0 * 0.62 + f1 * 0.48) * (1.0 - depth * 0.55);
    //村火倒影：岸下拉长的暖竖纹，随水流揉碎
    float refl = pow(noiseTex(float2(ux * 2.9 + 0.53, 0.31)), 2.5)
        * exp2(-max(rel, 0.0) * 6.0) * (0.35 + 0.65 * uLightGate);
    water += ember * refl * 0.18;
    //近水面焦散
    float caus = pow(noiseTex(float2(ux * 3.4 - uTime * 0.022, uv.y * 9.0)), 6.0)
        * exp2(-max(rel, 0.0) * 11.0);
    water += foam * caus * 0.28;
    //沸腾气泡
    float bub = noiseTex(float2(ux * 7.0, uv.y * 5.0 - uTime * 0.30));
    water += foam * saturate((bub - 0.72) * 6.0) * uBoil * 0.9 * step(0.0, rel);

    //泡沫缝线
    float wob = (noiseTex(float2(ux * 2.8 - uTime * 0.020, 0.71)) - 0.5)
        * (0.010 + 0.022 * uBoil);
    float seamD = rel + wob;
    float seamBand = exp2(-abs(seamD) * (170.0 - 60.0 * saturate(uStir + uBoil)));
    float flicker2 = noiseTex(float2(ux * 3.6 - uTime * 0.05, 0.41));
    float3 seam = foam * seamBand
        * (0.30 + 0.34 * flicker2 + 0.26 * uStir + 0.5 * uBoil);

    float toWater = saturate(seamD * 240.0) * step(SHORE_Y - 0.02, uv.y);
    col = lerp(col, water, toWater);
    col += seam * step(SHORE_Y - 0.02, uv.y);

    //====== 远场烬点 ======
    float mote = noiseTex(float2(ux * 2.7 + uTime * 0.006, uv.y * 3.1 + uTime * 0.030));
    float spark = saturate((mote - 0.80) * 12.0)
        * smoothstep(horizon + 0.10, horizon - 0.30, uv.y);
    col += ember * spark * 0.10;

    //====== 画心边缘：浸润沉暗 + 结算白闪 ======
    float soakIn = exp2(min(mk.y, 0.0) * 0.22);
    col *= 1.0 - soakIn * 0.22;
    col = lerp(col, float3(0.93, 0.94, 0.95), saturate(uFlash) * 0.85);

    float aOut = mk.x * uAlpha;
    return float4(col * aOut, aOut);
}

technique TechVista {
    pass P0 {
        PixelShader = compile ps_3_0 PSVista();
    }
}

//============================================================================
//湿纸卡片（TechCard）：题跋卡/引导卡底板。噪蚀圆角矩形湿纸面，
//纸纤维两频微光 + 缘内浸润沉暗 + 底缘一线水痕（uRain 浸染）；
//uTear 驱动自中线向上下的撕开揭示
//============================================================================

float2 cardMask(float2 uv) {
    float2 pc = (uv - 0.5) * uResolution;
    float2 halfSize = uResolution * 0.5 - 5.0;
    float2 q = abs(pc) - halfSize + 9.0;
    float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - 9.0;

    float j0 = noiseTex(uv * float2(2.2, 3.6) + uTime * 0.008);
    float j1 = noiseTex(uv * float2(6.8, 7.4) - uTime * 0.011);
    d += (j0 * 0.6 + j1 * 0.4 - 0.5) * 6.0;

    float mask = 1.0 - smoothstep(-1.2, 1.2, d);
    return float2(mask, d);
}

float4 PSCard(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float2 mk = cardMask(uv);
    float3 tint = lerp(TINT_B, TINT_R, uRain);
    float3 foam = lerp(FOAM_B, FOAM_R, uRain);

    float fiberN0 = noiseTex(float2(uv.x * 8.0, uv.y * 12.0));
    float fiberN1 = noiseTex(float2(uv.x * 2.6 + 5.2, uv.y * 3.4));
    float3 col = PAPER_BASE + FIBER_COL * (fiberN0 * 0.040 + fiberN1 * 0.028);
    float damp = noiseTex(float2(uv.x * 1.3 + uTime * 0.006, uv.y * 1.8));
    col *= 1.0 - damp * 0.18;

    float seep = exp2(-abs(uv.y - 0.94) * 26.0);
    float seepFlicker = noiseTex(float2(uv.x * 3.2 - uTime * 0.03, 0.57));
    col += tint * seep * (0.10 + 0.10 * seepFlicker);
    col += foam * seep * seepFlicker * 0.05;

    float distN = abs(uv.y - 0.5) / 0.52;
    float tearJag = (noiseTex(float2(uv.x * 4.6, uv.y * 3.2) + uTime * 0.02) - 0.5) * 0.14;
    float tearE = 1.0 - pow(1.0 - saturate(uTear), 2.0);
    float front = tearE * 1.12 - distN + tearJag;
    float reveal = smoothstep(0.0, 0.12, front);
    float frontBand = exp2(-abs(front - 0.06) * 100.0) * step(tearE, 0.985);
    col += FIBER_COL * frontBand * 0.30;

    float d = mk.y;
    float fiberE = noiseTex(float2(uv.x * 13.0, uv.y * 9.0) - uTime * 0.04);
    float fiber = exp(-d * d / 5.0) * (0.35 + 0.65 * fiberE);
    col += FIBER_COL * fiber * 0.22;
    float soakIn = exp2(min(d, 0.0) * 0.24);
    col *= 1.0 - soakIn * 0.20;

    float aOut = mk.x * reveal * uAlpha * 0.96;
    return float4(col * aOut, aOut);
}

technique TechCard {
    pass P0 {
        PixelShader = compile ps_3_0 PSCard();
    }
}

//============================================================================
//玻璃风铃（TechChime）：掌中风铃 HUD 的铃身。SDF 球形铃体 + 噪蚀波口
//（真风铃的切口本就不平），玻璃=低透近黑体 + 菲涅尔缘增亮 + 窗形高光；
//铃内盛一小汪血湖：uWaterY 复用为液面充盈度（0 空 1 满），uSwing=当前摆角
//（quad 随摆旋转，液面在铃内反向找平），uStir 晃荡、uBoil 沸腾、
//uFlash 白闪、uLightGate=液中烬点稠度（湖藏填充率）。预乘输出
//============================================================================

float4 PSChime(float2 coords : TEXCOORD0) : COLOR0 {
    float aspect = uResolution.x / max(uResolution.y, 1.0);
    float2 p = float2((coords.x - 0.5) * aspect, coords.y - 0.5);

    //双形态色板：液体沿用血湖/鬼雨水族，玻璃自带一对近黑与缘光色
    float3 waterHi = lerp(WATER_HI_B, WATER_HI_R, uRain);
    float3 waterLo = lerp(WATER_LO_B, WATER_LO_R, uRain);
    float3 tint = lerp(TINT_B, TINT_R, uRain);
    float3 foam = lerp(FOAM_B, FOAM_R, uRain);
    float3 ember = lerp(EMBER_B, EMBER_R, uRain);
    float3 glassDeep = lerp(float3(0.050, 0.014, 0.020), float3(0.018, 0.026, 0.032), uRain);
    float3 glassRim = lerp(float3(0.760, 0.360, 0.300), float3(0.560, 0.660, 0.690), uRain);

    //铃身：球体，口缘在下方被波状切开
    float2 c = float2(0.0, -0.035);
    float r = 0.385;
    float d = length(p - c) - r;
    float jag = (noiseTex(float2(coords.x * 2.7 + 0.19, 0.53)) - 0.5) * 0.045;
    float cut = p.y - (0.295 + jag);

    float body = 1.0 - smoothstep(-0.012, 0.010, d);
    float keep = 1.0 - smoothstep(-0.012, 0.012, cut);
    float glass = body * keep;

    //玻璃体：低透近黑 + 菲涅尔缘增亮（中心最透，能看见后面的世界）
    float fres = smoothstep(-0.16, -0.005, d);
    float3 col = glassDeep * (0.55 + fres * 0.9);
    float a = 0.14 + fres * 0.42;

    //铃内液面：找平（摆角反向）+ 晃荡波
    float fill = saturate(uWaterY);
    float surf = lerp(0.325, -0.135, fill)
        - p.x * uSwing * 1.1
        + sin(p.x * 10.0 + uTime * 2.9) * (0.008 + 0.030 * uStir + 0.035 * uBoil);
    float inWater = step(surf, p.y) * glass * step(0.02, fill);
    float depth = saturate((p.y - surf) * 2.2);
    float3 water = lerp(waterHi, waterLo, depth) * 1.35;
    //液中微流
    water += tint * (0.20 + 0.22 * uStir)
        * noiseTex(float2(p.x * 1.7 + uTime * 0.05, p.y * 2.6 + 0.31));
    //烬点：湖藏越满，液里漂的村火越稠
    float mote = noiseTex(float2(p.x * 5.0 + uTime * 0.03, p.y * 6.0 - uTime * 0.05));
    water += ember * saturate((mote - 0.74) * 9.0) * uLightGate * 0.85;
    //沸腾气泡
    float bub = noiseTex(float2(p.x * 8.0, p.y * 7.0 - uTime * 0.35));
    water += foam * saturate((bub - 0.70) * 6.0) * uBoil;
    //液面缝光
    float seam = exp2(-abs(p.y - surf) * (110.0 - 40.0 * saturate(uStir + uBoil)));
    water += foam * seam * (0.35 + 0.35 * uStir + 0.4 * uBoil);
    col = lerp(col, water, inWater);
    a = lerp(a, 0.92, inWater);

    //窗形高光：左上一枚斜长亮斑随摆角轻移，右下一线弱反光
    float2 hp = (p - float2(-0.145 + uSwing * 0.22, -0.245)) / float2(0.085, 0.050);
    float hl = exp(-dot(hp, hp));
    float2 hp2 = (p - float2(0.160 + uSwing * 0.16, 0.080)) / float2(0.035, 0.100);
    float hl2 = exp(-dot(hp2, hp2)) * 0.35;
    col += float3(0.92, 0.90, 0.88) * (hl * 0.55 + hl2) * glass;
    a += (hl * 0.35 + hl2 * 0.2) * glass;

    //缘线：球缘一线 + 波口一线亮唇
    float rimLine = exp2(-abs(d) * 70.0) * keep;
    float lip = exp2(-abs(cut) * 60.0) * body;
    col += glassRim * (rimLine * 0.55 + lip * 0.75);
    a += rimLine * 0.30 + lip * 0.35;

    //结算白闪
    col = lerp(col, float3(0.93, 0.94, 0.95), saturate(uFlash) * 0.8);

    float aOut = saturate(a) * glass * uAlpha;
    return float4(col * aOut, aOut);
}

technique TechChime {
    pass P0 {
        PixelShader = compile ps_3_0 PSChime();
    }
}
