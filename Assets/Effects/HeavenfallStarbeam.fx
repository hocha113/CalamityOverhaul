// =====================================================================
// HeavenfallStarbeam.fx 天堂星河光柱着色器
// 服务于 HeavenRainbowImpact (从天而降的彩虹光柱)
// 单 Technique StarBeam, 拖尾条带渲染
// UV.x 沿光柱方向 (0=源头, 1=末端), UV.y 横截
// 风格: 等离子核心 + 彩虹日冕 + 分形闪电分支 + 行进能量包 + 头部冲击花
// =====================================================================

float4x4 transformMatrix;

float uTime;          // 时间
float progress;       // 寿命进度 0~1 (0=刚生成, 1=即将消亡)
float fadeAlpha;      // 整体透明度 0~1
float beamWidth;      // 光柱宽度参考 (0~1, 在 UV 空间里)
float hueOffset;      // 色相偏移
float impactBurst;    // 头部冲击花强度 0~1 (生命前期 1, 后期衰减)

texture uNoiseTex;
sampler2D noiseSamp = sampler_state
{
    texture   = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

struct VSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VS(VSInput v)
{
    PSInput o;
    o.Position  = mul(v.Position, transformMatrix);
    o.Color     = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float3 hueToRGB(float h)
{
    h = frac(h);
    return saturate(float3(
        abs(h * 6.0 - 3.0) - 1.0,
        2.0 - abs(h * 6.0 - 2.0),
        2.0 - abs(h * 6.0 - 4.0)
    ));
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PS_StarBeam(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;            // 0=源头 (高空), 1=末端 (近地命中点)
    float cross_ = uv.y;
    float crossDist = abs(cross_ - 0.5) * 2.0; // 0=中心 1=边缘

    // ---- 噪声采样 ----
    float n1 = tex2D(noiseSamp, frac(float2(along * 4.5 + uTime * 2.0, cross_ * 1.6))).r;
    float n2 = tex2D(noiseSamp, frac(float2(along * 9.0 - uTime * 3.0, cross_ * 2.5 + 0.3))).g;
    float n3 = tex2D(noiseSamp, frac(float2(along * 2.0 + uTime * 0.5, cross_ * 5.0 + 0.6))).b;
    float n4 = tex2D(noiseSamp, frac(float2(along * 15.0 + uTime * 6.0, cross_ * 8.0))).r;

    // 生命周期相关 (前期狂暴, 后期收缩)
    float lifePulse = 1.0 - progress * 0.55;     // 整体亮度淡出
    float widthMul  = lerp(1.0, 0.55, progress); // 整体收窄

    // ============================================================
    // A. 等离子核心柱 (极窄白热)
    // ============================================================
    float coreW = beamWidth * 0.18 * widthMul + n1 * 0.015;
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.15);
    float corePulse = 0.85 + 0.15 * sin(uTime * 22.0 + along * 60.0);
    core *= corePulse * lifePulse;

    // ============================================================
    // B. 彩虹日冕 (横轴 sech-like 衰减)
    // ============================================================
    float coronaW = beamWidth * 0.55 * widthMul;
    float corona = exp(-pow(crossDist / coronaW, 2.0) * 1.8);
    corona *= (0.7 + n2 * 0.3);

    // 彩虹色相沿光柱流动
    float hue = along * 1.3 - uTime * 0.45 + hueOffset;
    hue += (cross_ - 0.5) * 0.2;
    float3 rainbow = hueToRGB(hue);
    // 稍微提亮
    rainbow = saturate(rainbow * 1.15);

    // ============================================================
    // C. 分形闪电分支 (沿光柱两侧的细闪电)
    // ============================================================
    // 分支偏移由噪声驱动, 制造曲折感
    float branchOffset = (n3 - 0.5) * 0.35 + (n4 - 0.5) * 0.2;
    float branchPos = 0.5 + branchOffset;
    float branchDist = abs(cross_ - branchPos);
    float branchMask = 1.0 - smoothstep(0.0, 0.012, branchDist);
    branchMask *= step(0.6, n4);                    // 间断出现
    branchMask *= step(0.3, along) * (1.0 - along * 0.6); // 沿光柱出现频率

    // ============================================================
    // D. 行进能量包 (沿光柱方向脉冲移动)
    // ============================================================
    float packTime = uTime * 1.2;
    float pack1 = exp(-pow(frac(along - packTime) - 0.5, 2.0) * 60.0);
    float pack2 = exp(-pow(frac(along * 1.3 - packTime + 0.33) - 0.5, 2.0) * 90.0);
    float pack3 = exp(-pow(frac(along * 0.7 - packTime + 0.66) - 0.5, 2.0) * 80.0);
    float pack = (pack1 + pack2 * 0.8 + pack3 * 0.7) * (1.0 - crossDist * 0.7);
    pack *= lifePulse;

    // ============================================================
    // E. 头部冲击花 (光柱末端的爆发, 前期最强)
    // ============================================================
    float headDist = along;                         // 0=源头, 1=末端 (近地命中)
    float headPos = 1.0 - 0.02;
    float impactRing = 0.0;
    {
        // 头部沿横截的爆开光圈
        float h = abs(headDist - headPos);
        float ringFalloff = exp(-h * 18.0);         // 在末端汇聚
        float petals = sin(cross_ * 24.0 + uTime * 4.0) * 0.5 + 0.5;
        petals = pow(petals, 3.0);
        impactRing = ringFalloff * (0.55 + petals * 0.6);
        impactRing *= impactBurst;
    }

    // ============================================================
    // F. 边缘溶解 (光柱外缘的不规则化)
    // ============================================================
    float edgeNoise = n2 * 0.20 + n3 * 0.15;
    float edgeMask = 1.0 - smoothstep(0.55 - edgeNoise, 0.95, crossDist);

    // ============================================================
    // G. 沿光柱顶端的"星点"散射 (像极光雪花)
    // ============================================================
    float starID = floor(along * 50.0) + floor(cross_ * 10.0) * 17.0;
    float starHash = hash21(float2(starID, floor(uTime * 5.0)));
    float starPoint = step(0.93, starHash) * (1.0 - crossDist * 0.6);
    starPoint *= lifePulse * 0.6;

    // ============================================================
    // 颜色合成
    // ============================================================
    float3 cWhite = float3(1.0, 0.98, 0.95);
    float3 color = float3(0, 0, 0);
    color += cWhite  * core * 1.5;                  // 白热核心
    color += rainbow * corona * 1.1;                // 彩虹日冕
    color += rainbow * pack * 1.3;                  // 行进能量包
    color += cWhite  * pack * 0.7;
    color += cWhite  * branchMask * 1.4;            // 闪电支线
    color += rainbow * branchMask * 0.8;
    color += cWhite  * impactRing * 1.6;            // 冲击花核心
    color += rainbow * impactRing * 1.2;            // 冲击花彩虹边
    color += cWhite  * starPoint * 1.2;             // 星点

    float alpha = saturate(
          core         * 0.7
        + corona       * 0.55
        + pack         * 0.4
        + branchMask   * 0.8
        + impactRing   * 0.7
        + starPoint    * 0.4
        + edgeMask     * 0.25
    );
    alpha *= fadeAlpha * lifePulse;

    return float4(color * alpha, alpha) * input.Color;
}

technique StarBeam
{
    pass P0
    {
        VertexShader = compile vs_2_0 VS();
        PixelShader  = compile ps_3_0 PS_StarBeam();
    }
}
