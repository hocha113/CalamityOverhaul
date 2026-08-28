// ============================================================================
//KiameWater.fx 鬼雨子世界洼地黑水(屏幕空间逐列水面合成)
//TechWater:逐列水面条带(s2)重建各洼水面线,水下绕线垂直镜像采样拷屏
//         (废屋/伞鬼/玩家一并入镜,伪透视纵向压缩+涟漪扰动+染墨+深度衰减),
//         墨渊纵深分级,水面一线尸斑青锐亮沿+碎波挂亮,
//         雨砸溅环(逐格哈希相位,每拍换落点,扩张扁环+初拍冠状水花,世界锚定),
//         涉水实体接触涟漪与足下压暗(uFeet[8]),雷闪回照,水线上方溅雾;
//         条带外原样输出
//血统:镜像语法承 OniPuddleMirror,溅环/足环/挂亮承 ShenyoMenuLake(湖畔全景)
//色板:湿墨冷灰青/尸斑青/灰白,禁红禁暖
//坐标全笛卡尔像素空间;直线算术无动态分支;普通 tex2D,FNA3D 安全;不透明回写
//s0=拷屏(批主纹理) s1=PerlinNoise(实测值域 0.227~0.776,阈值前先过 nrm)
//s2=水面条带 256x1(Point 采样;R=有水 G/B=面高16位 A=水深px/2)
//消费入口 Scenarios/Kiame/Water/KiameWaterRender.cs
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);

float uTime;
float2 uScreenSize;     //像素
float2 uTopLeftWorld;   //屏幕(0,0)的世界px
float uPxToWorld;       //一屏幕像素对应的世界px(=1/zoom)
float uStripLeftPx;     //条带texel0的世界x
float uStripTopPx;      //面高编码基准(世界y)
float uStripSpanYPx;    //面高编码跨度
float uReflScale;       //伪透视压缩:水下每深1px,镜像源上移这么多px
float uFlash;           //0-1 雷闪包络
float uGust;            //0-1 风暴脉动
float uRainDensity;     //0-1 雨密度(溅环/溅雾密度门)
float uAlpha;           //整体强度(场景在场包络)
float3 uInkShallow;     //浅水墨
float3 uInkDeep;        //深水墨
float3 uSheen;          //尸斑青湿亮
float3 uFlashPale;      //雷闪惨白
float4 uFeet[8];        //xy=足点世界px z=强度0-1 w=半径(世界px)

//条带几何:256 texel x 每texel16px
static const float StripSpanPx = 4096.0;
static const float CellPx = 16.0;

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//绑定噪声实测值域 0.227~0.776
float nrm(float v) {
    return saturate((v - 0.227) / 0.549);
}

float h11(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
}

//雨砸溅环(侧视):逐格哈希相位,命中格内每拍换落点,
//水面沿线扩张的扁环 + 初拍在环心上方蹦一粒冠状水花
float rainSplash(float worldX, float dySurf, float cell, float seed, float gateTh) {
    float g = worldX / cell;
    float id = floor(g);
    float n = id * 11.31 + seed;
    float gate = saturate((h11(n + 4.7) - gateTh) * 10.0);
    float cyc = 0.5 + h11(n + 9.1) * 0.65;
    float k = floor(uTime / cyc + h11(n + 2.3));
    float t = frac(uTime / cyc + h11(n + 2.3));
    //每拍换落点:雨不会两次砸同一处
    float cx = (id + 0.12 + 0.76 * h11(n + 1.1 + k * 13.7)) * cell;
    float dx = worldX - cx;
    float ringR = 2.5 + t * 15.0;
    float ring = saturate(1.0 - abs(abs(dx) - ringR) / 2.8) * (1.0 - t) * (1.0 - t);
    //环贴水面线
    ring *= exp2(-abs(dySurf) * 1.1);
    //冠状水花:命中头一拍在落点上方一粒
    float crownY = dySurf + 3.0 + t * 8.0;
    float crown = exp2(-(dx * dx + crownY * crownY) * 0.22)
        * (1.0 - smoothstep(0.05, 0.38, t));
    return (ring * 0.85 + crown * 0.9) * gate;
}

