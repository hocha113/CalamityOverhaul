sampler noiseTex : register(s1);
float colorMult;        //颜色倍增器
float time;             //时间参数，用于动画
float radius;           //效果半径
float maxOpacity;       //最大不透明度
float burnIntensity;    //燃烧强度
float2 screenPosition;  //屏幕坐标位置
float2 screenSize;      //屏幕尺寸
float2 setPoint;        //设定点坐标（效果中心）

float InverseLerp(float a, float b, float t)
{
    return saturate((t - a) / (b - a));
}

//火焰颜色调色板函数
//基于噪声值生成火焰颜色，模拟黑体辐射
float3 firePalette(float noise)
{
    //根据噪声计算温度值（1500-3000K范围）
    float temperature = 1500. + 1500. * noise;

    //定义火焰颜色渐变的三个关键色
    float3 darkColor = float3(0.81, 0.45, 0.23) * colorMult;    //暗红色调
    float3 midColor = float3(1., 0.75, 0.29) * colorMult;        //橙黄色调
    float3 brightColor = float3(1., 1., 0.95) * colorMult;     //亮白色调
    
    //根据噪声值在颜色之间插值
    float3 fireColor;
    if (noise < 0.5)
        fireColor = lerp(darkColor, midColor, noise * 2.);
    else
        fireColor = lerp(midColor, brightColor, (noise - 0.5) * 2.);
    
    //应用物理正确的黑体辐射公式
    //使用普朗克定律的简化版本
    fireColor = pow(fireColor, float3(5, 5, 5)) * (exp(1.43876719683e5 / (temperature * fireColor)) - 1.);

    //应用色调映射以获得最终颜色
    return 1. - exp(-1.3e8 / fireColor);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    //计算世界空间UV坐标
    float2 worldUV = screenPosition + screenSize * uv;
    
    //预留变量（用于保持与原始代码的兼容性）
    float2 provUV = setPoint / screenSize;
    
    //计算像素到效果中心的距离
    float worldDistance = distance(worldUV, setPoint);
    
    //时间缩放因子，用于控制动画速度
    float adjustedTime = time * 0.1;
    
    //==================================================
    //像素化UV坐标处理
    //创建类似像素风格的火焰效果
    //==================================================
    float2 pixelatedUV = worldUV / screenSize;
    
    //X轴像素化处理
    pixelatedUV.x -= worldUV.x % (1 / screenSize.x);
    
    //Y轴像素化处理（使用2倍步长）
    pixelatedUV.y -= worldUV.y % (1 / (screenSize.y / 2) * 2);
    
    //==================================================
    //多层噪声采样
    //通过叠加不同频率和速度的噪声创建复杂的火焰纹理
    //==================================================
    
    //第一层：慢速大尺度噪声
    float noiseMesh1 = tex2D(noiseTex, frac(pixelatedUV * 0.58 + float2(0, time * 0.25))).g;
    
    //第二层：中速中尺度噪声
    float noiseMesh2 = tex2D(noiseTex, frac(pixelatedUV * 1.57 + float2(0, time * 0.35))).g;
    
    //第三层：快速旋转噪声
    float noiseMesh3 = tex2D(noiseTex, frac(pixelatedUV * 1.46 + float2(adjustedTime * 0.56, adjustedTime * 1.2))).g;
    
    //第四层：反向旋转噪声
    float noiseMesh4 = tex2D(noiseTex, frac(pixelatedUV * 1.57 + float2(adjustedTime * -0.56, adjustedTime * 1.2))).g;
    
    //加权混合所有噪声层
    //权重分配：12.5% + 20% + 35% + 35% = 102.5% (略微过度混合以增强效果)
    float textureMesh = noiseMesh1 * 0.125 + noiseMesh2 * 0.2 + noiseMesh3 * 0.35 + noiseMesh4 * 0.35;
    
    //==================================================
    //不透明度计算
    //基于距离和燃烧强度
    //==================================================
    float distToPlayer = distance(setPoint, worldUV);
    
    //初始不透明度 = 燃烧强度
    float opacity = burnIntensity;
    
    //根据距离调整不透明度（距离越近，不透明度越高）
    //在500-800像素范围内进行渐变
    opacity += InverseLerp(800, 500, distToPlayer);
    
    //==================================================
    //边界处理和颜色衰减
    //==================================================
    
    //判断是否在效果边界内
    bool border = worldDistance < radius && opacity > 0;
    
    //计算边缘渐变因子
    float colorMult = 1;
    if (border) 
        colorMult = InverseLerp(radius * 0.94, radius, worldDistance);
    
    //限制不透明度在合理范围内
    opacity = clamp(opacity, 0, maxOpacity);
    
    //==================================================
    //早期退出优化
    //如果不需要渲染火焰效果，直接返回原始颜色
    //==================================================
    if (colorMult == 1 && (opacity == 0 || worldDistance < radius))
        return sampleColor;
    
    //==================================================
    //最终颜色合成
    //生成火焰颜色并应用边缘渐变和不透明度
    //==================================================
    return float4(firePalette(textureMesh), 1) * colorMult * opacity;
}

technique Technique1
{
    pass EbnShaderPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}