sampler uImage0 : register(s0);
float offsetStrength;

//RGB错位分离，抄的风凌的，很简单，名字都不改
float4 TetradShiverShader(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);
    if (!any(color))
        return color;
    float2 offset = coords + float2(-offsetStrength, -offsetStrength);
    float2 offset2 = coords + float2( offsetStrength, -offsetStrength);
    float4 Tex1 = tex2D(uImage0, offset);
    Tex1.r *= 0;
    Tex1.b *= 0;
    float4 Tex2 = tex2D(uImage0, offset2);
    Tex2.r *= 0;
    Tex2.g *= 0;
    
    color.g *= 0;
    color.b *= 0;
    float4 Final = lerp(color, color + Tex1 + Tex2, 1);
    return Final;
}

technique Technique1
{
    pass TetradShiverShader
    {
        PixelShader = compile ps_2_0 TetradShiverShader();
    }
}