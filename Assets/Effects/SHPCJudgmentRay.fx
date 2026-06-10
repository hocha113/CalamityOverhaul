// ============================================================================
// SHPCJudgmentRay.fx — 精密瞄具裁决射线着色器
// 瞬发狙击光束：刀刃般的白红弧芯 + 色散描边 + 沿程数据刻度 + 消散裂解
// 以拉伸四边形渲染：coords.x = 沿光束（0=枪口 1=终点），coords.y = 横截
// 配合 SHPCJudgmentRayProj（PrecisionOpticModule）使用
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float lifeProgress;     // 0~1 生命进度（0=刚出现 1=完全消散）
float fadeAlpha;        // 整体透明度 0~1
float rayLength;        // 射线像素长度（用于让刻度密度不随长度拉伸）
float3 coreColor;       // 弧芯色（近白）
float3 edgeColor;       // 描边色（猩红）

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float along = coords.x;
    float crossDist = abs(coords.y - 0.5) * 2.0;     // 0=中心 1=边缘
    float t = lifeProgress;

    // ---- 出现/消散包络：出现时从枪口向终点扫出，消散时整体变细裂解 ----
    float sweep = smoothstep(t * 3.2, t * 3.2 - 0.18, along);   // 前0.3生命内完成扫出
    float shrink = 1.0 - smoothstep(0.45, 1.0, t);               // 后半生命逐渐收窄

    // ---- 弧芯：刀刃般的极细白线 ----
    float coreW = 0.10 * shrink + 0.012;
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.6);

    // ---- 辉光：紧贴弧芯的红色光带 ----
    float glowW = 0.45 * shrink + 0.05;
    float glow = 1.0 - smoothstep(coreW * 0.5, glowW, crossDist);

    // ---- 色散描边：消散期边缘红蓝分离的伪色差 ----
    float fringe = smoothstep(glowW * 0.55, glowW, crossDist) * (1.0 - smoothstep(glowW, glowW * 1.35, crossDist));
    fringe *= t * 1.4;

    // ---- 沿程数据刻度：等距细环，像测距标尺 ----
    float tickFreq = rayLength / 46.0;
    float tick = frac(along * tickFreq - uTime * 0.8);
    tick = step(0.94, tick) * (1.0 - smoothstep(0.0, glowW * 0.9, crossDist));
    tick *= 0.5 * shrink;

    // ---- 消散裂解：生命后期沿光束随机段落断裂 ----
    float segID = floor(along * tickFreq * 0.5);
    float segHash = hash21(float2(segID, floor(uTime * 18.0)));
    float dissolve = step(segHash, 1.0 - (t - 0.5) * 1.8);       // t>0.5后逐段消失
    dissolve = max(dissolve, step(t, 0.5));

    // ---- 枪口闪光与终点耀斑 ----
    float muzzle = pow(saturate(1.0 - along / 0.06), 2.0) * (1.0 - crossDist) * (1.0 - t);
    float impact = pow(saturate((along - 0.97) / 0.03), 1.5) * (1.0 - crossDist * 0.7) * shrink;

    // ---- 噪声热扰动 ----
    float heat = tex2D(noiseSamp, float2(along * 6.0 - uTime * 3.0, coords.y)).r;
    float heatGlow = glow * heat * 0.25;

    float3 color = float3(0.0, 0.0, 0.0);
    color += coreColor * core * 1.3;
    color += edgeColor * glow * 0.65;
    color += float3(0.25, 0.05, 1.0) * fringe * 0.5;             // 蓝紫色差侧
    color += edgeColor * fringe * 0.5;
    color += coreColor * tick;
    color += edgeColor * heatGlow;
    color += coreColor * (muzzle * 1.2 + impact * 0.9);

    float alpha = saturate(core + glow * 0.55 + fringe * 0.4 + tick * 0.5 + muzzle + impact);
    alpha *= fadeAlpha * sweep * dissolve;

    return float4(color * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCJudgmentRayPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
