// ============================================================================
//KingSlimeRoyalAura.fx 史莱姆王皇家威能光环
//AlphaBlend 预乘 alpha，采样 uImage0 作遮罩
// ============================================================================

sampler uImage0 : register(s0);

float uTime;          //全局时间
float intensity;      //总强度 0~1
float mode;           //0=Idle 1=Charging 2=Enraged 3=Slamming
float progress;       //当前模式的进度 0~1
float2 texelSize;     //1/纹理宽高
float seed;           //实例化扰动种子

float3 royalCore;     //核心皇冠色（亮金）
float3 royalEdge;     //边缘流光色（皇室紫蓝）

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

//8 邻域 alpha 最大值：粗描边检测
float edgeMax(float2 uv, float radius)
{
    float maxA = 0;
    [unroll]
    for (int oy = -1; oy <= 1; oy++)
    {
        [unroll]
        for (int ox = -1; ox <= 1; ox++)
        {
            if (ox == 0 && oy == 0) continue;
            float2 off = float2(ox, oy) * texelSize * radius;
            maxA = max(maxA, tex2D(uImage0, uv + off).a);
        }
    }
    return maxA;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 texColor = tex2D(uImage0, coords);

    bool isCharging = mode > 0.5 && mode < 1.5;
    bool isEnraged  = mode > 1.5 && mode < 2.5;
    bool isSlamming = mode > 2.5;

    //========================================
    //脉冲：常态慢呼吸 / 蓄力快脉冲 / 暴怒急促 / 砸地极速
    //========================================
    float pulseFreq = 1.4;
    if (isCharging) pulseFreq = 5.0 + progress * 4.0;
    if (isEnraged)  pulseFreq = 3.5;
    if (isSlamming) pulseFreq = 9.0;
    float pulse = 0.5 + 0.5 * sin(uTime * pulseFreq + seed * 6.28);

    //========================================
    //像素是否处于贴图实体内
    //========================================
    if (texColor.a < 0.04)
    {
        //透明像素：绘制宫廷光晕
        float inner  = edgeMax(coords, 1.5);
        float middle = edgeMax(coords, 3.5);
        float outer  = edgeMax(coords, 6.0);

        if (inner < 0.02 && middle < 0.02 && outer < 0.02)
            return float4(0, 0, 0, 0);

        //沿轮廓奔走的"皇家流光"
        float runPhase = frac(uTime * 0.9 + coords.y * 1.6 + seed * 0.27);
        float runSpot  = smoothstep(0.40, 0.50, runPhase) * smoothstep(0.60, 0.50, runPhase);

        float3 edgeCol;
        float edgeStrength;

        if (isSlamming)
        {
            //砸地：金白爆闪 + 强外晕
            float3 hot = float3(1.0, 0.95, 0.65);
            edgeCol = lerp(royalCore, hot, 0.55 + 0.45 * pulse);
            edgeStrength = inner * 1.20 + middle * 0.65 + outer * 0.35 + runSpot * 0.45;
        }
        else if (isEnraged)
        {
            //暴怒：紫蓝深邃 + 内层金芒，描边整体偏厚
            float3 deepRoyal = float3(0.45, 0.20, 0.85);
            edgeCol = lerp(deepRoyal, royalCore, saturate(inner * 0.6 + runSpot * 0.7 + 0.25 * pulse));
            edgeStrength = inner * 1.05 + middle * 0.55 + outer * 0.30 + runSpot * 0.35;
        }
        else if (isCharging)
        {
            //蓄力：皇室紫闪 + 进度越高描边越厚
            float prog = saturate(progress);
            float3 warmGold = float3(1.0, 0.85, 0.35);
            edgeCol = lerp(royalEdge, warmGold, 0.40 + 0.55 * pulse * prog);
            edgeStrength = (inner * 1.05 + middle * 0.50 + outer * 0.22) * (0.45 + 0.55 * prog);
        }
        else
        {
            //常态：柔和宝石蓝紫描边，稳定的光晕
            edgeCol = lerp(royalEdge * 0.8, royalEdge, 0.35 + 0.45 * pulse);
            edgeStrength = inner * 0.65 + middle * 0.30 + outer * 0.12;
        }

        edgeStrength = saturate(edgeStrength) * intensity;
        float3 premultRGB = edgeCol * edgeStrength;
        return float4(premultRGB, edgeStrength);
    }

    //========================================
    //实体像素：保留原色 + 添加皇室光泽
    //========================================
    float4 color = texColor * vertexColor;
    float lum = dot(color.rgb / max(color.a, 0.0001), float3(0.299, 0.587, 0.114));

    //内边缘检测：加强凝胶折光
    float a_r = tex2D(uImage0, coords + float2( texelSize.x * 1.5, 0)).a;
    float a_l = tex2D(uImage0, coords + float2(-texelSize.x * 1.5, 0)).a;
    float a_u = tex2D(uImage0, coords + float2(0,  texelSize.y * 1.5)).a;
    float a_d = tex2D(uImage0, coords + float2(0, -texelSize.y * 1.5)).a;
    float innerEdge = saturate(1.0 - (a_r + a_l + a_u + a_d) * 0.25);
    innerEdge = smoothstep(0.0, 0.6, innerEdge);

    //果冻内部散射光：两组斜向波叠加产生干涉纹，避免扫描线
    float wave1 = sin(coords.y * 5.0 + coords.x * 2.5 - uTime * 0.9 + seed);
    float wave2 = sin(coords.y * 3.5 - coords.x * 3.0 + uTime * 0.6 + seed * 2.1);
    float bandMask = smoothstep(0.0, 0.45, saturate(wave1 * wave2 * 0.5 + 0.15));

    float3 result = color.rgb;

    if (isSlamming)
    {
        //砸地：白热爆闪
        float3 hot = float3(1.0, 0.95, 0.7);
        float darkMask = 1.0 - smoothstep(0.10, 0.55, lum);
        result += hot * 0.30 * intensity * darkMask * color.a;
        result += royalCore * innerEdge * 0.60 * intensity * color.a;
        result += hot * bandMask * 0.30 * intensity * color.a;
    }
    else if (isEnraged)
    {
        //暴怒：深紫宝石覆色 + 金芒包裹
        float darkMask = 1.0 - smoothstep(0.10, 0.55, lum);
        float3 deepRoyal = float3(0.55, 0.30, 1.0);
        float blink = 0.6 + 0.4 * pulse;
        result = lerp(result, deepRoyal * color.a + result * 0.30, intensity * 0.45 * darkMask * blink);
        result += royalCore * innerEdge * 0.40 * intensity * color.a;
        result += royalCore * bandMask * 0.20 * intensity * color.a;
    }
    else if (isCharging)
    {
        //蓄力：皇冠琥珀光
        float prog = saturate(progress);
        float blink = 0.55 + 0.45 * pulse;
        float darkMask = 1.0 - smoothstep(0.08, 0.55, lum);
        float warnMix = prog * intensity * 0.40 * blink * darkMask;
        result = lerp(result, royalEdge * color.a + result * 0.35, warnMix);
        result += royalCore * innerEdge * 0.45 * intensity * prog * color.a;
        result += royalCore * bandMask * 0.18 * intensity * prog * color.a;
    }
    else
    {
        //常态：内部缓流的宝石光带
        result += royalEdge * 0.18 * intensity * (1.0 - smoothstep(0.10, 0.55, lum)) * color.a;
        result += royalCore * innerEdge * 0.20 * intensity * color.a * (0.6 + 0.4 * pulse);
        result += royalEdge * bandMask * 0.10 * intensity * color.a;
    }

    return float4(result, color.a);
}

technique Technique1
{
    pass KingSlimeRoyalAuraPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
