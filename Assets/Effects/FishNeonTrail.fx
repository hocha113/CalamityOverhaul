// ============================================================================
//FishNeonTrail.fx 霓虹足迹青-品红荧光缎带（NeonTetraLightProjectile 尾迹专用）
//生物荧光纪律：高饱和低明度双色，无纯白；亮度结构=深渊暗外圈/饱和中层/细热芯（同色提亮封顶）
//热芯与整体随 uBreath 呼吸起伏，uFade 承载入场/退场包络（缎带随鱼化现/消散，禁 pop）
//uv.x: 0=头端(最新，oldPos[0] 侧) 1=尾端(最旧)；像素内翻转为 along（1=头 0=尾）
//uv.y: 0..1 跨带。顶点色承载 C# 端透明度包络
//极角审计：无 atan2/theta/phi 消费，全部笛卡尔 uv+贴图采样，无缝隙风险
//Additive 输出（GraniteMarbleVFX.DrawTrailFromOldPos 设 BlendState.Additive）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;       //流动相位（含每鱼相位偏移）
float uBreath;     //呼吸 0..1，荧光节律
float uFade;       //生命包络 0..1
float3 uColCyan;   //深海青（饱和低明度）
float3 uColMagenta;//品红（饱和低明度）
float3 uColAbyss;  //深渊暗蓝（外圈压底）

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
    //along: 1=头端(最新) 0=尾端(最旧)
    float along = 1.0 - uv.x;

    //跨带抛物线剖面：中心 1 → 两缘 0
    float across = uv.y * 2.0 - 1.0;
    float profile = saturate(1.0 - across * across);

    //沿带缓流噪声：低频云絮 + 高频断丝，反向流速制造荧光涌动
    float n1 = tex2D(noiseSamp, float2(along * 1.3 - uTime * 0.7, uv.y * 0.5)).r;
    float n2 = tex2D(noiseSamp, float2(along * 3.1 + uTime * 0.4, uv.y * 1.2 + 0.43)).r;

    //尾端侵蚀：尾迹散成浮游断絮，禁平滑收口
    float erode = smoothstep(along - 0.36, along + 0.10, n1 * 0.84 + 0.08);
    float body = profile * erode;

    //色相沿带+时间滑移：青↔品红，n2 微扰让分界呈云纹而非硬带
    float hueT = 0.5 + 0.5 * sin(along * 4.6 - uTime * 1.6 + n2 * 1.3);
    float3 mid = lerp(uColCyan, uColMagenta, hueT);

    //亮度结构：深渊暗外圈 → 饱和中层 → 细热芯（同色提亮，单通道饱和保色相不发白）
    float3 col = lerp(uColAbyss, mid, saturate(profile * 1.55));
    float coreT = pow(profile, 6.0) * (0.45 + 0.55 * n2);
    float3 coreCol = mid * (1.35 + 0.55 * uBreath);
    col = lerp(col, coreCol, coreT * (0.30 + 0.45 * uBreath));

    //头端略实 + 整体呼吸压暗：荧光节律，最亮态也不满幅
    float headBoost = 0.45 + 0.55 * smoothstep(0.12, 0.92, along);
    float alpha = body * headBoost * uFade * (0.42 + 0.58 * uBreath);

    //Additive：预乘颜色，顶点色承载包络
    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
