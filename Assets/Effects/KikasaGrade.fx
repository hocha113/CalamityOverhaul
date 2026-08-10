// ============================================================================
//KikasaGrade.fx 鬼伞血湖领域全屏调色，两个 technique 对应两个渲染时机
//TechGrade（NPC 层之前，只吃环境）：血暮调色（红罩+暗部沉深绯+非红轻去饱和）
//TechUnify（EndCapture，吃整帧含实体）：
//  轻血罩 + 血湖镜面（水位线以下真垂直镜像倒影，血染+深度血雾+浮渣+缝线血沫，
//  反射率贴缝强向深弱、透出水下真实世界）+ 湿纸撕裂前沿（浸润带/湿纤维缘/卷影）
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

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
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
float3 GradeDusk(float3 src, float d) {
    float luma = dot(src, LUMA_W);
    float redness = src.r - max(src.g, src.b);
    float redMask = smoothstep(0.05, 0.30, redness);
    float3 c = lerp(src, luma.xxx, 0.22 * (1.0 - redMask));
    c *= DUSK_TINT;
    float shadowAmt = (1.0 - smoothstep(0.08, 0.50, luma)) * 0.44;
    c = lerp(c, DUSK_SHADOW * (0.5 + luma * 1.2), shadowAmt);
    //氛围级暗角
    float vig = smoothstep(0.52, 1.05, d);
    c *= 1.0 - vig * 0.20;
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
    final = lerp(final, final * SOAK_MUL, fl.x * 0.42 * frontGate);

    return float4(final, 1.0);
}

//====== TechUnify：全帧轻罩 + 血湖镜面 + 撕裂前沿（EndCapture 执行） ======
float4 PSUnify(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float3 src = tex2D(uImage0, uv).rgb;

    float2 mf = tearMaskFront(coords);
    float mask = mf.x;
    float sd = mf.y;

    //域内全帧轻罩：微量去饱和 + 轻血染，实体色相/轮廓仍清晰
    float luma = dot(src, LUMA_W);
    float redness = src.r - max(src.g, src.b);
    float redMask = smoothstep(0.05, 0.30, redness);
    float3 tone = lerp(src, luma.xxx, 0.14 * (1.0 - redMask));
    tone *= float3(1.030, 0.905, 0.885);

    //水位线：稳定枢轴 + 噪声波动（波动只动遮罩边界，不动镜像几何）
    float n0 = noiseTex(float2(uv.x * 2.6 + uTime * 0.020, uTime * 0.011));
    float n1 = noiseTex(float2(uv.x * 7.2 - uTime * 0.016, 0.41 + uTime * 0.027));
    float lineWave = (n0 - 0.5) * 1.4 + (n1 - 0.5) * 0.6;
    float waterY = uWaterLevel + lineWave * uWaterWobble;
    float below = uv.y - waterY;
    float belowMask = saturate(below * 320.0);

    //镜像采样：绕稳定缝线 uPivotY 垂直镜像，近水面涟漪扰动
    float seamProx = exp2(-abs(below) * 22.0);
    float2 muv = float2(uv.x, 2.0 * uPivotY - uv.y);
    muv.x += ((n0 - 0.5) * (0.0042 + seamProx * 0.010) + (n1 - 0.5) * 0.0026) * belowMask;
    muv.y += (n1 - 0.5) * 0.0040 * belowMask;
    float2 cuv = clamp(muv, 0.002, 0.998);
    float3 mcol = tex2D(uImage0, cuv).rgb;
    float srcOk = saturate(muv.y * 16.0) * saturate((1.0 - muv.y) * 16.0);

    //镜像血染：去饱和→血红乘色→深度压暗→沉入血雾（深度以水位线起算）
    float mgrey = dot(mcol, float3(0.30, 0.55, 0.15));
    float3 mirror = lerp(mcol, mgrey.xxx, 0.40);
    mirror *= LAKE_TINT;
    float depth = saturate(below * 1.6);
    mirror *= 1.0 - depth * 0.30;
    mirror = lerp(mirror, LAKE_FOG, saturate(depth * 0.42 + (1.0 - srcOk)));

    //水下真实世界：透过血水看到的沉暗世界，倒影浮在其上
    float3 under = lerp(src, luma.xxx, 0.30);
    under *= UNDER_TINT;
    under = lerp(under, LAKE_FOG, saturate(depth * 0.55));

    //反射率：贴缝掠射强、向深处弱（看穿浅血水），战斗可读性也靠它
    float refl = lerp(0.42, 0.85, exp2(-max(below, 0.0) * 5.0));
    float3 lake = lerp(under, mirror, refl);

    //水面浮渣：贴水面漂的血凝斑块
    float scum = saturate((n0 - 0.58) * 4.0) * exp2(-max(below, 0.0) * 24.0);
    lake *= 1.0 - scum * (0.10 + 0.15 * uFoamBoost);

    float3 domainCol = lerp(tone, lake, belowMask);
    float3 final = lerp(src, domainCol, mask);

    //缝线血沫：贴水位线的一线微光，噪声闪烁不与全屏同相
    float seamBand = exp2(-abs(below) * 150.0);
    float foam = saturate((n1 - 0.35) * 2.2);
    float glintN = noiseTex(float2(uv.x * 5.0 - uTime * 0.05, 0.77));
    final += FOAM_COL * seamBand * uSeamGlow * mask
        * (0.26 + 0.32 * glintN + 0.30 * foam * uFoamBoost);

    //湿纸撕裂前沿：浸润带压暗旧世界，湿纤维缘勾撕口，卷影垫出纸厚
    float3 fl = paperFront(coords, sd);
    float frontGate = step(0.5, uSpreadMode) * uFrontFade;
    final = lerp(final, final * SOAK_MUL, fl.x * 0.60 * frontGate);
    final += FIBER_COL * fl.y * 0.50 * frontGate;
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
