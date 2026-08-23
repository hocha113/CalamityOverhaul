// ============================================================================
//ShenyoMenuLake.fx 鬼湖夜雨主菜单全景
//TechLake：倒置明度压顶天穹（头顶近黑沉云、地平尸青雾光）+ 溺月（画云前被云吞）
//         + 双层流动沉云 + 倾斜雨幡 + 低平远岸一线 + 镜面鬼湖
//         （月光路铺向观者、碎波挂亮、雨点碎闪、立影足下接触涟漪与压暗）
//         + 水线潮雾带 + 雷闪惨白，Opaque 整幅铺底
//TechRain：前景两层视差雨幕（溺月逆光挂亮），预乘输出进 AlphaBlend
//湿墨色板：冷灰青/尸斑青/灰白，禁红禁暖
//s0=占位白图（本文件不采样，批次主纹理占位） s1=PerlinNoise
//绑定噪声实测值域 0.227~0.776，高阈值一律先过 nrm 归一
//全笛卡尔无极角；直线算术无动态分支
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uIntensity;   //0-1 入场渐显
float2 uScreenSize; //像素
float2 uParallax;   //近层满额视差偏移（uv），元素按远近折减
float uFlash;       //0-1 雷闪包络
float uHorizon;     //水线 y（uv）
float2 uMoonUv;     //溺月圆心（uv）
float4 uFeet[8];    //立影足点：xy=uv z=在场0-1 w=接触半径（uv 纵向尺度）

//====== 湿墨调色板 ======
static const float3 SKY_TOP = float3(0.020, 0.026, 0.034);     //头顶近黑沉云顶
static const float3 SKY_HOR = float3(0.185, 0.222, 0.232);     //地平尸青雾光
static const float3 CLOUD_DARK = float3(0.043, 0.053, 0.064);  //沉云
static const float3 CLOUD_UNDER = float3(0.165, 0.196, 0.204); //云底衬光
static const float3 MOON_PALE = float3(0.640, 0.690, 0.700);   //溺月惨白
static const float3 SHAFT_PALE = float3(0.290, 0.335, 0.345);  //雨幡帘
static const float3 RIDGE_FAR = float3(0.080, 0.100, 0.112);   //远岸墨青
static const float3 WATER_DEEP = float3(0.014, 0.020, 0.027);  //近处深水
static const float3 WATER_HOR = float3(0.135, 0.168, 0.178);   //水线远水
static const float3 WATER_SHINE = float3(0.310, 0.360, 0.370); //碎波反光
static const float3 MIST_PALE = float3(0.200, 0.235, 0.245);   //潮雾
static const float3 RING_PALE = float3(0.430, 0.490, 0.505);   //接触涟漪
static const float3 FLASH_PALE = float3(0.560, 0.630, 0.650);  //雷闪惨白

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//绑定噪声实测值域 0.227~0.776
float nrm(float v) {
    return saturate((v - 0.227) / 0.549);
}

float fbm2(float2 uv) {
    return noiseTex(uv) * 0.65 + noiseTex(uv * 2.7 + 13.1) * 0.35;
}

