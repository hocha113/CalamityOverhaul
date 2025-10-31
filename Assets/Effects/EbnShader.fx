sampler noiseTex : register(s1);
float colorMult;        //��ɫ������
float time;             //ʱ����������ڶ���
float radius;           //Ч���뾶
float maxOpacity;       //���͸����
float burnIntensity;    //ȼ��ǿ��
float2 screenPosition;  //��Ļ����λ��
float2 screenSize;      //��Ļ�ߴ�
float2 setPoint;        //�趨�����꣨Ч�����ģ�

float InverseLerp(float a, float b, float t)
{
    return saturate((t - a) / (b - a));
}

//������ɫ��ɫ�庯��
//��������ֵ���ɻ�����ɫ��ģ��������
float3 firePalette(float noise)
{
    //�������������¶�ֵ��1500-3000K��Χ��
    float temperature = 1500. + 1500. * noise;

    //���������ɫ����������ؼ�ɫ
    float3 darkColor = float3(0.81, 0.45, 0.23) * colorMult;    //����ɫ��
    float3 midColor = float3(1., 0.75, 0.29) * colorMult;        //�Ȼ�ɫ��
    float3 brightColor = float3(1., 1., 0.95) * colorMult;     //����ɫ��
    
    //��������ֵ����ɫ֮���ֵ
    float3 fireColor;
    if (noise < 0.5)
        fireColor = lerp(darkColor, midColor, noise * 2.);
    else
        fireColor = lerp(midColor, brightColor, (noise - 0.5) * 2.);
    
    //Ӧ��������ȷ�ĺ�����乫ʽ
    //ʹ�����ʿ˶��ɵļ򻯰汾
    fireColor = pow(fireColor, float3(5, 5, 5)) * (exp(1.43876719683e5 / (temperature * fireColor)) - 1.);

    //Ӧ��ɫ��ӳ���Ի��������ɫ
    return 1. - exp(-1.3e8 / fireColor);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    //��������ռ�UV����
    float2 worldUV = screenPosition + screenSize * uv;
    
    //Ԥ�����������ڱ�����ԭʼ����ļ����ԣ�
    float2 provUV = setPoint / screenSize;
    
    //�������ص�Ч�����ĵľ���
    float worldDistance = distance(worldUV, setPoint);
    
    //ʱ���������ӣ����ڿ��ƶ����ٶ�
    float adjustedTime = time * 0.1;
    
    //==================================================
    //���ػ�UV���괦��
    //�����������ط��Ļ���Ч��
    //==================================================
    float2 pixelatedUV = worldUV / screenSize;
    
    //X�����ػ�����
    pixelatedUV.x -= worldUV.x % (1 / screenSize.x);
    
    //Y�����ػ�����ʹ��2��������
    pixelatedUV.y -= worldUV.y % (1 / (screenSize.y / 2) * 2);
    
    //==================================================
    //�����������
    //ͨ�����Ӳ�ͬƵ�ʺ��ٶȵ������������ӵĻ�������
    //==================================================
    
    //��һ�㣺���ٴ�߶�����
    float noiseMesh1 = tex2D(noiseTex, frac(pixelatedUV * 0.58 + float2(0, time * 0.25))).g;
    
    //�ڶ��㣺�����г߶�����
    float noiseMesh2 = tex2D(noiseTex, frac(pixelatedUV * 1.57 + float2(0, time * 0.35))).g;
    
    //�����㣺������ת����
    float noiseMesh3 = tex2D(noiseTex, frac(pixelatedUV * 1.46 + float2(adjustedTime * 0.56, adjustedTime * 1.2))).g;
    
    //���Ĳ㣺������ת����
    float noiseMesh4 = tex2D(noiseTex, frac(pixelatedUV * 1.57 + float2(adjustedTime * -0.56, adjustedTime * 1.2))).g;
    
    //��Ȩ�������������
    //Ȩ�ط��䣺12.5% + 20% + 35% + 35% = 102.5% (��΢���Ȼ������ǿЧ��)
    float textureMesh = noiseMesh1 * 0.125 + noiseMesh2 * 0.2 + noiseMesh3 * 0.35 + noiseMesh4 * 0.35;
    
    //==================================================
    //��͸���ȼ���
    //���ھ����ȼ��ǿ��
    //==================================================
    float distToPlayer = distance(setPoint, worldUV);
    
    //��ʼ��͸���� = ȼ��ǿ��
    float opacity = burnIntensity;
    
    //���ݾ��������͸���ȣ�����Խ������͸����Խ�ߣ�
    //��500-800���ط�Χ�ڽ��н���
    opacity += InverseLerp(800, 500, distToPlayer);
    
    //==================================================
    //�߽紦�����ɫ˥��
    //==================================================
    
    //�ж��Ƿ���Ч���߽���
    bool border = worldDistance < radius && opacity > 0;
    
    //�����Ե��������
    float colorMult = 1;
    if (border) 
        colorMult = InverseLerp(radius * 0.94, radius, worldDistance);
    
    //���Ʋ�͸�����ں���Χ��
    opacity = clamp(opacity, 0, maxOpacity);
    
    //==================================================
    //�����˳��Ż�
    //�������Ҫ��Ⱦ����Ч����ֱ�ӷ���ԭʼ��ɫ
    //==================================================
    if (colorMult == 1 && (opacity == 0 || worldDistance < radius))
        return sampleColor;
    
    //==================================================
    //������ɫ�ϳ�
    //���ɻ�����ɫ��Ӧ�ñ�Ե����Ͳ�͸����
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