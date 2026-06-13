// ============================================================================
// SHPCPlasmaSun.fx 等离子残阳
// s0+s1 四边形；Additive
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float fadeAlpha;        // 整体透明度 0~1
float sunRadius;        // 恒星本体半径（占四边形半宽比例，约0.34）
float3 coreColor;       // 核心色（白金）
float3 surfaceColor;    // 表面等离子色（橙红）
float3 coronaColor;     // 日冕色（深红）

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    // =
    // A. 恒星本体 —— 湍流等离子表面
    // =
    // 双层旋转噪声制造表面对流胞
    float2 swirl1 = centered * 1.6;
    float c1 = cos(uTime * 0.35);
    float s1 = sin(uTime * 0.35);
    swirl1 = float2(swirl1.x * c1 - swirl1.y * s1, swirl1.x * s1 + swirl1.y * c1);
    float pn1 = tex2D(noiseSamp, swirl1 * 0.5 + float2(uTime * 0.06, 0.0)).r;

    float2 swirl2 = centered * 3.1;
    float c2 = cos(-uTime * 0.22);
    float s2 = sin(-uTime * 0.22);
    swirl2 = float2(swirl2.x * c2 - swirl2.y * s2, swirl2.x * s2 + swirl2.y * c2);
    float pn2 = tex2D(noiseSamp, swirl2 * 0.5 + float2(0.0, uTime * 0.09)).g;

    float plasma = pn1 * 0.6 + pn2 * 0.4;

    // 本体遮罩：噪声扰动边缘，让恒星轮廓微微涌动
    float edgeWobble = (plasma - 0.5) * 0.05;
    float bodyMask = 1.0 - smoothstep(sunRadius + edgeWobble - 0.02, sunRadius + edgeWobble + 0.02, dist);

    // 临边昏暗：中心亮、边缘按余弦衰减（真实恒星观感）
    float limb = saturate(1.0 - (dist / max(sunRadius, 0.001)));
    float limbDarken = pow(limb, 0.42);

    // 表面颜色：暗对流胞 → 亮等离子流，再混入核心白金
    float3 surface = lerp(surfaceColor * 0.45, surfaceColor, plasma);
    surface = lerp(surface, coreColor, pow(limbDarken, 2.4) * (0.55 + plasma * 0.35));

    // =
    // B. 色球闪焰 —— 表面随机爆亮的小区域
    // =
    float cell = hash21(floor(centered * 9.0) + floor(uTime * 3.0));
    float flarePatch = step(0.93, cell) * bodyMask * plasma;

    // =
    // C. 日冕 —— 本体外的不规则光晕
    // =
    float coronaNoise = tex2D(noiseSamp, float2(normAngle * 3.0 + uTime * 0.10, dist * 1.5 - uTime * 0.05)).r;
    float coronaReach = sunRadius * (1.85 + coronaNoise * 0.9);
    float corona = 1.0 - smoothstep(sunRadius * 0.9, coronaReach, dist);
    corona = pow(saturate(corona), 1.6) * (1.0 - bodyMask) * (0.5 + coronaNoise * 0.5);

    // =
    // D. 耀斑尖刺 —— 沿角向的旋转日珥光芒
    // =
    float spikes = pow(abs(sin(angle * 5.0 + uTime * 0.7)), 18.0)
                 + pow(abs(sin(angle * 3.0 - uTime * 0.45 + 1.3)), 26.0) * 0.7;
    float spikeFade = (1.0 - smoothstep(sunRadius, sunRadius * 2.6, dist)) * (1.0 - bodyMask);
    spikes *= spikeFade;

    // =
    // E. 呼吸脉动 —— 整体亮度低频起伏
    // =
    float pulse = 0.9 + 0.1 * sin(uTime * 2.1) + 0.04 * sin(uTime * 7.7);

    // =
    // 颜色合成
    // =
    float3 color = float3(0.0, 0.0, 0.0);
    color += surface * bodyMask * limbDarken * 1.25;
    color += coreColor * flarePatch * 0.8;
    color += coronaColor * corona * 0.9;
    color += surfaceColor * spikes * 0.75;

    float alpha = saturate(bodyMask * (0.55 + limbDarken * 0.45) + corona * 0.7 + spikes * 0.6);
    alpha *= fadeAlpha * pulse;

    return float4(color * alpha * pulse, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCPlasmaSunPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
