// ============================================================================
// NeutronSlashTrail.fx —— 中子偃月刀刀光着色器
// 深空引力弧光：深空紫雾底 + 引力拖曳暗纹 + 闪烁星屑 + 白紫刃口芯
// 回环重劈（uHeat 高）时星屑密度与亮度提升，如同拖出一条星河
// uv.x: 1=刀口最新位置 → 0=最旧尾迹
// uv.y: 0=刀尖外缘 → 1=持握者内缘
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //引力强度 0~1，回环重劈时提升

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

    //深空尘埃流：缓慢沉稳的双层噪声
    float n1 = tex2D(noiseSamp, float2(uv.x * 1.4 - uTime * 0.8, uv.y * 0.7 + uTime * 0.1)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 2.8 + uTime * 0.35, uv.y * 1.6 - uTime * 0.5)).r;
    float dust = n1 * 0.65 + n2 * 0.35;

    //外缘：引力场平滑包裹，仅有轻微扰动
    float edgeBite = (dust - 0.5) * 0.15;
    float outerMask = smoothstep(0.02 + edgeBite, 0.17 + edgeBite, uv.y);
    //内缘朝持握者缓慢渐隐（长柄武器弧光宽厚）
    float innerMask = smoothstep(1.0, 0.26, uv.y);

    //尾迹老化：引力残留消散得慢，星河绵长
    float ageMask = smoothstep(0.0, 0.45, age);
    ageMask = ageMask * ageMask * (3.0 - 2.0 * ageMask);

    float intensity = outerMask * innerMask * ageMask;

    //引力拖曳暗纹：沿弧光方向被拉伸弯曲的条带
    float lane = 0.82 + 0.18 * sin((uv.y * 9.0 + dust * 2.6 - uTime * 1.2) * 3.14159);
    intensity *= lane;

    //星屑：超高频噪声双重阈值产生闪点，随时间闪烁
    float starNoise = tex2D(noiseSamp, float2(uv.x * 9.0 + 31.7, uv.y * 8.0 - 17.3)).r;
    float twinkle = 0.55 + 0.45 * sin(uTime * 6.0 + starNoise * 40.0);
    float star = smoothstep(0.82 - uHeat * 0.06, 0.93, starNoise) * twinkle * ageMask * innerMask;

    //尘埃丝缕
    float filament = smoothstep(0.58, 0.85, dust) * intensity;

    //刃口白紫芯
    float hotCore = smoothstep(0.72, 1.0, age) * smoothstep(0.30, 0.04, uv.y)
                  * outerMask * (0.55 + uHeat * 0.45);

    //颜色：深空紫 → 紫罗兰 → 白紫
    float3 cDark = float3(0.10, 0.04, 0.26);
    float3 cMain = float3(0.52, 0.31, 1.00);
    float3 cGlow = float3(0.86, 0.78, 1.00);

    float3 color = cDark * intensity * 1.3;
    color += cMain * intensity * 0.45;
    color = lerp(color, cMain, filament * 0.8);
    color += cGlow * hotCore;
    color += cGlow * star * (0.9 + uHeat * 0.6);
    color += cGlow * filament * (0.2 + uHeat * 0.35);

    float alpha = saturate(intensity * 0.85 + filament * 0.3 + hotCore * 0.6 + star * 0.55);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass NeutronSlashTrailPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
