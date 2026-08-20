// ============================================================================
//KikasaGrade.fx 鬼伞血湖领域全屏调色，两个 technique 对应两个渲染时机
//TechGrade（NPC 层之前，只吃环境）：血暮调色（红罩+暗部沉深绯+非红轻去饱和）
//TechUnify（EndCapture，吃整帧含实体）：
//  轻血罩 + 血湖镜面（水位线以下真垂直镜像倒影，血染+深度血雾+浮渣+缝线血沫，
//  反射率贴缝强向深弱、透出水下真实世界——被淹之物经折射采样随水摆动）
//  + 湿纸撕裂前沿（浸润带/湿纤维缘/卷影）
//开合遮罩是"被水浸烂的破纸"：圆扩散 + 三频纤维毛边，材质与鬼切墨浪刻意分野
//直线算术+平 tex2D，门控走 step/lerp 不用分支；s0=屏幕帧 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;            //EffectTime 秒，遮罩噪声与水波共用时基
float2 uScreenSize;     //像素
float uSpreadMode;      //0=全覆盖 1=开合撕纸
float uSpreadProgress;  //0~1 撕开覆盖
float2 uSpreadOrigin;   //撕裂原点（视口像素）
float uFrontFade;       //0~1 撕裂前沿层可见度（开合时显、稳态归零）
float uPivotY;          //血湖镜面缝线 uv.y（LakeWorldY 投影，稳定枢轴）
float uWaterLevel;      //水位线 uv.y：1.15(屏下) 涨到 uPivotY
float uWaterWobble;     //水位线噪声波动幅度
float uFoamBoost;       //0~1 涨水期泡沫/浮渣增强
float uSeamGlow;        //0~1 缝线血沫水膜辉光
float uAspect;          //宽/高
float uRain;            //0~1 鬼雨异化混合：血暮↔湿墨浊水，全套色板权重乘混合
float4 uLineWave[4];    //水线行波源 x=源uv.x y=寿命进度01 z=幅度(uv.y) w=备用；空槽 z=0
float4 uCoverRect;      //倒影抹除矩形（屏幕 uv：xy=左上 zw=右下）——倒影恶犬替换施术者镜像时用
float uCoverA;          //0~1 抹除强度，随倒影出没渐变；0=不生效
float uWispGlow;        //0~1 鬼火燃湖：浅水金光渗色 + 缝线金辉（火层画在实体层，这里补水体被照亮）

#define LUMA_W float3(0.299, 0.587, 0.114)

//====== 血暮调色板 ======
static const float3 DUSK_SHADOW = float3(0.315, 0.045, 0.075);  //暗部沉入的深绯
static const float3 DUSK_TINT   = float3(1.055, 0.845, 0.800);  //血暮轻罩（乘色）
//====== 血湖 ======
static const float3 LAKE_TINT   = float3(0.930, 0.300, 0.270);  //镜像血染乘色
static const float3 LAKE_FOG    = float3(0.170, 0.024, 0.036);  //湖底血雾
static const float3 UNDER_TINT  = float3(0.640, 0.170, 0.185);  //水下真实世界沉染
static const float3 FOAM_COL    = float3(0.965, 0.520, 0.440);  //缝线血沫微光
//====== 湿纸前沿 ======
static const float3 SOAK_MUL    = float3(0.610, 0.385, 0.305);  //浸水纸乘暗（湿褐）
static const float3 FIBER_COL   = float3(0.880, 0.795, 0.690);  //湿纤维苍白
//====== 鬼雨异化色板（湿墨浊水，禁红禁暖） ======
static const float3 RAIN_SHADOW = float3(0.058, 0.075, 0.086);  //暗部沉入的墨青
static const float3 RAIN_TINT   = float3(0.855, 0.945, 1.010);  //冷雨轻罩（乘色）
static const float3 RAIN_LAKE   = float3(0.520, 0.620, 0.640);  //镜像浊水乘色
static const float3 RAIN_FOG    = float3(0.085, 0.108, 0.126);  //湖底冷雾
static const float3 RAIN_UNDER  = float3(0.380, 0.460, 0.500);  //水下沉染（冷）
static const float3 RAIN_FOAM   = float3(0.620, 0.700, 0.720);  //缝线冷沫
static const float3 RAIN_SOAK   = float3(0.470, 0.520, 0.545);  //浸水纸乘暗（冷灰）
static const float3 RAIN_FIBER  = float3(0.720, 0.790, 0.810);  //湿纤维冷白
//====== 鬼火 ======
static const float3 WISP_GOLD   = float3(1.000, 0.740, 0.300);  //鬼火金（燃湖时渗入水体的光）

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//水线行波：一次落点扰动向两侧荡开的衰减波包。像素度量跨分辨率一致：
//基准波长约 100px、波前约 620px/寿命外扩、距离半衰约 100px、幅度随寿命线性退场；
//w=范围乘数等比放大波长/传播/衰减——大扰动荡长浪，密峰会读成音频波谱
float lineWaveOne(float uvx, float4 src) {
    float dpx = abs(uvx - src.x) * uScreenSize.x / max(src.w, 0.25);
    float gate = saturate((src.y * 620.0 - dpx) * 0.05);
    float ph = dpx * 0.062 - src.y * 16.0;
    return sin(ph) * exp2(-dpx * 0.010) * (1.0 - src.y) * gate * src.z;
}

