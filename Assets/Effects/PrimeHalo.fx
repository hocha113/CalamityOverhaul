float colorMult;
float time;
float radius;
float maxOpacity;

float2 screenPosition;
float2 screenSize;
float2 anchorPoint;
float2 playerPosition;

float projTime;
bool isVmos;

//平滑插值函数，避免硬边缘
float InverseLerp(float a, float b, float t)
{
    return saturate(smoothstep(a, b, t)); //使用 smoothstep 来平滑过渡
}

//生成渐变边缘效果
float EdgeEffect(float dist, float edgeRadius)
{
    //使用 smoothstep 来平滑边缘区域的过渡
    return smoothstep(edgeRadius - 10.0f, edgeRadius, dist);
}

//核心函数
float4 Function(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    //计算世界坐标
    float2 worldUV = screenPosition + screenSize * uv;
    float2 provUV = anchorPoint / screenSize;

    //像素化处理
    float2 pixelatedUV = worldUV / screenSize;
    pixelatedUV.x -= worldUV.x % (1 / screenSize.x);
    pixelatedUV.y -= worldUV.y % (1 / (screenSize.y / 2) * 2);

    //计算距离玩家的距离
    float distToPlayer = distance(playerPosition, worldUV);

    //透明度控制：平滑过渡
    float opacity = 1 - smoothstep(200, 1800, distToPlayer);

    float distToRotPos = distance(worldUV, anchorPoint);
    if (distToRotPos < radius + 200)
    {
        opacity = (distToRotPos - radius) / 200;
    }

    //控制透明度的范围
    opacity = clamp(opacity, 0, maxOpacity);

    //基础颜色
    float4 newColor = float4(1, 0.1, 0.1, 1); //红色

    //动态颜色变化（例如：基于时间变化）
    if (isVmos)
    {
        float num1 = projTime / 30;
        newColor = float4(0.8f + (num1 * 0.2), 1 - (num1 * 0.9), 0.1, 1) * num1;
    }

    //最终颜色输出：混合色彩和透明度
    return newColor * opacity;
}

technique Technique1
{
    pass PrimeHaloPass
    {
        PixelShader = compile ps_2_0 Function();
    }
}