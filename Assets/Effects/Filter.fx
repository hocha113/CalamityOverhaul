sampler uImage0 : register(s0);
float3 filterRGB;
// 灰蓝色滤镜，非常简单的shader

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    // 以下照抄自fs49
    float4 color = tex2D(uImage0, coords);
    if (!any(color))
        return color;
        // 灰度 = r*0.3 + g*0.59 + b*0.11
    float gs = dot(float3(0.3, 0.59, 0.11), color.rgb);
    // 直接加上滤镜颜色，效果自己调
    return float4(gs + filterRGB.r, gs + filterRGB.g, gs + filterRGB.b, color.a);
}
technique Technique1
{
    pass Filter
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}