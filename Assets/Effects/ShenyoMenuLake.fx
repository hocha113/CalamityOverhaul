// ============================================================================
//ShenyoMenuLake.fx 鬼湖夜雨主菜单全景
//TechLake：倒置明度压顶天穹（头顶近黑沉云、地平尸青雾光）+ 溺月（画云前被云吞）
//         + 双层流动沉云 + 倾斜雨幡 + 低平远岸一线 + 镜面鬼湖
//         （月光路铺向观者、碎波挂亮、三档深度带雨砸溅环+雨点碎闪、
//          立影足下涟漪高度场：波包扩张+毛细颤纹，解析梯度定坡向——朝月坡亮/背月坡暗、
//          月光路与碎波被坡度折弯、接触处压暗）+ 水线溅雾/潮雾带 + 雷闪惨白，Opaque 整幅铺底
//TechRain：视差雨幕层（溺月逆光挂亮）；uRainCfg/uRainBottom 参数化后
//         由C#按远/中/近三次插层绘制——远雨止于水线、近雨盖顶，纵深由遮挡读出
//uGust=风暴脉动：雨幕密度斜度、湖面溅环、雨幡同源呼吸
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
float uGust;        //0-1 风暴脉动
float uHorizon;     //水线 y（uv）
float2 uMoonUv;     //溺月圆心（uv）
float4 uFeet[8];    //立影水线接触点：xy=uv z=在场（可>1，入水/沉湖时激波增幅） w=涟漪半径（透视空间尺度）
float4 uRainCfg;    //雨幕层配置 x=频率倍率 y=速度倍率 z=透明度倍率 w=附加斜度
float uRainBottom;  //雨幕下缘软截止（uv y）：远雨止于水线读出纵深

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

float h11(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
}

//湖面溅环（单深度带）：逐格哈希相位，命中点扩张透视椭圆环+初拍冠状竖点
float lakeSplash(float2 pos, float cell, float seed, float gateTh) {
    float2 g = pos / cell;
    float2 id = floor(g);
    float2 f = frac(g);
    float n = id.x * 11.31 + id.y * 57.7 + seed;
    float gate = saturate((h11(n + 4.7) - gateTh) * 10.0);
    float cyc = 0.55 + h11(n + 9.1) * 0.75;
    float k = floor(uTime / cyc + h11(n + 2.3));
    float t = frac(uTime / cyc + h11(n + 2.3));
    //每拍换落点：雨不会两次砸同一处
    float2 c = 0.5 + (float2(h11(n + 1.1 + k * 13.7), h11(n + 6.3 + k * 7.3)) - 0.5) * 0.44;
    float2 dv = (f - c) * float2(1.0, 3.0);
    float r = length(dv);
    float ringR = lerp(0.06, 0.46, t);
    float ring = saturate(1.0 - abs(r - ringR) / 0.10) * (1.0 - t) * (1.0 - t);
    //冠状竖点：命中头一拍在环心上方蹦一粒水花
    float2 cd = (f - c - float2(0.0, -0.10 * t)) * float2(6.0, 2.2);
    float crown = exp(-dot(cd, cd) * 3.0) * (1.0 - smoothstep(0.10, 0.30, t));
    return (ring * 0.85 + crown * 0.90) * gate;
}

