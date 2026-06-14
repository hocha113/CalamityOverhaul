// ============================================================================
//HackWraithHighlight.fx 骇客时间灵异目标高亮
//采样 uImage0；选中=紫红撕裂色差，悬停=冷紫魂光描边
// ============================================================================

sampler uImage0 : register(s0);

float2 texelSize;  //1/宽 1/高
float intensity;   //0~1
float isSelected;  //1选中 0悬停
float uTime;

//伪随机哈希
float hash2(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

//值噪声，适合生成血雾斑块
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = hash2(i);
    float b = hash2(i + float2(1, 0));
    float c = hash2(i + float2(0, 1));
    float d = hash2(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

//多层分形噪声，堆叠出呼吸感血雾
float fbm(float2 p)
{
    float v = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += amp * valueNoise(p);
        p *= 2.03;
        amp *= 0.5;
    }
    return v;
}

//心跳节律：双拍非对称脉冲，营造生物心脏感而非机械呼吸
float heartbeat(float t)
{
    float phase = frac(t * 0.55);
    float beat1 = exp(-pow(phase - 0.10, 2.0) * 260.0);
    float beat2 = exp(-pow(phase - 0.28, 2.0) * 180.0);
    return saturate(beat1 + beat2 * 0.75);
}

float4 HackWraithPass(float2 coords : TEXCOORD0, float4 smpColor : COLOR0) : COLOR0
{
    float str = intensity;

    //========================================
    //0 采样扰动：按FBM做非线性撕裂偏移
    //========================================
    float2 nCoord = coords / max(texelSize, float2(0.0001, 0.0001)) * float2(0.025, 0.04);
    float warpSeed = fbm(nCoord + uTime * 0.35);
    //选中时撕裂更剧烈，悬停仅有微弱漂移
    float warpAmp = (isSelected > 0.5 ? 0.022 : 0.006) * str;
    //周期性撕裂闪断，选中时出现短窗口的大偏移
    float tearWindow = 0.0;
    if (isSelected > 0.5)
    {
        float phase = frac(uTime * 0.8);
        tearWindow = smoothstep(0.0, 0.04, phase) * smoothstep(0.18, 0.10, phase);
        float rowRand = hash2(float2(floor(coords.y / (texelSize.y * 4.0)), floor(uTime * 9.0)));
        warpAmp += (rowRand - 0.5) * 0.04 * tearWindow;
    }
    float2 sampleCoords = coords + float2((warpSeed - 0.5) * warpAmp, (warpSeed - 0.5) * warpAmp * 0.3);

    //========================================
    //1 色差分离：紫红通道镜像错位
    //========================================
    float aber = (isSelected > 0.5 ? 0.006 : 0.002) * str;
    float aR = tex2D(uImage0, sampleCoords + float2(aber, 0)).a;
    float aG = tex2D(uImage0, sampleCoords).a;
    float aB = tex2D(uImage0, sampleCoords - float2(aber, 0)).a;
    float4 texColor = tex2D(uImage0, sampleCoords);
    float3 chroma = float3(
        tex2D(uImage0, sampleCoords + float2(aber, 0)).r,
        tex2D(uImage0, sampleCoords).g,
        tex2D(uImage0, sampleCoords - float2(aber, 0)).b);
    texColor.rgb = lerp(texColor.rgb, chroma, 0.85 * str);

    //========================================
    //2 邻域扫描：灵异魂光描边+外部血雾
    //========================================
    float stp1 = 1.6;
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

    float stp2 = 4.5;
    float a2_r = tex2D(uImage0, sampleCoords + float2( texelSize.x * stp2, 0)).a;
    float a2_l = tex2D(uImage0, sampleCoords + float2(-texelSize.x * stp2, 0)).a;
    float a2_u = tex2D(uImage0, sampleCoords + float2(0,  texelSize.y * stp2)).a;
    float a2_d = tex2D(uImage0, sampleCoords + float2(0, -texelSize.y * stp2)).a;
    float outerMax = max(max(a2_r, a2_l), max(a2_u, a2_d));

    //心跳脉冲，替代NPC着色器的三角波pulse
    float heart = heartbeat(uTime);
    //FBM血雾底噪，慢速漂移
    float mist = fbm(nCoord * 0.8 + uTime * 0.15);

    //灵异双色：紫偏冷魂光，红偏灼痕伤口
    float3 violet = float3(0.70, 0.20, 0.95);
    float3 crimson = float3(1.00, 0.15, 0.35);

    //========================================
    //3 外部区域：紫红描边+扩散血雾
    //========================================
    if (texColor.a < 0.1)
    {
        if (innerMax > 0.25)
        {
            //紫红交替描边，沿Y轴流动
            float band = frac(coords.y * 8.0 - uTime * 1.6);
            float3 edgeCol = lerp(violet, crimson, band);
            //选中时描边更亮且有心跳呼吸
            float edgeStr = innerMax * str * (isSelected > 0.5 ? (0.85 + heart * 0.6) : 0.55);
            //撕裂窗口附加破碎闪烁
            if (isSelected > 0.5)
            {
                float flicker = hash2(float2(floor(uTime * 22.0), floor(coords.y * 120.0)));
                edgeStr *= lerp(1.0, (flicker > 0.2 ? 1.0 : 0.2), tearWindow);
            }
            return float4(edgeCol * edgeStr, edgeStr);
        }
        if (outerMax > 0.15)
        {
            //外部血雾：FBM调制的柔光扩散
            float mistMod = 0.4 + mist * 0.8;
            float3 mistCol = lerp(violet * 0.6, crimson * 0.5, mist);
            float mistStr = outerMax * str * 0.28 * mistMod * (isSelected > 0.5 ? (0.9 + heart * 0.5) : 0.6);
            return float4(mistCol * mistStr, mistStr);
        }
        return float4(0, 0, 0, 0);
    }

    if (texColor.a < 0.01)
        return float4(0, 0, 0, 0);

    //========================================
    //4 本体内部：灵异重染
    //========================================
    float4 color = texColor * smpColor;
    float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));
    float edgeFactor = saturate(1.0 - innerAvg);
    edgeFactor = smoothstep(0.0, 0.5, edgeFactor);

    float3 result = color.rgb;

    if (isSelected > 0.5)
    {
        //：A 去饱和后紫红映射，暗部深紫亮部灼红：
        float3 desat = float3(lum, lum, lum);
        result = lerp(result, desat, str * 0.55);
        float3 darkTint = float3(0.28, 0.04, 0.35);
        float3 midTint = float3(0.70, 0.12, 0.55);
        float3 brightTint = float3(1.00, 0.35, 0.55);
        float3 tint;
        if (lum < 0.4)
            tint = lerp(darkTint, midTint, lum / 0.4);
        else
            tint = lerp(midTint, brightTint, saturate((lum - 0.4) / 0.6));
        result = lerp(result, tint, str * 0.70);

        //：B 横向撕裂伤口带：不均匀离散的暗红色条：
        float tearSeed = hash2(float2(floor(coords.y * 50.0), 0.0));
        float tearMask = smoothstep(0.82, 0.92, tearSeed);
        float tearFlow = frac(coords.x - uTime * 0.2 + tearSeed * 2.0);
        float tearBand = smoothstep(0.35, 0.5, tearFlow) * smoothstep(0.65, 0.5, tearFlow);
        result = lerp(result, crimson * 0.8, tearMask * tearBand * str * 0.7);

        //：C 心跳发光：整体亮度随心跳双拍脉动：
        result += violet * heart * str * 0.25;
        result += crimson * heart * heart * str * 0.15;

        //：D 内边缘魂光：
        float3 haloCol = lerp(violet, crimson, 0.5 + 0.5 * sin(uTime * 3.0));
        result += haloCol * edgeFactor * str * 0.75;

        //：E 垂直渗血细流：FBM调制的下落数据流：
        float dropY = frac(coords.y * 24.0 + uTime * 1.4);
        float dropCol = frac(coords.x / (texelSize.x * 14.0) + hash2(float2(floor(coords.x / (texelSize.x * 14.0)), 0.0)));
        float dropLine = smoothstep(0.47, 0.5, dropCol) * smoothstep(0.53, 0.5, dropCol);
        float dropPulse = smoothstep(0.1, 0.3, dropY) * smoothstep(0.9, 0.7, dropY);
        result += crimson * dropLine * dropPulse * str * 0.45;

        //：F 整体灵异底噪：
        float bodyMist = fbm(nCoord * 1.5 + uTime * 0.4);
        result = lerp(result, result * (0.65 + bodyMist * 0.55), str * 0.35);

        //：G 撕裂窗口短暂反色闪光：
        result += float3(1.0, 0.5, 0.8) * tearWindow * str * 0.6;

        //：H 暗色保底：
        result = max(result, float3(0.12, 0.02, 0.10) * str);
    }
    else
    {
        //悬停：冷紫幽灵描绘
        float3 ghostTint = float3(0.55, 0.30, 0.90);
        result = lerp(result, lum * ghostTint * 1.25, str * 0.28);
        result += float3(0.05, 0.02, 0.10) * str;

        //柔和魂光描边，呼吸更缓
        float slowPulse = 0.55 + 0.45 * sin(uTime * 2.2);
        result += violet * edgeFactor * str * 0.35 * slowPulse;

        //纵向细数据流，紫色窄线
        float colMask = frac(coords.x / (texelSize.x * 16.0));
        float colLine = smoothstep(0.48, 0.5, colMask) * smoothstep(0.52, 0.5, colMask);
        float streamY = frac(coords.y * 18.0 - uTime * 1.1);
        float streamMask = smoothstep(0.45, 0.55, streamY) * smoothstep(0.65, 0.55, streamY);
        result += violet * colLine * streamMask * str * 0.25;

        //FBM幽灵漂移
        result = lerp(result, result * (0.8 + mist * 0.3), str * 0.2);
    }

    //========================================
    //5 对比度微调
    //========================================
    float3 mid = float3(0.3, 0.25, 0.35);
    result = mid + (result - mid) * lerp(1.0, 1.18, str * 0.35);

    result = saturate(result);
    return float4(result, color.a);
}

technique Technique1
{
    pass HackWraithHighlightPass
    {
        PixelShader = compile ps_3_0 HackWraithPass();
    }
}
