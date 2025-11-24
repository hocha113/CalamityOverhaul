sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
texture noiseTexture;
sampler noiseTex = sampler_state
{
    texture = <noiseTexture>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

matrix transformMatrix;
float uTime;
float rotationSpeed;
float flattenRatio;
float2 centerPos;
float brightness;
float distortionStrength;
float pulseIntensity;

//吸积盘颜色配置
float4 innerColor;
float4 midColor;
float4 outerColor;

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
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    output.Position = mul(input.Position, transformMatrix);
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    //计算相对于中心的坐标
    float2 coords = input.TexCoords;
    float2 center = float2(0.5, 0.5);
    float2 toCenter = coords - center;
    
    //应用压扁效果到Y轴
    toCenter.y /= flattenRatio;
    
    //计算距离和角度
    float dist = length(toCenter);
    float angle = atan2(toCenter.y, toCenter.x);
    
    //基于距离的旋转速度
    float rotSpeed = rotationSpeed * (1.0 / (dist + 0.1));
    
    //应用旋转
    float rotatedAngle = angle + uTime * rotSpeed;
    
    //将旋转后的坐标映射到噪声纹理
    float2 rotatedCoords = float2(
        cos(rotatedAngle) * dist,
        sin(rotatedAngle) * dist * flattenRatio
    ) + center;
    
    //采样噪声纹理
    float4 noise = tex2D(noiseTex, rotatedCoords * 2.0 + float2(uTime * 0.1, 0));
    
    //创建多层噪声扭曲
    float2 distortedCoords = rotatedCoords;
    distortedCoords += (noise.xy - 0.5) * distortionStrength * (1.0 - dist);
    
    //再次采样扭曲后的噪声
    float4 detailNoise = tex2D(noiseTex, distortedCoords * 4.0 + float2(uTime * 0.15, uTime * 0.08));
    
    //计算吸积盘的环形强度（更窄的环形）
    float innerRadius = 0.2;
    float outerRadius = 0.9;
    float diskMask = 1.0 - smoothstep(innerRadius, innerRadius + 0.05, dist);
    diskMask *= smoothstep(outerRadius, outerRadius - 0.1, dist);
    
    //基于距离的颜色渐变
    float colorLerp = (dist - innerRadius) / (outerRadius - innerRadius);
    colorLerp = saturate(colorLerp);
    
    float4 baseColor;
    if (colorLerp < 0.5)
    {
        baseColor = lerp(innerColor, midColor, colorLerp * 2.0);
    }
    else
    {
        baseColor = lerp(midColor, outerColor, (colorLerp - 0.5) * 2.0);
    }
    
    //添加噪声细节
    float noiseDetail = noise.r * 0.7 + detailNoise.r * 0.3;
    
    //创建热点和暗带效果
    float bands = sin(dist * 40.0 + uTime * 3.0) * 0.5 + 0.5;
    bands = pow(bands, 2.0);
    
    //脉动效果
    float pulse = sin(uTime * 4.0) * pulseIntensity + 1.0;
    
    //混合所有效果
    float intensity = diskMask * noiseDetail * bands * pulse;
    intensity *= brightness;
    
    //添加径向亮度变化
    float radialBrightness = 1.0 - smoothstep(innerRadius, outerRadius, dist);
    radialBrightness = pow(radialBrightness, 1.5);
    
    //3D边缘高光效果（模拟法线）
    float normalEffect = abs(toCenter.y * flattenRatio) * 2.0;
    normalEffect = saturate(normalEffect);
    float4 edgeHighlight = float4(1, 1, 1, 0) * normalEffect * diskMask * 0.3;
    
    float4 finalColor = baseColor * intensity * (1.0 + radialBrightness * 0.5) + edgeHighlight;
    finalColor.a = intensity * input.Color.a;
    
    //添加发光效果
    float glow = pow(1.0 - dist, 3.0) * diskMask;
    finalColor.rgb += innerColor.rgb * glow * 1.2;
    
    //中心能量核心
    float coreGlow = pow(1.0 - dist * 2.0, 8.0);
    finalColor.rgb += float3(1, 1, 1) * coreGlow * brightness;
    
    return finalColor * input.Color;
}

technique Technique1
{
    pass FlattenedDiskPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
