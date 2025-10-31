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

// �������Բ�ֵ����
float InverseLerp(float a, float b, float t)
{
    return saturate((t - a) / (b - a));
}

// ��ǻ����ɫ�� - ����ɫ����
float3 sulfurFirePalette(float noise)
{
    // ������ǻ����ɫ�ݶ� - ����쵽���Ⱥ�
    float3 deepRedColor = float3(0.35, 0.08, 0.05) * colorMult; // ���ɫ
    float3 crimsonColor = float3(0.75, 0.15, 0.12) * colorMult; // �ɺ�ɫ
    float3 sulfurOrange = float3(0.95, 0.35, 0.18) * colorMult; // ��ǳ�ɫ
    float3 brightCore = float3(1.0, 0.55, 0.25) * colorMult; // ��������
    
    float3 fireColor;
    // ʹ�ø����ӵĽ�����������ǻ�Ч��
    if (noise < 0.3)
    {
        // ������� - ���ɫ���ɺ�ɫ
        fireColor = lerp(deepRedColor, crimsonColor, noise / 0.3);
    }
    else if (noise < 0.65)
    {
        // �м����� - �ɺ�ɫ����ǳ�ɫ
        fireColor = lerp(crimsonColor, sulfurOrange, (noise - 0.3) / 0.35);
    }
    else
    {
        // ���������� - ��ǳ�ɫ����������
        fireColor = lerp(sulfurOrange, brightCore, (noise - 0.65) / 0.35);
    }
    
    // ���ӶԱȶȺͱ��Ͷ�
    fireColor = pow(fireColor, float3(1.8, 2.2, 2.5));
    
    // �����ǻ����еĻԹ�Ч��
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
    
    // ���ػ�UV���� - ������ֲڵ���ǻ�����
    float2 pixelatedUV = worldUV / screenSize;
    pixelatedUV.x -= worldUV.x % (1 / screenSize.x);
    pixelatedUV.y -= worldUV.y % (1 / (screenSize.y / 2) * 2);
    
    // ������������ - ����Ȩ���Դ�������ҵ���ǻ�Ч��
    float noiseMesh1 = tex2D(upwardNoise, frac(pixelatedUV * 0.68 + float2(0, time * 0.18))).g;
    float noiseMesh2 = tex2D(upwardPerlinTex, frac(pixelatedUV * 1.35 + float2(0, time * 0.28))).g;
    float noiseMesh3 = tex2D(diagonalNoise, frac(pixelatedUV * 1.72 + float2(adjustedTime * 0.48, adjustedTime * 1.35))).g;
    float noiseMesh4 = tex2D(diagonalNoise, frac(pixelatedUV * 1.88 + float2(adjustedTime * -0.62, adjustedTime * 1.45))).g;
    
    // �������Ȩ������ǿ��ǻ�Ĳ�θ�
    float textureMesh = noiseMesh1 * 0.15 + noiseMesh2 * 0.25 + noiseMesh3 * 0.3 + noiseMesh4 * 0.3;
    
    // ��Ӷ�����Ŷ��Դ�����ǻ�Ĳ��ȶ���
    float turbulence = pow(abs(sin(worldUV.y * 0.1 + time * 0.5)), 2.0) * 0.15;
    textureMesh = saturate(textureMesh + turbulence);
    
    // ��ȡ���ص���ҵľ���
    float distToPlayer = distance(playerPosition, worldUV);
    // ���ݾ��������ȷ�Ĳ�͸����
    float opacity = burnIntensity;
    // ��ҽӽ�ʱ���ٵ���
    opacity += InverseLerp(800, 500, distToPlayer);
    
    // ����߽粢��ϻ���Ч����ʵ��ƽ������
    bool border = worldDistance < radius && opacity > 0;
    float colorMult = 1;
    if (border) 
        colorMult = InverseLerp(radius * 0.94, radius, worldDistance);
    opacity = clamp(opacity, 0, maxOpacity);
    
    // �����ɫ����δ�ı䣨�Ǳ߽����أ��Ҳ�͸����Ϊ0�����ڰ뾶��
    if (colorMult == 1 && (opacity == 0 || worldDistance < radius))
        return sampleColor;
    
    // Ӧ����ǻ��ɫ��
    float3 sulfurColor = sulfurFirePalette(textureMesh);
    
    // ��ӱ�Ե��˸Ч����ǿ��ǻ���Ӿ����
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