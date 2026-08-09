// ============================================================================
//OniRainSky.fx 鬼雨世界专属天空，跨0深度切片单次绘制，覆盖所有原版背景层
//倒置明度的压顶天穹：头顶近黑沉云、地平尸青雾光反亮 + 双层流动雨云 +
//倾斜大尺度雨幡 + 被雨晕开的惨白溺月（画在云前，被云吞没）+
//低平墨色远山两层视差 + 地平一线积水反光（呼应入场涨水）+
//雷闪先光后声：云底闪惨白，极稀有闪光瞬间露出巨大伞形剪影
//湿墨色板：冷灰青/尸斑青/灰白，禁红禁暖；预乘 Alpha
//s0=占位白图 s1=PerlinNoise；全部噪声输入为笛卡尔 UV
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uIntensity;   //0-1 天空在场
float2 uScreenSize; //像素
float uCamX;        //Main.screenPosition.X 像素，视差用
float uFlash;       //0-1 雷闪包络，快起慢衰
float uFlashSeed;   //本次雷闪随机种子：剪影位置与是否露剪影
float uDepth;       //0-1 嵌套深度归一（第一层 0 最深层 1）：溺月下沉/天穹压暗/远山渐没

//====== 湿墨调色板 ======
static const float3 SKY_TOP = float3(0.026, 0.032, 0.040);      //头顶近黑沉云顶
static const float3 SKY_HORIZON = float3(0.225, 0.262, 0.268);  //地平尸青雾光
static const float3 CLOUD_DARK = float3(0.050, 0.060, 0.070);   //沉云
static const float3 CLOUD_UNDER = float3(0.180, 0.212, 0.218);  //云底衬光
static const float3 MOON_PALE = float3(0.620, 0.670, 0.680);    //溺月惨白
static const float3 SHAFT_PALE = float3(0.300, 0.345, 0.355);   //雨幡帘
static const float3 RIDGE_FAR = float3(0.105, 0.128, 0.138);    //远山墨青
static const float3 RIDGE_NEAR = float3(0.048, 0.058, 0.068);   //近山焦墨
static const float3 WATER_SHINE = float3(0.300, 0.350, 0.360);  //地平积水反光
static const float3 FLASH_PALE = float3(0.550, 0.620, 0.640);   //雷闪惨白

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float fbm2(float2 uv) {
    return noiseTex(uv) * 0.65 + noiseTex(uv * 2.7 + 13.1) * 0.35;
}

float hash1(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
}

//山脊线高度：给定世界视差 U 与层参数，返回脊线 y（uv 空间，向下增大）
float ridgeY(float worldU, float seed, float freq, float amp, float baseY) {
    float h = fbm2(float2(worldU * freq + seed, seed * 0.73));
    h = h * 0.8 + noiseTex(float2(worldU * freq * 3.1 + seed * 1.7, seed)) * 0.2;
    return baseY - (h - 0.5) * amp;
}

//倾斜雨幡带：斜向剪切后取低频噪声做柱状条带
float shaftBand(float2 uv, float parallax, float freq, float speed, float seedY) {
    float su = (uv.x + uv.y * 0.16) * freq
        + uCamX * parallax / uScreenSize.x + uTime * speed;
    return smoothstep(0.58, 0.82, noiseTex(float2(su, seedY)));
}

//巨大伞形剪影：p 局部空间（y 向上，盖半径 1），返回覆盖度
float umbrellaSil(float2 p) {
    if (abs(p.x) > 1.1 || p.y < -1.45 || p.y > 1.1) {
        return 0.0;
    }
    //盖：上半椭圆
    float canopy = step(length(p * float2(1.0, 1.45)), 1.0) * step(0.0, p.y);
    //下缘三个扇贝扣边
    float sc = 0.0;
    [unroll]
    for (int i = 0; i < 3; i++) {
        float cx = -0.667 + 0.667 * (float)i;
        sc = max(sc, step(length((p - float2(cx, 0.0)) * float2(3.0, 6.5)), 1.0));
    }
    canopy *= 1.0 - sc * step(p.y, 0.10);
    //柄
    float pole = step(abs(p.x), 0.035) * step(-1.40, p.y) * step(p.y, 0.05);
    return max(canopy, pole);
}

