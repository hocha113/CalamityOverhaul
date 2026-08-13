// ============================================================================
//GolemSunTelegraph.fx 石巨人金色落点环预警
//RingTech origin 中心，主环 r=0.77，收缩圈重合时刻=起爆
//Additive 批（输出 (rgb, a)，最终叠加 rgb*a）
//极角审计：theta 仅进 sin(4θ)，4∈整数，跨 ±π 连续
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uProgress;   //0~1 预警进度（timeLeft 推导，全端一致）
float uIntensity;

float4 RingPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float3 warnDeep = float3(1.00, 0.55, 0.10);
    float3 warnHot  = float3(1.00, 0.88, 0.45);
    float pulse = 0.6 + 0.4 * sin(uTime * (5.0 + 8.0 * uProgress));
    float3 col = lerp(warnDeep, warnHot, 0.30 + 0.50 * pulse * uProgress);

    float2 c = (coords - 0.5) * 2.0 + 1e-5;
    float r = length(c);
    float ang = atan2(c.y, c.x);

    //主边界环：细线 + 柔光（真实危险半径）
    float mainR = 0.77;
    float ring = exp(-pow((r - mainR) * 58.0, 2.0));
    float ringGlow = exp(-pow((r - mainR) * 13.0, 2.0)) * 0.28;

    //倒计时收缩圈
    float collapseR = lerp(0.99, mainR, uProgress);
    float collapse = exp(-pow((r - collapseR) * 44.0, 2.0)) * (0.25 + 0.75 * uProgress);

    //神庙刻纹：四方位棱纹沿环缓转（整数倍角连续）
    float glyph = sin(ang * 4.0 - uTime * 1.6) * 0.5 + 0.5;
    glyph = pow(glyph, 8.0) * ring * 1.2;

    //区域内部极淡填充
    float fill = (1.0 - smoothstep(0.0, mainR, r)) * 0.05 * (0.4 + 0.6 * uProgress);

    float a = ring * (0.55 + 0.45 * pulse) + ringGlow + collapse + glyph + fill;
    a = saturate(a * uIntensity);
    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

technique RingTech
{
    pass RingPass
    {
        PixelShader = compile ps_3_0 RingPS();
    }
}
