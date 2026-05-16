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
    //超驱时核心仅小幅变宽（0.12→0.04），避免热核心区域无限膨胀
    coreWidth += od * 0.04;
    coreWidth *= lerp(1.0, 0.3, distFromHead);
    float core = 1.0 - smoothstep(0.0, coreWidth, rCrossDist);
    //幂次提高一点，让核心边缘更柔和，避免硬白色块
    core = pow(saturate(core), 1.35);
    float corePulse = 0.85 + 0.15 * sin(uTime * 18.0 + rAlong * 40.0);
    //超驱时脉冲幅度降低（0.5→0.2），避免亮度暴走
    corePulse += od * 0.2 * sin(uTime * 50.0 + rAlong * 80.0);
    core *= corePulse;

    // ============================================================
    // B. 内层辉光 —— 主题色明亮层（超驱加成降低）
    // ============================================================
    float innerW = 0.25 + n2 * 0.08;
    innerW += od * 0.05;                       //0.15→0.05，内层不再大幅膨胀
    innerW *= lerp(1.0, 0.5, distFromHead);
    float inner = 1.0 - smoothstep(coreWidth * 0.4, innerW, rCrossDist);
    inner *= 0.7 + od * 0.15;                  //0.4→0.15，超驱亮度温和提升

    // ============================================================
    // C. 外层光晕 —— 柔和的主题色扩散（超驱加成降低）
    // ============================================================
    float outerFade = 1.0 - smoothstep(0.15, 0.95, rCrossDist);
    outerFade *= lerp(0.4, 0.1, distFromHead);
    outerFade *= (1.0 + od * 0.4);             //1.5→0.4，外层不再翻倍叠加

    // ============================================================
    // D. 纵向能量流纹 —— 沿光束方向的流动条纹（超驱加成降低）
    // ============================================================
    float streamSpeed = 4.0 + od * 10.0;
    float streamUV1 = frac(rAlong * 10.0 - uTime * streamSpeed);
    float stream1 = smoothstep(0.0, 0.06, streamUV1) * smoothstep(0.22, 0.10, streamUV1);
    float streamUV2 = frac(rAlong * 16.0 - uTime * (streamSpeed + 2.5) + 0.33);
    float stream2 = smoothstep(0.0, 0.04, streamUV2) * smoothstep(0.15, 0.07, streamUV2);
    float streams = (stream1 + stream2 * 0.6) * (1.0 - rCrossDist * 0.7) * 0.35;
    streams *= lerp(1.0, 0.2, distFromHead);
    streams *= (1.0 + od * 0.5);               //1.5→0.5，流纹强度温和增长

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
    // 超驱下额外减少白热混入比例，让"灼烧橙"代替"白热"传达高温感。
    // ============================================================
    float3 cWhite = float3(1.0, 0.97, 0.92);
    //核心高光：超驱时白光混入大幅减少（0.45→0.15），让熔岩橙 effCore 主导
    float coreWhiteBlend = 0.45 - od * 0.30;
    float headWhiteBlend = 0.35 - od * 0.25;
    float3 cCoreHot = lerp(effCore, cWhite, coreWhiteBlend);
    float3 cHeadHot = lerp(effCore, cWhite, headWhiteBlend);

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
    // I. 超驱故障层 —— "黑墙撕裂"美学
    // 设计哲学：故障感来自"破坏"而非"叠加亮度"。
    // 亮度叠加 → 故障变成"更亮"，本质还是手电筒。
    // 这里改为：扫描线/方块用红炽辉光（不是白热），强化深色撕裂、
    // 数据丢失黑块、RGB 通道分离，形成"灼烧的电子屏崩溃"质感。
    // ============================================================
    if (od > 0.01)
    {
        float burst = glitchBurst;

        // I-1. 扫描线干扰（红炽细线，不再是白热）
        //  - 颜色改用 effGlow（深红）而非 effCore（橙），与黑色撕裂形成对比
        //  - 强度从 1.0 降到 0.5，避免和核心高光叠加
        float scanFreq = 80.0 + burst * 120.0;
        float scanline = frac(rCross * scanFreq + uTime * 3.5);
        scanline = step(0.93, scanline);
        color += effGlow * scanline * od * 0.5;
        alpha += scanline * od * 0.18;

        // I-2. 方块腐蚀（红炽辉光方块）
        //  - 颜色去掉纯白渐变，改为 effGlow → effCore 渐变（红→橙）
        //  - 整体强度大幅降低
        float blockW = 0.05 + burst * 0.1;
        float blockID2 = floor(rAlong / blockW) + floor(rCross / (blockW * 2.0)) * 23.0;
        float blockFlash = hash21(float2(blockID2, floor(uTime * 12.0 + burst * 30.0)));
        float blockThresh = 0.80 - burst * 0.4;
        float blockOn = step(blockThresh, blockFlash);
        float3 blockColor = lerp(effGlow, effCore, burst * 0.5);
        color += blockColor * blockOn * od * (0.25 + burst * 0.45);
        alpha += blockOn * od * 0.12;

        // I-3. 边缘红炽辉光（保留但温和化）
        float magentaEdge = smoothstep(0.25, 0.50, rCrossDist) * (1.0 - smoothstep(0.50, 0.85, rCrossDist));
        color += effGlow * magentaEdge * od * (0.4 + burst * 0.5);
        alpha += magentaEdge * od * 0.15;

        // I-4. RGB 通道偏移（保留作为故障感关键 —— 这是"色相"扰动而非"亮度"扰动）
        float channelShift = od * (0.06 + burst * 0.15);
        float3 shiftedColor;
        shiftedColor.r = color.r * (1.0 + channelShift * sin(uTime * 30.0 + rAlong * 60.0));
        shiftedColor.g = color.g * (1.0 - channelShift * 0.3 * sin(uTime * 25.0));
        shiftedColor.b = color.b * (1.0 + channelShift * cos(uTime * 35.0 + rAlong * 70.0));
        color = shiftedColor;

        // I-5. 全局闪烁（幅度大幅降低：3.5→1.2，避免整屏闪瞎）
        float globalFlicker = hash21(float2(floor(uTime * 30.0), 13.7));
        float flickerAmount = burst * (globalFlicker - 0.3) * 1.2;
        color *= 1.0 + flickerAmount;

        // I-6. 水平撕裂带（强化为"黑墙"：更频繁、更深、连 alpha 一起挖空）
        //  - 阈值从 0.92→0.82（更频繁出现）
        //  - 暗化系数 0.7→0.92（接近全黑），让撕裂带成为真正的"数据缺口"
        //  - alpha 也下降 0.55，制造"光束被撕开一道缝"的视觉
        float tearID = floor(rCross * 20.0);
        float tearHash = hash21(float2(tearID, floor(uTime * 15.0)));
        float tearOn = step(0.82 - burst * 0.20, tearHash) * od;
        color *= 1.0 - tearOn * 0.92;
        alpha *= 1.0 - tearOn * 0.55;

        // I-7. 数据丢失黑块（NEW —— 真正的"黑墙"美学）
        //  - 像马赛克方块一样掏空光束局部，模拟"显存损坏"
        //  - 颜色全黑、alpha 大幅下降，与亮度叠加形成黑白对比
        float corBlockW = 0.05 + burst * 0.04;
        float corID = floor(rAlong / corBlockW) * 47.0 + floor(rCross / (corBlockW * 1.5)) * 13.0;
        float corHash = hash21(float2(corID, floor(uTime * 10.0 + 7.3)));
        float corOn = step(0.90 - burst * 0.18, corHash) * od;
        color *= 1.0 - corOn * 0.88;
        alpha *= 1.0 - corOn * 0.45;

        // I-8. 超驱整体 alpha 微量增强（0.5→0.1，原值过激）
        alpha *= 1.0 + od * 0.1;
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
