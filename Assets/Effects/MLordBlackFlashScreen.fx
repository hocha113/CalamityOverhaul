// ============================================================================
//MLordBlackFlashScreen.fx 黑闪爆点全屏后效（月总）
//采样 uImage0 屏幕；两层门控：一帧黑白反转冲击帧（亮度阈值双色调+红描边）
//+ 红黑冲击波（环形折射推挤 + 红色波前 + 向红黑压暗的余韵）
//纯径向算术，无角向项，无极缝
// ============================================================================

sampler uImage0 : register(s0);

float uAspect;
//xy=爆心uv z=冲击帧开关(0/1×强度) w=余韵强度
float4 uFlash;
//xy=爆心uv z=波半径(屏高归一) w=波强度
float4 uWave;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 d = (coords - uWave.xy) * float2(uAspect, 1.0);
    float r = length(d) + 1e-5;
    float2 dir = d / r;
    dir.x /= uAspect;

    //―――― 冲击波：环形折射推挤 ――――
    float band = exp(-pow((r - uWave.z) / 0.07, 2.0));
    float2 off = dir * band * 0.02 * uWave.w;

    float3 col;
    col.r = tex2D(uImage0, coords - off * 1.3).r;
    col.g = tex2D(uImage0, coords - off).g;
    col.b = tex2D(uImage0, coords - off * 0.75).b;

    //―――― 冲击帧：亮度阈值双色调（黑白世界），红描边勾出轮廓 ――――
    float lum = dot(col, float3(0.299, 0.587, 0.114));
    float3 mono = step(0.34, lum).xxx;
    float edge = exp(-pow((lum - 0.34) / 0.06, 2.0));
    float3 impact = mono * float3(0.96, 0.94, 0.95) + float3(1.0, 0.1, 0.13) * edge * 1.3;
    col = lerp(col, impact, saturate(uFlash.z));

    //―――― 红色波前 + 余韵向红黑压暗（世界被黑闪染过一瞬）――――
    col += float3(0.6, 0.05, 0.07) * band * uWave.w;
    float vin = (1.0 - exp(-r * r * 1.8)) * uFlash.w;
    col = lerp(col, float3(0.03, 0.0, 0.012), vin * 0.3);

    return float4(col, 1.0);
}

technique Technique1
{
    pass MLordBlackFlashScreenPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
