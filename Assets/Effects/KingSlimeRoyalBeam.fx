// ============================================================================
// KingSlimeRoyalBeam.fx 残酷史莱姆王皇冠光柱
// Trail 条带 Additive 叠加
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;       // 全局透明度 0~1.4
float phase;           // 0=警示 1=命中 2=淡出（连续值用于过渡）
float warnProg;        // 警示阶段 0~1
float strikeProg;      // 命中阶段 0~1
float fadeProg;        // 淡出阶段 0~1
float seed;            // 实例化扰动种子
float3 coreColor;      // 白热核心
float3 goldColor;      // 皇室金辉
float3 redColor;       // 深皇红外晕

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

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;          // 0=皇冠端 1=落点端
    float cross_ = uv.y;
    float crossDist = abs(cross_ - 0.5) * 2.0; // 0=中线 1=边缘

    bool isWarn = phase < 0.5;
    bool isStrike = phase >= 0.5 && phase < 1.5;
    bool isFade = phase >= 1.5;

    // 噪声采样：果冻流动
    float n1 = tex2D(noiseSamp, frac(float2(along * 3.0 + uTime * 0.6, cross_ * 0.8 + seed * 0.13))).r;
    float n2 = tex2D(noiseSamp, frac(float2(along * 6.0 - uTime * 1.1, cross_ * 1.5 + 0.3))).g;
    float n3 = tex2D(noiseSamp, frac(float2(along * 1.8 + uTime * 0.3, cross_ * 2.5 + 0.7))).b;

    // =
    // 阶段宽度调制：警示窄、命中粗、淡出渐缩
    // =
    float phaseWidth = 1.0;
    if (isWarn)
    {
        // 警示阶段：随蓄力进度由极窄变略宽
        phaseWidth = 0.18 + 0.10 * warnProg;
    }
    else if (isStrike)
    {
        // 命中阶段：粗光柱 + 高频脉冲
        phaseWidth = 0.95 + 0.10 * sin(uTime * 30.0 + along * 12.0);
        // 撞击瞬间略微暴胀
        phaseWidth *= 1.0 + 0.10 * (1.0 - smoothstep(0.0, 0.25, strikeProg));
    }
    else
    {
        // 淡出阶段：渐缩
        phaseWidth = lerp(0.9, 0.35, fadeProg);
    }

    // =
    // A. 白热核心
    // =
    float coreWidth = (0.10 + n1 * 0.04) * phaseWidth;
    float core = 1.0 - smoothstep(0.0, coreWidth, crossDist);
    core = pow(saturate(core), 1.25);
    float corePulse = 0.85 + 0.15 * sin(uTime * 22.0 + along * 30.0);
    core *= corePulse;

    // =
    // B. 内层金辉
    // =
    float innerW = (0.30 + n2 * 0.10) * phaseWidth;
    float inner = 1.0 - smoothstep(coreWidth * 0.4, innerW, crossDist);

    // =
    // C. 外层红晕（皇室红霭）
    // =
    float outerFade = 1.0 - smoothstep(0.18, 0.95, crossDist);
    outerFade *= 0.55;
    outerFade *= phaseWidth;

    // =
    // D. 沿轴向流动的"皇室金箔流光" —— 凝胶内部能量带
    // =
    float bandSpeed = 4.0 + strikeProg * 2.0;
    float bandUV1 = frac(along * 4.5 - uTime * bandSpeed + seed * 0.27);
    float band1 = smoothstep(0.0, 0.08, bandUV1) * smoothstep(0.32, 0.12, bandUV1);
    float bandUV2 = frac(along * 7.5 - uTime * (bandSpeed + 1.6) + 0.4 + seed * 0.51);
    float band2 = smoothstep(0.0, 0.05, bandUV2) * smoothstep(0.20, 0.08, bandUV2);
    float bands = (band1 + band2 * 0.7) * (1.0 - crossDist * 0.65) * 0.45;
    bands *= phaseWidth;

    // =
    // E. 警示阶段虚线/锁定标记
    // =
    float warnDash = 1.0;
    float warnTick = 0.0;
    if (isWarn)
    {
        // 滚动虚线
        float dashUV = frac(along * 22.0 - uTime * 7.0);
        warnDash = step(0.45, dashUV) * step(dashUV, 0.95);
        // 渐入
        warnDash *= smoothstep(0.0, 0.3, warnProg);

        // 沿光束反复扫过的"锁定指针"
        float ptrPos = frac(uTime * 0.9 + seed * 0.31);
        warnTick = exp(-pow((along - ptrPos) * 14.0, 2.0));
    }

    // =
    // F. 端点光斑：皇冠端起始 / 落点端聚焦
    // =
    float crownOrb = 1.0 - smoothstep(0.0, 0.07, along);
    crownOrb *= (1.0 - crossDist * 0.65);
    crownOrb *= 0.85 + 0.15 * sin(uTime * 15.0 + seed);

    float endOrb = 1.0 - smoothstep(0.92, 1.0, along);
    endOrb *= (1.0 - crossDist * 0.40);
    // 命中瞬间猛烈爆开
    if (isStrike)
    {
        endOrb *= 1.2 + 1.5 * (1.0 - smoothstep(0.0, 0.25, strikeProg));
    }

    // =
    // G. 边缘有机切割（噪声驱动）
    // =
    float edgeNoise = n2 * 0.20 + n3 * 0.14;
    float edgeMask = 1.0 - smoothstep(0.45 - edgeNoise, 0.96, crossDist);
    edgeMask *= phaseWidth;

    // =
    // H. 颜色合成
    // =
    float3 color = float3(0, 0, 0);
    float alpha = 0.0;

    if (isWarn)
    {
        // 警示阶段：金红虚线 + 微弱外晕 + 锁定指针亮斑
        float warnPulse = 0.5 + 0.5 * sin(uTime * 18.0 + seed * 3.14);
        float3 warnInner = lerp(redColor * 1.4, goldColor, warnPulse);

        color += warnInner * core * 0.85 * warnDash;
        color += redColor * inner * 0.45 * warnDash;
        color += redColor * outerFade * 0.55 * warnDash;
        color += goldColor * crownOrb * (0.6 + 0.6 * warnProg);
        color += goldColor * warnTick * 1.3 * warnProg;
        color += coreColor * warnTick * 0.6 * warnProg;

        alpha = saturate(
            edgeMask * 0.45 * warnDash
            + core * 0.55 * warnDash
            + crownOrb * 0.5 * warnProg
            + warnTick * 0.6 * warnProg
        );
        alpha *= 0.55 + 0.4 * warnProg;
    }
    else if (isStrike)
    {
        // 命中阶段：白金核心 + 金箔流光 + 红色外晕
        color += coreColor * core;
        color += goldColor * inner * 0.85;
        color += redColor * outerFade;
        color += goldColor * bands;
        color += coreColor * bands * 0.4;
        color += coreColor * crownOrb * 0.95;
        color += goldColor * crownOrb * 0.5;
        color += coreColor * endOrb * 1.15;
        color += goldColor * endOrb * 0.55;
        color += redColor * endOrb * 0.35;

        alpha = saturate(
            edgeMask
            + core * 0.65
            + crownOrb * 0.55
            + endOrb * 0.7
            + bands * 0.25
        );
        // 命中初期更亮
        alpha *= 1.05 + 0.25 * (1.0 - smoothstep(0.0, 0.4, strikeProg));
    }
    else
    {
        // 淡出阶段：金红余辉柔和散开
        float fadeAmt = 1.0 - fadeProg;
        color += goldColor * core * 0.7 * fadeAmt;
        color += redColor * inner * 0.55 * fadeAmt;
        color += redColor * outerFade * 0.7 * fadeAmt;
        color += goldColor * bands * 0.4 * fadeAmt;
        color += redColor * endOrb * 0.6 * fadeAmt;
        color += goldColor * endOrb * 0.3 * fadeAmt;

        alpha = saturate(
            edgeMask * fadeAmt
            + core * 0.45 * fadeAmt
            + endOrb * 0.4 * fadeAmt
            + bands * 0.15 * fadeAmt
        );
    }

    alpha *= fadeAlpha;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass KingSlimeRoyalBeamPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
