// ============================================================================
//SHPCJudgmentRay.fx 精密瞄具裁决射线
//coords.x 沿光束 0枪口 1终点，coords.y 横截；s0+s1
//ps_3_0
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float lifeProgress;     //0~1 生命进度
float fadeAlpha;        //整体透明度
float rayLength;        //射线像素长度(刻度密度)
float3 coreColor;       //弧芯色
float3 edgeColor;       //描边色

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float along = coords.x;
    float crossDist = abs(coords.y - 0.5) * 2.0;     //0=中心 1=边缘
    float t = lifeProgress;

    //出现/消散包络
    float sweep = smoothstep(t * 3.2, t * 3.2 - 0.18, along);
    float shrink = 1.0 - smoothstep(0.45, 1.0, t);

    //弧芯
    float coreW = 0.10 * shrink + 0.012;
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.6);

    //辉光
    float glowW = 0.45 * shrink + 0.05;
    float glow = 1.0 - smoothstep(coreW * 0.5, glowW, crossDist);

    //色散描边
    float fringe = smoothstep(glowW * 0.55, glowW, crossDist) * (1.0 - smoothstep(glowW, glowW * 1.35, crossDist));
    fringe *= t * 1.4;

    //沿程数据刻度
    float tickFreq = rayLength / 46.0;
    float tick = frac(along * tickFreq - uTime * 0.8);
    tick = step(0.94, tick) * (1.0 - smoothstep(0.0, glowW * 0.9, crossDist));
    tick *= 0.5 * shrink;

    //消散裂解
    float segID = floor(along * tickFreq * 0.5);
    float segHash = hash21(float2(segID, floor(uTime * 18.0)));
    float dissolve = step(segHash, 1.0 - (t - 0.5) * 1.8);
    dissolve = max(dissolve, step(t, 0.5));

    //枪口闪光与终点耀斑
    float muzzle = pow(saturate(1.0 - along / 0.06), 2.0) * (1.0 - crossDist) * (1.0 - t);
    float impact = pow(saturate((along - 0.97) / 0.03), 1.5) * (1.0 - crossDist * 0.7) * shrink;

    //噪声热扰动
    float heat = tex2D(noiseSamp, float2(along * 6.0 - uTime * 3.0, coords.y)).r;
    float heatGlow = glow * heat * 0.25;

    float3 color = float3(0.0, 0.0, 0.0);
    color += coreColor * core * 1.3;
    color += edgeColor * glow * 0.65;
    color += float3(0.25, 0.05, 1.0) * fringe * 0.5;
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
