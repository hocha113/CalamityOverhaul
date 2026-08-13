// ============================================================================
//QueenHoneyPool.fx 蜂蜜黏滞洼面
//uv.x 横向 0→1，uv.y 纵向 0顶(液面)→1底；预乘输出+AlphaBlend
//浓蜜材质三律：液面张力挂边(边缘更暗更饱和)、高光只走各向异性窄反射带、
//体色随深度沉暗；白只在气泡破裂的一瞬(uTime驱动的哈希脉冲，≤数帧)
//全程笛卡尔直线算术，无分支，无极角
//ps_3_0
// ============================================================================

float uTime;
float uIntensity;   //整体透明度包络
float uProgress;    //0→1 铺开(溅落带过冲)
float uDrain;       //0→1 收干(自液面向下啃掉)
float uAspect;      //宽高比，保证噪声各向同性

//哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 HoneyPoolPS(float2 uv : TEXCOORD0) : COLOR0
{
    float x = (uv.x - 0.5) * 2.0;      //-1..1 横向
    float ax = uv.x * uAspect;         //等比横坐标

    //铺开包络：从落点向两侧漫开，边缘带噪声毛边(黏液不走光滑胶囊)
    float edgeNoise = valueNoise(float2(ax * 2.6, 3.3)) - 0.5;
    float spreadHalf = uProgress * (1.02 + edgeNoise * 0.22);
    float inside = smoothstep(spreadHalf, spreadHalf - 0.16, abs(x));

    //液面高度：慢波+微噪声，黏稠故波幅小频率低
    float surfWave = sin(ax * 2.2 + uTime * 0.9) * 0.018
                   + (valueNoise(float2(ax * 1.4 - uTime * 0.22, 0.7)) - 0.5) * 0.05;
    float surfY = 0.10 + surfWave;
    float below = smoothstep(surfY, surfY + 0.10, uv.y);

    //收干：液面下沉(自顶向下啃)
    float drainCut = smoothstep(uDrain, uDrain + 0.14, uv.y - surfY + 0.12);
    below *= drainCut;

    //体色随深度沉暗：琥珀→焦糖褐
    float depth = saturate((uv.y - surfY) / max(1.0 - surfY, 0.001));
    float3 colShallow = float3(0.94, 0.63, 0.19);
    float3 colDeep = float3(0.36, 0.19, 0.05);
    float3 body = lerp(colShallow, colDeep, depth * depth * 0.9 + depth * 0.1);

    //张力挂边：横向边缘更暗更饱和
    float rimDark = smoothstep(0.55, 0.98, abs(x) / max(spreadHalf, 0.001));
    body *= 1.0 - rimDark * 0.38;
    body.gb *= 1.0 - rimDark * 0.22;

    //液面亮线：表面一条窄亮缘
    float surfLine = exp(-pow((uv.y - surfY) * 30.0, 2.0));
    body += float3(1.0, 0.82, 0.42) * surfLine * 0.5;

    //各向异性窄反射带：横向长条高光，随时间缓移(圆形高光=塑料，禁)
    float bandY = 0.32 + sin(uTime * 0.5) * 0.05;
    float bandWobble = (valueNoise(float2(ax * 1.1 + uTime * 0.3, 9.1)) - 0.5) * 0.08;
    float band = exp(-pow((uv.y - bandY - bandWobble) * 16.0, 2.0));
    float bandMask = 0.55 + 0.45 * valueNoise(float2(ax * 2.8 - uTime * 0.4, 5.5));
    body += float3(0.95, 0.72, 0.30) * band * bandMask * 0.34;

    //大而慢的黏泡：格子哈希定泡位，缓升，顶破瞬间白闪(≤数帧)
    float2 bubGrid = float2(floor(ax * 3.0), 0.0);
    float bubSeed = hash21(bubGrid + 17.0);
    float bubPhase = frac(uTime * (0.10 + bubSeed * 0.12) + bubSeed * 7.0);
    float bubX = frac(ax * 3.0) - 0.5;
    float bubY = uv.y - lerp(0.85, surfY + 0.05, bubPhase);
    float bubR = 0.06 + bubSeed * 0.05;
    float bubDist = length(float2(bubX * 0.5, bubY * (1.0 + uAspect * 0.0)));
    float bubble = smoothstep(bubR, bubR * 0.55, bubDist) * step(0.35, bubSeed);
    //泡体亮环
    float bubRing = bubble * smoothstep(bubR * 0.4, bubR * 0.7, bubDist);
    body += float3(0.9, 0.66, 0.26) * bubRing * 0.45;
    //顶破白闪：泡到达液面附近的极短脉冲
    float pop = bubble * smoothstep(0.93, 1.0, bubPhase);
    body += float3(1.0, 0.95, 0.8) * pop * 0.8;

    //整体透明度：主体近实，越深越实，边缘略薄
    float alpha = below * inside * (0.62 + depth * 0.3) * (1.0 - rimDark * 0.2);
    alpha *= uIntensity;

    //预乘输出
    return float4(body * alpha, alpha);
}

technique HoneyPool
{
    pass P0
    {
        PixelShader = compile ps_3_0 HoneyPoolPS();
    }
}