float lineWaveSum(float uvx) {
    return lineWaveOne(uvx, uLineWave[0]) + lineWaveOne(uvx, uLineWave[1])
         + lineWaveOne(uvx, uLineWave[2]) + lineWaveOne(uvx, uLineWave[3]);
}

//撕纸遮罩：圆扩散 + 三频纤维毛边（大团湿斑/细碎裂纹/横向纤维丝）
//返回 x=覆盖遮罩 y=前沿 sd（对角线归一），两个 technique 与天空共用同一前沿
float2 tearMaskFront(float2 coords) {
    float diag = length(uScreenSize);
    float2 rel = (coords * uScreenSize - uSpreadOrigin) / diag;
    float dist = length(rel);
    float j0 = noiseTex(coords * 2.4 + uTime * 0.013);
    float j1 = noiseTex(coords * 6.9 - uTime * 0.016);
    float j2 = noiseTex(float2(coords.x * 14.0, coords.y * 3.4) + uTime * 0.020);
    float jag = j0 * 0.45 + j1 * 0.25 + j2 * 0.30;
    //毛边振幅随覆盖率成长：早期半径小，固定大振幅会把前沿拽成偏心歪圆
    float jagAmp = lerp(0.035, 0.175, smoothstep(0.10, 0.70, uSpreadProgress));
    float sd = dist + (jag - 0.5) * jagAmp - uSpreadProgress * 1.18;
    float useSpread = step(0.5, uSpreadMode);
    float mask = lerp(1.0, 1.0 - smoothstep(-0.010, 0.012, sd), useSpread);
    return float2(mask, sd);
}

//湿纸前沿三层：x=浸润带（旧世界侧湿渍变暗）y=湿纤维撕裂缘 z=撕缘内侧卷影
//始终整算、调用方乘门控合成：动态分支会在 FNA 效果翻译下损坏整个 effect
float3 paperFront(float2 coords, float sd) {
    float blotch = noiseTex(coords * 3.1 + uTime * 0.021) * 0.6
                 + noiseTex(coords * 8.2 - uTime * 0.011) * 0.4;
    float soak = exp2(-max(sd, 0.0) * 20.0) * step(0.0, sd) * (0.40 + 0.60 * blotch);
    //纤维噪声横向拉伸，撕缘读作断续的纸纤维而非光滑亮线
    float fiber = noiseTex(float2(coords.x * 17.0, coords.y * 4.2) - uTime * 0.05);
    float edge = exp(-sd * sd / 0.000055) * (0.30 + 0.70 * fiber);
    float curlSd = sd + 0.016;
    float curl = exp(-curlSd * curlSd / 0.00042) * step(sd, 0.0);
    return float3(soak, edge, curl);
}

//血暮环境调色：红是领域的colour保真，其余轻去饱和；暗部沉深绯
//鬼雨异化（uRain）后不再保红、去饱和加重、罩色与暗部全套转冷
float3 GradeDusk(float3 src, float d) {
    float luma = dot(src, LUMA_W);
    float redness = src.r - max(src.g, src.b);
    float redMask = smoothstep(0.05, 0.30, redness) * (1.0 - uRain);
    float3 c = lerp(src, luma.xxx, (0.22 + 0.16 * uRain) * (1.0 - redMask));
    c *= lerp(DUSK_TINT, RAIN_TINT, uRain);
    float shadowAmt = (1.0 - smoothstep(0.08, 0.50, luma)) * 0.44;
    c = lerp(c, lerp(DUSK_SHADOW, RAIN_SHADOW, uRain) * (0.5 + luma * 1.2), shadowAmt);
    //氛围级暗角，冷雨里略沉
    float vig = smoothstep(0.52, 1.05, d);
    c *= 1.0 - vig * (0.20 + 0.05 * uRain);
    return c;
}

