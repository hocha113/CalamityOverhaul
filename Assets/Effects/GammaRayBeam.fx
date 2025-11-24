sampler uImage0 : register(s0); //主纹理
sampler uImage1 : register(s1); //噪声纹理
sampler uImage2 : register(s2); //星光纹理
sampler uImage3 : register(s3); //光束纹理

float3 uColor;
float3 uSecondaryColor;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;
float2 uDirection;
float uOpacity;
float uTime;
float uIntensity;
float uProgress;
float2 uImageSize1;
float2 uImageSize2;
float2 uImageSize3;
float2 uImageOffset;
float uSaturation;
float4 uSourceRect;
float2 uZoom;

//伽马射线特定参数
float uBeamWidth;
float uBeamLength;
float uPulseSpeed;
float uDistortionStrength;
float uCoreIntensity;

//颜色配置
static const float3 CoreColor = float3(1.0, 1.0, 1.0); 
static const float3 InnerColor = float3(0.4, 0.9, 1.0);
static const float3 OuterColor = float3(0.2, 0.6, 1.0);
static const float3 EdgeColor = float3(0.1, 0.4, 0.8); 

struct VertexShaderInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = input.Position;
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    return output;
}

//简化的噪声函数
float noise(float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

//平滑噪声
float smoothNoise(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);
    
    float a = noise(i);
    float b = noise(i + float2(1.0, 0.0));
    float c = noise(i + float2(0.0, 1.0));
    float d = noise(i + float2(1.0, 1.0));
    
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//分形布朗运动
float fbm(float2 uv, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * smoothNoise(uv * frequency);
        frequency *= 2.0;
        amplitude *= 0.5;
    }
    
    return value;
}

//主像素着色器函数
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    
    //计算到光束中心的距离（Y方向）
    float distFromCenter = abs(uv.y - 0.5) * 2.0;
    
    //沿光束方向的位置（X方向）
    float alongBeam = uv.x;
    
    //=== 核心光束 ===
    //使用光束纹理 (ShineLine) 作为基础形状
    float beamShape = tex2D(uImage3, float2(alongBeam, 0.5)).r;
    
    //根据距离中心的远近创建强度衰减
    float coreIntensity = 1.0 - smoothstep(0.0, 0.3, distFromCenter);
    coreIntensity = pow(coreIntensity, 2.0) * beamShape;
    
    //=== 动态噪声扰动 ===
    //使用噪声纹理创建流动效果
    float2 noiseUV = float2(alongBeam * 3.0 - uTime * 2.0, distFromCenter * 2.0);
    float noise1 = tex2D(uImage1, noiseUV).r;
    
    float2 noiseUV2 = float2(alongBeam * 5.0 + uTime * 1.5, distFromCenter * 3.0);
    float noise2 = tex2D(uImage1, noiseUV2).g;
    
    //组合噪声创建湍流效果
    float turbulence = (noise1 * 0.6 + noise2 * 0.4) * uDistortionStrength;
    
    //应用湍流到强度
    float distortedDist = distFromCenter + turbulence * 0.1;
    float turbulentIntensity = 1.0 - smoothstep(0.0, 0.5, distortedDist);
    turbulentIntensity = pow(turbulentIntensity, 1.5);
    
    //=== 脉冲效果 ===
    float pulse = sin(alongBeam * 10.0 - uTime * uPulseSpeed) * 0.5 + 0.5;
    pulse = pow(pulse, 3.0) * 0.3 + 0.7; //调整脉冲强度
    
    //=== 能量波纹 ===
    float wave = sin(alongBeam * 20.0 - uTime * 3.0) * 0.5 + 0.5;
    wave *= (1.0 - distFromCenter * 0.5);
    
    //=== 星光闪烁 ===
    //使用星光纹理添加闪烁效果
    float2 starUV = float2(alongBeam * 2.0 - uTime * 0.5, uv.y);
    float starGlow = tex2D(uImage2, starUV).r;
    starGlow *= (1.0 - distFromCenter);
    
    //=== 边缘辉光 ===
    float edgeGlow = smoothstep(0.3, 0.6, distFromCenter) * smoothstep(0.8, 0.6, distFromCenter);
    edgeGlow *= beamShape;
    
    //=== 组合所有效果 ===
    float totalIntensity = 0.0;
    
    //核心
    totalIntensity += coreIntensity * uCoreIntensity * pulse;
    
    //湍流层
    totalIntensity += turbulentIntensity * 0.6 * pulse;
    
    //波纹
    totalIntensity += wave * 0.2;
    
    //星光
    totalIntensity += starGlow * 0.3;
    
    //边缘辉光
    totalIntensity += edgeGlow * 0.4;
    
    //应用整体强度和透明度
    totalIntensity *= uIntensity * uOpacity;
    
    //=== 颜色混合 ===
    float3 finalColor = float3(0, 0, 0);
    
    //根据到中心的距离混合颜色
    if (distFromCenter < 0.2)
    {
        //核心区域 - 纯白到青色
        float t = distFromCenter / 0.2;
        finalColor = lerp(CoreColor, InnerColor, t);
    }
    else if (distFromCenter < 0.5)
    {
        //中间区域 - 青色到蓝色
        float t = (distFromCenter - 0.2) / 0.3;
        finalColor = lerp(InnerColor, OuterColor, t);
    }
    else
    {
        //外围区域 - 蓝色到深蓝
        float t = (distFromCenter - 0.5) / 0.3;
        finalColor = lerp(OuterColor, EdgeColor, t);
    }
    
    //添加脉冲颜色变化
    finalColor = lerp(finalColor, CoreColor, pulse * coreIntensity * 0.3);
    
    //添加星光闪烁的白色
    finalColor = lerp(finalColor, float3(1, 1, 1), starGlow * 0.5);
    
    //=== 高光效果 ===
    //在核心添加额外的亮度
    float highlight = pow(1.0 - distFromCenter, 8.0) * beamShape;
    finalColor += CoreColor * highlight * 0.8;
    
    //=== 边缘发光 ===
    float3 edgeColor = EdgeColor * edgeGlow * 0.8;
    finalColor += edgeColor;
    
    //应用输入颜色调制
    finalColor *= input.Color.rgb;
    
    //最终透明度
    float alpha = totalIntensity * input.Color.a;
    
    //确保alpha不超过1
    alpha = saturate(alpha);
    
    return float4(finalColor, alpha);
}

//=== 简化版着色器（用于低配置） ===
float4 SimplePixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float distFromCenter = abs(uv.y - 0.5) * 2.0;
    float alongBeam = uv.x;
    
    //简单的径向渐变
    float intensity = 1.0 - smoothstep(0.0, 0.5, distFromCenter);
    intensity = pow(intensity, 2.0);
    
    //简单脉冲
    float pulse = sin(alongBeam * 10.0 - uTime * uPulseSpeed) * 0.3 + 0.7;
    intensity *= pulse * uIntensity * uOpacity;
    
    //简单颜色混合
    float3 color = lerp(OuterColor, CoreColor, pow(1.0 - distFromCenter, 2.0));
    color *= input.Color.rgb;
    
    return float4(color, intensity * input.Color.a);
}

technique Technique1
{
    pass GammaRayPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
    
    pass SimpleGammaRayPass
    {
        PixelShader = compile ps_2_0 SimplePixelShaderFunction();
    }
}
