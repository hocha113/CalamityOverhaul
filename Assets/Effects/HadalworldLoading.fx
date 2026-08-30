// ============================================================================
//HadalworldLoading.fx 深渊海沟加载屏,「下潜即加载」全屏水体
//材质=深海水体+穿透天光,三签名:①光随深度指数衰减沉入墨黑(六停靠色逐像素渐变+微抖动防色阶断层)
//②天光成放射束斜穿水面,随时间漂移闪烁,深处被水体吸收殆尽 ③悬浮微粒让光束可见,水团低频涌动
//光束坐标用斜率 s=x/y 参数化(虚拟光源钉在屏上方,分母恒正):连续无极缝,零 atan2(极角审计:零 theta 消费)
//束类三位置具名答案:源头=水面眩光带(顶缘波动辉光),沿程=微粒使束可见+宽度随扇形展开,
//末端=水体吸收律 exp(-y·k(depth)) 渐灭+噪声撕散,无一处平切
//色板与 HadalworldLoadTheme 六停靠同源(uniform 传入,双改),深度窗口映射与 CPU 版同式(depth+(y-0.38)*0.13)
//直线算术,无动态分支,无 tex2Dlod,无采样器;噪声全 hash 手拼,fbm ≤3 octave(FNA3D 安全水位)
// ============================================================================

float uTime;        //实时秒(加载屏墙钟)
float uDepth;       //归一化深度 0..1(进入=下行,退出=上行)
float uSkyLight;    //天光强度(CPU 已算入 (1-depth)²×呼吸×入场包络)
float uIntroFade;   //入场包络 0..1(只压水体亮度,不改色相,镜像 CPU 版)
float uAspectRatio; //屏宽/屏高
float3 uWater0;     //海面浅青(=HadalworldLoadTheme.SurfaceCyan,同源双改)
float3 uWater1;     //日光带蓝(=SunlitBlue)
float3 uWater2;     //暮光带残蓝(=TwilightBlue)
float3 uWater3;     //午夜带深蓝(=MidnightBlue)
float3 uWater4;     //深渊带墨色(=AbyssInk)
float3 uWater5;     //超深渊近黑(=HadalBlack)
float4 uKeys;       //六停靠的四个内断点(带底归一化深度,首尾 0/1 隐含)
float3 uShaft;      //天光束色(=SkyShaft)

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.07 + float2(3.1, 7.7);
        a *= 0.5;
    }
    return v;
}

