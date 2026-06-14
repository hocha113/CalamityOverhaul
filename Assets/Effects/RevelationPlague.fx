// ============================================================================
//RevelationPlague.fx 启示录瘟疫 NPC 滤镜
//病绿重映射+静脉脉冲+边缘辉光；采样 uImage0
// ============================================================================

sampler uImage0 : register(s0);

float2 texelSize;     //1/宽 1/高
float intensity;      //0~1
float progress;       //0~1 腐朽进度
float uTime;          //静脉脉冲相位

float hash(float2 p)
{
    float h = dot(p, float2(127.1, 311.7));
    return frac(sin(h) * 43758.5453);
}

float4 RevelationPlaguePass(float2 coords : TEXCOORD0, float4 smpColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float plagueStrength = saturate(intensity);

    //微扰动：让瘟疫表面有轻微流动感
    float2 distorted = uv;
    float waveX = sin(uv.y * 60.0 + uTime * 4.0) * 0.0008 * plagueStrength;
    float waveY = cos(uv.x * 50.0 - uTime * 3.2) * 0.0008 * plagueStrength;
    distorted += float2(waveX, waveY);

    float4 texColor = tex2D(uImage0, distorted);
    if (texColor.a < 0.01)
        return float4(0, 0, 0, 0);

    float4 color = texColor * smpColor;
    float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));

    //边缘检测
    float aR = tex2D(uImage0, distorted + float2(texelSize.x * 2.0, 0)).a;
    float aL = tex2D(uImage0, distorted - float2(texelSize.x * 2.0, 0)).a;
    float aU = tex2D(uImage0, distorted + float2(0, texelSize.y * 2.0)).a;
    float aD = tex2D(uImage0, distorted - float2(0, texelSize.y * 2.0)).a;
    float neighbor = (aR + aL + aU + aD) * 0.25;
    float edge = saturate(1.0 - neighbor);

    //静脉纹理
    float vein1 = sin((uv.x + uv.y) * 80.0 + uTime * 5.0);
    float vein2 = cos((uv.x - uv.y) * 65.0 - uTime * 4.0);
    float veinMask = smoothstep(0.55, 0.9, vein1 * 0.55 + vein2 * 0.45);
    float pulse = 0.7 + 0.3 * sin(uTime * 6.0 + lum * 8.0);

    //主颜色重映射
    float3 sickDark = float3(0.08, 0.16, 0.04);
    float3 sickMid = float3(0.28, 0.55, 0.10);
    float3 sickBright = float3(0.78, 0.95, 0.32);
    float3 plagueTint = lum < 0.45
        ? lerp(sickDark, sickMid, lum / 0.45)
        : lerp(sickMid, sickBright, saturate((lum - 0.45) / 0.55));

    float3 result = lerp(color.rgb, plagueTint, plagueStrength * 0.7);

    //病灶静脉
    result += float3(0.35, 0.75, 0.12) * veinMask * pulse * plagueStrength * 0.35;

    //边缘病态辉光
    result += float3(0.45, 0.95, 0.18) * edge * plagueStrength * 0.45;

    //腐朽暗斑
    float blot = hash(floor(uv * 64.0) + floor(uTime * 2.0));
    float blotMask = smoothstep(0.7, 0.9, blot) * plagueStrength * (0.4 + progress * 0.6);
    result = lerp(result, result * float3(0.55, 0.7, 0.5), blotMask * 0.35);

    result = saturate(result);
    return float4(result, color.a);
}

technique RevelationPlaguePassTech
{
    pass P0
    {
        PixelShader = compile ps_3_0 RevelationPlaguePass();
    }
}
