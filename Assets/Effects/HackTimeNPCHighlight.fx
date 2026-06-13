// ============================================================================
// HackTimeNPCHighlight.fx 骇客时间 NPC 高亮
// 采样 uImage0；选中=赛博红故障，悬停=冷青全息
// ============================================================================

sampler uImage0 : register(s0);

float2 texelSize;  //1/宽 1/高
float intensity;   //0~1
float isSelected;  //1选中 0悬停
float uTime;

//伪随机哈希
float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

//平滑脉冲
float pulse(float x, float center, float width)
{
    return smoothstep(width, 0.0, abs(x - center));
}

float4 HackNPCPass(float2 coords : TEXCOORD0, float4 smpColor : COLOR0) : COLOR0
{
    float effectStr = intensity;

    //========================================
    //0. 故障位移：选中时随机水平错位像素行
    //========================================
    float2 sampleCoords = coords;
    if (isSelected > 0.5)
    {
        //周期性故障窗口：每隔一段时间出现短暂的强烈故障
        float glitchCycle = frac(uTime * 0.6);
        float glitchWindow = smoothstep(0.0, 0.05, glitchCycle) * smoothstep(0.15, 0.10, glitchCycle);
        //基于行号的随机偏移
        float row = floor(coords.y / (texelSize.y * 3.0));
        float rowRand = hash(row + floor(uTime * 12.0));
        float displacement = (rowRand - 0.5) * 0.03 * glitchWindow * effectStr;
        sampleCoords.x += displacement;
    }

    float4 texColor = tex2D(uImage0, sampleCoords);

    //========================================
    //1.多层邻域采样——边缘检测
    //========================================
    //内层：精细描边
    float stp1 = 1.5;
    float a1_r = tex2D(uImage0, sampleCoords + float2( texelSize.x * stp1, 0)).a;
    float a1_l = tex2D(uImage0, sampleCoords + float2(-texelSize.x * stp1, 0)).a;
    float a1_u = tex2D(uImage0, sampleCoords + float2(0,  texelSize.y * stp1)).a;
    float a1_d = tex2D(uImage0, sampleCoords + float2(0, -texelSize.y * stp1)).a;
    float a1_ru = tex2D(uImage0, sampleCoords + texelSize * stp1).a;
    float a1_ld = tex2D(uImage0, sampleCoords - texelSize * stp1).a;
    float a1_lu = tex2D(uImage0, sampleCoords + float2(-texelSize.x, texelSize.y) * stp1).a;
    float a1_rd = tex2D(uImage0, sampleCoords + float2(texelSize.x, -texelSize.y) * stp1).a;
    float innerMax = max(max(max(a1_r, a1_l), max(a1_u, a1_d)),
                         max(max(a1_ru, a1_ld), max(a1_lu, a1_rd)));
    float innerAvg = (a1_r + a1_l + a1_u + a1_d + a1_ru + a1_ld + a1_lu + a1_rd) * 0.125;

    //外层：扩散辉光
    float stp2 = 3.5;
    float a2_r = tex2D(uImage0, sampleCoords + float2( texelSize.x * stp2, 0)).a;
    float a2_l = tex2D(uImage0, sampleCoords + float2(-texelSize.x * stp2, 0)).a;
    float a2_u = tex2D(uImage0, sampleCoords + float2(0,  texelSize.y * stp2)).a;
    float a2_d = tex2D(uImage0, sampleCoords + float2(0, -texelSize.y * stp2)).a;
    float outerMax = max(max(a2_r, a2_l), max(a2_u, a2_d));

    float pulseSlow = 0.65 + 0.35 * sin(uTime * 2.8);
    float pulseFast = 0.5 + 0.5 * sin(uTime * 6.0);

    //========================================
    //2.外描边+扩散辉光：透明像素但有不透明邻居
    //========================================
    if (texColor.a < 0.1)
    {
        //内层描边：锐利明亮
        if (innerMax > 0.3)
        {
            float3 outlineColor = isSelected > 0.5
                ? float3(1.0, 0.15, 0.08)
                : float3(0.1, 0.8, 0.9);
            float glowStr = innerMax * effectStr * 0.8 * pulseSlow;
            //选中时描边有闪烁毛刺
            if (isSelected > 0.5)
            {
                float flicker = hash(floor(uTime * 18.0) + coords.y * 100.0);
                glowStr *= (flicker > 0.15) ? 1.0 : 0.3;
            }
            return float4(outlineColor * glowStr, glowStr);
        }
        //外层扩散：柔和远距辉光
        if (outerMax > 0.2)
        {
            float3 glowColor = isSelected > 0.5
                ? float3(0.6, 0.04, 0.02)
                : float3(0.03, 0.35, 0.40);
            float glowStr = outerMax * effectStr * 0.25 * pulseSlow;
            return float4(glowColor * glowStr, glowStr);
        }
        return float4(0, 0, 0, 0);
    }

    //完全透明直接丢弃
    if (texColor.a < 0.01)
        return float4(0, 0, 0, 0);

    //========================================
    //3.应用顶点着色(环境光照)
    //========================================
    float4 color = texColor * smpColor;
    float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));

    //内边缘因子
    float edgeFactor = 1.0 - innerAvg;
    edgeFactor = saturate(edgeFactor);
    edgeFactor = smoothstep(0.0, 0.5, edgeFactor);

    float3 result = color.rgb;

    //========================================
    //4.选中状态：赛博朋克红色威胁风格（强化版）
    //========================================
    if (isSelected > 0.5)
    {
        //——A.强力去饱和再红色重映射——
        //先大幅去饱和，然后用红色通道重建
        float3 desat = float3(lum, lum, lum);
        result = lerp(result, desat, effectStr * 0.7); //先洗掉原色

        //红色重映射：整体偏红，暗部深红，亮部亮橙红
        float3 darkTint = float3(0.45, 0.03, 0.01);
        float3 midTint = float3(0.85, 0.10, 0.05);
        float3 brightTint = float3(1.0, 0.35, 0.15);
        float3 cyberColor;
        if (lum < 0.4)
            cyberColor = lerp(darkTint, midTint, lum / 0.4);
        else
            cyberColor = lerp(midTint, brightTint, saturate((lum - 0.4) / 0.6));
        result = lerp(result, cyberColor, effectStr * 0.75);

        //——B.强亮度增益——
        result += float3(0.22, 0.04, 0.02) * effectStr;
        float highBoost = smoothstep(0.25, 0.65, lum);
        result += float3(0.3, 0.08, 0.03) * highBoost * effectStr;

        //——C.内边缘强辉光（加宽加亮）——
        result += float3(1.0, 0.22, 0.10) * edgeFactor * effectStr * 0.8 * pulseSlow;

        //——D.水平扫描带（更宽更亮）——
        float scanSweep = frac(uTime * 0.25);
        float scanBand = pulse(coords.y, scanSweep, 0.03);
        result += float3(1.0, 0.30, 0.18) * scanBand * effectStr * 0.45;

        //——E.细密CRT扫描线——
        float scanLine = frac(coords.y / (texelSize.y * 2.5));
        float scanDark = smoothstep(0.45, 0.5, scanLine) * smoothstep(0.55, 0.5, scanLine);
        result *= 1.0 - scanDark * 0.10 * effectStr;

        //——F.数字网格叠加（更明显）——
        float gridX = frac(coords.x / (texelSize.x * 8.0));
        float gridY = frac(coords.y / (texelSize.y * 8.0));
        float gridLineX = smoothstep(0.46, 0.5, gridX) * smoothstep(0.54, 0.5, gridX);
        float gridLineY = smoothstep(0.46, 0.5, gridY) * smoothstep(0.54, 0.5, gridY);
        float gridMask = max(gridLineX, gridLineY);
        result += float3(0.6, 0.10, 0.05) * gridMask * effectStr * 0.12 * (0.6 + 0.4 * pulseSlow);

        //——G.周期性全身闪烁（更频繁更亮）——
        float flashCycle = frac(uTime * 0.5);
        float flash = smoothstep(0.0, 0.02, flashCycle) * smoothstep(0.08, 0.05, flashCycle);
        result += float3(0.8, 0.15, 0.08) * flash * effectStr * 0.6;

        //——H.红色底色叠加（确保即使暗色NPC也能明显看出红色）——
        result = max(result, float3(0.18, 0.02, 0.01) * effectStr);

        //——H.色差分离（局部RGB错位）——
        float aberStr = 0.0015 * effectStr;
        float rShift = tex2D(uImage0, sampleCoords + float2(aberStr, 0)).r;
        float bShift = tex2D(uImage0, sampleCoords - float2(aberStr, 0)).b;
        result.r = lerp(result.r, rShift * smpColor.r * 1.1, 0.3 * effectStr);
        result.b = lerp(result.b, bShift * smpColor.b * 0.9, 0.3 * effectStr);
    }
    //========================================
    //5.悬停状态：冷青全息扫描风格
    //========================================
    else
    {
        //——A.冷青色调——
        float3 cyberTint = float3(0.20, 0.80, 0.88);
        result = lerp(result, lum * cyberTint * 1.3, effectStr * 0.25);

        //——B.轻微亮度提升——
        result += float3(0.02, 0.06, 0.07) * effectStr;

        //——C.内边缘辉光：柔和青色脉冲——
        result += float3(0.10, 0.65, 0.72) * edgeFactor * effectStr * 0.35 * pulseSlow;

        //——D.数据流纹理（竖向流动的细线）——
        float dataFlow = frac(coords.y * 30.0 - uTime * 2.0);
        float dataMask = smoothstep(0.48, 0.5, dataFlow) * smoothstep(0.52, 0.5, dataFlow);
        float colMask = frac(coords.x / (texelSize.x * 12.0));
        float colLine = smoothstep(0.48, 0.5, colMask) * smoothstep(0.52, 0.5, colMask);
        result += float3(0.05, 0.3, 0.33) * dataMask * colLine * effectStr * 0.3;
    }

    //========================================
    //6.全局对比度微调
    //========================================
    float3 mid = float3(0.35, 0.35, 0.35);
    result = mid + (result - mid) * lerp(1.0, 1.15, effectStr * 0.4);

    result = saturate(result);
    return float4(result, color.a);
}

technique Technique1
{
    pass HackNPCHighlightPass
    {
        PixelShader = compile ps_3_0 HackNPCPass();
    }
}
