sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float uSaturation;
float uRotation;
float uTime;
float4 uSourceRect;
float2 uWorldPosition;
float uDirection;
float3 uLightSource;
float2 uImageSize0;
float2 uImageSize1;
float power;
float speed;
float opacity;
//如果想要这个Shader正常工作，就不要动这些参数
float2 offset;
float4 drawColor;

float4 Function(float2 coords : TEXCOORD0) : COLOR0
{
    // 获取原始纹理颜色和 alpha 值
    float4 originalColor = tex2D(uImage0, coords);
    float alpha = originalColor.a;
    // 计算三个正弦波
    float sinx = sin(uTime + (coords.x + offset.x) * speed);
    float siny = sin(1.57 + (coords.x + offset.x) * 0.37 * speed - uTime);
    float sinz = sin((coords.x + offset.x) * 0.21 * speed - uTime);
    // 计算偏移量，使用 alpha 值调整扭曲强度
    float2 off = float2(0, sinx * siny * sinz * power * (1.0 - alpha));
    float4 color = tex2D(uImage0, coords + off);
    // 应用绘制颜色和透明度
    color *= drawColor;
    return color * opacity;
}

technique Technique1
{
    pass TwistColoringPass
    {
        PixelShader = compile ps_2_0 Function();
    }
}