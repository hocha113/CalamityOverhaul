// ============================================================================
//KikasaSky.fx 鬼伞血湖领域天空，跨0深度切片单次绘制，覆盖所有原版背景层
//血红黄昏：凝血暗红天穹 + 半沉湖平线下的凝血暗日（盘面比血光更暗）+ 湖面血光倒影柱
//        + 无山脊的无际血湖地平线（天空倒影+横向波光）
//        + 低垂湿重的暗红云带 + 地平血雾。死寂无雨无鸟。
//远湖破纸伞剪影已删（半椭圆壳+顶针 SDF 实机读成锅盖，2026-08 按玩家反馈去掉，别再加回来）
//鬼雨异化（uRain）：全套色板权重乘混合转湿墨冷青（禁红禁暖），凝血日褪成苍白溺月，
//        云底垂下倾斜雨幡与细密雨纹，远雷 uFlash 云底先亮（光先于声）；
//        雨幡/雷闪写在天穹函数里，湖面镜像重采时倒影免费同步
//构图与鬼切刻意分野：无金橙、无山脊、无鸟居、无飞鸟，地平线是水不是山
//开合浸染遮罩与 KikasaGrade 同公式（含纤维毛边与振幅成长），圈到哪天空换到哪
//s0=占位白图 s1=PerlinNoise；全部噪声输入为笛卡尔 UV
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;            //秒（全局视效时间）
float uSkyAlpha;        //0~1 天空整体在场
float2 uScreenSize;     //像素
float uCamX;            //真实相机 X 像素，视差用
float uCamY;            //真实相机 Y 像素
float uSpreadMode;      //0=全覆盖 1=开合撕纸
float uSpreadProgress;  //0~1 撕开覆盖
float2 uSpreadOrigin;   //撕裂原点（视口像素）
float uMaskTime;        //遮罩噪声时间，必须与 KikasaGrade 的 uTime 同源，否则毛边前沿错位
float uRain;            //0~1 鬼雨异化混合：血暮↔湿墨
float uFlash;           //0~1 雷闪包络，快起慢衰（异化态）
float uWaterLevel;      //血湖水线 uv.y：与 KikasaGrade 同公式，1.15(屏下) 涨到枢轴
float uWaterWobble;     //水线噪声波动幅度，与 KikasaGrade 同值
float4 uLineWave[4];    //水线行波源，与 KikasaGrade 同源同公式，垫底顶边跟着一起荡

//====== 血暮调色板 ======
static const float3 SKY_TOP    = float3(0.085, 0.012, 0.030);  //凝血暗红近黑
static const float3 SKY_MID    = float3(0.360, 0.048, 0.068);  //深绯
static const float3 SKY_LOW    = float3(0.760, 0.130, 0.085);  //地平伤口亮红
static const float3 SUN_CORE   = float3(0.300, 0.022, 0.032);  //凝血暗日盘面
static const float3 SUN_RIM    = float3(0.980, 0.300, 0.140);  //盘缘一线亮红
static const float3 SUN_HAZE   = float3(0.900, 0.180, 0.100);  //日周血光
static const float3 CLOUD_COL  = float3(0.205, 0.038, 0.048);  //浸血云体
static const float3 CLOUD_EDGE = float3(0.780, 0.150, 0.095);  //云底伤口红描边
static const float3 LAKE_DIM   = float3(0.520, 0.180, 0.180);  //湖面倒影乘暗
static const float3 LAKE_DEEP  = float3(0.110, 0.014, 0.026);  //湖向下沉底色
static const float3 MIST_COL   = float3(0.470, 0.095, 0.085);  //地平血雾
//====== 鬼雨异化色板（压顶湿墨，禁红禁暖） ======
static const float3 RAIN_SKY_TOP    = float3(0.026, 0.032, 0.040);  //头顶近黑沉云
static const float3 RAIN_SKY_MID    = float3(0.085, 0.105, 0.115);  //墨青
static const float3 RAIN_SKY_LOW    = float3(0.225, 0.262, 0.268);  //地平尸青雾光
static const float3 RAIN_SUN_CORE   = float3(0.500, 0.545, 0.555);  //溺月苍白盘面
static const float3 RAIN_SUN_RIM    = float3(0.620, 0.670, 0.680);  //月缘惨白
static const float3 RAIN_SUN_HAZE   = float3(0.300, 0.345, 0.355);  //月晕湿光
static const float3 RAIN_CLOUD      = float3(0.050, 0.060, 0.070);  //沉云
static const float3 RAIN_CLOUD_EDGE = float3(0.180, 0.212, 0.218);  //云底衬光
static const float3 RAIN_LAKE_DIM   = float3(0.300, 0.360, 0.380);  //浊水倒影乘暗
static const float3 RAIN_LAKE_DEEP  = float3(0.026, 0.034, 0.042);  //浊水沉底
static const float3 RAIN_MIST       = float3(0.140, 0.170, 0.180);  //地平潮雾
static const float3 RAIN_SHAFT      = float3(0.300, 0.345, 0.355);  //雨幡帘
static const float3 RAIN_FLASH      = float3(0.550, 0.620, 0.640);  //雷闪惨白

