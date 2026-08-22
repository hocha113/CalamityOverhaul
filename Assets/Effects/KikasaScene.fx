// ============================================================================
//KikasaScene.fx 鬼伞 UI 共用技法集（湖畔村图退役后只剩两件）：
//TechCard：湿纸引导卡底（引导卡走 KikasaPanoramaRenderer.DrawCardBg 消费）。
//TechChime：掌中风铃 HUD 的玻璃铃身（铃内盛血湖，uWaterY 复用为液面充盈度）。
//uRain 在血湖族与鬼雨族（KikasaSky RAIN_* 同源）之间浸染。
//预乘输出。s0=白像素 s1=PerlinNoise
//（旧 TechVista 活画技法随湖畔村图退役删除；全屏背景现由 KikasaPanorama.fx 承担）
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;         //秒
float uAlpha;        //整体透明度
float2 uResolution;  //quad 像素尺寸
float uWaterY;       //TechChime 用：液面充盈度（0 空 1 满）
float uRain;         //0~1 鬼雨浸染
float uStir;         //0~1 水面活性
float uBoil;         //0~1 翻转沸腾
float uFlash;        //0~1 结算白闪
float uLightGate;    //0~1 烬萤稠度（湖藏填充率）
float uTear;         //TechCard 用：撕开揭示
float uSwing;        //TechChime 用：当前摆角（弧度，液面反向找平）
float uHover;        //TechChime 用：悬停唤醒 0~1（烬萤/凝露亮一拍，缘光呼吸略快）

//====== 血湖族（暮红） ======
static const float3 HORIZON_B   = float3(0.560, 0.118, 0.055);
static const float3 EMBER_B     = float3(0.950, 0.340, 0.140);
static const float3 WATER_HI_B  = float3(0.150, 0.028, 0.038);
static const float3 WATER_LO_B  = float3(0.046, 0.008, 0.013);
static const float3 TINT_B      = float3(0.930, 0.300, 0.270);
static const float3 FOAM_B      = float3(0.965, 0.520, 0.440);
//====== 鬼雨族（墨青，禁红禁暖） ======
static const float3 HORIZON_R   = float3(0.225, 0.262, 0.268);
static const float3 EMBER_R     = float3(0.620, 0.670, 0.680);
static const float3 WATER_HI_R  = float3(0.055, 0.072, 0.082);
static const float3 WATER_LO_R  = float3(0.020, 0.027, 0.034);
static const float3 TINT_R      = float3(0.300, 0.345, 0.355);
static const float3 FOAM_R      = float3(0.620, 0.670, 0.680);
//湿纸
static const float3 FIBER_COL   = float3(0.880, 0.795, 0.690);
static const float3 PAPER_BASE  = float3(0.052, 0.028, 0.024);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
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
//（真风铃的切口本就不平）。材质=手工吹制玻璃：双壁厚度（内壁第二线）、
//熔珠唇、顺球面的吹制旋纹、封存气泡、弧形暮色反射带、底缘聚光；
//常驻内景（无水时铃也不空）：烬萤浮游（稠度=uLightGate 湖藏填充率）、
//内壁凝露（一枚周期下滑留渐干水迹）、干涸潮痕圈——全部画在水体 lerp
//之前，涨水自然覆盖。uHover=悬停唤醒（烬萤/凝露亮一拍，缘光呼吸略快）。
//铃内盛一小汪血湖：uWaterY 复用为液面充盈度（0 空 1 满），uSwing=当前摆角
//（quad 随摆旋转，液面在铃内反向找平），uStir 晃荡、uBoil 沸腾、
//uFlash 白闪。预乘输出
//============================================================================

//封存气泡锚位与尺寸（p 空间，嵌在玻璃壁环带内、波口之上）
static const float2 BUB_P[4] = {
    float2(-0.257, 0.145), float2(-0.304, -0.177),
    float2(0.330, -0.093), float2(0.192, 0.239)
};
static const float BUB_S[4] = { 0.016, 0.012, 0.014, 0.011 };
//凝露定珠锚位（下内壁，水汽垂积处）
static const float2 DEW_P[2] = { float2(-0.195, 0.243), float2(0.240, 0.190) };

