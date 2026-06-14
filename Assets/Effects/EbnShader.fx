// ============================================================================
//EbnShader.fx 屏幕空间燃烧/暗角
//采样 noiseTex；ps_3_0
// ============================================================================

sampler noiseTex : register(s1);
float colorMult;
float time;
float radius;
float maxOpacity;
float burnIntensity;
float2 screenPosition;
float2 screenSize;
float2 setPoint; //效果中心

float InverseLerp(float a, float b, float t)
{
    return saturate((t - a) / (b - a));
}

//域扭曲：用一层噪声偏移UV，产生有机的流动形态
float2 domainWarp(float2 uv, float scale, float speed, float strength)
{
    float2 offset;
    offset.x = tex2D(noiseTex, frac(uv * scale + float2(time * speed, time * speed * 0.7))).r;
    offset.y = tex2D(noiseTex, frac(uv * scale + float2(time * speed * -0.6, time * speed * 1.1))).g;
    return (offset - 0.5) * strength;
}

//手调火焰调色板，五段线性插值，为Additive混合和colorMult≈7适配
float3 firePalette(float noise)
{
    noise = saturate(noise);
    //smoothstep对比度增强，让火焰层次更分明
    noise = noise * noise * (3.0 - 2.0 * noise);

    //五个色阶从暗红余烬到白热核心
    float3 c0 = float3(0.06, 0.01, 0.005);   //暗红余烬
    float3 c1 = float3(0.12, 0.035, 0.01);   //深红焰心
    float3 c2 = float3(0.16, 0.075, 0.018);  //橙色火焰
    float3 c3 = float3(0.19, 0.12, 0.04);    //炽热黄橙
    float3 c4 = float3(0.20, 0.18, 0.14);    //白热核心

    float3 color;
    if (noise < 0.25)
        color = lerp(c0, c1, noise * 4.0);
    else if (noise < 0.5)
        color = lerp(c1, c2, (noise - 0.25) * 4.0);
    else if (noise < 0.75)
        color = lerp(c2, c3, (noise - 0.5) * 4.0);
    else
        color = lerp(c3, c4, (noise - 0.75) * 4.0);

    return color * colorMult;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    //计算世界空间UV坐标
    float2 worldUV = screenPosition + screenSize * uv;

    //计算像素到效果中心的距离
    float worldDistance = distance(worldUV, setPoint);

    float adjustedTime = time * 0.1;

    //==================================================
    //像素化UV坐标处理，保留像素风格的火焰效果
    //==================================================
    float2 pixelatedUV = worldUV / screenSize;
    pixelatedUV.x -= worldUV.x % (1.0 / screenSize.x);
    pixelatedUV.y -= worldUV.y % (1.0 / (screenSize.y / 2.0) * 2.0);

    //==================================================
    //域扭曲：两级噪声偏移UV，让火焰形态有机流动
    //一级低频大幅扭曲控制整体形态
    //二级中频小幅扭曲打破规则感
    //==================================================
    float2 warp1 = domainWarp(pixelatedUV, 0.55, 0.04, 0.035);
    float2 warpedUV = pixelatedUV + warp1;

    float2 warp2 = domainWarp(warpedUV, 1.1, 0.06, 0.015);
    warpedUV += warp2;

    //==================================================
    //多层噪声叠加（FBM思路）
    //用扭曲后的UV采样，高频层权重递减
    //==================================================

    //第一层：低频大尺度基础形态
    float n1 = tex2D(noiseTex, frac(warpedUV * 0.5 + float2(0, time * 0.18))).g;

    //第二层：中频结构
    float n2 = tex2D(noiseTex, frac(warpedUV * 1.1 + float2(0, time * 0.28))).g;

    //第三层：高频细节，带横向漂移
    float n3 = tex2D(noiseTex, frac(warpedUV * 1.8 + float2(adjustedTime * 0.5, adjustedTime * 1.1))).g;

    //第四层：反向旋转细节
    float n4 = tex2D(noiseTex, frac(warpedUV * 2.4 + float2(adjustedTime * -0.45, adjustedTime * 0.9))).g;

    //第五层：微细节层
    float n5 = tex2D(noiseTex, frac(warpedUV * 3.6 + float2(adjustedTime * 0.6, adjustedTime * -0.35))).r;

    //FBM加权合并：低频权重大，高频权重小
    float textureMesh = n1 * 0.30 + n2 * 0.25 + n3 * 0.22 + n4 * 0.15 + n5 * 0.08;

    //==================================================
    //不透明度计算，基于距离和燃烧强度
    //==================================================
    float distToPlayer = distance(setPoint, worldUV);
    float opacity = burnIntensity;
    opacity += InverseLerp(800, 500, distToPlayer);

    //==================================================
    //边界处理：smoothstep替代线性衰减，过渡更自然
    //==================================================
    bool border = worldDistance < radius && opacity > 0;
    float edgeFade = 1.0;
    if (border)
    {
        float t = InverseLerp(radius * 0.92, radius, worldDistance);
        edgeFade = t * t * (3.0 - 2.0 * t); //smoothstep曲线
    }

    opacity = clamp(opacity, 0, maxOpacity);

    //提前退出优化
    if (edgeFade >= 0.999 && (opacity == 0 || worldDistance < radius))
        return sampleColor;

    //==================================================
    //火焰颜色合成
    //==================================================
    float3 fireColor = firePalette(textureMesh);

    //余烬高光：高频噪声阈值化，产生随机炽热亮点
    float emberNoise = tex2D(noiseTex, frac(pixelatedUV * 4.2 + float2(time * 0.11, time * 0.32))).r;
    float ember = pow(saturate(emberNoise - 0.65) * 2.86, 2.5);
    fireColor += float3(0.14, 0.10, 0.04) * ember * colorMult;

    //时空闪烁：微弱的正弦强度波动，让火焰有呼吸感
    float flicker = 0.92 + 0.08 * sin(time * 5.3 + worldUV.x * 0.008)
                                   * sin(time * 7.9 + worldUV.y * 0.012);

    //远处火焰微暗，增加纵深感
    float distDim = lerp(1.0, 0.8, InverseLerp(350, 850, distToPlayer));

    return float4(fireColor * flicker * distDim, 1.0) * edgeFade * opacity;
}

technique Technique1
{
    pass EbnShaderPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