//假湖平线 uv.y 的上限：实际湖平线随真水线动态下压（站湖面时贴水线上方，
//飞高俯瞰时回到此构图值），见 PSSky 的 horizonY
#define HORIZON_MAX 0.66

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//水线行波：与 KikasaGrade.lineWaveOne 完全同式，两边水线在波峰处也逐像素重合
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

float fbm2(float2 uv) {
    return noiseTex(uv) * 0.65 + noiseTex(uv * 2.7 + 13.1) * 0.35;
}

//天穹半球：渐变 + 凝血暗日/溺月 + 云带 + 异化雨幡与雷闪；
//above 空间（uv.y < horizonY）也供湖面镜像重采，雨幡/雷闪因此在倒影里免费同步
float3 skyDome(float2 uv, float aspect, float horizonY) {
    //三段渐变：血暮往地平线越烧越亮；湿墨往地平只剩尸青雾光
    float grad = saturate(uv.y / horizonY);
    float3 col = lerp(lerp(SKY_TOP, RAIN_SKY_TOP, uRain),
        lerp(SKY_MID, RAIN_SKY_MID, uRain), smoothstep(0.05, 0.58, grad));
    col = lerp(col, lerp(SKY_LOW, RAIN_SKY_LOW, uRain), smoothstep(0.52, 0.97, grad));
    float wash = fbm2(uv * float2(1.5 * aspect, 1.5) + float2(uTime * 0.005, uCamY * 0.00001));
    col *= 0.93 + wash * 0.14;

    //凝血暗日↔溺月：同一轮盘，血形态盘面比血光更暗，异化后褪成苍白月斑、缘光收敛
    //盘心钉在湖平线上，水线动构图跟着动——半沉的暗日始终泡在血湖里
    float2 sunC = float2(0.620 - uCamX * 0.000010, horizonY);
    float2 ds = (uv - sunC) * float2(aspect, 1.0);
    float sr = length(ds);
    float sun = 1.0 - smoothstep(0.128, 0.142, sr);
    float rim = exp(-pow(abs(sr - 0.142) / 0.016, 2.0));
    float haze = exp(-pow(max(sr - 0.10, 0.0) / 0.30, 1.5));
    col += lerp(SUN_HAZE, RAIN_SUN_HAZE, uRain) * haze * lerp(0.55, 0.38, uRain);
    col = lerp(col, lerp(SUN_CORE, RAIN_SUN_CORE, uRain), sun);
    col += lerp(SUN_RIM, RAIN_SUN_RIM, uRain) * rim * lerp(0.85, 0.40, uRain);

    //低垂湿重的云带：横长条带极慢漂，血形态云底一线伤口红、异化换冷衬光
    float2 cuv = float2(uv.x + uCamX * 0.016 / uScreenSize.x + uTime * 0.0028, uv.y);
    float cField = fbm2(cuv * float2(1.7, 8.5));
    float yEnv = smoothstep(0.04, 0.16, uv.y) * (1.0 - smoothstep(0.42, 0.60, uv.y));
    float band = smoothstep(0.545, 0.585, cField) * yEnv;
    float edge = band * (1.0 - band) * 4.0;
    col = lerp(col, lerp(CLOUD_COL, RAIN_CLOUD, uRain), band * (0.82 + 0.06 * uRain));
    col += lerp(CLOUD_EDGE, RAIN_CLOUD_EDGE, uRain) * edge * 0.30;

    //倾斜雨幡（异化态）：云底垂向湖面的冷灰雨柱，两层缓移
    float su1 = (uv.x + uv.y * 0.16) * 1.9 + uCamX * 0.05 / uScreenSize.x + uTime * 0.006;
    float shaft = smoothstep(0.60, 0.82, noiseTex(float2(su1, 0.31))) * 0.6;
    float su2 = (uv.x + uv.y * 0.13) * 2.8 + uCamX * 0.08 / uScreenSize.x + uTime * 0.010;
    shaft += smoothstep(0.62, 0.84, noiseTex(float2(su2, 0.67))) * 0.4;
    float shaftEnv = smoothstep(0.10, 0.30, uv.y) * (1.0 - smoothstep(0.56, 0.66, uv.y));
    col = lerp(col, RAIN_SHAFT, shaft * shaftEnv * 0.30 * uRain);
    //幡内细密下落雨纹
    float fallN = noiseTex(float2(uv.x * 4.6 + uv.y * 0.8, uv.y * 0.38 - uTime * 0.5));
    col += RAIN_SHAFT * saturate((fallN - 0.62) * 5.0) * (0.08 + shaft * 0.10) * shaftEnv * uRain;

    //雷闪：云底先亮起的惨白，光先于声；写在天穹里，湖面倒影同步反照。
    //不吃 uRain 门控——触发时机由 C# 决定，血形态的翻转起手也要这记凶兆冷闪
    float flashQ = uFlash * uFlash;
    float flashGrad = 1.0 - smoothstep(0.05, 0.55, uv.y);
    col += RAIN_FLASH * flashQ * (0.20 + band * 0.45) * flashGrad;

    return col;
}

