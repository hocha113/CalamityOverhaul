// ============================================================================
//GolemTrapWork.fx 神殿机关单元：基座刻纹 / 尖刺柱 / 火焰喷口 / 预警柱
//PlateTech：AlphaBlend 预乘批，石板基座 + 蜥蜴人刻纹充能发亮
//SpikeTech：AlphaBlend 预乘批，三角齿列石刺（uv.y=1 基座 → 0 尖端），齿高按 uSeed 参差
//FlameTech：AlphaBlend 预乘批，向尖端滚动的撕边喷焰
//WarnTech：AlphaBlend 预乘批，待命预警柱——淡轮廓画满整段喷发footprint，
//          热浪填充随 uProgress 升起，临爆末段整柱频闪
//全笛卡尔构造，无极角；无动态分支；噪声走 uNoise 贴图
// ============================================================================

float uTime;
float uProgress;   //Plate=充能进度 / Spike=伸出包络 / Flame=喷焰包络 / Warn=预警进度
float uIntensity;
float uKind;       //0尖刺 1喷焰 2射线口（基座刻纹配色）
float uSeed;       //单元个体种子（0~1），错开噪声相位防整排同款
// 噪声固定 s1：三个 pass 均不采样 s0（画布只是白像素 quad），
// 旧 sampler_state 自动分配落 s0，被 SpriteBatch 用画布贴图覆写→石纹/焰噪全读成辉光渐变；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSampler : register(s1);

//------------------------------------------------------------------
//基座：切角石板 + 中缝刻纹，充能时刻纹自内向外点亮
//------------------------------------------------------------------
float4 PlatePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;   //-1..1

    //切角矩形 SDF（近似）：max 范数 + 角斜切
    float box = max(abs(p.x), abs(p.y) * 1.9);
    float chamfer = (abs(p.x) + abs(p.y) * 1.9) * 0.82;
    float d = max(box, chamfer);
    float plate = 1.0 - smoothstep(0.86, 1.0, d);

    //石面纹理（种子错相，整排基座各有各的石纹）
    float grain = tex2D(noiseSampler, coords * 2.2 + uSeed).r;
    float3 stoneDark = float3(0.23, 0.19, 0.14);
    float3 stoneLite = float3(0.42, 0.36, 0.26);
    float3 col = lerp(stoneDark, stoneLite, grain);

    //顶缘受光
    float topLight = smoothstep(0.4, -0.9, p.y) * 0.18;
    col += topLight;

    //刻纹：中带菱形链（笛卡尔周期），充能自中心向两端点亮
    float cell = abs(frac(p.x * 3.0 + 0.5) - 0.5) * 2.0 + abs(p.y) * 1.3;
    float glyph = 1.0 - smoothstep(0.42, 0.62, cell);
    float lightUp = step(abs(p.x), uProgress * 1.05);
    float pulse = 0.75 + 0.25 * sin(uTime * (4.0 + 8.0 * uProgress));

    //类型配色：尖刺砂金 / 喷焰炽橙 / 射线口日白
    float3 glowSpike = float3(1.00, 0.78, 0.30);
    float3 glowFlame = float3(1.00, 0.48, 0.10);
    float3 glowRay   = float3(1.00, 0.92, 0.60);
    float kindFlame = saturate(1.0 - abs(uKind - 1.0));
    float kindRay = saturate(1.0 - abs(uKind - 2.0));
    float3 glowCol = glowSpike * saturate(1.0 - kindFlame - kindRay) + glowFlame * kindFlame + glowRay * kindRay;

    col += glowCol * glyph * lightUp * pulse * (0.4 + 0.8 * uProgress);

    float a = plate * uIntensity;
    return float4(col * a, a) * vertexColor.a;
}

//------------------------------------------------------------------
//尖刺柱：三角齿列，石身 + 基部熔缝 + 齿缘热线
//uv.y=1 基座，uv.y=0 尖端；uProgress 已在 C# 侧转成长度，柱内取满高
//------------------------------------------------------------------
float4 SpikePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float h = 1.0 - coords.y;          //0 基座 → 1 尖端
    float x = coords.x;

    //三齿：每齿中心最高；齿高按种子参差（72%~100%），整排刺不再复制粘贴
    float toothPhase = abs(frac(x * 3.0) - 0.5) * 2.0;   //0 齿心 → 1 齿缝
    float toothId = floor(x * 3.0);
    float hVar = tex2D(noiseSampler, float2(toothId * 0.31 + uSeed * 3.7, 0.17)).r;
    float peak = (1.0 - toothPhase * 0.92) * (0.72 + 0.28 * hVar);

    float solid = step(h, peak);

    //石身色：竖向渐暗 + 颗粒（种子错相）
    float grain = tex2D(noiseSampler, coords * float2(3.0, 1.4) + uSeed).r;
    float3 stoneDark = float3(0.26, 0.21, 0.15);
    float3 stoneLite = float3(0.48, 0.41, 0.30);
    float3 col = lerp(stoneDark, stoneLite, grain) * (1.0 - h * 0.35);

    //裂隙暗纹：石刺是崩出来的，不是铸出来的
    float crack = tex2D(noiseSampler, coords * float2(5.5, 2.6) + uSeed * 1.9).r;
    col *= 1.0 - smoothstep(0.62, 0.78, crack) * 0.38;

    //齿缘热线：贴近轮廓的内侧亮边
    float edge = exp(-abs(peak - h) * 16.0);
    col += float3(1.0, 0.62, 0.18) * edge * 0.9;

    //基部熔缝
    float baseGlow = exp(-h * 5.5) * (0.5 + 0.5 * sin(uTime * 9.0 + x * 21.0 + uSeed * 12.0));
    col += float3(1.0, 0.45, 0.10) * baseGlow * 0.5;

    //暴出余温：刚出土的几帧齿身整体带热（uProgress<1 时=伸出中）
    float fresh = saturate(1.0 - uProgress) ;
    col += float3(1.0, 0.55, 0.15) * fresh * 0.35;

    float a = solid * uIntensity;
    return float4(col * a, a) * vertexColor.a;
}

