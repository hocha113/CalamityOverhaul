// ============================================================================
// CyberDetonation.fx 赛博科技环形爆破
// s0 基础 s1 噪声；SpriteBatch Immediate；支持超驱
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float ringProgress;     // 0~1 环扩张进度
float fadeAlpha;        // 整体透明度 0~1
float3 coreColor;       // 核心色（白青）
float3 ringColor;       // 环色（亮青/黄）
float3 fragColor;       // 碎片色（暗色调）
float overdriveAmount;  // 0=正常 1=完全超驱

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
    float normAngle = (angle + 3.14159) / 6.28318; // 0~1

    // =
    // A. 主冲击环 —— 核心扩展环
    // =
    // 噪声扰动环边缘
    float n1 = tex2D(noiseSamp, float2(normAngle * 5.0 + uTime * 2.5, ringProgress * 3.0)).r;
    float n2 = tex2D(noiseSamp, float2(normAngle * 9.0 - uTime * 1.8, ringProgress + 0.7)).g;
    float noiseDisp = (n1 * 0.55 + n2 * 0.45 - 0.5) * 0.06;
    float adjDist = dist + noiseDisp;

    // 主环遮罩
    float thickness = 0.07 + (1.0 - ringProgress) * 0.03;
    float ringDist = abs(adjDist - ringProgress);
    float mainRing = 1.0 - smoothstep(0.0, thickness, ringDist);

    // 内侧压缩面更亮
    float innerBias = smoothstep(ringProgress, ringProgress - thickness * 0.7, adjDist);

    // =
    // B. 次级环 —— 稍领先主环的细线前驱波
    // =
    float precursorPos = ringProgress * 1.12 + 0.02;
    float precursorDist = abs(adjDist - precursorPos);
    float precursor = 1.0 - smoothstep(0.0, 0.015, precursorDist);
    precursor *= smoothstep(0.95, 0.7, ringProgress); // 后期衰减

    // =
    // C. 六边形网格碎裂 —— 科技感核心视觉
    // =
    // 六边形网格坐标
    float hexScale = 18.0;
    float2 hexUV = centered * hexScale;
    float2 hexID = floor(hexUV);
    float2 hexFrac = frac(hexUV) - 0.5;

    // 简化六边形距离
    float hexDist = max(abs(hexFrac.x) + abs(hexFrac.y) * 0.577, abs(hexFrac.y) * 1.155);
    float hexEdge = smoothstep(0.45, 0.48, hexDist);

    // 每个网格单元的随机碎裂时序
    float cellHash = hash21(hexID + float2(7.31, 13.17));
    float cellActivate = smoothstep(cellHash * 0.8, cellHash * 0.8 + 0.15, ringProgress);
    float cellFade = 1.0 - smoothstep(cellHash * 0.5 + 0.5, 1.0, ringProgress);

    // 网格只在环内区域显示
    float gridZone = smoothstep(ringProgress + 0.05, ringProgress - 0.2, dist);
    float gridIntensity = hexEdge * cellActivate * cellFade * gridZone * 0.35;

    // 单元格内闪烁
    float cellFlicker = step(0.6, hash21(hexID + floor(uTime * 8.0)));
    float cellFill = (1.0 - hexDist * 2.0) * cellFlicker * cellActivate * cellFade * gridZone * 0.2;

    // =
    // D. 内部能量扭曲残余 —— 环通过后的涟漪
    // =
    float innerZone = smoothstep(ringProgress - thickness, 0.0, adjDist);
    float ripple = sin((dist - ringProgress * 0.5) * 60.0 + uTime * 10.0) * 0.5 + 0.5;
    ripple = pow(ripple, 6.0) * innerZone * 0.12;
    ripple *= 1.0 - ringProgress; // 环扩大后涟漪减弱

    // =
    // E. 数据流拖尾 —— 环外侧的数字碎片
    // =
    float trailing = smoothstep(ringProgress + thickness * 2.5, ringProgress, adjDist);
    trailing *= trailing;
    float trailNoise = tex2D(noiseSamp, float2(normAngle * 20.0 + uTime * 1.2, dist * 4.0)).r;
    trailing *= step(0.45, trailNoise) * 0.4;

    // =
    // F. 中心闪光 —— 爆破瞬间的核心白闪
    // =
    float centerFlash = pow(saturate(1.0 - dist / 0.15), 3.0);
    centerFlash *= pow(saturate(1.0 - ringProgress / 0.25), 2.0); // 极早期迅速消退

    // =
    // 颜色合成
    // =
    float3 cWhite = float3(1.0, 0.98, 0.95);

    // 主环颜色：内侧偏核心色，外侧偏环色
    float mainBrightness = mainRing * (0.7 + innerBias * 0.8);
    float3 mainColor = lerp(ringColor, coreColor, innerBias * 0.6) * mainBrightness;

    // 环上微观网格纹理
    float gridA = frac(normAngle * 60.0);
    float gridR = frac(dist * 35.0);
    float microGrid = smoothstep(0.025, 0.0, min(gridA, 1.0 - gridA));
    microGrid += smoothstep(0.012, 0.0, min(gridR, 1.0 - gridR));
    microGrid = saturate(microGrid);
    mainColor += ringColor * 0.15 * microGrid * mainRing;

    float3 finalColor = float3(0, 0, 0);
    finalColor += mainColor;                                    // A: 主冲击环
    finalColor += coreColor * precursor * 0.6;                  // B: 前驱波
    finalColor += ringColor * gridIntensity;                    // C: 六边形网格边
    finalColor += fragColor * cellFill;                         // C: 单元格闪烁
    finalColor += coreColor * ripple;                           // D: 内部涟漪
    finalColor += fragColor * trailing;                         // E: 数据流拖尾
    finalColor += cWhite * centerFlash;                         // F: 中心白闪

    float alpha = saturate(
        mainBrightness
        + precursor * 0.5
        + gridIntensity
        + cellFill
        + ripple
        + trailing * 0.5
        + centerFlash
    );
    alpha *= fadeAlpha;

    // =
    // G. 超驱故障叠加 —— 爆破的黑墙撕裂效果
    // =
    if (overdriveAmount > 0.01)
    {
        float od = overdriveAmount;
        // 爆破进度作为内在burst强度（初期最强，扩散后减弱）
        float detonBurst = pow(saturate(1.0 - ringProgress), 1.5);

        // G-1. RGB通道偏移（径向方向分离）
        float2 radDir = normalize(centered + 0.001);
        float splitAmt = od * (0.01 + detonBurst * 0.03);
        float2 splitOff = radDir * splitAmt;
        float rShift = tex2D(noiseSamp, float2(normAngle * 3.0 + 0.1, dist * 2.0) + splitOff).r;
        float bShift = tex2D(noiseSamp, float2(normAngle * 3.0 + 0.1, dist * 2.0) - splitOff).b;
        finalColor.r *= 0.7 + rShift * 0.6;
        finalColor.b *= 0.7 + bShift * 0.6;

        // G-2. 方块腐蚀（爆破内区域闪烁）
        float2 blkUV = floor(coords * (12.0 + detonBurst * 8.0)) / (12.0 + detonBurst * 8.0);
        float blockH = hash21(blkUV + float2(floor(uTime * 15.0), 0.0));
        float blockOn = step(0.82 - detonBurst * 0.25, blockH);
        float blockZone = smoothstep(ringProgress + 0.05, ringProgress - 0.15, dist);
        finalColor += coreColor * blockOn * od * blockZone * (0.4 + detonBurst * 0.8);
        alpha += blockOn * od * blockZone * 0.12;

        // G-3. 扫描线干扰（环及内部，用环色降低亮度叠加）
        float scanD = frac(dist * 60.0 + uTime * 3.0);
        scanD = step(0.93, scanD);
        finalColor += ringColor * scanD * od * 0.4;

        // G-4. 角向撕裂带（随机扇形黑带）
        float tearA = floor(normAngle * 12.0);
        float tearH = hash21(float2(tearA, floor(uTime * 12.0)));
        float tearOn = step(0.88 - detonBurst * 0.15, tearH) * od;
        finalColor *= 1.0 - tearOn * 0.5;

        // G-5. 全局闪烁（幅度收敛，避免爆炸瞬间整屏暴闪）
        float flickG = hash21(float2(floor(uTime * 20.0), 7.7));
        float flickAmt = detonBurst * (flickG - 0.3) * 1.2 * od;
        finalColor *= 1.0 + flickAmt;

        // G-6. alpha增强
        alpha *= 1.0 + od * 0.3;
    }

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass CyberDetonationPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
