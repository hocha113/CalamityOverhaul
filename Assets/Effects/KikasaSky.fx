// ============================================================================
//KikasaSky.fx 鬼伞血湖领域天空，跨0深度切片单次绘制，覆盖所有原版背景层
//血红黄昏：凝血暗红天穹 + 半沉湖平线下的凝血暗日（盘面比血光更暗）+ 湖面血光倒影柱
//        + 无山脊的无际血湖地平线（天空倒影+横向波光）+ 漂浮的破纸伞剪影
//        + 低垂湿重的暗红云带 + 地平血雾。死寂无雨无鸟。
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
static const float3 UMB_COL    = float3(0.052, 0.010, 0.020);  //纸伞剪影

//湖平线 uv.y，天空与湖的分界
#define HORIZON_Y 0.66

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float fbm2(float2 uv) {
    return noiseTex(uv) * 0.65 + noiseTex(uv * 2.7 + 13.1) * 0.35;
}

float hash1(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
}

//天穹半球：渐变 + 凝血暗日 + 云带；above 空间（uv.y < HORIZON_Y）也供湖面镜像重采
float3 skyDome(float2 uv, float aspect) {
    //三段血暮渐变，往地平线越烧越亮
    float grad = saturate(uv.y / HORIZON_Y);
    float3 col = lerp(SKY_TOP, SKY_MID, smoothstep(0.05, 0.58, grad));
    col = lerp(col, SKY_LOW, smoothstep(0.52, 0.97, grad));
    float wash = fbm2(uv * float2(1.5 * aspect, 1.5) + float2(uTime * 0.005, uCamY * 0.00001));
    col *= 0.93 + wash * 0.14;

    //凝血暗日：半沉在湖平线下，盘面比周围血光更暗，缘上一线亮红
    float2 sunC = float2(0.620 - uCamX * 0.000010, HORIZON_Y);
    float2 ds = (uv - sunC) * float2(aspect, 1.0);
    float sr = length(ds);
    float sun = 1.0 - smoothstep(0.128, 0.142, sr);
    float rim = exp(-pow(abs(sr - 0.142) / 0.016, 2.0));
    float haze = exp(-pow(max(sr - 0.10, 0.0) / 0.30, 1.5));
    col += SUN_HAZE * haze * 0.55;
    col = lerp(col, SUN_CORE, sun);
    col += SUN_RIM * rim * 0.85;

    //低垂湿重的暗红云带：横长条带极慢漂，云底一线伤口红
    float2 cuv = float2(uv.x + uCamX * 0.016 / uScreenSize.x + uTime * 0.0028, uv.y);
    float cField = fbm2(cuv * float2(1.7, 8.5));
    float yEnv = smoothstep(0.04, 0.16, uv.y) * (1.0 - smoothstep(0.42, 0.60, uv.y));
    float band = smoothstep(0.545, 0.585, cField) * yEnv;
    float edge = band * (1.0 - band) * 4.0;
    col = lerp(col, CLOUD_COL, band * 0.82);
    col += CLOUD_EDGE * edge * 0.30;

    return col;
}

//纸伞剪影：局部空间 y 向上、0=吃水线；伞盖=半椭圆壳 + 顶针
float umbrellaShape(float2 p) {
    float inX = saturate(1.0 - p.x * p.x);
    float domeY = 0.58 * sqrt(inX);
    float cov = (1.0 - smoothstep(domeY - 0.05, domeY + 0.02, p.y))
        * step(0.0, p.y) * step(abs(p.x), 1.02);
    float nub = 1.0 - smoothstep(0.040, 0.085, length(p - float2(0.0, 0.60)));
    return saturate(max(cov, nub));
}