float4 PSHadalworldLoading(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float aspect = uAspectRatio;

    //==================== 水体渐变(逐像素,窗口映射与 CPU 版同式) ====================
    //大尺度缓速涌动把窗口轻微扭一下:等深线不再是数学直线,读作活水而非色卡
    float sway = (fbm3(float2(uv.x * aspect * 1.3, uv.y * 1.3 + uDepth * 2.6)
        + float2(t * 0.021, t * 0.013)) - 0.5) * 0.030;
    float frac01 = saturate(uDepth + (uv.y - 0.38) * 0.13 + sway);

    //六停靠分段线性(链式 relerp,饱和权重天然分段,无分支)
    float3 water = uWater0;
    water = lerp(water, uWater1, saturate(frac01 / max(uKeys.x, 1e-4)));
    water = lerp(water, uWater2, saturate((frac01 - uKeys.x) / max(uKeys.y - uKeys.x, 1e-4)));
    water = lerp(water, uWater3, saturate((frac01 - uKeys.y) / max(uKeys.z - uKeys.y, 1e-4)));
    water = lerp(water, uWater4, saturate((frac01 - uKeys.z) / max(uKeys.w - uKeys.z, 1e-4)));
    water = lerp(water, uWater5, saturate((frac01 - uKeys.w) / max(1.0 - uKeys.w, 1e-4)));

    //入场包络只压亮度不改色相(向超深渊近黑收,镜像 CPU 版 Color.Lerp(HadalBlack, c, fade))
    float3 col = lerp(uWater5, water, uIntroFade);

    //水团明暗涌动(±3%,低频防塑料;深处稍强,水更稠)
    float mass = fbm3(float2(uv.x * aspect * 2.1, uv.y * 2.1 - uDepth * 1.8) + float2(t * 0.017, -t * 0.011));
    col *= 1.0 + (mass - 0.5) * (0.05 + 0.03 * uDepth);

    //==================== 天光束(源头=水面眩光,沿程=微粒显形,末端=吸收渐灭) ====================
    //斜率参数化:虚拟光源在屏上方 0.42,lp.y 恒正,s 全屏连续
    float2 lp = float2((uv.x - 0.5) * aspect, uv.y + 0.42);
    float s = lp.x / lp.y;

    //扇形包络:光只从头顶开口进来,越偏越弱;末端交给吸收律,不做纵向平切
    float fan = exp(-s * s * 2.4);
    //水体吸收律:深度越大吸收越凶,束的可见长度自然缩短
    float absorb = exp(-uv.y * (1.9 + 3.6 * uDepth));

    //双频束:宽束缓漂+窄束逆向碎闪(纵向频率压低保持束身连贯,横向漂移=水面波动折射)
    float beamWide = smoothstep(0.50, 0.95, valueNoise(float2(s * 4.3 + t * 0.045, uv.y * 0.55 + 11.3)));
    float beamFine = smoothstep(0.44, 0.93, valueNoise(float2(s * 9.7 - t * 0.062, uv.y * 0.85 + 4.7)));
    //悬浮微粒:束身里缓慢上飘的浊度,让光束读作体积而非贴片
    float mote = 0.72 + 0.28 * fbm3(float2(uv.x * aspect * 5.0, uv.y * 5.0 + t * 0.07));
    //软基柱:替掉 CPU 版三层矩形的宽底光,自身也吃扇形与吸收
    float baseGlow = exp(-s * s * 5.5) * 0.16;

    float shaft = (beamWide * 0.46 + beamFine * 0.24 + baseGlow) * fan * absorb * mote;
    col += uShaft * (shaft * uSkyLight);

    //源头=水面眩光带:顶缘随波起伏的辉光,只在浅深度可见(uSkyLight 已含 (1-depth)²)
    float glare = exp(-uv.y * (8.0 + 3.0 * uDepth))
        * (0.55 + 0.45 * valueNoise(float2(uv.x * aspect * 12.0 + t * 0.55, t * 0.23)));
    col += uShaft * (glare * uSkyLight * 0.30);

    //==================== 深带生物微光(午夜带起浮现,浅处被天光淹没) ====================
    //材质=深渊生物发光:稀疏点状冷青光,秒级呼吸脉动,整场随下潜缓慢漂移;
    //超深渊无天光时由它接管画面,"下面有东西亮着,那不是灯"
    float2 gp = float2(uv.x * aspect, uv.y + uDepth * 0.68 + t * 0.004) * 13.0;
    float2 gi = floor(gp);
    float2 gf = frac(gp);
    float gh = hash21(gi);
    float2 pv = gf - (0.25 + 0.5 * float2(hash21(gi + 17.1), hash21(gi + 43.7)));
    float pr2 = dot(pv, pv);
    float pulse = 0.30 + 0.70 * (0.5 + 0.5 * sin(t * (0.5 + gh * 0.9) + gh * 41.0));
    float deepRamp = smoothstep(0.50, 0.82, frac01);
    //小粒近光(~18% 格子)+偶发一粒远处大物的光(更大更糊更暗)
    float bio = exp(-pr2 * 170.0) * step(0.82, gh) * pulse * deepRamp;
    float bioBig = exp(-pr2 * 34.0) * step(0.965, gh) * pulse * deepRamp;
    col += float3(0.40, 0.72, 0.70) * (bio * 0.15) + float3(0.28, 0.60, 0.58) * (bioBig * 0.11);

    //==================== 收尾:深度加重的边缘压迫暗角 + 抖动破色阶 ====================
    float2 vg = uv * 2.0 - 1.0;
    float vig = saturate(dot(vg * float2(0.52, 0.55), vg * float2(0.52, 0.55)));
    col *= 1.0 - vig * (0.10 + 0.16 * uDepth);

    //近黑段 8-bit 色阶断层用 ±1LSB 级噪声打散(方块拼接的最后残余也在这里死掉)
    col += (hash21(uv * 1733.9 + frac(t * 0.37) * 61.7) - 0.5) * 0.0078;

    return float4(saturate(col), 1.0);
}

technique HadalworldLoading
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSHadalworldLoading();
    }
}
