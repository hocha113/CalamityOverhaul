sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
sampler uImage3 : register(s3);

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

float uBeamWidth;
float uBeamLength;
float uPulseSpeed;
float uDistortionStrength;
float uCoreIntensity;

//伽马射线色调 - 高能紫蓝-白光谱
static const float3 CoreColor = float3(0.95, 0.9, 1.0);   //近白微紫核心
static const float3 InnerColor = float3(0.7, 0.5, 1.0);   //亮紫
static const float3 OuterColor = float3(0.4, 0.25, 0.9);  //深蓝紫
static const float3 EdgeColor = float3(0.2, 0.1, 0.6);    //暗靛蓝边缘
static const float3 CherenkovColor = float3(0.3, 0.6, 1.0); //切伦科夫辐射蓝

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

float noise(float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

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

//伽马射线像素着色器
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    
    float distFromCenter = abs(uv.y - 0.5) * 2.0;
    float alongBeam = uv.x;
    
    //光束基础形状 - 使用白色纹理定义
    float beamShape = tex2D(uImage3, float2(alongBeam, 0.5)).r;
    
    //=== 锐利的核心光柱 ===
    //伽马射线极其集中，核心衰减更锐利
    float coreIntensity = 1.0 - smoothstep(0.0, 0.15, distFromCenter);
    coreIntensity = pow(coreIntensity, 1.5) * beamShape;
    
    //次级核心 - 稍宽的柔和辉光
    float subCore = 1.0 - smoothstep(0.0, 0.35, distFromCenter);
    subCore = pow(subCore, 2.5) * beamShape * 0.6;
    
    //=== 高能噪声扰动 ===
    float2 noiseUV = float2(alongBeam * 4.0 - uTime * 3.0, distFromCenter * 2.0);
    float noise1 = tex2D(uImage1, noiseUV).r;
    
    float2 noiseUV2 = float2(alongBeam * 7.0 + uTime * 2.0, distFromCenter * 4.0);
    float noise2 = tex2D(uImage1, noiseUV2).g;
    
    //高频电离扰动
    float2 noiseUV3 = float2(alongBeam * 12.0 - uTime * 5.0, distFromCenter * 1.5 + uTime * 0.8);
    float ionNoise = tex2D(uImage1, noiseUV3).b;
    
    float turbulence = (noise1 * 0.5 + noise2 * 0.3 + ionNoise * 0.2) * uDistortionStrength;
    float distortedDist = distFromCenter + turbulence * 0.12;
    float turbulentIntensity = 1.0 - smoothstep(0.0, 0.45, distortedDist);
    turbulentIntensity = pow(turbulentIntensity, 1.8);
    
    //=== 高能脉冲 ===
    float pulse = sin(alongBeam * 15.0 - uTime * uPulseSpeed * 1.5) * 0.5 + 0.5;
    pulse = pow(pulse, 2.0) * 0.25 + 0.75;
    
    //=== 电离闪烁 ===
    //模拟高能电离效应，沿光束随机闪烁
    float ionFlicker = noise(float2(alongBeam * 30.0 + uTime * 8.0, distFromCenter * 5.0));
    ionFlicker = step(0.85, ionFlicker); //只有极少数点闪烁
    float ionGlow = ionFlicker * (1.0 - distFromCenter) * 2.0;
    
    //=== 切伦科夫辐射边缘 ===
    //光束边缘出现蓝色切伦科夫辐射光晕
    float cherenkov = smoothstep(0.2, 0.5, distFromCenter) * smoothstep(0.7, 0.5, distFromCenter);
    cherenkov *= beamShape;
    //给切伦科夫效果加上波动
    float cherenkovWave = sin(alongBeam * 25.0 - uTime * 4.0) * 0.4 + 0.6;
    cherenkov *= cherenkovWave;
    
    //=== 星光闪烁 ===
    float2 starUV = float2(alongBeam * 2.5 - uTime * 0.7, uv.y);
    float starGlow = tex2D(uImage2, starUV).r;
    starGlow *= pow(1.0 - distFromCenter, 2.0);
    
    //=== 总强度合成 ===
    float totalIntensity = 0.0;
    totalIntensity += coreIntensity * uCoreIntensity * pulse;
    totalIntensity += subCore * pulse;
    totalIntensity += turbulentIntensity * 0.5 * pulse;
    totalIntensity += ionGlow * 0.6;
    totalIntensity += cherenkov * 0.35;
    totalIntensity += starGlow * 0.25;
    
    totalIntensity *= uIntensity * uOpacity;
    
    //=== 伽马射线色带映射 ===
    float3 finalColor = float3(0, 0, 0);
    
    if (distFromCenter < 0.12)
    {
        //极亮核心 - 近白微紫
        float t = distFromCenter / 0.12;
        finalColor = lerp(CoreColor, InnerColor, t);
    }
    else if (distFromCenter < 0.35)
    {
        //内层 - 紫色渐变
        float t = (distFromCenter - 0.12) / 0.23;
        finalColor = lerp(InnerColor, OuterColor, t);
    }
    else if (distFromCenter < 0.55)
    {
        //过渡层 - 混入切伦科夫蓝
        float t = (distFromCenter - 0.35) / 0.2;
        finalColor = lerp(OuterColor, CherenkovColor, t * 0.6);
    }
    else
    {
        //边缘 - 暗靛蓝消散
        float t = (distFromCenter - 0.55) / 0.25;
        float3 edgeMix = lerp(CherenkovColor * 0.6, EdgeColor, t);
        finalColor = edgeMix;
    }
    
    //切伦科夫辐射叠加蓝色
    finalColor = lerp(finalColor, CherenkovColor, cherenkov * 0.4);
    
    //电离闪烁叠加白色
    finalColor = lerp(finalColor, CoreColor, ionGlow * 0.8);
    
    //核心超亮溢出
    float highlight = pow(1.0 - distFromCenter, 10.0) * beamShape;
    finalColor += CoreColor * highlight * 1.0;
    
    //星光闪烁的白色点缀
    finalColor = lerp(finalColor, float3(1, 1, 1), starGlow * 0.4);
    
    //=== 切伦科夫边缘辉光 ===
    float3 edgeRadiance = CherenkovColor * cherenkov * 0.6;
    finalColor += edgeRadiance;
    
    //应用顶点颜色
    finalColor *= input.Color.rgb;
    
    float alpha = saturate(totalIntensity * input.Color.a);
    
    return float4(finalColor, alpha);
}

//简化版伽马射线（低端降级）
float4 SimplePixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float distFromCenter = abs(uv.y - 0.5) * 2.0;
    float alongBeam = uv.x;
    
    //锐利核心
    float intensity = 1.0 - smoothstep(0.0, 0.35, distFromCenter);
    intensity = pow(intensity, 2.0);
    
    //脉冲
    float pulse = sin(alongBeam * 15.0 - uTime * uPulseSpeed * 1.5) * 0.25 + 0.75;
    intensity *= pulse * uIntensity * uOpacity;
    
    //伽马色带
    float3 color = lerp(OuterColor, CoreColor, pow(1.0 - distFromCenter, 2.0));
    //边缘加入切伦科夫蓝
    color = lerp(color, CherenkovColor, smoothstep(0.3, 0.6, distFromCenter) * 0.4);
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
        PixelShader = compile ps_3_0 SimplePixelShaderFunction();
    }
}