float4 PSChime(float2 coords : TEXCOORD0) : COLOR0 {
    float aspect = uResolution.x / max(uResolution.y, 1.0);
    float2 p = float2((coords.x - 0.5) * aspect, coords.y - 0.5);

    //双形态色板：液体沿用血湖/鬼雨水族，玻璃自带一对近黑与缘光色
    float3 waterHi = lerp(WATER_HI_B, WATER_HI_R, uRain);
    float3 waterLo = lerp(WATER_LO_B, WATER_LO_R, uRain);
    float3 tint = lerp(TINT_B, TINT_R, uRain);
    float3 foam = lerp(FOAM_B, FOAM_R, uRain);
    float3 ember = lerp(EMBER_B, EMBER_R, uRain);
    float3 horizonC = lerp(HORIZON_B, HORIZON_R, uRain);
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

    //球面坐标：q=归一偏心，lon=经线坐标（旋纹顺球面弯曲的依据）
    float2 q = (p - c) / r;
    float lon = q.x / max(sqrt(saturate(1.0 - q.y * q.y)), 0.35);

    //内域遮罩：浅内域（潮痕，允许贴到近壁）与深内域（烬萤悬浮），都避开缘线
    float inner = (1.0 - smoothstep(-0.060, -0.018, d)) * keep;
    float air = (1.0 - smoothstep(-0.095, -0.050, d)) * keep;

    //液面参数先行：内景细节据它让位（内景画在水体 lerp 之前，涨水自然覆盖）
    float fill = saturate(uWaterY);
    float surf = lerp(0.325, -0.135, fill)
        - p.x * uSwing * 1.1
        + sin(p.x * 10.0 + uTime * 2.9) * (0.008 + 0.030 * uStir + 0.035 * uBoil);
    float inWater = step(surf, p.y) * glass * step(0.02, fill);

    //玻璃体：低透近黑 + 菲涅尔缘增亮（中心仍最透，能看见后面的世界）
    //+ 朝口部渐浓的烟色纵渐变——铃体读作有形的器物，不是一圈描边
    float fres = smoothstep(-0.16, -0.005, d);
    float smokeG = smoothstep(-0.30, 0.30, p.y);
    float3 col = glassDeep * (0.62 + fres * 0.9 + smokeG * 0.55);
    float a = 0.20 + fres * 0.40 + smokeG * 0.07;

    //吹制旋纹：顺经线的明暗微差条纹，缘重心轻，随时间缓慢流转
    float striaN = noiseTex(float2(lon * 0.9 + uTime * 0.008, q.y * 0.22 + 0.71)) - 0.5;
    float stria = striaN * (0.35 + 0.65 * fres) * glass;
    col += glassRim * stria * 0.14;
    a += stria * 0.10;

    //弧形暮色反射带：铃身下三分之一顺球面弯的天光映带，随摆轻移，涨水让位
    float refY = 0.38 - 0.14 * q.x * q.x + uSwing * 0.8 * q.x;
    float hband = exp2(-abs(q.y - refY) * 7.5) * glass * saturate(1.0 - fill * 0.8);
    float3 dusk = lerp(horizonC, tint, 0.35);
    col += dusk * hband * 0.30;
    a += hband * 0.15;

    //潮痕圈：两道干涸旧水位弧，噪声啮边；涨水中渐隐，没入水下由水体覆盖
    float nJit = noiseTex(float2(coords.x * 3.4 + 0.61, 0.83)) - 0.5;
    float rings = (exp2(-abs(p.y - 0.075 + nJit * 0.016) * 130.0) * 0.8
        + exp2(-abs(p.y - 0.185 + nJit * 0.022) * 150.0)) * inner
        * saturate(1.0 - fill * 1.4);
    col += tint * rings * 0.30;
    a += rings * 0.14;

    //烬萤浮游：空铃里缓缓上漂的村火微萤，稠度=湖藏填充率，悬停亮一拍
    float flyN = noiseTex(float2(p.x * 3.6 + uTime * 0.020, p.y * 4.2 - uTime * 0.035));
    float fly = saturate((flyN - 0.76) * 9.0) * air * uLightGate;
    float flyAmp = 0.40 + 0.35 * uHover;
    col += ember * fly * flyAmp;
    a += fly * flyAmp * 0.45;

    //封存气泡：四枚静态微泡嵌在玻璃壁内，暗底点上一粒偏光亮斑，轻呼吸
    float bubDark = 0.0;
    float bubGlint = 0.0;
    [unroll]
    for (int i = 0; i < 4; i++) {
        float2 bp = (p - BUB_P[i]) / BUB_S[i];
        bubDark += exp(-dot(bp, bp));
        float2 gp = (p - BUB_P[i] + float2(0.006, 0.007)) / (BUB_S[i] * 0.55);
        bubGlint += exp(-dot(gp, gp));
    }
    float bubBreath = 0.8 + 0.2 * sin(uTime * 1.3);
    col = lerp(col, glassDeep * 0.35, saturate(bubDark) * 0.45 * glass);
    col += glassRim * bubGlint * 0.28 * bubBreath * glass;
    a += (bubGlint * 0.22 + bubDark * 0.10) * glass;

    //凝露：两枚定珠贴下内壁，另一枚周期沿右内壁下滑并留一线渐干水迹——湖气未散
    float dewFix = 0.0;
    [unroll]
    for (int j = 0; j < 2; j++) {
        float2 ep = (p - DEW_P[j]) / 0.013;
        dewFix += exp(-dot(ep, ep));
    }
    float cyc = frac(uTime * 0.043);
    float slideT = smoothstep(0.55, 0.85, cyc);
    float dropY = lerp(-0.140, 0.235, slideT);
    float dyq = dropY + 0.035;
    float2 dropP = float2(sqrt(max(0.1156 - dyq * dyq, 0.0)), dropY);
    float2 sp = (p - dropP) / 0.014;
    float bead = smoothstep(0.08, 0.30, cyc) * (1.0 - smoothstep(0.86, 0.97, cyc));
    float slideBead = exp(-dot(sp, sp)) * bead;
    float dyp = p.y + 0.035;
    float wx = sqrt(max(0.1156 - dyp * dyp, 0.0));
    float trail = exp2(-abs(p.x - wx) * 110.0)
        * step(p.y, dropY) * step(-0.150, p.y)
        * saturate(1.0 - (dropY - p.y) * 4.5)
        * slideT * (1.0 - smoothstep(0.86, 1.0, cyc)) * glass;
    float dewAmp = 0.55 + 0.45 * uHover;
    float dewAll = (dewFix * (0.55 + 0.20 * sin(uTime * 1.7)) + slideBead) * glass;
    col += foam * (dewAll * 0.38 + trail * 0.10) * dewAmp;
    a += (dewAll * 0.30 + trail * 0.06) * dewAmp;

    //铃内液面：找平（摆角反向）+ 晃荡波
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

    //底缘聚光：波口唇上方内壁一弧微亮（玻璃在底缘拢光）
    float gather = exp2(-abs(cut + 0.055) * 26.0) * glass;
    col += glassRim * gather * 0.16;
    a += gather * 0.10;

    //缘线：外缘线粗细随噪声微起伏（手作不是机加工）+ 内壁第二线（玻璃厚度）
    //+ 波口熔珠唇（亮唇芯 + 圆润珠晕）；慢呼吸，悬停呼吸略快
    float rimN = noiseTex(float2(lon * 0.7 + 0.13, q.y * 0.4 + 0.29));
    float breath = 0.88 + 0.12 * sin(uTime * (1.1 + 0.9 * uHover));
    float rimLine = exp2(-abs(d) * (86.0 - 30.0 * rimN)) * keep;
    float innerLine = exp2(-abs(d + 0.042) * 120.0) * keep;
    float lipCore = exp2(-abs(cut) * 70.0) * body;
    float lipBead = exp2(-abs(cut + 0.012) * 26.0) * body;
    col += glassRim * ((rimLine * 0.55 + lipCore * 0.70) * breath
        + innerLine * 0.22 + lipBead * 0.26);
    a += (rimLine * 0.30 + lipCore * 0.33) * breath + innerLine * 0.13 + lipBead * 0.15;

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
