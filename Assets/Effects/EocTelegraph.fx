// ============================================================================
//EocTelegraph.fx 克眼预警(冲刺车道/雾团出击环)
//血材质预警：深酒红而非机械橙，边线湿润下滴
//C# 用 Techniques[name] 选 LaneTech/RingTech，禁 uniform 模式分支
//LaneTech origin 左端中点；RingTech origin 中心，主环 r=0.77
//AlphaBlend 预乘输出
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uProgress;   //0~1 预警进度，1=起跑/出击
float uIntensity;

static const float3 WarnDeep  = float3(0.427, 0.047, 0.078);
static const float3 WarnHot   = float3(0.760, 0.118, 0.157);

//----------------------------------------------------------------------------
//冲刺车道：中脊亮线+软缘+向前奔流的血丝+进度扫描
//----------------------------------------------------------------------------
float4 LanePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float x = coords.x;                     //0起点→1远端
    float centered = abs(coords.y - 0.5) * 2.0;

    float pulse = 0.6 + 0.4 * sin(uTime * (4.6 + 7.5 * uProgress));
    float3 col = lerp(WarnDeep, WarnHot, 0.3 + 0.5 * pulse * uProgress);

    //中脊细线+柔缘
    float spine = exp(-pow(centered * 7.5, 2.0));
    float soft = exp(-pow(centered * 2.4, 2.0)) * 0.28;

    //远端渐隐
    float reach = (1.0 - smoothstep(0.72, 1.0, x)) * smoothstep(0.0, 0.05, x);

    //向前奔流血丝：条纹沿 x 滚动
    float flow = sin(x * 34.0 - uTime * 7.0) * 0.5 + 0.5;
    flow = pow(flow, 5.0) * spine * 0.9;

    //进度扫描：亮头自起点推进到远端，扫到即起跑
    float sweep = exp(-pow((x - uProgress) * 12.0, 2.0)) * (0.4 + 0.6 * uProgress);

    float a = (spine * (0.24 + 0.5 * uProgress) + soft * (0.35 + 0.65 * uProgress) + flow + sweep) * reach;
    a = saturate(a * uIntensity);
    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

//----------------------------------------------------------------------------
//出击环：主边界环+倒计时收缩圈+沿环流动血弧+湿垂
//----------------------------------------------------------------------------
float4 RingPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 c = (coords - 0.5) * 2.0 + 1e-5;
    float r = length(c);
    float ang = atan2(c.y, c.x);

    float pulse = 0.6 + 0.4 * sin(uTime * (4.6 + 8.0 * uProgress));
    float3 col = lerp(WarnDeep, WarnHot, 0.3 + 0.5 * pulse * uProgress);

    //主边界环
    float mainR = 0.77;
    float ring = exp(-pow((r - mainR) * 52.0, 2.0));
    float ringGlow = exp(-pow((r - mainR) * 12.0, 2.0)) * 0.26;

    //倒计时收缩圈：与主环重合时刻=出击
    float collapseR = lerp(0.99, mainR, uProgress);
    float collapse = exp(-pow((r - collapseR) * 42.0, 2.0)) * (0.22 + 0.78 * uProgress);

    //沿环三段流动血弧(整数倍角，跨±π连续)
    float flow = sin(ang * 3.0 - uTime * 2.4) * 0.5 + 0.5;
    flow = pow(flow, 6.0) * ring * 1.05;

    //湿垂：下半环更饱和，血往下坠的材质暗示
    float sag = saturate(c.y) * 0.35 * ring;

    float a = ring * (0.5 + 0.45 * pulse) + ringGlow + collapse + flow + sag;
    a = saturate(a * uIntensity);
    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

technique LaneTech
{
    pass LanePass
    {
        PixelShader = compile ps_3_0 LanePS();
    }
}

technique RingTech
{
    pass RingPass
    {
        PixelShader = compile ps_3_0 RingPS();
    }
}
