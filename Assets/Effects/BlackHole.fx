// ============================================================================
//BlackHole.fx 黑洞
//EventHorizon AlphaBlend + Accretion Additive；s0+s1
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
float eventHorizonRadius;
float diskInnerRadius;
float diskOuterRadius;
float brightness;
float dopplerStrength;
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

//=== 事件视界像素着色器 ===
//输出纯黑色+alpha遮罩，配合AlphaBlend吞噬背景光
//圆形渐隐+3D凹陷+边缘扰动
float4 EventHorizonPS(VertexShaderOutput input) : COLOR0
{
    float2 coords = input.TexCoords;
    float2 center = float2(0.5, 0.5);
    float2 toCenter = coords - center;
    float dist = length(toCenter);
    float angle = atan2(toCenter.y, toCenter.x);

    //圆形边界渐隐（纹理空间0.48处彻底透明，杜绝方形边框）
    float circleFade = 1.0 - smoothstep(0.36, 0.48, dist);

    float ehR = eventHorizonRadius;

    //=== 事件视界边缘噪声扰动（非完美圆，有撕裂感）===
    float edgeNoise = tex2D(noiseTex, float2(angle / 6.283 + uTime * 0.04, dist * 3.0 + uTime * 0.02)).r;
    float edgeDistort = (edgeNoise - 0.5) * 0.02;
    float distortedDist = dist + edgeDistort;

    //核心黑暗区域
    float coreAlpha = 1.0 - smoothstep(ehR * 0.4, ehR * 1.15, distortedDist);

    //=== 3D球体凹陷深度模拟 ===
    //将黑洞视为一个「凹进屏幕」的暗球
    float normD = saturate(dist / (ehR * 1.2));
    float sphereZ = sqrt(max(1.0 - normD * normD, 0.0));

    //光照暗角：让核心呈现凹陷球面感，而非平面圆
    float2 lightDir = float2(0.3, -0.25);
    float lightDot = dot(normalize(toCenter + 0.001), lightDir);
    //核心区域内部有轻微的明暗变化（半球凹陷）
    float depthShade = 0.7 + lightDot * 0.3 * (1.0 - sphereZ * 0.5);

    //视界边缘光晕（薄大气层被引力弯曲发出的最后一点光）
    float rimGlow = pow(max(1.0 - abs(normD - 0.95) * 12.0, 0.0), 2.0) * 0.15;
    //rim是微弱的暗紫色光而不是纯黑，但在AlphaBlend下体现为黑暗减弱
    float rimReduce = rimGlow * smoothstep(ehR * 0.8, ehR * 1.2, dist);

    //引力暗化带
    float gravDim = exp(-max(dist - ehR, 0.0) * 12.0) * 0.3;

    //合算
    float totalAlpha = saturate(coreAlpha * depthShade + gravDim - rimReduce);
    totalAlpha *= circleFade;

    return float4(0, 0, 0, totalAlpha * input.Color.a);
}

