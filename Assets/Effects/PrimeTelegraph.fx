// ============================================================================
//PrimeTelegraph.fx 机械骷髅王预警(扇形/圆环)
//禁 uniform 模式分支，C# 用 Techniques[name] 选 FanTech/RingTech
//FanTech origin 左端中点；RingTech origin 中心主环 r=0.77
//Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uProgress;   //0~1 预警充能进度（由 timeLeft 推导，全端一致）
float uIntensity;  //总强度
float uFanAngle;   //扇形半角（弧度）

//----------------------------------------------------------------------------
//扇形：两条细边界线 + 克制的内部填充 + 径向进度扫描
//----------------------------------------------------------------------------
float4 FanPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float3 warnDeep = float3(1.00, 0.22, 0.06);
    float3 warnHot  = float3(1.00, 0.80, 0.28);
    float pulse = 0.6 + 0.4 * sin(uTime * (5.0 + 8.0 * uProgress));
    float3 col = lerp(warnDeep, warnHot, 0.30 + 0.50 * pulse * uProgress);

    float2 p = float2(coords.x, (coords.y - 0.5) * 2.0);
    float r = length(p);
    float ang = atan2(p.y, max(p.x, 1e-4));
    float absAng = abs(ang);

    float inside = 1.0 - smoothstep(uFanAngle * 0.92, uFanAngle, absAng);
    float radial = smoothstep(0.02, 0.10, r) * (1.0 - smoothstep(0.82, 1.0, r));
    //两条角度边界细亮线 + 柔光
    float edge = exp(-pow((absAng - uFanAngle) * 34.0, 2.0)) * 1.2
               + exp(-pow((absAng - uFanAngle) * 10.0, 2.0)) * 0.3;
    //径向进度扫描（充能推进到外缘 = 即将开火）
    float fillR = 1.0 - smoothstep(uProgress - 0.05, uProgress + 0.05, r);

    float a = inside * radial * (0.06 + 0.18 * uProgress + 0.22 * fillR)
            + edge * radial * (0.35 + 0.65 * uProgress);

    a = saturate(a * uIntensity);
    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

//----------------------------------------------------------------------------
//圆环（区域边界标记）：细亮边界环 + 倒计时收缩圈 + 流动能量段
//收缩圈与边界环重合的时刻 = 攻击激活
//----------------------------------------------------------------------------
float4 RingPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float3 warnDeep = float3(1.00, 0.22, 0.06);
    float3 warnHot  = float3(1.00, 0.80, 0.28);
    float pulse = 0.6 + 0.4 * sin(uTime * (5.0 + 8.0 * uProgress));
    float3 col = lerp(warnDeep, warnHot, 0.30 + 0.50 * pulse * uProgress);

    float2 c = (coords - 0.5) * 2.0 + 1e-5;
    float r = length(c);
    float ang = atan2(c.y, c.x);

    //主边界环：细线 + 柔光（位置固定，标记真实危险半径）
    float mainR = 0.77;
    float ring = exp(-pow((r - mainR) * 60.0, 2.0));
    float ringGlow = exp(-pow((r - mainR) * 14.0, 2.0)) * 0.30;

    //倒计时收缩圈：自外侧收拢，触发时刻与主环重合
    float collapseR = lerp(0.99, mainR, uProgress);
    float collapse = exp(-pow((r - collapseR) * 46.0, 2.0)) * (0.25 + 0.75 * uProgress);

    //沿环缓慢流动的三段能量弧
    float flow = sin(ang * 3.0 - uTime * 2.2) * 0.5 + 0.5;
    flow = pow(flow, 6.0) * ring * 1.1;

    //区域内部极淡填充：标记"圈内即危险区"
    float fill = (1.0 - smoothstep(0.0, mainR, r)) * 0.05 * (0.4 + 0.6 * uProgress);

    float a = ring * (0.55 + 0.45 * pulse) + ringGlow + collapse + flow + fill;
    a = saturate(a * uIntensity);
    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

technique FanTech
{
    pass FanPass
    {
        PixelShader = compile ps_3_0 FanPS();
    }
}

technique RingTech
{
    pass RingPass
    {
        PixelShader = compile ps_3_0 RingPS();
    }
}
