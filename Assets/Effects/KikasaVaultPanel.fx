// ============================================================================
//KikasaVaultPanel.fx v2 湖窗面板：撕开的湿纸口子里看血湖，不是圆角卡片。
//外形＝噪蚀撕纸 SDF（三频毛边，与 KikasaGrade 撕纸遮罩同族），
//uOpen 驱动开窗孔径：自锚线（撕口）向上下撕开，上侧先行下侧微滞；
//撕缘湿纤维苍白 + 缘内浸润沉暗，均由着色器自持，不叠 CPU 黑块。
//内容＝血暮空气（漂雾）/ 动态水位 uWaterY（开窗时湖水在窗里涨起）/
//深血水（双频横流+浮渣+湿亮+自缝线下垂的光柱）/ 缝线血沫（噪声闪烁+波动）。
//uHoverX/uHoverGlow：悬停列的水下血光，水知道你在看哪一件。
//门控全走 step/lerp 无动态分支；输出预乘。s0=白像素 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;         //秒
float uAlpha;        //整体透明度
float2 uResolution;  //面板像素尺寸
float uWaterY;       //当前水位 uv.y（开窗动画里从 ~0.95 涨到 0.30）
float uSlitY;        //撕口锚线 uv.y（孔径自此撕开，取水位终值）
float uOpen;         //0~1 撕开孔径
float uStir;         //0~1 活性（悬停/提取/涨水时湖水更躁）
float uHoverX;       //悬停列中心 uv.x，<0 表示无
float uHoverGlow;    //0~1 悬停列血光强度

static const float3 LAKE_TINT = float3(0.930, 0.300, 0.270);
static const float3 LAKE_FOG  = float3(0.170, 0.024, 0.036);
static const float3 FOAM_COL  = float3(0.965, 0.520, 0.440);
static const float3 FIBER_COL = float3(0.880, 0.795, 0.690);
static const float3 AIR_DEEP  = float3(0.030, 0.008, 0.012);
static const float3 AIR_WARM  = float3(0.092, 0.020, 0.028);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//撕纸外形：竖向孔径（上先下滞）+ 横向盒限 + 三频毛边
//返回 x=面板遮罩 y=有符号距离（px，负在内）
float2 tearMask(float2 uv) {
    float2 pc = (uv - 0.5) * uResolution;
    float2 halfSize = uResolution * 0.5 - 12.0;
    float slitCy = (uSlitY - 0.5) * uResolution.y;

    float openE = 1.0 - pow(1.0 - saturate(uOpen), 3.0);
    float upNow = (slitCy + halfSize.y) * saturate(openE * 1.15);
    float dnNow = (halfSize.y - slitCy) * saturate((openE - 0.12) / 0.88);

    float dy = pc.y - slitCy;
    //上下两侧各自的越界量，max 即竖向距离
    float vy = max(-dy - upNow, dy - dnNow);
    float vx = abs(pc.x) - halfSize.x;
    float d = max(vx, vy);

    //三频湿纸毛边，正在撕开的前沿也吃同一套噪声
    float j0 = noiseTex(uv * float2(2.1, 3.1) + uTime * 0.010);
    float j1 = noiseTex(uv * float2(5.8, 6.6) - uTime * 0.013);
    float j2 = noiseTex(float2(uv.x * 11.0, uv.y * 3.0) + uTime * 0.016);
    float jag = j0 * 0.45 + j1 * 0.25 + j2 * 0.30;
    d += (jag - 0.5) * 30.0;

    float mask = 1.0 - smoothstep(-1.5, 1.5, d);
    return float2(mask, d);
}