float4 PSWater(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);

    //本像素世界坐标
    float2 world = uTopLeftWorld + uv * uScreenSize * uPxToWorld;

    //条带采样:该列有无水面与面高
    float su = (world.x - uStripLeftPx) / StripSpanPx;
    float4 strip = tex2D(uImage2, float2(su, 0.5));
    float hasWater = step(0.5, strip.r) * step(abs(su - 0.5), 0.5) * uAlpha;
    float surfaceY = uStripTopPx + (strip.g * 65280.0 + strip.b * 255.0) / 65535.0 * uStripSpanYPx;
    float bottomDepth = strip.a * 510.0;

    float depth = world.y - surfaceY;                 //>0 在水下
    float inWater = hasWater * step(0.0, depth) * step(depth, bottomDepth + 4.0);
    float d01 = saturate(depth / 150.0);

    //涟漪:世界坐标喂平铺噪声,两频对称扰动(中心值~0.5,不作阈值)
    float n1 = noiseTex(world * 0.006 + float2(uTime * 0.05, -uTime * 0.11)) - 0.5;
    float n2 = noiseTex(world * float2(0.013, 0.010) + float2(-uTime * 0.08, uTime * 0.07)) - 0.5;
    float ripple = (n1 + n2 * 0.6) * (0.4 + 0.6 * d01) * (0.7 + 0.6 * uGust);

    //绕水面线垂直镜像:伪透视纵向压缩,涟漪推横向为主、纵向少许
    float mirrorWorldY = surfaceY - depth * uReflScale;
    float2 mirrorPx = (float2(world.x, mirrorWorldY) - uTopLeftWorld) / uPxToWorld;
    float2 muv = mirrorPx / uScreenSize;
    muv.x += ripple * 3.5 / uScreenSize.x;
    muv.y += ripple * 1.5 / uScreenSize.y;
    muv = clamp(muv, 0.002, 0.998);
    float3 refl = tex2D(uImage0, muv).rgb;
    //倒影染墨:压暗向墨色,亮部保辨识
    refl = lerp(refl, uInkDeep, 0.42 + 0.30 * d01);

    //墨体:浅深分级,残留两成拷屏让水下地形隐约可读
    float3 water = lerp(uInkShallow, uInkDeep, smoothstep(0.0, 0.65, d01));
    water = lerp(water, src.rgb, 0.18);

    //水体覆盖 + 倒影叠加(反射率随深度衰减)
    float reflK = (1.0 - d01 * 0.72) * 0.58;
    float3 col = lerp(src.rgb, water, inWater * (0.60 + 0.32 * d01));
    col = lerp(col, refl, inWater * reflK);

    //碎波挂亮:横向拉丝的滚动噪声,只在近水面几行
    float glint = smoothstep(0.60, 0.92,
        nrm(noiseTex(world * float2(0.018, 0.11) + float2(uTime * 0.05, -uTime * 0.33))));
    col += uSheen * glint * inWater * exp2(-depth * 0.05) * 0.12;

    //水面一线尸斑青锐亮沿(一次只有一条锐利水平线)
    float rim = exp2(-abs(depth) * 0.9) * hasWater;
    col += uSheen * rim * (0.32 + 0.18 * (n1 + 0.5));

    //雨砸溅环:双层错格免网格感,密度吃雨密与风暴脉动
    float gateTh = lerp(0.72, 0.38, uGust);
    float dySurf = world.y - surfaceY;
    float splash = rainSplash(world.x, dySurf, 52.0, 3.1, gateTh)
        + rainSplash(world.x + 26.0, dySurf, 88.0, 17.9, gateTh + 0.05);
    col += uSheen * splash * hasWater * uRainDensity * (0.40 + 0.35 * uFlash);

    //涉水实体:足下双圈扩散涟漪 + 接触压暗
    [unroll]
    for (int i = 0; i < 8; i++) {
        float4 f = uFeet[i];
        float dx = world.x - f.x;
        float rw = max(f.w, 1.0);
        float ph1 = frac(uTime * 0.55 + (float)i * 0.373);
        float ph2 = frac(ph1 + 0.5);
        float r1 = rw * (0.22 + 0.78 * ph1);
        float r2 = rw * (0.22 + 0.78 * ph2);
        float ring1 = saturate(1.0 - abs(abs(dx) - r1) / 3.0) * (1.0 - ph1) * (1.0 - ph1);
        float ring2 = saturate(1.0 - abs(abs(dx) - r2) / 3.0) * (1.0 - ph2) * (1.0 - ph2);
        float onLine = exp2(-abs(dySurf) * 0.9);
        float blob = saturate(1.0 - abs(dx) / (rw * 0.8)) * exp2(-max(dySurf, 0.0) * 0.10)
            * step(0.0, dySurf);
        col = lerp(col, uInkDeep, blob * 0.32 * f.z * hasWater);
        col += uSheen * (ring1 + ring2 * 0.7) * onLine * 0.28 * f.z * hasWater;
    }

    //雷闪回照:面线与倒影同帧打亮
    float flashQ = uFlash * uFlash;
    col += uFlashPale * flashQ * (rim * 0.55 + inWater * 0.10);

    //水线溅雾:雨砸水面弹起的薄雾,只挂面线上方一窄条,随风暴脉动起伏
    float spray = exp2(-max(-dySurf, 0.0) * 0.14) * step(dySurf, 0.0) * hasWater;
    float sprayN = nrm(noiseTex(world * float2(0.045, 0.16) - float2(uTime * 0.35, uTime * 1.1)));
    col += uSheen * spray * (0.25 + 0.5 * sprayN) * 0.10 * (0.3 + uGust * 0.7) * uRainDensity;

    return float4(col, src.a);
}

technique TechWater {
    pass P0 {
        PixelShader = compile ps_3_0 PSWater();
    }
}