float4 PSLake(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float aspect = uScreenSize.x / uScreenSize.y;

    //====== 天穹：倒置明度，头顶最黑、水线雾光反亮 ======
    float yn = saturate(uv.y / uHorizon);
    float2 skyPar = uParallax * 0.18;
    float grad = pow(smoothstep(0.0, 0.95, yn), 1.30);
    float wash = fbm2(float2((uv.x + skyPar.x) * 1.5 * aspect, yn * 1.5) + float2(uTime * 0.004, 0.0));
    float3 skyCol = lerp(SKY_TOP, SKY_HOR, grad) * (0.90 + wash * 0.20);

    //====== 溺月：惨白晕斑慢呼吸，画在云前被云吞没 ======
    float2 moonP = uMoonUv + skyPar;
    float2 dm = (uv - moonP) * float2(aspect, 1.0);
    float mr = length(dm);
    float breathe = 0.75 + 0.25 * sin(uTime * 0.07);
    float halo = exp(-pow(mr / 0.235, 1.55));
    float core = exp(-pow(mr / 0.058, 2.0));
    skyCol += MOON_PALE * (halo * 0.30 + core * 0.62) * breathe;

    //====== 沉云两层：大团慢漂压顶 + 低掠碎云；月周留一圈云隙 ======
    float moonClear = saturate(1.0 - core * 0.62 - halo * 0.32);
    float2 cloudPar = uParallax * 0.24;
    float2 cuv1 = float2(uv.x * 1.4 + cloudPar.x + uTime * 0.0021, yn * 2.2);
    float cloud1 = smoothstep(0.38, 0.72, fbm2(cuv1)) * (1.0 - smoothstep(0.30, 0.70, yn));
    cloud1 *= moonClear;
    float cloudEdge = cloud1 * (1.0 - cloud1) * 4.0;
    skyCol = lerp(skyCol, CLOUD_DARK, cloud1 * 0.85);
    skyCol += CLOUD_UNDER * cloudEdge * 0.20 * smoothstep(0.15, 0.50, yn);

    float2 cuv2 = float2(uv.x * 2.6 + cloudPar.x * 1.4 + uTime * 0.015, yn * 3.4 + 7.7);
    float cloud2 = smoothstep(0.52, 0.78, fbm2(cuv2))
        * smoothstep(0.18, 0.38, yn) * (1.0 - smoothstep(0.60, 0.85, yn));
    skyCol = lerp(skyCol, CLOUD_DARK * 1.22, cloud2 * 0.50 * moonClear);

    //====== 远景雨幡：云底垂到水线的倾斜雨柱，两层缓移 ======
    float shaftEnv = smoothstep(0.25, 0.55, yn) * (1.0 - smoothstep(0.86, 1.0, yn));
    float2 shaftPar = uParallax * 0.30;
    float su1 = (uv.x + shaftPar.x + yn * 0.16) * 1.7 + uTime * 0.006;
    float su2 = (uv.x + shaftPar.x + yn * 0.21) * 2.6 - uTime * 0.009;
    float shafts = smoothstep(0.58, 0.82, noiseTex(float2(su1, 0.13))) * 0.6
        + smoothstep(0.58, 0.82, noiseTex(float2(su2, 0.53))) * 0.4;
    skyCol = lerp(skyCol, SHAFT_PALE, shafts * shaftEnv * 0.30);
    //幡内细密下落雨纹
    float fall = noiseTex(float2(uv.x * 4.6 + yn * 0.8, yn * 0.38 - uTime * 0.5));
    skyCol += SHAFT_PALE * saturate((fall - 0.62) * 5.0) * shafts * shaftEnv * 0.15;

    //====== 远岸一线：被雨压平的低矮墨影，贴着水线 ======
    float ru = uv.x + uParallax.x * 0.40;
    float rh = fbm2(float2(ru * 1.15 + 4.3, 0.31));
    float ridgeY = uHorizon - 0.010 - (rh - 0.5) * 0.028;
    float ridgeM = smoothstep(ridgeY - 0.004, ridgeY + 0.004, uv.y);
    skyCol = lerp(skyCol, RIDGE_FAR, ridgeM * 0.90);

    //====== 雷闪（天侧）：云底先亮的惨白 ======
    float flashQ = uFlash * uFlash;
    float flashGrad = 1.0 - smoothstep(0.08, 0.62, yn);
    skyCol += FLASH_PALE * flashQ * (0.22 + cloud1 * 0.50) * flashGrad;

    //====== 鬼湖：镜面死水，月光路铺向观者 ======
    float d = saturate((uv.y - uHorizon) / max(1.0 - uHorizon, 0.001));
    float3 lakeCol = lerp(WATER_HOR, WATER_DEEP, smoothstep(0.0, 0.55, d));
    //水线一线亮
    lakeCol += WATER_SHINE * exp2(-d * 130.0) * 0.50;

    //月光路：随透视向观者展宽，横向微摆
    float pathX = uMoonUv.x + uParallax.x * lerp(0.18, 0.85, d);
    float pw = lerp(0.014, 0.170, pow(d, 1.35));
    float pWob = (noiseTex(float2(uv.y * 3.0 - uTime * 0.05, 0.77)) - 0.5) * 0.030 * d;
    float lp = exp(-pow(abs(uv.x - pathX + pWob) / max(pw, 0.001), 1.7));

    //碎波挂亮：横向拉丝的滚动噪声，近水线密、近岸疏
    float shim = nrm(noiseTex(float2(uv.x * 7.0 + uTime * 0.02,
        uv.y * lerp(46.0, 9.0, d) - uTime * 0.33)));
    float glint = smoothstep(0.55, 0.95, shim);
    lakeCol += MOON_PALE * lp * (0.10 + glint * 0.55) * breathe;
    lakeCol += WATER_SHINE * glint * 0.07 * (0.30 + d * 0.70);

    //雨点碎闪：高频阈值 + 逐拍重播种
    float spark = step(0.90, nrm(noiseTex(
        uv * float2(34.0 * aspect, 30.0) + floor(uTime * 7.0) * 0.37)));
    lakeCol += MOON_PALE * spark * (0.04 + lp * 0.16 + d * 0.08);

    //====== 立影足下：接触压暗 + 双圈扩散涟漪 ======
    [unroll]
    for (int i = 0; i < 8; i++) {
        float4 f = uFeet[i];
        float2 dv = (uv - f.xy) * float2(aspect, 3.2);
        float dist = length(dv);
        float rw = max(f.w, 0.001);
        float ph1 = frac(uTime * 0.42 + (float)i * 0.373);
        float ph2 = frac(ph1 + 0.5);
        float r1 = rw * (0.20 + 0.80 * ph1);
        float r2 = rw * (0.20 + 0.80 * ph2);
        float ring1 = saturate(1.0 - abs(dist - r1) / (rw * 0.14));
        float ring2 = saturate(1.0 - abs(dist - r2) / (rw * 0.14));
        float rings = ring1 * ring1 * (1.0 - ph1) + ring2 * ring2 * (1.0 - ph2) * 0.7;
        float blob = saturate(1.0 - dist / (rw * 0.55));
        lakeCol = lerp(lakeCol, WATER_DEEP, blob * blob * 0.55 * f.z);
        lakeCol += RING_PALE * rings * 0.26 * f.z;
    }

    //====== 雷闪（水侧）：月光路与水线回照 ======
    lakeCol += FLASH_PALE * flashQ * (0.09 + lp * 0.22);

    //====== 天水合成：水线微颤不走激光直线 ======
    float wl = uHorizon + (noiseTex(float2(uv.x * 3.1 + uTime * 0.02, 0.91)) - 0.5) * 0.0035;
    float waterSide = smoothstep(wl - 0.0012, wl + 0.0012, uv.y);
    float3 col = lerp(skyCol, lakeCol, waterSide);

    //====== 水线潮雾带：两侧洇开的惨白湿气 ======
    float mistBand = exp(-abs(uv.y - uHorizon) * 24.0);
    float mistN = fbm2(float2(uv.x * 2.2 * aspect + uTime * 0.010, 0.61));
    col += MIST_PALE * mistBand * (0.30 + 0.45 * mistN) * 0.40;

    //轻渐晕聚焦画面中心
    float2 vd = (uv - 0.5) * float2(aspect, 1.0);
    float vig = 1.0 - 0.20 * saturate(pow(length(vd) / 0.95, 2.2));
    col *= vig;

    return float4(col * uIntensity, 1.0);
}

