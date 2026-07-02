// ============================================================================
//DemoBloom.fx 亮度提取 + 可分离高斯模糊（Bloom 双技术）
//ThresholdTech: 屏幕 → 亮部缓冲；BlurTech: uDelta 方向 13 抽头高斯
//合成回屏用普通 Additive SpriteBatch，不需要着色器
//ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

float uThreshold;   //亮度阈值 0..1
float uBoost;       //提取后的增益
float2 uDelta;      //模糊步长(uv)，方向*texel*半径

float4 ThresholdPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float3 src = tex2D(uImage0, coords).rgb;
    float lum = dot(src, float3(0.299, 0.587, 0.114));
    //软拐点：阈值下方平滑归零，避免硬边闪烁
    float knee = uThreshold * 0.5;
    float soft = saturate((lum - uThreshold + knee) / max(knee * 2.0, 1e-4));
    float w = max(lum - uThreshold, 0.0) / max(lum, 1e-4);
    w = max(w, soft * soft * 0.25);
    return float4(src * w * uBoost, 1.0);
}

static const float gw[7] = { 0.1964, 0.1747, 0.1216, 0.0662, 0.0281, 0.0093, 0.0024 };

float4 BlurPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float3 acc = tex2D(uImage0, coords).rgb * gw[0];
    [unroll]
    for (int i = 1; i < 7; i++)
    {
        float2 off = uDelta * i;
        acc += tex2D(uImage0, coords + off).rgb * gw[i];
        acc += tex2D(uImage0, coords - off).rgb * gw[i];
    }
    return float4(acc, 1.0);
}

technique ThresholdTech
{
    pass P0
    {
        PixelShader = compile ps_3_0 ThresholdPS();
    }
}

technique BlurTech
{
    pass P0
    {
        PixelShader = compile ps_3_0 BlurPS();
    }
}
