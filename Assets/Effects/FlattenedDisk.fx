// ============================================================================
// FlattenedDisk.fx 吸积盘
// 高亮基底+加法细节；s0+s1；ps_3_0
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
float flattenRatio;
float2 centerPos;
float brightness;
float distortionStrength;
float pulseIntensity;
float dopplerStrength;

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

    //压扁Y轴
    float2 flatTC = toCenter;
    flatTC.y /= max(flattenRatio, 0.01);

    float dist = length(flatTC);
    float angle = atan2(flatTC.y, flatTC.x);

    //盘面范围
    float iR = 0.06;
    float oR = 0.46;
    float normDist = saturate((dist - iR) / (oR - iR));

    //=== 开普勒差分旋转 ===
    float keplerV = rotationSpeed * pow(max(dist + 0.02, 0.02), -1.5);
    float rotAngle = angle + uTime * keplerV;

    //=== 旋转UV ===
    float2 rotUV = float2(cos(rotAngle) * dist, sin(rotAngle) * dist);

    //=== 多层噪声采样 ===
    float4 n1 = tex2D(noiseTex, rotUV * 1.8 + float2(uTime * 0.06, uTime * 0.03));
    float4 n2 = tex2D(noiseTex, rotUV * 4.5 + float2(-uTime * 0.05, uTime * 0.08));
    float4 n3 = tex2D(noiseTex, rotUV * 9.0 + float2(uTime * 0.1, -uTime * 0.06));
    float4 n4 = tex2D(noiseTex, rotUV * 1.2 + float2(uTime * 0.03, uTime * 0.03));

    //扭曲后再采样一层
    float2 warpOff = (n4.xy - 0.5) * distortionStrength;
    float4 nW = tex2D(noiseTex, (rotUV + warpOff) * 6.0 + float2(uTime * 0.08, -uTime * 0.04));

    //湍流合成（范围0.4~0.95，高下限保证基底亮度）
    float turb = n1.r * 0.35 + n2.g * 0.28 + n3.b * 0.17 + nW.r * 0.20;
    turb = turb * 0.55 + 0.4;

    //=== 螺旋臂（3臂，宽调制让结构清晰可见）===
    float spiral = sin(rotAngle * 3.0 - dist * 30.0 + uTime * 0.6);
    spiral = spiral * 0.5 + 0.5;
    float spiralMod = 0.45 + spiral * 0.55;

    //=== 盘面遮罩（宽过渡带） ===
    float diskMask = smoothstep(iR - 0.02, iR + 0.06, dist);
    diskMask *= 1.0 - smoothstep(oR - 0.06, oR + 0.04, dist);

    //=== 多普勒增亮 ===
    float dopplerPhase = cos(angle + uTime * rotationSpeed * 0.2 + 1.57);
    float doppler = 1.0 + dopplerPhase * dopplerStrength;

    //=== 温度梯度颜色（降低内部增益保色） ===
    float4 baseColor;
    if (normDist < 0.3)
    {
        baseColor = lerp(innerColor * 1.1, midColor, normDist / 0.3);
    }
    else if (normDist < 0.7)
    {
        baseColor = lerp(midColor, outerColor, (normDist - 0.3) / 0.4);
    }
    else
    {
        baseColor = outerColor * (0.9 - (normDist - 0.7) * 0.3);
    }

    //=== 纤维丝（增强调制） ===
    float filaments = sin(rotAngle * 12.0 + dist * 55.0 + turb * 8.0);
    filaments = filaments * 0.5 + 0.5;
    float filaMod = 0.6 + filaments * 0.4;

    //=== 脉动 ===
    float pulse = 1.0 + sin(uTime * 2.5 + dist * 8.0) * pulseIntensity;

    //=== 核心强度 ===
    float baseBright = diskMask * turb * brightness * pulse;
    float detail = spiralMod * filaMod * doppler;

    //=== 径向亮度梯度 ===
    float radGrad = 1.0 + (1.0 - normDist) * 0.5;

    //=== 最终盘面颜色 ===
    float4 finalColor;
    finalColor.rgb = baseColor.rgb * baseBright * detail * radGrad;

    //=== 光子环 ===
    float photonDist = abs(dist - iR) * 22.0;
    float photonRing = exp(-photonDist * photonDist) * 2.0;
    float3 photonCol = lerp(innerColor.rgb, float3(1, 1, 1), 0.6);
    finalColor.rgb += photonCol * photonRing * brightness * doppler * diskMask;

    //=== 内缘高温辐射 ===
    float innerHeat = exp(-max(dist - iR, 0.0) * 12.0) * diskMask;
    finalColor.rgb += innerColor.rgb * innerHeat * brightness * 1.0;

    //=== 外缘柔和光晕 ===
    float outerHalo = exp(-(dist - oR) * (dist - oR) * 80.0) * 0.5;
    finalColor.rgb += outerColor.rgb * outerHalo * brightness * 0.5;

    //=== 热点闪烁 ===
    float hotspot = n1.g * n2.b;
    hotspot = hotspot * hotspot;
    finalColor.rgb += baseColor.rgb * hotspot * diskMask * brightness * 0.4;

    //=== RT不稳定性 ===
    float rtWave = sin(angle * 14.0 + uTime * 3.5 + turb * 6.0);
    rtWave = max(rtWave, 0.0);
    float rtZone = exp(-max(dist - iR - 0.03, 0.0) * 20.0);
    finalColor.rgb += innerColor.rgb * rtWave * rtZone * brightness * 0.7;

    //=== alpha ===
    finalColor.a = saturate(baseBright * detail * 2.5 + photonRing * 0.5 + innerHeat + outerHalo) * input.Color.a;

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
