// ============================================================================
//KikasaHud.fx 伞下水鏡：鬼伞 HUD 的鏡体。外形＝伞拱窗（圆拱伞盖 + 四瓣荷缘），
//与湖窗同族的湿纸毛边（幅度按 HUD 尺度收小）。
//内容三态：干纸面（域关，uTear=0 纸纤维微光）→ 撕开（uTear 自水线锚外扩，
//撕缘湿纤维苍白）→ 湖景（血暮空气 / 动态水位 uWaterY / 深水双频横流 /
//缝线泡沫，配方与 KikasaVaultPanel 同源）。
//双形态浸染：uRain 在血湖族与鬼雨族（KikasaSky RAIN_* 同源）之间 lerp，禁分支。
//翻转联动：uBoil 沸腾气泡与蒸汽、uTilt 水面倾荡（sin(倒转角) 驱动，无跳变）、
//uFlash 结算白闪。门控全走 step/lerp；输出预乘。s0=白像素 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;         //秒
float uAlpha;        //整体透明度
float2 uResolution;  //quad 像素尺寸
float uWaterY;       //水面 uv.y（1.05=空湖，0.30=满水）
float uTear;         //0~1 撕开进度（域的 SpreadProgress）
float uRain;         //0~1 鬼雨浸染（RainBlend 含翻转预览）
float uStir;         //0~1 活性（事件/涨退水时水更躁）
float uBoil;         //0~1 沸腾（翻转期）
float uTilt;         //水面倾荡角（弧度，翻转期 sin 包络）
float uFlash;        //0~1 结算白闪

//血湖族（与 KikasaVaultPanel/KikasaGrade 同源）
static const float3 AIR_DEEP_B  = float3(0.030, 0.008, 0.012);
static const float3 AIR_WARM_B  = float3(0.092, 0.020, 0.028);
static const float3 WATER_HI_B  = float3(0.150, 0.028, 0.038);
static const float3 WATER_LO_B  = float3(0.046, 0.008, 0.013);
static const float3 TINT_B      = float3(0.930, 0.300, 0.270);
static const float3 FOAM_B      = float3(0.965, 0.520, 0.440);
//鬼雨族（与 KikasaSky RAIN_* 同源，禁红禁暖）
static const float3 AIR_DEEP_R  = float3(0.020, 0.026, 0.032);
static const float3 AIR_WARM_R  = float3(0.062, 0.078, 0.086);
static const float3 WATER_HI_R  = float3(0.055, 0.072, 0.082);
static const float3 WATER_LO_R  = float3(0.020, 0.027, 0.034);
static const float3 TINT_R      = float3(0.300, 0.345, 0.355);
static const float3 FOAM_R      = float3(0.620, 0.670, 0.680);
//湿纸
static const float3 FIBER_COL   = float3(0.880, 0.795, 0.690);
static const float3 PAPER_BASE  = float3(0.052, 0.028, 0.024);
//撕口锚线 uv.y（自此向上下撕开）
static const float TEAR_ANCHOR  = 0.55;

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//伞拱窗 SDF：圆拱伞盖（顶部半圆）∪ 裙身（矩形），下缘四瓣荷缘；
//返回 x=遮罩 y=有符号距离（px，负在内）
float2 archMask(float2 uv) {
    float2 pc = (uv - 0.5) * uResolution;
    float2 halfSize = uResolution * 0.5 - 6.0;
    float domeR = halfSize.x;
    float domeCy = -halfSize.y + domeR;

    //顶部半圆盘
    float dDome = max(length(float2(pc.x, pc.y - domeCy)) - domeR, pc.y - domeCy);
    //裙身：四瓣荷缘，瓣心最低、瓣间上收成尖
    float scallop = 5.0 * (1.0 - abs(sin(pc.x / halfSize.x * 6.2832)));
    float dSkirt = max(abs(pc.x) - halfSize.x,
        max(domeCy - pc.y, pc.y - (halfSize.y - scallop)));
    float d = min(dDome, dSkirt);

    //湿纸毛边，HUD 尺度下幅度收小；再收一档让 CPU 墨骨勾线贴得上边
    float j0 = noiseTex(uv * float2(2.6, 3.4) + uTime * 0.010);
    float j1 = noiseTex(uv * float2(6.4, 7.2) - uTime * 0.013);
    float jag = j0 * 0.6 + j1 * 0.4;
    d += (jag - 0.5) * 4.5;

    float mask = 1.0 - smoothstep(-1.2, 1.2, d);
    return float2(mask, d);
}