float4 PSSky(float2 coords : TEXCOORD0) : COLOR0 {
    if (uIntensity < 0.004) {
        return float4(0, 0, 0, 0);
    }
    float2 uv = coords;
    float aspect = uScreenSize.x / uScreenSize.y;

    //====== 压顶天穹：倒置明度，头顶最黑、地平雾光反亮；越深天越沉 ======
    float grad = pow(smoothstep(0.0, 0.92, uv.y), 1.35);
    float wash = fbm2(uv * float2(1.5 * aspect, 1.5) + float2(uTime * 0.004, 0.0));
    float3 skyTop = SKY_TOP * (1.0 - uDepth * 0.55);
    float3 skyHor = SKY_HORIZON * (1.0 - uDepth * 0.38);
    float3 col = lerp(skyTop, skyHor, grad) * (0.90 + wash * 0.20);

    //====== 溺月：被雨晕开的惨白光斑，慢呼吸；画在云前，云过即吞 ======
    //逐层下沉——第一层挂天上，第二层贴向水面，最深层半沉进地平积水，
    //越沉晕越大越糊：月位即层数
    float2 moonC = float2(0.63 - uCamX * 0.000012, 0.27 + uDepth * 0.58);
    float2 dm = (uv - moonC) * float2(aspect, 1.0);
    float mr = length(dm);
    float breathe = 0.72 + 0.28 * sin(uTime * 0.06);
    float halo = exp(-pow(mr / (0.17 + uDepth * 0.12), 1.6));
    float core = exp(-pow(mr / (0.05 + uDepth * 0.045), 2.0));
    col += MOON_PALE * (halo * (0.22 + uDepth * 0.10) + core * 0.55) * breathe;

    //====== 沉云两层：大团慢漂压顶 + 低掠碎云 ======
    float2 cuv1 = float2(uv.x * 1.5 + uCamX * 0.022 / uScreenSize.x + uTime * 0.0022,
        uv.y * 2.3);
    float cloud1 = smoothstep(0.38, 0.72, fbm2(cuv1))
        * (1.0 - smoothstep(0.28, 0.62, uv.y));
    float cloudEdge = cloud1 * (1.0 - cloud1) * 4.0;
    col = lerp(col, CLOUD_DARK, cloud1 * 0.85);
    col += CLOUD_UNDER * cloudEdge * 0.20 * smoothstep(0.15, 0.45, uv.y);

    float2 cuv2 = float2(uv.x * 2.6 + uCamX * 0.05 / uScreenSize.x + uTime * 0.016,
        uv.y * 3.4 + 7.7);
    float cloud2 = smoothstep(0.52, 0.78, fbm2(cuv2))
        * smoothstep(0.16, 0.34, uv.y) * (1.0 - smoothstep(0.55, 0.80, uv.y));
    col = lerp(col, CLOUD_DARK * 1.25, cloud2 * 0.55);

    //====== 远景雨幡：云底垂到地平的倾斜雨柱，两层缓移 ======
    float shaftEnv = smoothstep(0.20, 0.50, uv.y) * (1.0 - smoothstep(0.78, 0.96, uv.y));
    float shafts = shaftBand(uv, 0.05, 1.7, 0.005, 0.13) * 0.6
        + shaftBand(uv, 0.09, 2.6, 0.009, 0.53) * 0.4;
    //越深雨幡越密
    col = lerp(col, SHAFT_PALE, shafts * shaftEnv * (0.30 + uDepth * 0.12));
    //幡内细密下落雨纹
    float fall = noiseTex(float2(uv.x * 4.6 + uv.y * 0.8, uv.y * 0.38 - uTime * 0.5));
    col += SHAFT_PALE * saturate((fall - 0.62) * 5.0) * shafts * shaftEnv * 0.14;

    //====== 低平远山两层：被雨压平的世界；越深山越低越平，渐没进水里 ======
    float u1 = uv.x + uCamX * 0.040 / uScreenSize.x;
    float y1 = ridgeY(u1, 4.3, 1.30, 0.045 * (1.0 - uDepth * 0.5), 0.775 + uDepth * 0.10);
    float m1 = smoothstep(y1 - 0.004, y1 + 0.004, uv.y);
    col = lerp(col, RIDGE_FAR, m1);

    float u2 = uv.x + uCamX * 0.100 / uScreenSize.x;
    float y2 = ridgeY(u2, 9.1, 0.90, 0.060 * (1.0 - uDepth * 0.5), 0.855 + uDepth * 0.075);
    float m2 = smoothstep(y2 - 0.005, y2 + 0.005, uv.y);
    col = lerp(col, RIDGE_NEAR, m2);

    //====== 地平积水：山脚泡进一线反光的死水，呼应入场涨水 ======
    //积水随深度漫上来，最深层近山几乎没顶、只剩水原
    float wl = 0.925 - uDepth * 0.045;
    float waterBand = smoothstep(wl - 0.004, wl + 0.010, uv.y);
    float shine = noiseTex(float2(uv.x * 2.6 + uTime * 0.008, 0.71));
    float3 waterCol = RIDGE_NEAR * 0.7 + WATER_SHINE * (0.30 + 0.28 * shine);
    col = lerp(col, waterCol, waterBand * 0.85);
    col += WATER_SHINE * exp2(-abs(uv.y - wl) * 240.0) * 0.35;

    //====== 雷闪：云底先亮起的惨白，光先于声 ======
    float flashQ = uFlash * uFlash;
    float flashGrad = 1.0 - smoothstep(0.08, 0.62, uv.y);
    col += FLASH_PALE * flashQ * (0.22 + cloud1 * 0.50) * flashGrad;
    //水面反照一线
    col += FLASH_PALE * flashQ * waterBand * 0.10;

    //极稀有：闪光最亮的一瞬，云中露出巨大伞形剪影；越深越常见
    float silGate = step(0.72 - uDepth * 0.25, uFlashSeed) * saturate((uFlash - 0.70) / 0.30);
    float2 silC = float2(0.25 + frac(uFlashSeed * 7.31) * 0.50, 0.32);
    float2 sp = (uv - silC) * float2(aspect, -1.0) / 0.13;
    col = lerp(col, CLOUD_DARK * 0.6, umbrellaSil(sp) * silGate * 0.85);

    float alpha = uIntensity * 0.97;
    return float4(col * alpha, alpha);
}

technique TechSky {
    pass P0 {
        PixelShader = compile ps_3_0 PSSky();
    }
}