//------------------------------------------------------------------
//喷焰柱：向尖端滚动的噪声焰体，撕裂尖端 + 白热根部
//------------------------------------------------------------------
float4 FlamePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float h = 1.0 - coords.y;          //0 喷口 → 1 尖端
    float across = abs(coords.x - 0.5) * 2.0;

    //焰体噪声：向尖端滚动 + 横向扰动（种子错相，双喷口不同焰形）
    float n1 = tex2D(noiseSampler, float2(coords.x * 1.8 + uSeed * 2.3, h * 1.4 - uTime * 1.7)).r;
    float n2 = tex2D(noiseSampler, float2(coords.x * 3.6 + 0.37 + uSeed, h * 2.7 - uTime * 2.6)).r;
    float flame = n1 * 0.62 + n2 * 0.38;

    //轮廓：越到尖端越细，噪声撕边
    float width = 1.0 - h * 0.55;
    float body = 1.0 - smoothstep(width * (0.45 + flame * 0.4), width, across);
    //尖端撕裂：flame 阈值随高度上升
    float tear = step(h * (0.72 - flame * 0.35), 0.62);
    body *= tear;

    //色阶：白热根部 → 金 → 橙 → 深红尖端
    float3 whiteHot = float3(1.00, 0.95, 0.80);
    float3 gold     = float3(1.00, 0.78, 0.28);
    float3 orange   = float3(1.00, 0.46, 0.08);
    float3 deepRed  = float3(0.62, 0.14, 0.03);
    float3 col = lerp(whiteHot, gold, smoothstep(0.0, 0.28, h));
    col = lerp(col, orange, smoothstep(0.28, 0.62, h));
    col = lerp(col, deepRed, smoothstep(0.62, 1.0, h));

    //根部增亮
    float rootBoost = exp(-h * 4.2) * 0.5;

    float a = saturate((body * (0.75 + flame * 0.35) + rootBoost * body) * uProgress * uIntensity);
    return float4(col * a, a) * vertexColor.a;
}

//------------------------------------------------------------------
//预警柱：待命期的喷发footprint预告
//淡边框勾出整段危险区，热浪填充自基座随 uProgress 升起，
//内部余烬上飘，临爆末段（>0.85）整柱频闪
//uv.y=1 基座，uv.y=0 尖端
//------------------------------------------------------------------
float4 WarnPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float h = 1.0 - coords.y;          //0 基座 → 1 尖端
    float across = abs(coords.x - 0.5) * 2.0;

    //类型配色：尖刺砂金 / 喷焰炽橙
    float kindFlame = saturate(1.0 - abs(uKind - 1.0));
    float3 warnCol = lerp(float3(1.00, 0.72, 0.24), float3(1.00, 0.46, 0.10), kindFlame);

    //边框：两侧细边 + 尖端盖线，勾出最终footprint
    float sideEdge = smoothstep(0.86, 0.97, across) * (1.0 - smoothstep(0.97, 1.0, across));
    float tipEdge = exp(-abs(h - 0.985) * 60.0);
    float frame = max(sideEdge, tipEdge) * 0.34;

    //热浪填充：自基座升到 uProgress 高度，锋面亮；面填充轻且带呼吸，别读成发光面板
    float fillFront = exp(-abs(h - uProgress) * 14.0) * step(0.02, uProgress) * 0.5;
    float breath = 0.8 + 0.2 * sin(uTime * 6.0 + uSeed * 7.0);
    float fillBody = step(h, uProgress) * (1.0 - h * 0.5) * 0.06 * breath;

    //内部余烬：稀疏噪点上飘（阈值按 PerlinNoise 实测值域 p50=0.51/p90=0.60/max=0.78 取带）
    float emberN = tex2D(noiseSampler, float2(coords.x * 2.6 + uSeed * 4.1, h * 1.8 - uTime * 0.9)).r;
    float emberGate = tex2D(noiseSampler, float2(coords.x * 5.3 + uSeed, h * 3.1 - uTime * 1.5)).r;
    float ember = smoothstep(0.58, 0.70, emberN) * smoothstep(0.48, 0.6, emberGate)
        * step(h, uProgress + 0.08) * (1.0 - across * 0.7) * 0.8;

    //临爆频闪：末段整柱抽搐
    float crit = smoothstep(0.85, 1.0, uProgress) * (0.5 + 0.5 * sin(uTime * 30.0 + uSeed * 9.0));
    float critBody = crit * (1.0 - across * 0.55) * 0.3;

    float a = saturate(frame + fillFront + fillBody + ember + critBody)
        * (0.25 + 0.75 * uProgress) * uIntensity;
    //白热锋面与余烬提亮
    float3 col = warnCol + float3(1.0, 0.9, 0.7) * (fillFront + crit * 0.4);
    return float4(col * a, a) * vertexColor.a;
}

technique PlateTech
{
    pass PlatePass
    {
        PixelShader = compile ps_3_0 PlatePS();
    }
}

technique WarnTech
{
    pass WarnPass
    {
        PixelShader = compile ps_3_0 WarnPS();
    }
}

technique SpikeTech
{
    pass SpikePass
    {
        PixelShader = compile ps_3_0 SpikePS();
    }
}

technique FlameTech
{
    pass FlamePass
    {
        PixelShader = compile ps_3_0 FlamePS();
    }
}