//====== 前景雨幕：两层视差雨丝，溺月逆光挂亮 ======
static const float3 RAIN_PALE = float3(0.470, 0.530, 0.548);

float4 PSRain(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float aspect = uScreenSize.x / uScreenSize.y;

    //溺月逆光：雨丝行经月晕与月光路时被点亮
    float2 dm = (uv - uMoonUv) * float2(aspect, 1.0);
    float backlit = exp2(-dot(dm, dm) * 7.5);

    //雨丝=高x频×低y频的竖长条带快速下滚；再乘一路错相噪声把长条掐断成段
    //y坐标混入x分量：x向重度欠采样时若屏幕行恒采贴图同一行，行亮度差会读成横向密度条带
    //中层：细、密、稍缓
    float x1 = (uv.x + uv.y * 0.070 + uParallax.x * 0.55) * 44.0;
    float y1 = uv.y * 2.2 + uv.x * 0.31 - uTime * 2.9;
    float st1 = smoothstep(0.62, 0.88, nrm(noiseTex(float2(x1, y1))));
    st1 *= smoothstep(0.34, 0.72, nrm(noiseTex(float2(x1 * 0.113 + 7.7, uv.y * 0.55 - uTime * 2.1))));
    //近层：粗、疏、更快更斜
    float x2 = (uv.x + uv.y * 0.115 + uParallax.x * 1.00) * 24.0 + 5.7;
    float y2 = uv.y * 1.2 + uv.x * 0.23 - uTime * 3.6;
    float st2 = smoothstep(0.68, 0.92, nrm(noiseTex(float2(x2, y2))));
    st2 *= smoothstep(0.38, 0.74, nrm(noiseTex(float2(x2 * 0.147 + 3.1, uv.y * 0.40 - uTime * 2.6))));

    float rain = st1 * (0.30 + backlit * 0.55) + st2 * (0.42 + backlit * 0.66);
    rain *= (1.0 + uFlash * uFlash * 1.6) * uIntensity;

    float a = saturate(rain * 0.35);
    return float4(RAIN_PALE * rain, a);
}

technique TechLake {
    pass P0 {
        PixelShader = compile ps_3_0 PSLake();
    }
}

technique TechRain {
    pass P0 {
        PixelShader = compile ps_3_0 PSRain();
    }
}
