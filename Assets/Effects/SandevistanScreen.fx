// ============================================================================
//SandevistanScreen.fx 沙德维斯坦全屏滤镜
//采样 uImage0 屏幕
// ============================================================================

sampler uImage0 : register(s0);

float intensity;           //0~1
float uTime;
float chromaticOffset;     //色差偏移基准
float vignetteStrength;    //暗角强度
float2 playerCenter;       //玩家屏幕归一化坐标
float radialBlurStrength;  //径向模糊

float4 SandevistanScreenPass(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);
    if (intensity < 0.001)
        return original;

    float2 toEdge = coords - playerCenter;
    float distFromCenter = length(toEdge);
    float2 blurDir = toEdge * radialBlurStrength;
    float2 caDir = normalize(toEdge + 0.0001) * chromaticOffset;

    //=== 1. 径向模糊 + 色差分离（Radial Blur + Chromatic Aberration）===
    //沿径向方向多次采样，同时对RGB三通道施加不同偏移实现色差
    //模拟"极速运动中世界向外拉伸"的感知扭曲：赛博朋克2077斯安威斯坦的标志性效果
    float3 accumulated = float3(0, 0, 0);
    const int SAMPLES = 8;

    for (int i = 0; i < SAMPLES; i++)
    {
        float t = ((float) i / (SAMPLES - 1.0)) - 0.5;
        float2 sampleOffset = blurDir * t * intensity;

        //R通道向外偏移，B通道向内偏移，实现与径向模糊融合的色差
        accumulated.r += tex2D(uImage0, coords + sampleOffset + caDir * intensity).r;
        accumulated.g += tex2D(uImage0, coords + sampleOffset).g;
        accumulated.b += tex2D(uImage0, coords + sampleOffset - caDir * intensity).b;
    }
    accumulated /= (float) SAMPLES;

    //模糊遮罩：屏幕中心保持清晰，边缘逐渐模糊，形成隧道视觉
    float blurMask = smoothstep(0.05, 0.55, distFromCenter);
    float3 result = lerp(original.rgb, accumulated, blurMask);

    //=== 2. 轻度去饱和 + 对比度增强 ===
    //不做重度青色偏移，仅轻微冷色调去饱和，保持画面力度
    //赛博朋克2077中义体加速是神经层面的感知变化，不是数字信号干扰
    float lum = dot(result, float3(0.299, 0.587, 0.114));
    float3 desaturated = float3(lum * 0.96, lum * 0.98, lum * 1.03);
    result = lerp(result, desaturated, 0.2 * intensity);

    //对比度：让暗处更暗、亮处更亮，增强"时间凝固"的视觉张力
    result = saturate((result - 0.5) * (1.0 + 0.18 * intensity) + 0.5);

    //=== 3. 暗角（Vignette）===
    //从屏幕边缘向中心的渐变遮罩
    float2 vc = (coords - 0.5) * 2.0;
    float vd = dot(vc, vc);
    float vignette = 1.0 - vd * vignetteStrength * intensity;
    result *= saturate(vignette);

    //=== 4. 边缘冷色辉光 ===
    //在暗角边缘添加极轻微的冷色调辉光，营造义体HUD边缘感
    float edgeGlow = smoothstep(0.5, 1.1, vd) * intensity * 0.06;
    result += float3(edgeGlow * 0.2, edgeGlow * 0.5, edgeGlow * 0.7);

    //=== 5. 神经脉冲（Neural Pulse）===
    //极轻微的亮度呼吸，模拟义体与神经系统的同步节律
    float pulse = sin(uTime * 2.5) * 0.015 * intensity;
    result *= (1.0 + pulse);

    return float4(result, original.a);
}

technique Technique1
{
    pass SandevistanScreenPass
    {
        PixelShader = compile ps_3_0 SandevistanScreenPass();
    }
}
