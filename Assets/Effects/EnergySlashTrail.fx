// ============================================================================
//EnergySlashTrail.fx 能量剑刀光
//Trail 条带 Additive
//UV.x 1=最新 0=尾 UV.y 0=外缘 1=内缘
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //能量充盈度 0~1，由武器充能与终结斩驱动

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float age = uv.x; //1=最新 越大越亮

    //等离子流：沿刃口高速滚动的双层噪声
    float n1 = tex2D(noiseSamp, float2(uv.x * 2.2 - uTime * 2.6, uv.y * 0.9 + uTime * 0.2)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 4.5 + uTime * 1.1, uv.y * 2.2 - uTime * 1.4)).r;
    float plasma = n1 * 0.6 + n2 * 0.4;

    //外缘：等离子体微微啃噬，但整体保持科技感的锐利
    float edgeBite = (plasma - 0.5) * 0.12;
    float outerMask = smoothstep(0.015 + edgeBite, 0.14 + edgeBite, uv.y);
    //内缘朝持握者快速渐隐（短剑刀光紧凑）
    float innerMask = smoothstep(1.0, 0.38, uv.y);

    //尾迹老化：能量剑余晖消散得快，干净利落
    float ageMask = smoothstep(0.0, 0.62, age);
    ageMask *= ageMask * ageMask;

    float intensity = outerMask * innerMask * ageMask;

    //横向扫描线：龙颅科技的数字感细纹
    float scanline = 0.86 + 0.14 * sin(uv.y * 46.0 - uTime * 9.0);
    intensity *= scanline;

    //等离子细丝：窄阈值噪声脊线
    float filament = smoothstep(0.60, 0.82, plasma) * intensity;

    //刃口热芯：只在充能充足时点亮
    float hotCore = smoothstep(0.66, 1.0, age) * smoothstep(0.30, 0.04, uv.y)
                  * outerMask * uHeat;

    //颜色：暗红 → 等离子红 → 橙白
    float3 cDark = float3(0.28, 0.05, 0.04);
    float3 cMain = float3(1.00, 0.32, 0.18);
    float3 cGlow = float3(1.00, 0.85, 0.62);

    //充能不足时主体色衰减到残光
    float energyLevel = 0.35 + uHeat * 0.65;

    float3 color = cDark * intensity * 1.3;
    color += cMain * intensity * 0.5 * energyLevel;
    color = lerp(color, cMain * energyLevel, filament * 0.8);
    color += cGlow * hotCore;
    color += cGlow * filament * uHeat * 0.4;

    float alpha = saturate(intensity * (0.55 + energyLevel * 0.35) + filament * 0.3 + hotCore * 0.6);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass EnergySlashTrailPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