//一列漂在湖面上的破纸伞：x=剪影 y=水下倒影；每把带各自的倾斜/吃水/慢漂
float2 umbrellaRow(float2 uv, float aspect, float parallax, float scale,
                   float density, float seed) {
    float driftDir = sign(hash1(seed * 3.3) - 0.5);
    float worldU = uv.x + uCamX * parallax / uScreenSize.x + uTime * 0.0014 * driftDir;
    float cellF = worldU * density;
    float cellId = floor(cellF);
    float h = hash1(cellId + seed);
    //六成空位，湖面稀稀落落
    float present = step(0.60, h);
    float s = scale * (0.80 + h * 0.40);
    //各自相位的极慢起伏，空间 hash 去同相
    float bob = sin(uTime * 0.35 + h * 6.2832) * 0.0045;
    float footY = HORIZON_Y + bob;

    float2 local;
    local.x = (frac(cellF) - 0.5) / density / s;
    local.y = (footY - uv.y) / s;
    //微倾，破伞歪着漂
    float tilt = (h - 0.5) * 0.30;
    float ct = cos(tilt);
    float st = sin(tilt);
    float2 lp = float2(local.x * ct - local.y * st, local.x * st + local.y * ct);

    float sil = umbrellaShape(lp) * present;
    //水下倒影：拉短、随水微晃
    float wob = (noiseTex(float2(uv.x * 6.0, uTime * 0.03)) - 0.5) * 0.14;
    float refl = umbrellaShape(float2(lp.x + wob, -lp.y * 1.45)) * present;
    return float2(sil, refl);
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

    //天穹与湖面镜像各算一遍，按湖区遮罩选择（直线算术，无分支）
    float3 dome = skyDome(uv, aspect);

    //湖面：天穹绕湖平线的垂直镜像，横向波纹扰动 + 压暗 + 向下沉底
    float lakeDepth = max(uv.y - HORIZON_Y, 0.0);
    float wob = (noiseTex(float2(uv.x * 3.2 + uTime * 0.02, uv.y * 9.0)) - 0.5)
        * (0.006 + lakeDepth * 0.05);
    float2 muv = float2(uv.x + wob, 2.0 * HORIZON_Y - uv.y);
    float3 lake = skyDome(muv, aspect) * LAKE_DIM;
    //横向波光：拉扁的噪声亮丝，近岸密远处疏
    float streak = noiseTex(float2(uv.x * 2.6 + uTime * 0.015, uv.y * 46.0));
    streak = saturate((streak - 0.60) * 5.0);
    lake += SUN_HAZE * streak * exp2(-lakeDepth * 9.0) * 0.16;
    //向下沉入死水底色
    lake = lerp(lake, LAKE_DEEP, smoothstep(0.0, 0.30, lakeDepth));

    float lakeArea = smoothstep(HORIZON_Y - 0.002, HORIZON_Y + 0.006, uv.y);
    float3 col = lerp(dome, lake, lakeArea);

    //湖平线本体：一线更亮的血光水膜
    float seam = exp(-pow((uv.y - HORIZON_Y) / 0.0045, 2.0));
    col += SUN_RIM * seam * 0.22;

    //漂浮的破纸伞：远近两列，倒影垫在剪影之前
    float2 umbFar = umbrellaRow(uv, aspect, 0.050, 0.042, 3.1, 3.7);
    float2 umbNear = umbrellaRow(uv, aspect, 0.110, 0.075, 1.7, 9.2);
    col = lerp(col, col * 0.55, (umbFar.y * 0.5 + umbNear.y * 0.6) * lakeArea);
    col = lerp(col, UMB_COL, saturate(umbFar.x * 0.88 + umbNear.x * 0.95));

    //地平血雾：湖平线上下各一带，噪声絮动
    float mistN = fbm2(uv * float2(2.0 * aspect, 3.2) + float2(uTime * 0.010, 0.0));
    float mistBand = exp(-pow((uv.y - HORIZON_Y) / 0.10, 2.0));
    col = lerp(col, MIST_COL, mistBand * (0.24 + mistN * 0.30));

    float mask = tearMask(coords);
    float alpha = uSkyAlpha * 0.97 * mask;
    return float4(col * alpha, alpha);
}

technique TechSky {
    pass P0 {
        PixelShader = compile ps_3_0 PSSky();
    }
}
