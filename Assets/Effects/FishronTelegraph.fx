// ============================================================================
// FishronTelegraph.fx 风暴预警线（水汽材质）
// uv.x 沿线 0源头→1末端，uv.y 横向；Additive 白 quad
// 断口治理：源头羽化生长、末端长渐隐、退场向轴心收拢，两端永无硬切平面
// ps_3_0，无分支、无极角
// ============================================================================

float uTime;
float uIntensity;     //整体亮度(含淡入)
float uGrow;          //0→1 线体从源头长出的推进度
float uLockProgress;  //0追踪 → 0~1锁定白闪推进
float uCollapse;      //0→1 退场收拢：宽度塌向轴心，让位给冲刺本体
float uAspect;        //长宽比，保持噪声各向同性
float uRootFeather;   //源头羽化长度：冲刺线 0.07(藏根)，落雷线 0.015(地面锚点要实)
float3 uColor;        //主色（海青）

//哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//分形噪声
float fbm2(float2 p)
{
    float v = 0.0;
    float amp = 0.5;
    float2 shift = float2(17.3, 31.7);
    for (int i = 0; i < 3; i++)
    {
        v += valueNoise(p) * amp;
        p = p * 2.17 + shift;
        amp *= 0.5;
    }
    return v;
}

float4 TelegraphPS(float2 uv : TEXCOORD0) : COLOR0
{
    float x = uv.x;                    //沿线 0..1
    //退场收拢：横向坐标向轴心放大 → 线变细变利，不是整体转淡
    float lat = (uv.y - 0.5) * 2.0 * (1.0 + uCollapse * 2.6);

    // ---- 两端包络 ----
    //源头羽化：从发射源"长出"（长度按模式可调）
    float rootFade = smoothstep(0.0, max(uRootFeather, 1e-3), x);
    //末端长渐隐：后 14% 由密转疏，没有端面
    float tipFade = smoothstep(1.0, 0.86, x);
    //生长前沿：线体随 uGrow 向前推进，前沿带 10% 软边 + 噪声毛边
    float frontN = fbm2(float2(x * uAspect * 0.3, lat * 2.0 + uTime * 3.0));
    float growEdge = smoothstep(uGrow + 0.02, uGrow - 0.10, x - frontN * 0.05);
    float endFade = rootFade * tipFade * growEdge;

    // ---- 横截面：核心 + 水汽晕 ----
    float coreSharp = lerp(42.0, 15.0, uLockProgress);
    float core = exp(-lat * lat * coreSharp);
    float haze = exp(-lat * lat * 4.2) * 0.4;

    // ---- 水汽流：雾沫沿线向末端流去（指向冲刺方向） ----
    float flowSpeed = 2.0 + uLockProgress * 3.2;
    float n = fbm2(float2(x * uAspect * 0.5 - uTime * flowSpeed, lat * 1.6));
    //水珠闪点：高频噪声窄阈值，顺流漂移
    float sparkleN = valueNoise(float2(x * uAspect * 1.7 - uTime * (flowSpeed * 1.7), lat * 5.0));
    float sparkle = smoothstep(0.78, 0.94, sparkleN) * (1.0 - abs(lat)) * 0.8;

    //向末端奔涌的脉冲段
    float pulse = 0.55 + 0.45 * sin(x * uAspect * 1.3 - uTime * (7.0 + uLockProgress * 15.0) + n * 4.5);
    pulse = pow(pulse, 1.5);

    //锁定白闪振荡
    float flash = 1.0 + uLockProgress * 0.5 * sin(uTime * 44.0);

    // ---- 合成 ----
    float lum = (core * (1.0 + uLockProgress * 1.5) + haze * (0.7 + n * 0.6) + sparkle)
        * pulse * endFade * flash;

    float3 col = uColor * lum;
    //锁定期核心煮成白沫色（冷白，不是纯白常驻，只随 flash 波动）
    col += float3(0.88, 1.0, 0.98) * core * uLockProgress * lum * 1.1;

    //退场时总量略降，让收拢读作"绷紧"而不是变暗消失
    float exitDim = 1.0 - uCollapse * 0.25;

    return float4(col * uIntensity * exitDim, 1.0);
}

technique Telegraph
{
    pass P0
    {
        PixelShader = compile ps_3_0 TelegraphPS();
    }
}