//====== TechGrade：环境调色（NPC 层之前执行，画面里只有环境） ======
float4 PSGrade(float2 coords : TEXCOORD0) : COLOR0 {
    float3 src = tex2D(uImage0, coords).rgb;
    float d = length((coords - 0.5) * float2(uAspect, 1.0)) * 1.15;

    float3 graded = GradeDusk(src, d);
    float2 mf = tearMaskFront(coords);
    float3 final = lerp(src, graded, mf.x);

    //前沿浸润带压在旧世界侧：纸吸了水，先暗一圈再撕
    float3 fl = paperFront(coords, mf.y);
    float frontGate = step(0.5, uSpreadMode) * uFrontFade;
    final = lerp(final, final * lerp(SOAK_MUL, RAIN_SOAK, uRain), fl.x * 0.42 * frontGate);

    return float4(final, 1.0);
}

//====== TechUnify：全帧轻罩 + 血湖镜面 + 撕裂前沿（EndCapture 执行） ======
float4 PSUnify(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float3 src = tex2D(uImage0, uv).rgb;

    float2 mf = tearMaskFront(coords);
    float mask = mf.x;
    float sd = mf.y;

    //域内全帧轻罩：微量去饱和 + 轻血染，实体色相/轮廓仍清晰；异化后转冷罩不保红
    float luma = dot(src, LUMA_W);
    float redness = src.r - max(src.g, src.b);
    float redMask = smoothstep(0.05, 0.30, redness) * (1.0 - uRain);
    float3 tone = lerp(src, luma.xxx, (0.14 + 0.10 * uRain) * (1.0 - redMask));
    tone *= lerp(float3(1.030, 0.905, 0.885), float3(0.900, 0.965, 1.005), uRain);

    //水位线：稳定枢轴 + 噪声波动 + 落点行波（波动只动遮罩边界，不动镜像几何）
    float n0 = noiseTex(float2(uv.x * 2.6 + uTime * 0.020, uTime * 0.011));
    float n1 = noiseTex(float2(uv.x * 7.2 - uTime * 0.016, 0.41 + uTime * 0.027));
    float lineWave = (n0 - 0.5) * 1.4 + (n1 - 0.5) * 0.6;
    float waveSum = lineWaveSum(uv.x);
    float waterY = uWaterLevel + lineWave * uWaterWobble + waveSum;
    float below = uv.y - waterY;
    float belowMask = saturate(below * 320.0);

    //镜像采样：绕稳定缝线 uPivotY 垂直镜像，近水面涟漪扰动
    float seamProx = exp2(-abs(below) * 22.0);
    float2 muv = float2(uv.x, 2.0 * uPivotY - uv.y);
    muv.x += ((n0 - 0.5) * (0.0070 + seamProx * 0.016) + (n1 - 0.5) * 0.0042) * belowMask;
    muv.y += (n1 - 0.5) * 0.0062 * belowMask;
    //倒影恶犬替换人影：镜像源落在施术者身上的像素，把采样点水平推到身侧——
    //镜里出现的是他背后的天，人从倒影里被抹去
    float inCover = step(uCoverRect.x, muv.x) * step(muv.x, uCoverRect.z)
        * step(uCoverRect.y, muv.y) * step(muv.y, uCoverRect.w) * uCoverA;
    float coverEdgeX = lerp(uCoverRect.x - 0.004, uCoverRect.z + 0.004,
        step(0.5 * (uCoverRect.x + uCoverRect.z), muv.x));
    muv.x = lerp(muv.x, coverEdgeX, inCover);
    float2 cuv = clamp(muv, 0.002, 0.998);
    float3 mcol = tex2D(uImage0, cuv).rgb;
    float srcOk = saturate(muv.y * 16.0) * saturate((1.0 - muv.y) * 16.0);

    //镜像染色：去饱和→乘色→深度压暗→沉雾，血湖↔浊水按 uRain 全套混合
    //浑浊=去饱和更重、雾更浓更早、倒影更糊
    float mgrey = dot(mcol, float3(0.30, 0.55, 0.15));
    float3 mirror = lerp(mcol, mgrey.xxx, lerp(0.40, 0.58, uRain));
    mirror *= lerp(LAKE_TINT, RAIN_LAKE, uRain);
    float depth = saturate(below * 1.6);
    mirror *= 1.0 - depth * lerp(0.30, 0.36, uRain);
    float3 fogc = lerp(LAKE_FOG, RAIN_FOG, uRain);
    mirror = lerp(mirror, fogc, saturate(depth * lerp(0.42, 0.60, uRain) + (1.0 - srcOk)));

    //水下折射采样：被淹之物随水摆动。双频偏移水平为主，幅度随深度增长、
    //贴水线渐入——y 向偏移恒小于离线距离，采不到水线以上的像素
    float refrIn = saturate(below * 26.0);
    float refrAmp = (0.0045 + 0.0075 * saturate(below * 2.2)) * refrIn;
    float rn0 = noiseTex(float2(uv.x * 3.4 + uTime * 0.050, uv.y * 9.0 - uTime * 0.060));
    float rn1 = noiseTex(float2(uv.x * 11.0 - uTime * 0.090, uv.y * 21.0 + uTime * 0.110));
    float2 ruv = uv;
    ruv.x += ((rn0 - 0.5) * 1.3 + (rn1 - 0.5) * 0.7) * refrAmp;
    ruv.y += (rn1 - 0.5) * refrAmp * 0.45;
    float3 usrc = tex2D(uImage0, clamp(ruv, 0.002, 0.998)).rgb;

    //水下真实世界：透过湖水看到的沉暗世界，倒影浮在其上；浊水里更快没入雾底
    float uluma = dot(usrc, LUMA_W);
    float3 under = lerp(usrc, uluma.xxx, lerp(0.30, 0.44, uRain));
    under *= lerp(UNDER_TINT, RAIN_UNDER, uRain);
    under = lerp(under, fogc, saturate(depth * lerp(0.55, 0.70, uRain)));

    //反射率：贴缝掠射强、向深处弱（看穿浅水），战斗可读性也靠它；浊水反光钝
    float refl = lerp(0.34, 0.85, exp2(-max(below, 0.0) * 5.0));
    refl *= 1.0 - 0.35 * uRain;
    float3 lake = lerp(under, mirror, refl);

    //水面浮渣：贴水面漂的凝斑，浊水里更密
    float scum = saturate((n0 - 0.58) * 4.0) * exp2(-max(below, 0.0) * 24.0);
    lake *= 1.0 - scum * (0.10 + 0.15 * uFoamBoost + 0.10 * uRain);

    //镜内雨丝已删：同 KikasaFlip，假雨丝勿加回；雨感交给雨帘倒影与天穹雨幡

    //鬼火渗色：湖面燃着金火时浅水层被照透，随深快速衰减、随水面噪声微闪
    float wispLit = exp2(-max(below, 0.0) * 7.0) * uWispGlow;
    lake += WISP_GOLD * wispLit * (0.10 + 0.10 * n1);

    float3 domainCol = lerp(tone, lake, belowMask);
    float3 final = lerp(src, domainCol, mask);

    //缝线水沫：贴水位线的一线微光，噪声闪烁不与全屏同相；异化态叠雨点砸水的碎闪；
    //行波扰动处水膜增亮，搅动读得出来
    float seamBand = exp2(-abs(below) * 150.0);
    float foam = saturate((n1 - 0.35) * 2.2);
    float glintN = noiseTex(float2(uv.x * 5.0 - uTime * 0.05, 0.77));
    float waveGlow = saturate(abs(waveSum) * uScreenSize.y * 0.10);
    float3 foamCol = lerp(FOAM_COL, RAIN_FOAM, uRain);
    final += foamCol * seamBand * uSeamGlow * mask
        * (0.26 + 0.32 * glintN + 0.30 * foam * uFoamBoost + 0.40 * waveGlow);
    float spat = noiseTex(float2(uv.x * 22.0, uTime * 1.7));
    final += foamCol * step(0.80, spat) * seamBand * uSeamGlow * uRain * 0.22 * mask;
    //鬼火缝线金辉：水线被贴水的火照亮
    final += WISP_GOLD * seamBand * uWispGlow * mask * (0.16 + 0.20 * glintN);

    //湿纸撕裂前沿：浸润带压暗旧世界，湿纤维缘勾撕口，卷影垫出纸厚
    float3 fl = paperFront(coords, sd);
    float frontGate = step(0.5, uSpreadMode) * uFrontFade;
    final = lerp(final, final * lerp(SOAK_MUL, RAIN_SOAK, uRain), fl.x * 0.60 * frontGate);
    final += lerp(FIBER_COL, RAIN_FIBER, uRain) * fl.y * 0.50 * frontGate;
    final = lerp(final, final * 0.62, fl.z * 0.68 * frontGate);

    return float4(final, 1.0);
}

technique TechGrade {
    pass P0 {
        PixelShader = compile ps_3_0 PSGrade();
    }
}

technique TechUnify {
    pass P0 {
        PixelShader = compile ps_3_0 PSUnify();
    }
}