float4 PSPanel(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float2 mk = tearMask(uv);

    //====== 血暮空气（水线以上） ======
    float airT = saturate(uv.y / max(uWaterY, 0.001));
    float3 air = lerp(AIR_DEEP, AIR_WARM, airT);
    float mist = noiseTex(float2(uv.x * 1.4 + uTime * 0.012, uv.y * 2.6));
    air += float3(0.10, 0.02, 0.03) * mist * 0.30 * airT;

    //====== 深血水（动态水位以下） ======
    float rel = uv.y - uWaterY;
    float depth = saturate(rel / max(1.0 - uWaterY, 0.001));
    float3 water = lerp(float3(0.150, 0.028, 0.038), float3(0.046, 0.008, 0.013), depth);
    //双频横流，方向相对
    float f0 = noiseTex(float2(uv.x * 1.1 - uTime * 0.016, uv.y * 1.9));
    float f1 = noiseTex(float2(uv.x * 2.3 + uTime * 0.011, uv.y * 4.1 + 3.7));
    float flowAmp = 0.16 + 0.10 * uStir;
    water += LAKE_TINT * flowAmp * (f0 * 0.62 + f1 * 0.48) * (1.0 - depth * 0.55);
    //浮渣斑贴近水面
    float scum = saturate((f0 - 0.60) * 4.5) * (1.0 - depth);
    water *= 1.0 - scum * 0.22;
    //稀疏湿亮
    float glint = pow(saturate(noiseTex(float2(uv.x * 2.8, uv.y * 1.2) + uTime * 0.035) * 1.1), 9.0);
    water += FOAM_COL * glint * 0.30 * (1.0 - depth * 0.7);
    //光柱：自缝线垂下的两组竖亮带，慢漂
    float s0 = noiseTex(float2(uv.x * 2.8 + uTime * 0.006, 0.31));
    float s1 = noiseTex(float2(uv.x * 5.3 - uTime * 0.004, 0.77));
    float shafts = (pow(s0, 2.2) * 0.8 + pow(s1, 3.0) * 0.5) * exp2(-max(rel, 0.0) * 7.0);
    water += (FOAM_COL * 0.10 + LAKE_TINT * 0.08) * shafts;

    //悬停列血光：水知道你在看哪一件
    float dxPx = (uv.x - uHoverX) * uResolution.x;
    float colGlow = exp2(-dxPx * dxPx / 1300.0)
        * step(0.0, uHoverX) * saturate(uHoverGlow);
    water += LAKE_TINT * colGlow * 0.20 * (1.0 - depth * 0.6);

    //====== 缝线血沫 ======
    float wob = (noiseTex(float2(uv.x * 2.4 - uTime * 0.020, 0.71)) - 0.5) * 0.014;
    float seamD = rel + wob;
    float seamBand = exp2(-abs(seamD) * (170.0 - 60.0 * uStir));
    float flicker = noiseTex(float2(uv.x * 3.4 - uTime * 0.05, 0.41));
    float3 seam = FOAM_COL * seamBand
        * (0.34 + 0.38 * flicker + 0.30 * uStir + 0.55 * colGlow);

    //====== 合成 ======
    float toWater = saturate(seamD * 240.0);
    float3 col = lerp(air, water, toWater) + seam;
    col = lerp(col, LAKE_FOG, depth * toWater * 0.35);

    //撕缘湿纤维苍白 + 缘内浸润沉暗（着色器自持，不靠 CPU 叠黑）
    float d = mk.y;
    float fiberN = noiseTex(float2(uv.x * 14.0, uv.y * 10.0) - uTime * 0.05);
    float fiber = exp(-d * d / 9.0) * (0.35 + 0.65 * fiberN);
    col += FIBER_COL * fiber * 0.40;
    float soakIn = exp2(min(d, 0.0) * 0.16);
    col *= 1.0 - soakIn * 0.26;

    //预乘输出，贴合引擎 (One, InvSrcAlpha) 混合
    float aOut = mk.x * uAlpha * 0.97;
    return float4(col * aOut, aOut);
}

technique TechPanel {
    pass P0 {
        PixelShader = compile ps_3_0 PSPanel();
    }
}
