// ============================================================================
//GolemTrapWork.fx 神殿机关单元的两种纯光效：火焰喷口 / 预警脚印
//机关本体（基座砖、刺矛、尖刺球、人面射线口）已改由 C# 拼原版神庙贴图，
//旧 PlateTech/SpikeTech 随之删除——石头和铁器不该用着色器手搓形体
//FlameTech：AlphaBlend 预乘批，向尖端滚动的撕边喷焰
//WarnTech：AlphaBlend 预乘批，待命预警柱——淡轮廓画满整段喷发footprint，
//          热浪填充随 uProgress 升起，临爆末段整柱频闪
//全笛卡尔构造，无极角；无动态分支；噪声走 s1 贴图
// ============================================================================

float uTime;
float uProgress;   //Flame=喷焰包络 / Warn=预警进度
float uIntensity;
float uKind;       //0刺矛 1喷焰（预警脚印配色）
float uSeed;       //单元个体种子（0~1），错开噪声相位防整排同款
// 噪声固定 s1：两个 pass 均不采样 s0（画布只是白像素 quad），
// 旧 sampler_state 自动分配落 s0，被 SpriteBatch 用画布贴图覆写→焰噪全读成辉光渐变；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSampler : register(s1);

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

technique WarnTech
{
    pass WarnPass
    {
        PixelShader = compile ps_3_0 WarnPS();
    }
}

technique FlameTech
{
    pass FlamePass
    {
        PixelShader = compile ps_3_0 FlamePS();
    }
}
