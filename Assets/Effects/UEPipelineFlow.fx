// ============================================================================
//UEPipelineFlow.fx 电力管道能量
//以管道能量贴图(uImage0)的 alpha 作为导管遮罩，在其内部绘制发光能量：
//亮度/不透明度随电量(顶点色 alpha)表现"含量"，外加一道沿管缓行的宽柔能量包做流动感。
//宽柔单包(每格一个)避免旧版"密集竖条"的违和纵向条纹，空管(电量≈0)返回透明。
//世界空间绘制，AlphaBlend 预乘 alpha。顶点色：rgb=色调(与贴图相乘), a=电量比例 0~1
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;

static const float3 COL_WARM = float3(0.55, 0.32, 0.12);   //高能暖芯

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 tex = tex2D(uImage0, coords);
    float mask = tex.a;
    if (mask < 0.02) return float4(0, 0, 0, 0);              //导管之外

    float fill = vertexColor.a;                              //电量比例 0~1
    if (fill < 0.004) return float4(0, 0, 0, 0);            //空管→透明
    float3 hue = tex.rgb * vertexColor.rgb;                  //导管色调(红)×顶点色

    float t = uTime;

    //含量→亮度（清晰单调，让管内电量一眼可读）
    float lvl = 0.30 + 0.85 * fill;

    //沿管缓行的宽柔能量包：每格一个、整数周期跨格无缝；够宽够柔，呈"移动的辉光"而非竖条
    float ph = frac(coords.x - t * 0.32);
    float pulse = exp(-pow(ph - 0.5, 2.0) * 4.5);

    //极轻微全局呼吸，给静止管道一点生气（无空间条纹）
    float breath = 0.95 + 0.05 * sin(t * 1.7);

    float3 col = hue * lvl * breath * (0.86 + 0.34 * pulse);
    col += hue * pulse * 0.35 * fill;                        //包峰更亮
    col += COL_WARM * pulse * fill * 0.30;                   //高能暖芯，仅满管时明显

    //不透明度：含量越高越实，低含量半透（与原版手感一致），空管已提前透明
    float a = mask * saturate(0.30 + 0.70 * fill);

    float fa = a * uAlpha;
    return float4(col * a * uAlpha, fa);                     //预乘 alpha
}

technique Technique1
{
    pass UEPipelineFlowPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