//=== 吸积效应像素着色器 ===
//光子环+吸积盘+引力透镜光效，中心遮罩为零
float4 AccretionPS(VertexShaderOutput input) : COLOR0
{
    float2 coords = input.TexCoords;
    float2 center = float2(0.5, 0.5);
    float2 toCenter = coords - center;
    float dist = length(toCenter);
    float angle = atan2(toCenter.y, toCenter.x);

    float ehR = eventHorizonRadius;

    //圆形边界渐隐（杜绝方形纹理边框）
    float circleFade = 1.0 - smoothstep(0.36, 0.48, dist);

    //事件视界遮罩（中心不发光）
    float horizonMask = smoothstep(ehR * 0.85, ehR * 1.4, dist);

    //=== 引力透镜UV扭曲 ===
    float lensF = distortionStrength * ehR / max(dist - ehR * 0.7, 0.008);
    lensF = min(lensF, 2.5);
    float2 lensedTC = toCenter * (1.0 + lensF * 0.012);
    float lensedDist = length(lensedTC);
    float lensedAngle = atan2(lensedTC.y, lensedTC.x);

    //=== 光子环（~1.5倍史瓦西半径的极亮薄环）===
    float photonR = ehR * 1.55;
    float photonD = abs(dist - photonR);
    float photonRing = exp(-photonD * photonD * 1500.0) * 2.5;
    //光子环微弱闪烁
    float pFlicker = tex2D(noiseTex, float2(angle / 6.283 + uTime * 0.25, 0.5 + uTime * 0.08)).r;
    photonRing *= (0.8 + pFlicker * 0.2);

    //=== 吸积盘 ===
    float diskNorm = saturate((lensedDist - diskInnerRadius) / (diskOuterRadius - diskInnerRadius));
    float diskMask = smoothstep(diskInnerRadius - 0.02, diskInnerRadius + 0.03, lensedDist);
    diskMask *= 1.0 - smoothstep(diskOuterRadius - 0.04, diskOuterRadius + 0.02, lensedDist);

    //开普勒差分旋转
    float keplerV = rotationSpeed * pow(max(lensedDist + 0.02, 0.02), -1.5);
    float diskAngle = lensedAngle + uTime * keplerV;

    //旋转UV采样噪声
    float2 diskUV = float2(cos(diskAngle) * lensedDist, sin(diskAngle) * lensedDist);

    float4 n1 = tex2D(noiseTex, diskUV * 2.0 + float2(uTime * 0.04, uTime * 0.025));
    float4 n2 = tex2D(noiseTex, diskUV * 5.5 + float2(-uTime * 0.05, uTime * 0.06));
    float4 n3 = tex2D(noiseTex, diskUV * 11.0 + float2(uTime * 0.07, -uTime * 0.04));

    //湍流（范围0.45~0.95）
    float turb = n1.r * 0.45 + n2.g * 0.35 + n3.b * 0.20;
    turb = turb * 0.5 + 0.45;

    //螺旋臂（3臂，适度缠绕）
    float spiral = sin(diskAngle * 3.0 - lensedDist * 30.0 + uTime * 0.5);
    spiral = spiral * 0.5 + 0.5;
    spiral = 0.5 + spiral * 0.5;

    //纤维丝细节
    float filament = sin(diskAngle * 7.0 + lensedDist * 40.0 + turb * 6.0);
    filament = filament * 0.5 + 0.5;
    filament = 0.65 + filament * 0.35;

    //多普勒增亮（接近侧更亮）
    float doppler = 1.0 + cos(angle + uTime * rotationSpeed * 0.12) * dopplerStrength;

    //温度梯度颜色（白热→橙→紫红）
    float4 diskColor;
    if (diskNorm < 0.25)
    {
        float4 hotWhite = float4(1.0, 0.95, 0.88, 1.0);
        diskColor = lerp(hotWhite, innerColor, diskNorm / 0.25);
    }
    else if (diskNorm < 0.6)
    {
        diskColor = lerp(innerColor, midColor, (diskNorm - 0.25) / 0.35);
    }
    else
    {
        diskColor = lerp(midColor, outerColor, (diskNorm - 0.6) / 0.4);
    }

    //径向亮度衰减（内圈热，外圈冷）
    float radialBright = 1.0 + (1.0 - diskNorm) * 0.6;

    //引力红移衰减（靠近视界变暗）
    float redshiftF = smoothstep(ehR, ehR + 0.1, lensedDist);

    //盘面合算
    float diskBright = diskMask * turb * spiral * filament * radialBright * doppler * redshiftF;

    //=== 内缘高温边界层 ===
    float innerEdgeDist = max(lensedDist - diskInnerRadius, 0.0);
    float innerEdge = exp(-innerEdgeDist * innerEdgeDist * 500.0);
    innerEdge *= smoothstep(ehR, diskInnerRadius, lensedDist);

    //=== 爱因斯坦环辉光（引力透镜亮环）===
    float einsteinD = abs(dist - ehR * 1.15);
    float einsteinRing = exp(-einsteinD * einsteinD * 600.0) * 1.2;

    //=== 喷流暗示（两极方向微弱能量束）===
    float jetCos = cos(angle);
    float jetMask = exp(-jetCos * jetCos * 6.0);
    jetMask *= exp(-max(dist - ehR, 0.0) * 4.0);
    jetMask *= 0.35;

    //=== 外围光晕 ===
    float outerHalo = exp(-max(dist - diskOuterRadius, 0.0) * 5.0) * 0.12;

    //=== 最终合成 ===
    float4 finalColor = float4(0, 0, 0, 0);

    //吸积盘
    finalColor.rgb += diskColor.rgb * diskBright * brightness;

    //光子环（白热色）
    float3 photonCol = lerp(innerColor.rgb, float3(1, 1, 1), 0.7);
    finalColor.rgb += photonCol * photonRing * brightness * horizonMask;

    //爱因斯坦环
    float3 einsteinCol = lerp(float3(0.7, 0.85, 1.0), innerColor.rgb, 0.3);
    finalColor.rgb += einsteinCol * einsteinRing * brightness * 0.5;

    //内缘高温
    finalColor.rgb += innerColor.rgb * innerEdge * brightness * horizonMask;

    //喷流
    finalColor.rgb += float3(0.5, 0.6, 1.0) * jetMask * brightness;

    //外围光晕
    finalColor.rgb += outerColor.rgb * outerHalo * brightness;

    //遮罩事件视界中心
    finalColor.rgb *= horizonMask;

    //alpha
    finalColor.a = saturate(diskBright * 1.5 + photonRing * 0.3 + einsteinRing * 0.3 + innerEdge + jetMask + outerHalo);
    finalColor.a *= horizonMask;
    finalColor.a *= circleFade;
    finalColor.a *= input.Color.a;
    finalColor.rgb *= circleFade;

    return finalColor * input.Color;
}

technique EventHorizon
{
    pass EventHorizonPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 EventHorizonPS();
    }
}

technique Accretion
{
    pass AccretionPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 AccretionPS();
    }
}