//湖景：空气 + 动态水位深水 + 缝线泡沫；p 为倾荡后的内容坐标（uv 语义）
float3 lakeScene(float2 p, float2 uv) {
    float3 airDeep = lerp(AIR_DEEP_B, AIR_DEEP_R, uRain);
    float3 airWarm = lerp(AIR_WARM_B, AIR_WARM_R, uRain);
    float3 waterHi = lerp(WATER_HI_B, WATER_HI_R, uRain);
    float3 waterLo = lerp(WATER_LO_B, WATER_LO_R, uRain);
    float3 tint = lerp(TINT_B, TINT_R, uRain);
    float3 foam = lerp(FOAM_B, FOAM_R, uRain);

    //====== 空气（水线以上）：血暮漂雾 / 鬼雨帘 ======
    float airT = saturate(p.y / max(uWaterY, 0.001));
    float3 air = lerp(airDeep, airWarm, airT);
    float mist = noiseTex(float2(p.x * 1.5 + uTime * 0.012, p.y * 2.8));
    air += tint * mist * 0.10 * airT;
    //云影层：低频暗带缓漂，空气有了远近
    float cloud = noiseTex(float2(p.x * 0.9 + uTime * 0.007, p.y * 1.7 + 7.3));
    air *= 1.0 - saturate(cloud - 0.56) * 0.9 * airT;
    air += tint * pow(saturate(cloud), 4.0) * 0.07 * airT;
    //鬼雨态的细雨幡：窄竖条纹缓落
    float shaft = pow(noiseTex(float2(p.x * 6.0, p.y * 0.5 - uTime * 0.05)), 3.0);
    air += foam * shaft * 0.12 * uRain * airT;

    //====== 深水（水线以下） ======
    float rel = p.y - uWaterY;
    float depth = saturate(rel / max(1.0 - uWaterY, 0.001));
    float3 water = lerp(waterHi, waterLo, depth);
    //双频横流，方向相对
    float f0 = noiseTex(float2(p.x * 1.3 - uTime * 0.016, p.y * 2.1));
    float f1 = noiseTex(float2(p.x * 2.6 + uTime * 0.011, p.y * 4.3 + 3.7));
    float flowAmp = 0.15 + 0.10 * uStir + 0.14 * uBoil;
    water += tint * flowAmp * (f0 * 0.62 + f1 * 0.48) * (1.0 - depth * 0.55);
    //稀疏湿亮
    float glint = pow(saturate(noiseTex(float2(p.x * 3.0, p.y * 1.3) + uTime * 0.035) * 1.1), 9.0);
    water += foam * glint * 0.26 * (1.0 - depth * 0.7);
    //近水面焦散：横向细碎游光，随深度速灭
    float caus = pow(noiseTex(float2(p.x * 3.6 - uTime * 0.022, p.y * 9.0)), 6.0)
        * exp2(-max(rel, 0.0) * 11.0);
    water += foam * caus * 0.30;
    //沸腾气泡：高频斑点自下而上，只活在水体里
    float bub = noiseTex(float2(p.x * 7.0, p.y * 5.0 - uTime * 0.30));
    water += foam * saturate((bub - 0.72) * 6.0) * uBoil * 0.9 * step(0.0, rel);

    //====== 缝线泡沫 ======
    float wob = (noiseTex(float2(p.x * 2.8 - uTime * 0.020, 0.71)) - 0.5)
        * (0.016 + 0.030 * uBoil);
    float seamD = rel + wob;
    float seamBand = exp2(-abs(seamD) * (150.0 - 55.0 * saturate(uStir + uBoil)));
    float flicker = noiseTex(float2(p.x * 3.6 - uTime * 0.05, 0.41));
    float3 seam = foam * seamBand
        * (0.32 + 0.36 * flicker + 0.28 * uStir + 0.55 * uBoil);
    //沸腾蒸汽：贴着水线往上翻的暖雾
    float steam = noiseTex(float2(p.x * 2.2, p.y * 3.0 + uTime * 0.10))
        * exp2(min(seamD, 0.0) * 26.0) * uBoil;
    seam += foam * steam * 0.30;

    float toWater = saturate(seamD * 220.0);
    return lerp(air, water, toWater) + seam;
}

