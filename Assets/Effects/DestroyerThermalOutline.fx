// ============================================================================
//DestroyerThermalOutline.fx 毁灭者热感/警告/冲刺描边
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;          //全局时间
float intensity;      //总强度 0~1
float mode;           //0=Idle, 1=Warning, 2=Dashing
float progress;       //警告/冲刺过程 0~1
float2 texelSize;     //1/纹理宽高
float seed;           //实例化扰动种子（区分体节）
float4 frameUV;       //当前帧UV范围 (minU, minV, maxU, maxV)，越界采样视为透明

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

//越界安全采样：防止采到相邻帧的像素
//毁灭者贴图按4帧竖排，靠近边缘做邻域采样会侵入下一帧，
//导致出现"凭空多出来的描边"。这里限制采样必须在当前帧 UV 范围内
float sampleAlphaSafe(float2 uv)
{
    if (uv.x < frameUV.x || uv.x > frameUV.z || uv.y < frameUV.y || uv.y > frameUV.w)
        return 0.0;
    return tex2D(uImage0, uv).a;
}

//8邻域 alpha 最大值：用于检测外轮廓
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
            float2 offset = float2(ox, oy) * texelSize * radius;
            maxA = max(maxA, sampleAlphaSafe(uv + offset));
        }
    }
    return maxA;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 texColor = tex2D(uImage0, coords);

    bool isWarning = mode > 0.5 && mode < 1.5;
    bool isDashing = mode > 1.5;

    //========================================
    //各状态色板（机械高温感）
    //========================================
    float3 idleDeep   = float3(0.55, 0.10, 0.04);  //暗红基底
    float3 idleHot    = float3(1.00, 0.45, 0.15);  //橙红高温
    float3 warnDeep   = float3(0.95, 0.20, 0.05);  //警告深红
    float3 warnBright = float3(1.00, 0.85, 0.20);  //警告亮黄
    float3 dashCore   = float3(1.00, 0.95, 0.65);  //冲刺白热
    float3 dashEdge   = float3(1.00, 0.45, 0.10);  //冲刺火橙

    //========================================
    //脉冲：常态慢呼吸 / 警告快脉冲 / 冲刺极速
    //========================================
    float pulseFreq = 2.2;
    if (isWarning) pulseFreq = 4.0 + progress * 6.0;
    if (isDashing) pulseFreq = 9.0;
    float pulse = 0.5 + 0.5 * sin(uTime * pulseFreq + seed * 6.28);

    //========================================
    //透明像素：绘制外描边光晕
    //========================================
    if (texColor.a < 0.04)
    {
        float inner  = edgeMax(coords, 1.5);
        float middle = edgeMax(coords, 3.0);
        float outer  = edgeMax(coords, 5.0);

        //邻域全空：纯背景，跳过
        if (inner < 0.05 && middle < 0.05 && outer < 0.05)
            return float4(0, 0, 0, 0);

        float3 edgeCol;
        float edgeStrength;

        if (isDashing)
        {
            //冲刺：白热边缘+宽外发光，沿轮廓有亮斑奔走
            float runPhase = frac(uTime * 1.6 + coords.y * 1.4 + seed * 0.31);
            float runSpot  = smoothstep(0.42, 0.50, runPhase) * smoothstep(0.58, 0.50, runPhase);
            edgeCol = lerp(dashEdge, dashCore, saturate(inner * (0.55 + 0.45 * pulse) + runSpot * 0.6));
            edgeStrength = inner * 1.10 + middle * 0.55 + outer * 0.28 + runSpot * 0.35;
        }
        else if (isWarning)
        {
            //警告：红黄高对比，进度越高描边越宽越亮
            float blink = 0.55 + 0.45 * pulse;
            float prog  = saturate(progress);
            edgeCol = lerp(warnDeep, warnBright, blink * (0.45 + 0.55 * prog));
            edgeStrength = (inner * 1.20 + middle * 0.55 + outer * 0.22) * (0.55 + 0.45 * prog);
        }
        else
        {
            //常态：稳定的机械红橙描边+缓呼吸（解决夜晚看不清）
            edgeCol = lerp(idleDeep, idleHot, 0.40 + 0.45 * pulse);
            edgeStrength = inner * 0.80 + middle * 0.30 + outer * 0.10;
        }

        edgeStrength = saturate(edgeStrength) * intensity;
        //注意：外描边故意不乘以 vertexColor。drawColor 会随世界光照变暗，
        //而我们的目标正是在夜晚保持轮廓可见，所以这层始终输出原色描边
        float3 premultRGB = edgeCol * edgeStrength;
        return float4(premultRGB, edgeStrength);
    }

    //========================================
    //实体像素：保留原色 + 微调以提升暗环境可辨识度
    //========================================
    float4 color = texColor * vertexColor;
    float lum = dot(color.rgb / max(color.a, 0.0001), float3(0.299, 0.587, 0.114));

    //内边缘检测：加强机械装甲边缘的高光（同样使用安全采样避免跨帧）
    float a_r = sampleAlphaSafe(coords + float2( texelSize.x * 1.5, 0));
    float a_l = sampleAlphaSafe(coords + float2(-texelSize.x * 1.5, 0));
    float a_u = sampleAlphaSafe(coords + float2(0,  texelSize.y * 1.5));
    float a_d = sampleAlphaSafe(coords + float2(0, -texelSize.y * 1.5));
    float innerEdge = saturate(1.0 - (a_r + a_l + a_u + a_d) * 0.25);
    innerEdge = smoothstep(0.0, 0.6, innerEdge);

    float3 result = color.rgb;

    if (isDashing)
    {
        //整体白热提亮（限于较暗区域，避免破坏纹理）
        float darkMask = 1.0 - smoothstep(0.10, 0.55, lum);
        result += dashCore * 0.18 * intensity * darkMask * color.a;
        result += dashEdge * innerEdge * 0.55 * intensity * color.a;

        //沿身体高速横扫的能量带
        float scan = frac(coords.y * 14.0 - uTime * 3.2 + seed);
        float scanMask = smoothstep(0.42, 0.50, scan) * smoothstep(0.58, 0.50, scan);
        result += dashCore * scanMask * 0.22 * intensity * color.a;
    }
    else if (isWarning)
    {
        float blink = 0.5 + 0.5 * sin(uTime * (4.0 + progress * 8.0) + seed);
        //红色警示覆盖：仅作用于中暗部，保留高光纹理
        float darkMask = 1.0 - smoothstep(0.08, 0.55, lum);
        float warnMix  = progress * intensity * 0.45 * (0.55 + 0.45 * blink) * darkMask;
        result = lerp(result, warnDeep * color.a + result * 0.35, warnMix);

        //装甲内边缘黄炽
        result += warnBright * innerEdge * 0.45 * intensity * progress * color.a;

        //急促警示扫描带
        float scan = frac(coords.y * 10.0 - uTime * 4.5);
        float scanMask = smoothstep(0.40, 0.50, scan) * smoothstep(0.60, 0.50, scan);
        result += warnBright * scanMask * 0.20 * intensity * progress * color.a;
    }
    else
    {
        //常态：仅在暗色区域注入轻微红橙热感，避免影响主纹理
        float darkMask = 1.0 - smoothstep(0.06, 0.45, lum);
        result += idleDeep * 0.18 * intensity * darkMask * color.a;
        //装甲缝隙处加一抹温暖描边
        result += idleHot * innerEdge * 0.22 * intensity * color.a * (0.7 + 0.3 * pulse);
        //整体微弱亮度补偿，让夜晚也能看清轮廓但不过曝
        result += idleDeep * 0.04 * intensity * color.a;
    }

    return float4(result, color.a);
}

technique Technique1
{
    pass DestroyerThermalOutlinePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
