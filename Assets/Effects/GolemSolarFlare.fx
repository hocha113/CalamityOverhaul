// ============================================================================
//GolemSolarFlare.fx 太阳核心材质 / 辐条光束 / 全屏太阳白闪
//CoreTech：Additive 批日面（米粒组织+临边昏暗+整数倍角冕芒）
//BeamTech：Additive 批辐条（origin 左端中点，沿 +X 延伸）
//FlashTech：Opaque 全屏 ping-pong（金色白化）
//极角审计：theta 仅进 sin(6θ)，6∈整数；米粒噪声走刚体旋转笛卡尔坐标
//无动态分支，噪声全走 uNoise 贴图
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图（FlashTech 全屏拷贝）

float uTime;
float uProgress;
float uIntensity;
float2 uCenter;    //FlashTech：白闪中心 uv
float uAspect;     //FlashTech：宽高比
// 噪声固定 s1：Core/Beam 两个 pass 不采样 s0，旧 sampler_state 自动分配在这两个 pass 落 s0，
// 被 SpriteBatch 用画布贴图覆写→米粒组织/撕边全读成辉光渐变；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSampler : register(s1);

//------------------------------------------------------------------
//日面：米粒组织 + 临边昏暗 + 冕芒
//------------------------------------------------------------------
float4 CorePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 c = (coords - 0.5) * 2.0 + 1e-5;
    float r = length(c);
    float ang = atan2(c.y, c.x);

    //刚体旋转坐标采噪声（无极角接缝）
    float cs = cos(uTime * 0.14);
    float sn = sin(uTime * 0.14);
    float2 rc = float2(c.x * cs - c.y * sn, c.x * sn + c.y * cs);

    //米粒组织：双频噪声乘积
    float g1 = tex2D(noiseSampler, rc * 0.9 + 0.5).r;
    float g2 = tex2D(noiseSampler, rc * 2.1 + float2(0.33, 0.71) + uTime * 0.01).r;
    float granule = 0.55 + 0.45 * (g1 * 0.6 + g2 * 0.4) * 1.4;

    //日盘：临边昏暗
    float discR = 0.42;
    float disc = 1.0 - smoothstep(discR * 0.86, discR, r);
    float limb = 1.0 - smoothstep(0.0, discR, r) * 0.5;

    //冕芒：六向长针（整数倍角），随时间缓转
    float spike = pow(saturate(sin(ang * 6.0 + uTime * 0.8) * 0.5 + 0.5), 10.0);
    float corona = spike * exp(-max(r - discR, 0.0) * 4.6) * step(discR * 0.9, r);
    //环冕辉光
    float halo = exp(-max(r - discR, 0.0) * 7.0) * 0.5;

    float3 coreCol = float3(1.00, 0.92, 0.62);
    float3 midCol  = float3(1.00, 0.62, 0.18);
    float3 edgeCol = float3(0.85, 0.28, 0.05);

    float3 col = lerp(coreCol, midCol, smoothstep(0.0, discR, r)) * granule * limb * disc;
    col += lerp(midCol, edgeCol, saturate((r - discR) * 3.0)) * (corona + halo);

    float a = saturate((disc * granule + corona * 0.9 + halo) * uIntensity);
    return float4(col * vertexColor.rgb, a * vertexColor.a);
}

//------------------------------------------------------------------
//辐条光束：白热芯 + 橙缘 + 噪声撕边，尖端渐灭
//------------------------------------------------------------------
float4 BeamPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float along = coords.x;            //0 根部 → 1 尖端
    float across = abs(coords.y - 0.5) * 2.0;

    //撕边噪声：沿束长滚动
    float tear = tex2D(noiseSampler, float2(along * 3.0 - uTime * 0.9, coords.y * 1.3)).r;
    float widthMask = 1.0 - smoothstep(0.42 + tear * 0.28, 0.95, across);

    //白热芯
    float coreMask = 1.0 - smoothstep(0.0, 0.3, across);

    //根部最亮，尖端渐灭 + 呼吸
    float lenFade = (1.0 - smoothstep(0.55, 1.0, along)) * (0.85 + 0.15 * sin(uTime * 12.0 + along * 9.0));
    float rootBoost = exp(-along * 2.6) * 0.6;

    float3 edgeCol = float3(1.00, 0.45, 0.08);
    float3 coreCol = float3(1.00, 0.95, 0.78);
    float3 col = edgeCol * widthMask + coreCol * coreMask * 1.1;

    float a = saturate((widthMask * 0.55 + coreMask * 0.9 + rootBoost) * lenFade * uProgress * uIntensity);
    return float4(col * a, a) * vertexColor.a;
}

//------------------------------------------------------------------
//全屏太阳白闪：金色白化自中心衰减，快起慢落
//------------------------------------------------------------------
float4 FlashPS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(uImage0, coords);

    //包络：前15%冲顶，其后二次衰减
    float attack = smoothstep(0.0, 0.15, uProgress);
    float decay = 1.0 - smoothstep(0.15, 1.0, uProgress);
    float envelope = attack * decay * decay;

    //自中心径向衰减（校正宽高比）
    float2 d = coords - uCenter;
    d.x *= uAspect;
    float dist = length(d);
    float radial = 1.0 - smoothstep(0.0, 1.05, dist);

    float mask = saturate(envelope * radial * uIntensity * 1.35);

    float3 flashCol = float3(1.0, 0.94, 0.78);
    float3 col = lerp(scene.rgb, flashCol, mask);
    //白化同时轻微提升整体曝光
    col += flashCol * mask * 0.25;

    return float4(col, scene.a);
}

technique CoreTech
{
    pass CorePass
    {
        PixelShader = compile ps_3_0 CorePS();
    }
}

technique BeamTech
{
    pass BeamPass
    {
        PixelShader = compile ps_3_0 BeamPS();
    }
}

technique FlashTech
{
    pass FlashPass
    {
        PixelShader = compile ps_3_0 FlashPS();
    }
}
