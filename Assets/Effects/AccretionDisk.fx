// ============================================================================
// AccretionDisk.fx 吸积盘/恒星球体
// EventHorizon AlphaBlend + Accretion Additive；s0+s1
// ps_3_0
// ============================================================================

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
float innerRadius;
float outerRadius;
float2 centerPos;
float brightness;
float distortionStrength;

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
    float2 coords = input.TexCoords;
    float2 center = float2(0.5, 0.5);
    float2 toCenter = coords - center;
    float dist = length(toCenter);
    float angle = atan2(toCenter.y, toCenter.x);

    //球体半径（纹理空间）
    float sR = outerRadius * 0.42;

    //=== 基础球体形状 ===
    //球体遮罩：柔和边缘
    float sphereMask = 1.0 - smoothstep(sR * 0.85, sR * 1.1, dist);

    //normDist: 0=中心, 1=球体边缘
    float normD = saturate(dist / sR);

    //=== 3D球体立体感（伪法线光照） ===
    //模拟球面：z = sqrt(1 - x^2 - y^2)
    float sphereZ = max(1.0 - normD * normD, 0.0);
    sphereZ = sqrt(sphereZ); //球面Z深度

    //基于球面法线的光照，光源偏右上方
    float2 lightDir = float2(0.35, -0.3); //偏移光源方向
    float lightDot = dot(normalize(toCenter), lightDir);
    float surfLight = 0.6 + lightDot * 0.4; //表面明暗变化

    //=== 临边增亮（limb brightening，像恒星大气） ===
    //球体边缘因视线路径穿透更多发光大气，所以更亮
    float limbBright = 1.0 + (1.0 - sphereZ) * 1.5;

    //=== 多层差分旋转等离子体 ===
    //三层以不同速度旋转的噪声，模拟深浅不同的等离子体层
    float rA1 = angle + uTime * rotationSpeed * 0.8;
    float rA2 = angle + uTime * rotationSpeed * 1.4 + 2.094;
    float rA3 = angle + uTime * rotationSpeed * 0.4 + 4.189;

    //球面映射UV（从平面映射到球面）
    float2 sphUV1 = float2(rA1 / 6.283, normD); //经纬度映射
    float2 sphUV2 = float2(rA2 / 6.283, normD * 0.8);
    float2 sphUV3 = float2(rA3 / 6.283, normD * 1.3);

    float4 p1 = tex2D(noiseTex, sphUV1 * 3.0 + float2(uTime * 0.05, 0));
    float4 p2 = tex2D(noiseTex, sphUV2 * 5.0 + float2(0, uTime * 0.06));
    float4 p3 = tex2D(noiseTex, sphUV3 * 2.0 + float2(uTime * 0.03, uTime * 0.04));

    //等离子体湍流（保持高基底，不要太暗）
    float plasma = p1.r * 0.4 + p2.g * 0.35 + p3.b * 0.25;
    plasma = plasma * 0.5 + 0.5; //映射到0.5~1.0

    //=== 表面纹理细节（太阳米粒/对流胞） ===
    float2 detailUV = float2(angle / 6.283 + uTime * rotationSpeed * 0.3, normD);
    float4 detail = tex2D(noiseTex, detailUV * 8.0 + float2(uTime * 0.08, -uTime * 0.04));
    float cellPattern = detail.r * 0.6 + detail.g * 0.4;
    cellPattern = cellPattern * 0.4 + 0.6; //调制范围0.6~1.0

    //=== 温度颜色（中心最热=白黄，边缘=橙红） ===
    float4 baseColor;
    if (normD < 0.4)
    {
        //核心区域：非常热，趋向白色
        float4 hotWhite = float4(1.0, 0.95, 0.85, 1.0);
        baseColor = lerp(hotWhite, innerColor * 1.1, normD / 0.4);
    }
    else if (normD < 0.75)
    {
        baseColor = lerp(innerColor * 1.1, midColor, (normD - 0.4) / 0.35);
    }
    else
    {
        baseColor = lerp(midColor, outerColor, (normD - 0.75) / 0.25);
    }

    //=== 能量脉动（呼吸感） ===
    float pulse1 = sin(uTime * 3.0 + normD * 10.0) * 0.12 + 0.88;
    float pulse2 = sin(uTime * 5.5 - normD * 15.0 + plasma * 4.0) * 0.08 + 0.92;

    //=== 日珥/射线（沿径向的高亮条纹） ===
    float rays = sin(angle * 6.0 + uTime * 2.0 + plasma * 5.0);
    rays = max(rays, 0.0); //只取正向
    rays = rays * rays * 0.35; //锐化
    float rayMask = smoothstep(0.5, 0.9, normD); //只在外缘显示

    //=== 核心合算 ===
    //基础强度 = 球体形状 × 等离子体 × 表面细节 × 脉动
    float baseBright = sphereMask * plasma * cellPattern * pulse1 * pulse2;

    //应用立体光照
    float lit = surfLight * limbBright;

    //最终球体颜色
    float4 finalColor;
    finalColor.rgb = baseColor.rgb * baseBright * lit * brightness;

    //=== 叠加效果层 ===

    //表面射线
    finalColor.rgb += innerColor.rgb * rays * rayMask * sphereMask * brightness * 0.7;

    //核心白热辉光（中心区域极亮）
    float coreGlow = pow(max(1.0 - normD * 1.8, 0.0), 3.0);
    finalColor.rgb += float3(1.0, 0.97, 0.92) * coreGlow * brightness * 1.0 * sphereMask;

    //边缘辉光（球体外部的散射光晕）
    float edgeGlow = exp(-(dist - sR) * (dist - sR) * 120.0);
    float3 glowCol = lerp(midColor.rgb, innerColor.rgb, 0.4);
    finalColor.rgb += glowCol * edgeGlow * brightness * 0.8;

    //外层大范围柔光（远距离可见的光晕）
    float farGlow = exp(-dist * 3.0) * 0.15;
    finalColor.rgb += innerColor.rgb * farGlow * brightness;

    //=== 热点闪烁 ===
    float hotspot = p1.g * p2.r;
    hotspot = hotspot * hotspot;
    finalColor.rgb += baseColor.rgb * hotspot * sphereMask * brightness * 0.3;

    //=== alpha通道（确保实体感） ===
    finalColor.a = saturate(baseBright * lit * 2.0 + edgeGlow + farGlow * 0.5 + coreGlow) * input.Color.a;

    return finalColor * input.Color;
}

technique Technique1
{
    pass AccretionDiskPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
