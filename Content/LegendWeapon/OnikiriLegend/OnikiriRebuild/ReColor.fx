sampler uImage0 : register(s0); //MainColor
sampler uImage1 : register(s1); //BackGroundColor

float4 newColor;

float3 ACESFilmic(float3 x)
{
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

float ScreenMode(float B, float A)//屏幕模式
{
    B = min(1, B);
    A = min(1, A);

    return 1 - (1 - A) * (1 - B);

}
float Luminance(float3 color)//灰度化
{
    return dot(color, float3(0.3, 0.59, 0.11));
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0,float4 Color: COLOR0) : COLOR0//正常模式
{
    float4 tex = tex2D(uImage0, coords);
    
    return tex * Color * newColor;
}

float4 PixelShaderFunction2(float2 coords : TEXCOORD0, float4 Color : COLOR0) : COLOR0//类似add但是不会过爆的模式
{
    float4 tex = tex2D(uImage0, coords);
    float4 tex2 = tex2D(uImage1, coords);

    return float4(ScreenMode(tex.r, tex2.r), ScreenMode(tex.g, tex2.g), ScreenMode(tex.b, tex2.b), ScreenMode(tex.a, tex2.a));
}

float4 PixelShaderFunction3(float2 coords : TEXCOORD0, float4 Color : COLOR0) : COLOR0//使用亮度作为透明度，因此不会出现黑色。
{
    float4 color = tex2D(uImage0, coords);
    float alpha = Luminance(color.rgb);
    
    return float4(color.rgb, alpha);
}


float4 PixelShaderFunction4(float2 coords : TEXCOORD0, float4 Color : COLOR0) : COLOR0//灰度模式
{
    float4 tex = tex2D(uImage0, coords);
    if (tex.r > 0 && tex.g > 0 && tex.b>0)
    {
        float c = Luminance(tex.rgb);
    
        return float4(c, c, c, c);
    }
    return float4(0, 0, 0, 0);

}

float4 BloomBlend(float2 coords : TEXCOORD0, float4 Color : COLOR0) : COLOR0//使用亮度作为透明度，因此不会出现黑色。
{
    float4 bloomColor = tex2D(uImage0, coords)* newColor;
    float4 sceneColor = tex2D(uImage1, coords);

    float brightness = Luminance(bloomColor.rgb);
    // 先混合
    float4 blended = float4(
        ScreenMode(saturate(bloomColor.r), sceneColor.r),
        ScreenMode(saturate(bloomColor.g), sceneColor.g),
        ScreenMode(saturate(bloomColor.b), sceneColor.b),
        saturate(bloomColor.a)
    );
    
    // 再应用色调映射
    //blended.rgb = ACESFilmic(blended.rgb);
    
    return max(blended, sceneColor);
}

technique Technique1
{
    pass Apply1
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }

    pass Apply2
    {
        PixelShader = compile ps_3_0 PixelShaderFunction2();
    }
    pass Apply3
    {
        PixelShader = compile ps_3_0 PixelShaderFunction3();
    }
    pass Apply4
    {
        PixelShader = compile ps_3_0 PixelShaderFunction4();
    }
    pass Apply5
    {
        PixelShader = compile ps_3_0 BloomBlend();
    }
}