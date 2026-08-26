// ============================================================================
//BRelicBlackFlash.fx 黑闪印记（残酷月总遗物）
//胸前节拍环 quad（白像素画布 uv 0~1）：收缩聚焦环（读秒）+ 吞光印记核（真 alpha）
//+ 竖瞳缝充金 + 窗口锁定环与十字亮芒 + 黑金电弧带（连闪层数）+ 失手碎裂闪烁
//+ 黑闪余辉外扩细环。s1 = PerlinNoise（旋转笛卡尔采样，无 atan2 无极缝）。
//输出预乘 alpha：AlphaBlend 下暗核与电弧暗鞘真正咬掉背景
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
//收缩前摇进度 0~1（1=窗口开）
float uPhase;
//窗口开度 0~1
float uWindow;
//黑闪余辉 0~1（触发瞬间 1，衰减中外扩）
float uFlash;
//连闪电弧强度 0~1
float uArc;
//失手碎裂 0~1
float uBreak;
//实例种子
float uSeed;
//整体可见度 0~1
float uAlpha;

float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

//PerlinNoise 实测灰度域 ~0.227..0.776，归一化后再用
float Nrm(float n)
{
    return saturate((n - 0.227) / 0.549);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);

    //―――― 收缩聚焦环：外起 0.88 收至 0.30，越近窗口越细锐（读秒感）――――
    float focus = smoothstep(0.0, 1.0, uPhase);
    float ringR = lerp(0.88, 0.30, focus);
    float ringW = lerp(0.050, 0.017, focus);
    float ring = exp(-pow((r - ringR) / ringW, 2.0));
    //环缘噪声撕裂：不完美圆
    float tear = Nrm(tex2D(uNoise, Rot(p, uTime * 0.4 + uSeed) * 0.42 + uSeed).r);
    ring *= (0.5 + 0.5 * tear) * step(0.02, uPhase);
    //失手：环被撕成噪声阈值碎段，急促闪烁
    float flick = 0.55 + 0.45 * sin(uTime * 46.0 + uSeed * 20.0);
    ring *= 1.0 - uBreak * (1.0 - step(0.45, tear) * flick);
    //色相：深红 → 鎏金；失手期压回黑闪红
    float3 ringCol = lerp(float3(0.72, 0.09, 0.11), float3(1.0, 0.78, 0.33), focus * focus);
    ringCol = lerp(ringCol, float3(1.0, 0.16, 0.18), uBreak);

    //―――― 窗口满开：锁定环爆金 + 十字亮芒（"现在打"的记号）――――
    float lockRing = exp(-pow((r - 0.30) / 0.045, 2.0)) * uWindow;
    float crossBeam = (exp(-pow(p.x / 0.026, 2.0)) + exp(-pow(p.y / 0.026, 2.0)))
        * exp(-r * r * 2.4) * uWindow;

    //―――― 中心印记：吞光暗核 + 竖长瞳缝（月总语汇），随节拍充金 ――――
    float coreR = 0.115 + 0.05 * uFlash;
    float core = 1.0 - smoothstep(coreR, coreR + 0.035, r);
    float pupil = exp(-pow(p.x / 0.020, 2.0)) * (1.0 - smoothstep(0.085, 0.13, abs(p.y)));
    float pupilHot = pupil * (0.22 + 0.78 * max(focus, uWindow)) * core;

    //―――― 黑金电弧带（连闪层数）：双反向噪声场脊线 + 吸光暗鞘 ――――
    float arcBand = exp(-pow((r - 0.52) / 0.14, 2.0));
    float n1 = Nrm(tex2D(uNoise, Rot(p, uTime * 1.7) * 0.5 + uSeed).r);
    float n2 = Nrm(tex2D(uNoise, Rot(p, -uTime * 1.3) * 0.46 + uSeed * 1.7).r);
    float arcs = pow(saturate(1.0 - abs(n1 - n2) * 4.2), 6.0) * arcBand * uArc;
    arcs *= 0.7 + 0.3 * sin(uTime * 16.0 + uSeed * 40.0);
    //暗鞘：只加 alpha 不加色 = 电弧周身的"黑"，黑金由此成立
    float arcSheath = arcBand * uArc * 0.28 * (0.4 + 0.6 * n1);

    //―――― 黑闪余辉：触发瞬间自印记外扩的细环 ――――
    float flashR = lerp(0.92, 0.32, uFlash);
    float flashRing = exp(-pow((r - flashR) / 0.030, 2.0)) * uFlash * uFlash;

    //―――― 预乘合成：暗件压底，光件叠亮 ――――
    float3 col = float3(0.020, 0.008, 0.020) * core;
    col += ringCol * ring * (0.65 + 0.6 * uWindow);
    col += float3(1.0, 0.85, 0.45) * lockRing * 1.2;
    col += float3(1.0, 0.90, 0.55) * crossBeam * 0.9;
    col += float3(1.0, 0.75, 0.30) * pupilHot;
    col += float3(1.0, 0.80, 0.36) * arcs;
    col += float3(0.85, 0.18, 0.16) * arcs * 0.35;
    col += float3(1.0, 0.92, 0.60) * flashRing;

    float alpha = saturate(core * 0.92 + ring * 0.75 + lockRing + crossBeam * 0.8
        + pupilHot + arcs * 0.85 + arcSheath + flashRing);
    return float4(col * uAlpha, alpha * uAlpha);
}

technique Technique1
{
    pass BRelicBlackFlashPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