//撕纸遮罩：公式与 KikasaGrade 完全一致（含纤维毛边与振幅成长），圈到哪天空换到哪
float tearMask(float2 coords) {
    float diag = length(uScreenSize);
    float2 rel = (coords * uScreenSize - uSpreadOrigin) / diag;
    float dist = length(rel);
    float j0 = noiseTex(coords * 2.4 + uMaskTime * 0.013);
    float j1 = noiseTex(coords * 6.9 - uMaskTime * 0.016);
    float j2 = noiseTex(float2(coords.x * 14.0, coords.y * 3.4) + uMaskTime * 0.020);
    float jag = j0 * 0.45 + j1 * 0.25 + j2 * 0.30;
    float jagAmp = lerp(0.035, 0.175, smoothstep(0.10, 0.70, uSpreadProgress));
    float sd = dist + (jag - 0.5) * jagAmp - uSpreadProgress * 1.18;
    return lerp(1.0, 1.0 - smoothstep(-0.010, 0.012, sd), step(0.5, uSpreadMode));
}

float4 PSSky(float2 coords : TEXCOORD0) : COLOR0 {
    if (uSkyAlpha < 0.004) {
        return float4(0, 0, 0, 0);
    }
    float2 uv = coords;
    float aspect = uScreenSize.x / uScreenSize.y;

    //真水线：与 KikasaGrade 同公式同噪声同时基（uMaskTime=EffectTime），逐像素重合；
    //水线以下的天空全部换成实体湖体，垫在 TechUnify 湖面之下遮死原版天空
    float wn0 = noiseTex(float2(uv.x * 2.6 + uMaskTime * 0.020, uMaskTime * 0.011));
    float wn1 = noiseTex(float2(uv.x * 7.2 - uMaskTime * 0.016, 0.41 + uMaskTime * 0.027));
    float waterY = uWaterLevel + ((wn0 - 0.5) * 1.4 + (wn1 - 0.5) * 0.6) * uWaterWobble
        + lineWaveSum(uv.x);
    float belowMask = saturate((uv.y - waterY) * 320.0);

    //假湖平线随真水线下压：站湖面时远湖带紧贴水线上方，飞高俯瞰回到 HORIZON_MAX 构图；
    //夹到正区间防梯度除零，相机全没入水下时 belowMask 满幅、此值不再可见
    float horizonY = clamp(min(HORIZON_MAX, uWaterLevel - 0.05), 0.05, HORIZON_MAX);

    //天穹与湖面镜像各算一遍，按湖区遮罩选择（直线算术，无分支）
    float3 dome = skyDome(uv, aspect, horizonY);

    //湖面：天穹绕湖平线的垂直镜像，横向波纹扰动 + 压暗 + 向下沉底；浊水倒影更糊更沉
    float lakeDepth = max(uv.y - horizonY, 0.0);
    float wob = (noiseTex(float2(uv.x * 3.2 + uTime * 0.02, uv.y * 9.0)) - 0.5)
        * (0.006 + lakeDepth * 0.05) * (1.0 + 0.8 * uRain);
    float2 muv = float2(uv.x + wob, 2.0 * horizonY - uv.y);
    float3 lake = skyDome(muv, aspect, horizonY) * lerp(LAKE_DIM, RAIN_LAKE_DIM, uRain);
    //横向波光：拉扁的噪声亮丝，近岸密远处疏；浊水波光钝
    float streak = noiseTex(float2(uv.x * 2.6 + uTime * 0.015, uv.y * 46.0));
    streak = saturate((streak - 0.60) * 5.0);
    lake += lerp(SUN_HAZE, RAIN_SUN_HAZE, uRain) * streak
        * exp2(-lakeDepth * 9.0) * lerp(0.16, 0.10, uRain);
    //向下沉入死水底色，浊水沉得更快
    lake = lerp(lake, lerp(LAKE_DEEP, RAIN_LAKE_DEEP, uRain),
        smoothstep(0.0, lerp(0.30, 0.22, uRain), lakeDepth));

    float lakeArea = smoothstep(horizonY - 0.002, horizonY + 0.006, uv.y);
    float3 col = lerp(dome, lake, lakeArea);

    //湖平线本体：一线更亮的水膜光
    float seam = exp(-pow((uv.y - horizonY) / 0.0045, 2.0));
    col += lerp(SUN_RIM, RAIN_SUN_RIM, uRain) * seam * lerp(0.22, 0.15, uRain);

    //地平雾：湖平线上下各一带，噪声絮动；血雾↔潮雾
    float mistN = fbm2(uv * float2(2.0 * aspect, 3.2) + float2(uTime * 0.010, 0.0));
    float mistBand = exp(-pow((uv.y - horizonY) / 0.10, 2.0));
    col = lerp(col, lerp(MIST_COL, RAIN_MIST, uRain), mistBand * (0.24 + mistN * 0.30));

    //雷闪在湖平线的一线反照
    col += RAIN_FLASH * uFlash * uFlash * seam * 0.10;

    //真水线以下读作一整块血团：远湖细节向死水底色加速沉没，
    //近线薄带里波光还探得进来一点，被 TechUnify 的湖面重染后成为水下暗底
    float bodyDepth = max(uv.y - waterY, 0.0);
    col = lerp(col, lerp(LAKE_DEEP, RAIN_LAKE_DEEP, uRain),
        belowMask * smoothstep(0.0, 0.24, bodyDepth) * 0.90);

    float mask = tearMask(coords);
    //水线以上留 3% 透底衬原版天光，水线以下抬满遮死天空
    float alpha = uSkyAlpha * lerp(0.97, 1.0, belowMask) * mask;
    return float4(col * alpha, alpha);
}

technique TechSky {
    pass P0 {
        PixelShader = compile ps_3_0 PSSky();
    }
}
