// ============================================================================
//GolemTrapWork.fx 神殿机关单元：基座刻纹 / 尖刺柱 / 火焰喷口
//PlateTech：AlphaBlend 预乘批，石板基座 + 蜥蜴人刻纹充能发亮
//SpikeTech：AlphaBlend 预乘批，三角齿列石刺（uv.y=1 基座 → 0 尖端）
//FlameTech：AlphaBlend 预乘批，向尖端滚动的撕边喷焰
//全笛卡尔构造，无极角；无动态分支；噪声走 uNoise 贴图
// ============================================================================

float uTime;
float uProgress;   //Plate=充能进度 / Spike=伸出包络 / Flame=喷焰包络
float uIntensity;
float uKind;       //0尖刺 1喷焰 2射线口（基座刻纹配色）
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

    //石面纹理
    float grain = tex2D(noiseSampler, coords * 2.2).r;
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

    //三齿：每齿中心最高
    float toothPhase = abs(frac(x * 3.0) - 0.5) * 2.0;   //0 齿心 → 1 齿缝
    float peak = 1.0 - toothPhase * 0.92;

    float solid = step(h, peak);

    //石身色：竖向渐暗 + 颗粒
    float grain = tex2D(noiseSampler, coords * float2(3.0, 1.4)).r;
    float3 stoneDark = float3(0.26, 0.21, 0.15);
    float3 stoneLite = float3(0.48, 0.41, 0.30);
    float3 col = lerp(stoneDark, stoneLite, grain) * (1.0 - h * 0.35);

    //齿缘热线：贴近轮廓的内侧亮边
    float edge = exp(-abs(peak - h) * 16.0);
    col += float3(1.0, 0.62, 0.18) * edge * 0.9;

    //基部熔缝
    float baseGlow = exp(-h * 5.5) * (0.5 + 0.5 * sin(uTime * 9.0 + x * 21.0));
    col += float3(1.0, 0.45, 0.10) * baseGlow * 0.5;

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

    //焰体噪声：向尖端滚动 + 横向扰动
    float n1 = tex2D(noiseSampler, float2(coords.x * 1.8, h * 1.4 - uTime * 1.7)).r;
    float n2 = tex2D(noiseSampler, float2(coords.x * 3.6 + 0.37, h * 2.7 - uTime * 2.6)).r;
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

technique PlateTech
{
    pass PlatePass
    {
        PixelShader = compile ps_3_0 PlatePS();
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