//====== 立影足下涟漪场（与 ShenyoMenuGhost.fx 的 feetRippleGrad 同源，改动须两处同步）======
//水面高度场 h 由两拍向外扩张的波包（身体晃动激起，随行程衰减）+ 接触点周围的毛细颤纹叠成，
//直接对 dist 求导得解析坡度再乘 ∇dist，返回 xyz=(h, ∂h/∂u, ∂h/∂v)（坡度已按半径归一） w=接触压暗
//透视：接触点越近岸椭圆越圆（yr 4.6→2.6），与月光路展宽同一套透视读法
//编译预算：fx_2_0 旧编译器对展开循环里每份内联拷贝各占一套常量，8 影×(2拍+颤纹)标量写法即撑爆
//ps_3_0 的 224 常量寄存器（2026-09 实测）——两拍波包并成 float2 向量算，逐影相位种子取运行期值
//而非按 i 折常量；颤纹只给 uFeet[4..7]（C# 契约：前四槽为远影，其半径本就小到颤纹渐隐为零）
//单影：两拍波包（h, ∂h/∂dist）——u=(dist-rk)/sg，h=sin(2.6u)·e^(-u²)·(1-ph)^1.5
float2 ripplePulses(float dist, float rw, float seed) {
    float2 ph = frac(uTime * 0.30 + seed + float2(0.0, 0.5));
    float2 rk = rw * (0.03 + 0.97 * ph);
    float2 sg = rw * (0.11 + 0.06 * ph);
    float2 u = (dist - rk) / sg;
    float2 env = exp(-u * u) * pow(1.0 - ph, 1.5);
    float2 s = sin(u * 2.6);
    float2 c = cos(u * 2.6);
    float2 dh = (2.6 * c - 2.0 * u * s) * env / sg;
    return float2(dot(s * env, 1.0), dot(dh, 1.0));
}

//毛细颤纹：身体微颤在接触点周围持续激起的短波，随距离指数衰减
//幅度压得很低：它只是波包之间的细肌理，抬高就成靶心（2026-09 沙盒实测）
float2 rippleTremor(float dist, float rw, float seed) {
    float kk = 6.2831853 / (rw * 0.13);
    float L = rw * 0.24;
    float att = exp(-dist / L);
    float phs = dist * kk - uTime * 7.0 + seed;
    float ts = sin(phs);
    float tc = cos(phs);
    return float2(ts * att, kk * tc * att - ts * att / L) * 0.07;
}

//单影几何：xy=∇dist（uv 空间） z=dist w=rw
float4 rippleGeo(float2 uv, float aspect, float4 f) {
    float dl = saturate((f.y - uHorizon) / max(1.0 - uHorizon, 0.001));
    float yr = lerp(4.6, 2.6, dl);
    float2 dv = (uv - f.xy) * float2(aspect, yr);
    float dist = max(length(dv), 1e-4);
    return float4(dv * float2(aspect, yr) / dist, dist, max(f.w, 0.001));
}