float4 PSMirror(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float2 mk = archMask(uv);

    //内容坐标：绕鏡心倾荡（翻转期水在鏡里晃过一个来回，无跳变）
    float2 pc01 = uv - 0.5;
    float aspect = uResolution.x / max(uResolution.y, 1.0);
    float2 pcA = float2(pc01.x * aspect, pc01.y);
    float s = sin(uTilt);
    float c = cos(uTilt);
    float2 pcR = float2(pcA.x * c - pcA.y * s, pcA.x * s + pcA.y * c);
    float2 p = float2(pcR.x / aspect, pcR.y) + 0.5;

    float3 lake = lakeScene(p, uv);

    //====== 干纸面（域关时鏡里只有纸） ======
    float fiberN0 = noiseTex(float2(uv.x * 9.0, uv.y * 6.5));
    float fiberN1 = noiseTex(float2(uv.x * 3.1 + 5.2, uv.y * 2.2));
    float3 paper = PAPER_BASE + FIBER_COL * (fiberN0 * 0.045 + fiberN1 * 0.030);
    //折痕两道：伞纸收拢时的旧折线，微暗一线 + 受光一丝
    float w1 = (fiberN1 - 0.5) * 0.05;
    float c1 = exp2(-abs(uv.x - 0.34 + (uv.y - 0.5) * 0.14 + w1) * 120.0);
    float c2 = exp2(-abs(uv.x - 0.67 - (uv.y - 0.5) * 0.10 + w1) * 120.0);
    paper *= 1.0 - (c1 + c2) * 0.30;
    paper += FIBER_COL * (c1 + c2) * fiberN0 * 0.05;
    //纸心一点将醒未醒的暗红晕（湖在纸背后）
    float heart = exp2(-dot(pc01, pc01) * 26.0);
    paper += lerp(TINT_B, TINT_R, uRain) * heart * 0.05;

    //====== 撕开：自锚线向外的揭示前沿，带湿纤维毛边 ======
    float distN = abs(uv.y - TEAR_ANCHOR) / 0.60;
    float tearJag = (noiseTex(float2(uv.x * 5.0, uv.y * 3.0) + uTime * 0.02) - 0.5) * 0.16;
    float tearE = 1.0 - pow(1.0 - saturate(uTear), 2.0);
    float front = tearE * 1.18 - distN + tearJag;
    float reveal = smoothstep(0.0, 0.14, front);
    float3 col = lerp(paper, lake, reveal);
    //撕裂前沿的湿纤维亮线（只在半开时存在）
    float frontBand = exp2(-abs(front - 0.07) * 90.0)
        * step(0.02, tearE) * step(tearE, 0.985);
    col += FIBER_COL * frontBand * 0.35;

    //====== 撕缘湿纤维苍白 + 缘内浸润沉暗 + 拱顶内缘受光 ======
    float d = mk.y;
    float fiberE = noiseTex(float2(uv.x * 14.0, uv.y * 10.0) - uTime * 0.05);
    float fiber = exp(-d * d / 6.0) * (0.35 + 0.65 * fiberE);
    col += FIBER_COL * fiber * (0.16 + 0.22 * reveal);
    float soakIn = exp2(min(d, 0.0) * 0.30);
    col *= 1.0 - soakIn * 0.24;
    //拱顶内侧一线受光：光从伞外渗进来，上半圈才有
    float rim = exp2(-(d + 3.5) * (d + 3.5) / 5.0) * saturate((0.42 - uv.y) * 3.0);
    col += lerp(FOAM_B, FOAM_R, uRain) * rim * (0.05 + 0.09 * reveal);

    //====== 结算白闪 ======
    col = lerp(col, float3(0.93, 0.94, 0.95), saturate(uFlash) * 0.85);

    //预乘输出，贴合引擎 (One, InvSrcAlpha) 混合
    float aOut = mk.x * uAlpha * 0.96;
    return float4(col * aOut, aOut);
}

technique TechMirror {
    pass P0 {
        PixelShader = compile ps_3_0 PSMirror();
    }
}

//============================================================================
//湿纸卡片（TechCard）：引导卡底板。噪蚀圆角矩形的湿纸面，
//纸纤维两频微光 + 缘内浸润沉暗 + 底缘一线血/雨水痕（uRain 浸染）；
//uTear 驱动自中线向上下的撕开揭示，与鏡体同一套撕纸语言
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

    //湿纸面：纤维两频 + 慢漂潮渍
    float fiberN0 = noiseTex(float2(uv.x * 8.0, uv.y * 12.0));
    float fiberN1 = noiseTex(float2(uv.x * 2.6 + 5.2, uv.y * 3.4));
    float3 col = PAPER_BASE + FIBER_COL * (fiberN0 * 0.040 + fiberN1 * 0.028);
    float damp = noiseTex(float2(uv.x * 1.3 + uTime * 0.006, uv.y * 1.8));
    col *= 1.0 - damp * 0.18;

    //底缘水痕：纸泡在湖边，下缘吸了一线水光
    float seep = exp2(-abs(uv.y - 0.94) * 26.0);
    float seepFlicker = noiseTex(float2(uv.x * 3.2 - uTime * 0.03, 0.57));
    col += tint * seep * (0.10 + 0.10 * seepFlicker);
    col += foam * seep * seepFlicker * 0.05;

    //撕开揭示：自中线向上下，与鏡体同族
    float distN = abs(uv.y - 0.5) / 0.52;
    float tearJag = (noiseTex(float2(uv.x * 4.6, uv.y * 3.2) + uTime * 0.02) - 0.5) * 0.14;
    float tearE = 1.0 - pow(1.0 - saturate(uTear), 2.0);
    float front = tearE * 1.12 - distN + tearJag;
    float reveal = smoothstep(0.0, 0.12, front);
    float frontBand = exp2(-abs(front - 0.06) * 100.0) * step(tearE, 0.985);
    col += FIBER_COL * frontBand * 0.30;

    //撕缘湿纤维苍白 + 缘内浸润沉暗
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
