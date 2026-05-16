// ============================================================================
// CyberTraceBeam.fx — 赛博追踪能量光束着色器
// 蓝/黄/青三色主题 · 流动能量纹理 · 科幻光球头部
// Trail条带渲染，配合 CyberTraceBeamProj 使用
// 支持领域超驱模式：高温红炽故障风格 + 黑墙撕裂
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;        // 整体透明度 0~1.4
float3 coreColor;       // 主题内层亮色（由C#端按主题传入）
float3 glowColor;       // 主题中层辉光色
float3 auraColor;       // 主题外层光晕色

// ---- 超驱参数 ----
float overdriveAmount;  // 0=正常 1=完全超驱（领域内）
float glitchBurst;      // 0-1 间歇性故障爆发强度
float3 odCoreColor;     // 超驱核心色（白热）
float3 odGlowColor;     // 超驱辉光色（红炽）
float3 odAuraColor;     // 超驱光晕色（深红）

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

// 简单哈希函数
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;          // 0=起点（尾部） 1=末端（头部）
    float cross_ = uv.y;         // 0=上边 1=下边
    float crossDist = abs(cross_ - 0.5) * 2.0; // 0=中心 1=边缘

    // along: 0=头部(当前位置), 1=尾端(最远历史位置)
    // 沿拖尾方向的自然衰减
    float distFromHead = along;

    // ---- 超驱色彩混合 ----
    float od = overdriveAmount;
    float3 effCore = lerp(coreColor, odCoreColor, od);
    float3 effGlow = lerp(glowColor, odGlowColor, od);
    float3 effAura = lerp(auraColor, odAuraColor, od);

    // ---- 超驱UV扰动：行位移故障 ----
    float2 renderUV = uv;
    if (od > 0.01)
    {
        float rowID = floor(cross_ * 30.0);
        float rowHash = hash21(float2(rowID, floor(uTime * 8.0 + glitchBurst * 20.0)));
        // 间歇性行位移：burst时大量行偏移，平时少量
        float rowThreshold = 0.85 - glitchBurst * 0.45;
        float rowShift = step(rowThreshold, rowHash) * (rowHash - 0.5) * 0.2 * od;
        rowShift *= (1.0 + glitchBurst * 5.0);
        renderUV.x += rowShift;
    }

    float rAlong = renderUV.x;
    float rCross = renderUV.y;
    float rCrossDist = abs(rCross - 0.5) * 2.0;

    // ---- 噪声采样 ----
    float n1 = tex2D(noiseSamp, frac(float2(rAlong * 4.0 + uTime * 1.5, rCross * 0.8))).r;
    float n2 = tex2D(noiseSamp, frac(float2(rAlong * 8.0 - uTime * 2.2, rCross * 1.5 + 0.4))).g;
    float n3 = tex2D(noiseSamp, frac(float2(rAlong * 2.5 + uTime * 0.8, rCross * 3.0 + 0.7))).b;

    // ============================================================
    // A. 白热能量核心 —— 光束中心最亮的通道（已优化：缩窄过曝区域）
    // ============================================================
    float coreWidth = 0.06 + n1 * 0.03;
    // 超驱时核心更宽更炽热
    coreWidth += od * 0.12;
    coreWidth *= lerp(1.0, 0.3, distFromHead);
    float core = 1.0 - smoothstep(0.0, coreWidth, rCrossDist);
    //幂次提高一点，让核心边缘更柔和，避免硬白色块
    core = pow(saturate(core), 1.35);
    float corePulse = 0.85 + 0.15 * sin(uTime * 18.0 + rAlong * 40.0);
    // 超驱时脉冲暴走
    corePulse += od * 0.5 * sin(uTime * 50.0 + rAlong * 80.0);
    core *= corePulse;

    // ============================================================
    // B. 内层辉光 —— 主题色明亮层
    // ============================================================
    float innerW = 0.25 + n2 * 0.08;
    innerW += od * 0.15;
    innerW *= lerp(1.0, 0.5, distFromHead);
    float inner = 1.0 - smoothstep(coreWidth * 0.4, innerW, rCrossDist);
    inner *= 0.7 + od * 0.4;

    // ============================================================
    // C. 外层光晕 —— 柔和的主题色扩散
    // ============================================================
    float outerFade = 1.0 - smoothstep(0.15, 0.95, rCrossDist);
    outerFade *= lerp(0.4, 0.1, distFromHead);
    outerFade *= (1.0 + od * 1.5);

    // ============================================================
    // D. 纵向能量流纹 —— 沿光束方向的流动条纹
    // ============================================================
    float streamSpeed = 4.0 + od * 10.0;
    float streamUV1 = frac(rAlong * 10.0 - uTime * streamSpeed);
    float stream1 = smoothstep(0.0, 0.06, streamUV1) * smoothstep(0.22, 0.10, streamUV1);
    float streamUV2 = frac(rAlong * 16.0 - uTime * (streamSpeed + 2.5) + 0.33);
    float stream2 = smoothstep(0.0, 0.04, streamUV2) * smoothstep(0.15, 0.07, streamUV2);
    float streams = (stream1 + stream2 * 0.6) * (1.0 - rCrossDist * 0.7) * 0.35;
    streams *= lerp(1.0, 0.2, distFromHead);
    streams *= (1.0 + od * 1.5);

    // ============================================================
    // E. 微弱数字网格 —— 赛博科幻质感
    // ============================================================
    float gridX = frac(rAlong * 30.0);
    float gridY = frac(rCross * 6.0);
    float gridLine = step(gridX, 0.04) + step(gridY, 0.06);
    gridLine = saturate(gridLine);
    float cellID = floor(rAlong * 30.0) + floor(rCross * 6.0) * 37.0;
    float cellFlicker = hash21(float2(cellID, floor(uTime * 5.0)));
    // 超驱时网格更多闪烁
    float gridThreshold = 0.75 - od * 0.3;
    gridLine *= step(gridThreshold, cellFlicker) * (0.15 + od * 0.4);
    gridLine *= (1.0 - rCrossDist * 0.6);
    gridLine *= lerp(0.8, 0.1, distFromHead);

    // ============================================================
    // F. 头部光球 —— 圆形高亮辐射
    // ============================================================
    float headOrb = 1.0 - smoothstep(0.0, 0.05, rAlong);
    headOrb *= (1.0 - rCrossDist * 0.6);
    float orbPulse = 0.8 + 0.2 * sin(uTime * 12.0);
    headOrb *= orbPulse;
    float headGlow = 1.0 - smoothstep(0.02, 0.12, rAlong);
    headGlow *= (1.0 - rCrossDist * 0.8) * 0.4;

    // ============================================================
    // G. 边缘腐蚀 —— 噪声驱动的有机能量边界
    // ============================================================
    float edgeNoise = n2 * 0.20 + n3 * 0.15;
    float edgeMask = 1.0 - smoothstep(0.45 - edgeNoise, 0.92, rCrossDist);

    // ============================================================
    // H. 尾端渐隐 —— 拖尾末端平滑消失
    // ============================================================
    float tailFade = 1.0 - smoothstep(0.7, 1.0, rAlong);

    // ============================================================
    // 颜色合成 —— 优化亮度：减少纯白叠加，让主题色辨识度更高
    // 在 Additive Blend 下，过多白色层叠加会让 RGB 三通道同时饱和
    // 导致中心区域过曝成纯白色，丢失主题色身份。
    // 这里将"白热"层替换为带主题色调的混色，更养眼且保留科幻光束质感。
    // ============================================================
    float3 cWhite = float3(1.0, 0.97, 0.92);
    //核心高光：以主题色为主体、混入少量白热，避免纯白爆炸
    float3 cCoreHot = lerp(effCore, cWhite, 0.45);
    //头部光球：偏向主题色，让光球带有清晰的颜色身份
    float3 cHeadHot = lerp(effCore, cWhite, 0.35);

    float3 color = float3(0, 0, 0);
    color += cCoreHot   * core * 0.55;          //核心白热幅度降低（1.0→0.55），改用混色
    color += effCore    * inner;                 //主题色辉光保持原强度，维持色彩饱和度
    color += effGlow    * outerFade;
    color += effGlow    * streams;
    color += effCore    * gridLine;
    color += cHeadHot   * headOrb * 0.45;       //头部白光降低（0.8→0.45）并改用混色
    color += effCore    * headGlow * 0.7;       //头部辉光适度降低，避免与光球叠加过曝
    color += effAura    * headGlow * 0.3;

    float alpha = saturate(
        edgeMask
        + core * 0.4               //核心 alpha 叠加降低（0.6→0.4），减少 Additive 放大
        + headOrb * 0.3            //头部 alpha 叠加降低（0.5→0.3）
        + streams * 0.2
        + gridLine * 0.1
    );
    alpha *= fadeAlpha * tailFade;

    // ============================================================
    // I. 超驱故障叠加 —— 极端黑墙撕裂效果
    // ============================================================
    if (od > 0.01)
    {
        float burst = glitchBurst;

        // I-1. 扫描线干扰（密集的高亮线）
        float scanFreq = 80.0 + burst * 120.0;
        float scanline = frac(rCross * scanFreq + uTime * 3.5);
        scanline = step(0.93, scanline);
        color += effCore * scanline * od * 1.0;
        alpha += scanline * od * 0.3;

        // I-2. 方块腐蚀（大面积高亮方块闪烁）
        float blockW = 0.05 + burst * 0.1;
        float blockID2 = floor(rAlong / blockW) + floor(rCross / (blockW * 2.0)) * 23.0;
        float blockFlash = hash21(float2(blockID2, floor(uTime * 12.0 + burst * 30.0)));
        float blockThresh = 0.80 - burst * 0.4;
        float blockOn = step(blockThresh, blockFlash);
        float3 blockColor = lerp(effGlow, float3(1.0, 0.97, 0.88), burst * 0.8);
        color += blockColor * blockOn * od * (0.5 + burst * 1.2);
        alpha += blockOn * od * 0.2;

        // I-3. 边缘红炽辉光增强
        float magentaEdge = smoothstep(0.25, 0.50, rCrossDist) * (1.0 - smoothstep(0.50, 0.85, rCrossDist));
        color += effGlow * magentaEdge * od * (0.8 + burst * 1.2);
        alpha += magentaEdge * od * 0.2;

        // I-4. 色彩通道偏移（剧烈RGB分离）
        float channelShift = od * (0.06 + burst * 0.15);
        float3 shiftedColor;
        shiftedColor.r = color.r * (1.0 + channelShift * sin(uTime * 30.0 + rAlong * 60.0));
        shiftedColor.g = color.g * (1.0 - channelShift * 0.3 * sin(uTime * 25.0));
        shiftedColor.b = color.b * (1.0 + channelShift * cos(uTime * 35.0 + rAlong * 70.0));
        color = shiftedColor;

        // I-5. 全局闪烁（burst期间整体亮度暴走）
        float globalFlicker = hash21(float2(floor(uTime * 30.0), 13.7));
        float flickerAmount = burst * (globalFlicker - 0.3) * 3.5;
        color *= 1.0 + flickerAmount;

        // I-6. 水平撕裂带（随机黑带瞬闪）
        float tearID = floor(rCross * 20.0);
        float tearHash = hash21(float2(tearID, floor(uTime * 15.0)));
        float tearOn = step(0.92 - burst * 0.15, tearHash) * od;
        color *= 1.0 - tearOn * 0.7;

        // I-7. 超驱时整体alpha大幅增强
        alpha *= 1.0 + od * 0.5;
    }

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass CyberTraceBeamPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
