sampler diagonalNoise : register(s1);
sampler upwardNoise : register(s2);
sampler upwardPerlinTex : register(s3);
float colorMult;
float time;
float radius;
float maxOpacity;
float burnIntensity;

float2 screenPosition;
float2 screenSize;
float2 anchorPoint;
float2 playerPosition;

// 反向线性插值函数
float InverseLerp(float a, float b, float t)
{
    return saturate((t - a) / (b - a));
}

// 硫磺火焰调色板 - 暗红色基调
float3 sulfurFirePalette(float noise)
{
    // 定义硫磺火的颜色梯度 - 从深红到亮橙红
    float3 deepRedColor = float3(0.35, 0.08, 0.05) * colorMult; // 深暗红色
    float3 crimsonColor = float3(0.75, 0.15, 0.12) * colorMult; // 猩红色
    float3 sulfurOrange = float3(0.95, 0.35, 0.18) * colorMult; // 硫磺橙色
    float3 brightCore = float3(1.0, 0.55, 0.25) * colorMult; // 明亮核心
    
    float3 fireColor;
    // 使用更复杂的渐变来创造硫磺火效果
    if (noise < 0.3)
    {
        // 最暗的区域 - 深红色到猩红色
        fireColor = lerp(deepRedColor, crimsonColor, noise / 0.3);
    }
    else if (noise < 0.65)
    {
        // 中间区域 - 猩红色到硫磺橙色
        fireColor = lerp(crimsonColor, sulfurOrange, (noise - 0.3) / 0.35);
    }
    else
    {
        // 最亮的区域 - 硫磺橙色到明亮核心
        fireColor = lerp(sulfurOrange, brightCore, (noise - 0.65) / 0.35);
    }
    
    // 增加对比度和饱和度
    fireColor = pow(fireColor, float3(1.8, 2.2, 2.5));
    
    // 添加硫磺火特有的辉光效果
    float glowFactor = pow(noise, 3.0) * 1.5;
    fireColor += float3(0.3, 0.1, 0.05) * glowFactor;
    
    return saturate(fireColor);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 worldUV = screenPosition + screenSize * uv;
    float2 provUV = anchorPoint / screenSize;
    float worldDistance = distance(worldUV, anchorPoint);
    float adjustedTime = time * 0.1;
    
    // 像素化UV坐标 - 创造更粗糙的硫磺火纹理
    float2 pixelatedUV = worldUV / screenSize;
    pixelatedUV.x -= worldUV.x % (1 / screenSize.x);
    pixelatedUV.y -= worldUV.y % (1 / (screenSize.y / 2) * 2);
    
    // 采样噪声纹理 - 调整权重以创造更激烈的硫磺火效果
    float noiseMesh1 = tex2D(upwardNoise, frac(pixelatedUV * 0.68 + float2(0, time * 0.18))).g;
    float noiseMesh2 = tex2D(upwardPerlinTex, frac(pixelatedUV * 1.35 + float2(0, time * 0.28))).g;
    float noiseMesh3 = tex2D(diagonalNoise, frac(pixelatedUV * 1.72 + float2(adjustedTime * 0.48, adjustedTime * 1.35))).g;
    float noiseMesh4 = tex2D(diagonalNoise, frac(pixelatedUV * 1.88 + float2(adjustedTime * -0.62, adjustedTime * 1.45))).g;
    
    // 调整混合权重以增强硫磺火的层次感
    float textureMesh = noiseMesh1 * 0.15 + noiseMesh2 * 0.25 + noiseMesh3 * 0.3 + noiseMesh4 * 0.3;
    
    // 添加额外的扰动以创造硫磺火的不稳定感
    float turbulence = pow(abs(sin(worldUV.y * 0.1 + time * 0.5)), 2.0) * 0.15;
    textureMesh = saturate(textureMesh + turbulence);
    
    // 获取像素到玩家的距离
    float distToPlayer = distance(playerPosition, worldUV);
    // 根据距离计算正确的不透明度
    float opacity = burnIntensity;
    // 玩家接近时快速淡入
    opacity += InverseLerp(800, 500, distToPlayer);
    
    // 定义边界并混合火焰效果以实现平滑过渡
    bool border = worldDistance < radius && opacity > 0;
    float colorMult = 1;
    if (border) 
        colorMult = InverseLerp(radius * 0.94, radius, worldDistance);
    opacity = clamp(opacity, 0, maxOpacity);
    
    // 如果颜色倍数未改变（非边界像素）且不透明度为0，或在半径内
    if (colorMult == 1 && (opacity == 0 || worldDistance < radius))
        return sampleColor;
    
    // 应用硫磺火调色板
    float3 sulfurColor = sulfurFirePalette(textureMesh);
    
    // 添加边缘闪烁效果增强硫磺火的视觉冲击
    float edgeFlicker = sin(time * 5.0 + worldDistance * 0.05) * 0.1 + 0.9;
    sulfurColor *= edgeFlicker;
    
    return float4(sulfurColor, 1) * colorMult * opacity;
}

technique Technique1
{
    pass EbnShaderPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}