float4 feetRipple(float2 uv, float aspect) {
    float4 acc = 0.0;
    //远影四槽：只有两拍波包
    [unroll]
    for (int i = 0; i < 4; i++) {
        float4 f = uFeet[i];
        float4 g = rippleGeo(uv, aspect, f);
        float2 hd = ripplePulses(g.z, g.w, f.y * 37.0 + f.w * 91.0);
        float blob = saturate(1.0 - g.z / (g.w * 0.50));
        acc.x += hd.x * f.z;
        acc.yz += hd.y * g.xy * g.w * 0.25 * f.z;
        acc.w += blob * blob * 0.55 * saturate(f.z);
    }
    //近影四槽：波包 + 颤纹
    [unroll]
    for (int j = 4; j < 8; j++) {
        float4 f = uFeet[j];
        float4 g = rippleGeo(uv, aspect, f);
        float seed = f.y * 37.0 + f.w * 91.0;
        float2 hd = ripplePulses(g.z, g.w, seed) + rippleTremor(g.z, g.w, seed);
        float blob = saturate(1.0 - g.z / (g.w * 0.50));
        acc.x += hd.x * f.z;
        //坡度按半径归一：远小近大的环在屏上读出同一档对比
        acc.yz += hd.y * g.xy * g.w * 0.25 * f.z;
        acc.w += blob * blob * 0.55 * saturate(f.z);
    }
    return acc;
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
    skyCol = lerp(skyCol, SHAFT_PALE, shafts * shaftEnv * (0.30 + uGust * 0.08));
    //幡内细密下落雨纹（阵风时更密更亮——远处的雨在幡里可见地下着）
    float fall = noiseTex(float2(uv.x * 4.6 + yn * 0.8, yn * 0.38 - uTime * 0.5));
    skyCol += SHAFT_PALE * saturate((fall - 0.58) * 5.0) * shafts * shaftEnv * (0.20 + uGust * 0.10);

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

    //立影足下涟漪场先算：坡度把镜面里的月光路与碎波一起折弯
    float4 rip = feetRipple(uv, aspect);
    float2 ruv = uv + rip.yz * 0.0009;

    //月光路：随透视向观者展宽，横向微摆
    float pathX = uMoonUv.x + uParallax.x * lerp(0.18, 0.85, d);
    float pw = lerp(0.014, 0.170, pow(d, 1.35));
    float pWob = (noiseTex(float2(uv.y * 3.0 - uTime * 0.05, 0.77)) - 0.5) * 0.030 * d;
    float lp = exp(-pow(abs(ruv.x - pathX + pWob) / max(pw, 0.001), 1.7));

    //碎波挂亮：横向拉丝的滚动噪声，近水线密、近岸疏
    float shim = nrm(noiseTex(float2(ruv.x * 7.0 + uTime * 0.02,
        ruv.y * lerp(46.0, 9.0, d) - uTime * 0.33)));
    float glint = smoothstep(0.55, 0.95, shim);
    lakeCol += MOON_PALE * lp * (0.10 + glint * 0.55) * breathe;
    lakeCol += WATER_SHINE * glint * 0.07 * (0.30 + d * 0.70);

    //====== 雨砸湖面：三档深度带溅环——近大远小、逐格哈希相位、月光路上更亮 ======
    float2 sp = float2(uv.x * aspect, uv.y);
    float gateS = lerp(0.72, 0.38, uGust);
    float bandF = smoothstep(0.00, 0.04, d) * (1.0 - smoothstep(0.16, 0.28, d));
    float bandM = smoothstep(0.14, 0.26, d) * (1.0 - smoothstep(0.50, 0.66, d));
    float bandN = smoothstep(0.48, 0.64, d);
    float splash = lakeSplash(sp, 0.030, 3.1, gateS + 0.06) * bandF * 0.55
        + lakeSplash(sp, 0.058, 17.9, gateS) * bandM * 0.85
        + lakeSplash(sp, 0.110, 41.3, gateS) * bandN;
    float splashLit = 0.55 + lp * 0.85 + flashQ * 0.90 + uGust * 0.25;
    lakeCol += MOON_PALE * splash * 0.36 * splashLit;

    //细密雨点碎闪打底：高频阈值 + 逐拍重播种
    float spark = step(0.88, nrm(noiseTex(
        uv * float2(34.0 * aspect, 30.0) + floor(uTime * 7.0) * 0.37)));
    lakeCol += MOON_PALE * spark * (0.05 + lp * 0.18 + d * 0.10) * (0.70 + uGust * 0.50);

    //====== 立影足下涟漪着色：坡向决定镜面里映到哪片天 ======
    //朝溺月/水线倾斜的坡面映到低空雾光与月晕→亮；朝观者倾斜的坡面映到头顶黑云→暗。
    //环因此不是等宽亮圈：远弧外亮内暗、近弧内亮外暗，月光路上再挑一层锐芒
    float2 toMoon = (uMoonUv - uv) * float2(aspect, 1.0);
    toMoon /= max(length(toMoon), 0.001);
    float slope = -dot(rip.yz, toMoon);
    float ripLit = 0.55 + lp * 1.10 + flashQ * 0.80;
    float ripBright = saturate(slope * 0.16);
    float ripDark = saturate(-slope * 0.16);
    lakeCol = lerp(lakeCol, WATER_DEEP, ripDark * 0.55);
    lakeCol += RING_PALE * ripBright * 0.42 * ripLit;
    lakeCol += MOON_PALE * ripBright * ripBright * ripBright * 0.45 * (lp + flashQ * 0.5) * breathe;
    //接触压暗：身体压住的水面与水中黑影
    lakeCol = lerp(lakeCol, WATER_DEEP, saturate(rip.w));

    //====== 雷闪（水侧）：月光路与水线回照 ======
    lakeCol += FLASH_PALE * flashQ * (0.09 + lp * 0.22);

    //====== 天水合成：水线微颤不走激光直线 ======
    float wl = uHorizon + (noiseTex(float2(uv.x * 3.1 + uTime * 0.02, 0.91)) - 0.5) * 0.0035;
    float waterSide = smoothstep(wl - 0.0012, wl + 0.0012, uv.y);
    float3 col = lerp(skyCol, lakeCol, waterSide);

    //====== 水线溅雾：雨砸水面弹起的薄雾，只挂水线上方一窄条、随阵风起伏 ======
    float sprayBand = exp(-max(uHorizon - uv.y, 0.0) * 60.0) * (1.0 - waterSide);
    float sprayN = nrm(noiseTex(float2(uv.x * 9.0 * aspect - uTime * 0.06, uv.y * 40.0 + uTime * 0.30)));
    col += MIST_PALE * sprayBand * (0.30 + 0.50 * sprayN) * (0.20 + uGust * 0.40);

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

//====== 雨幕层：双内层视差雨丝，uRainCfg 参数化后由C#按远/中/近三次插层绘制 ======
static const float3 RAIN_PALE = float3(0.470, 0.530, 0.548);

float4 PSRain(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float aspect = uScreenSize.x / uScreenSize.y;

    //溺月逆光：雨丝行经月晕与月光路时被点亮
    float2 dm = (uv - uMoonUv) * float2(aspect, 1.0);
    float backlit = exp2(-dot(dm, dm) * 7.5);

    //层配置：远雨细慢暗少视差、近雨粗快亮满视差；阵风加斜加密
    float freq = uRainCfg.x;
    float spdM = uRainCfg.y;
    float slant = uRainCfg.w + uGust * 0.06;

    //雨丝=高x频×低y频的竖长条带快速下滚；再乘一路错相噪声把长条掐断成段
    //y坐标混入x分量：x向重度欠采样时若屏幕行恒采贴图同一行，行亮度差会读成横向密度条带
    //中层：细、密、稍缓
    float x1 = (uv.x + uv.y * (0.070 + slant) + uParallax.x * 0.55 * spdM) * 44.0 * freq;
    float y1 = uv.y * 2.2 + uv.x * 0.31 - uTime * 2.9 * spdM;
    float st1 = smoothstep(0.62, 0.88, nrm(noiseTex(float2(x1, y1))));
    st1 *= smoothstep(lerp(0.40, 0.22, uGust), 0.72,
        nrm(noiseTex(float2(x1 * 0.113 + 7.7, uv.y * 0.55 - uTime * 2.1 * spdM))));
    //近层：粗、疏、更快更斜
    float x2 = (uv.x + uv.y * (0.115 + slant) + uParallax.x * 1.00 * spdM) * 24.0 * freq + 5.7;
    float y2 = uv.y * 1.2 + uv.x * 0.23 - uTime * 3.6 * spdM;
    float st2 = smoothstep(0.68, 0.92, nrm(noiseTex(float2(x2, y2))));
    st2 *= smoothstep(lerp(0.44, 0.26, uGust), 0.74,
        nrm(noiseTex(float2(x2 * 0.147 + 3.1, uv.y * 0.40 - uTime * 2.6 * spdM))));

    float rain = st1 * (0.30 + backlit * 0.55) + st2 * (0.42 + backlit * 0.66);
    rain *= (1.0 + uFlash * uFlash * 1.6) * uIntensity * uRainCfg.z * (0.75 + uGust * 0.45);
    //下缘软截止：远雨在水线处没入湖面，纵深由此读出
    rain *= 1.0 - smoothstep(uRainBottom - 0.05, uRainBottom + 0.02, uv.y);

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
