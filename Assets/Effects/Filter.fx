// ============================================================================
// Filter.fx 棕褐色调滤镜
// 采样 uImage0；ps_3_0
// ============================================================================

sampler uImage0 : register(s0);
float3 filterRGB; //RGB 偏移量

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);
    if (!any(color))
        return color;
    float gs = dot(float3(0.58, 0.39, 0.11), color.rgb);
    return float4(gs + filterRGB.r, gs + filterRGB.g, gs + filterRGB.b, color.a);
}
technique Technique1
{
    pass Filter
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
