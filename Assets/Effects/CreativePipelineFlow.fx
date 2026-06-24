// ============================================================================
//CreativePipelineFlow.fx 创造管道宇宙能量
//以管道能量贴图(uImage0)的 alpha 作为导管遮罩，在其内部绘制流动的紫色宇宙能量：
//星云流动 + 沿管行进的白炽核脉冲 + 闪烁微光。创造管道恒满电(无限能量)，故恒为高亮。
//与基础管道统一管线：合批单批次、世界空间、AlphaBlend 预乘 alpha，无 RT、无光照耦合。
//顶点色 a=强度(创造恒为 1)；色调由本着色器宇宙色阶自定义，不依赖贴图红色
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//宇宙紫色阶：深渊紫 → 亮紫 → 白紫核 → 星点
static const float3 COL_DEEP = float3(0.080, 0.020, 0.220);
static const float3 COL_MID  = float3(0.420, 0.160, 0.920);
static const float3 COL_HOT  = float3(0.820, 0.680, 1.000);
static const float3 COL_STAR = float3(1.000, 0.960, 1.000);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 tex = tex2D(uImage0, coords);
    float mask = tex.a;
    if (mask < 0.02) return float4(0, 0, 0, 0);          //导管之外
    float intensity = vertexColor.a;                      //强度(创造恒 1)
    float t = uTime;

    //星云流动：沿管(coords.x)滚动的双层噪声
    float neb1 = valueNoise(coords * float2(3.0, 5.0) + float2(-t * 0.9, 0.0));
    float neb2 = valueNoise(coords * float2(7.0, 9.0) + float2(-t * 1.5, t * 0.3));
    float neb = 0.6 * neb1 + 0.4 * neb2;
    float3 col = lerp(COL_DEEP, COL_MID, neb);

    //沿管行进的白炽核脉冲：整数周期(每格一个)跨格无缝
    float ph = frac(coords.x - t * 0.45);
    float pulse = exp(-pow(ph - 0.5, 2.0) * 6.0);
    col += COL_HOT * pulse * 0.75;
    //连续白炽脊线，呈"恒流"感
    float crest = sin(coords.x * 6.28318 - t * 2.0) * 0.5 + 0.5;
    col += COL_HOT * pow(crest, 3.0) * 0.30;

    //闪烁微光：沿管滚动的高频噪声尖点(非网格，避免规则平铺)
    float spark = valueNoise(coords * float2(14.0, 8.0) + float2(-t * 2.0, t * 0.5));
    col += COL_STAR * pow(saturate(spark), 8.0) * 1.6;

    col *= 0.6 + intensity * 0.6;

    //不透明度：创造恒满→近实心导管
    float a = mask * saturate(0.6 + intensity * 0.4);

    float fa = a * uAlpha;
    return float4(col * a * uAlpha, fa);                  //预乘 alpha
}

technique Technique1
{
    pass CreativePipelineFlowPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
