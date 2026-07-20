// ============================================================================
//FishJewelTrail.fx 虹彩序曲宝石窄条带（主宝石/碎片共用，六色调色板由 C# 传入）
//材质：切割宝石的折射余光而非能量束。磨光硬边剖面 + 沿带离散棱面闪点 + 尾端蚀成碎晶断齿
//uv.x: 0=头端(oldPos[0] 最新) 1=尾端；像素内翻转为 along(1=头 0=尾)。uv.y: 0..1 跨带
//顶点色承载 C# 端透明度包络。Additive 输出（DrawTrailFromOldPos 设 BlendState.Additive）
//极角审计：无 atan2/theta/phi 消费，全笛卡尔 uv + wrap 贴图采样，无缝隙风险
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //流动相位（含每弹幕相位偏移）
float3 uColDark;  //深宝石色（外缘暗部）
float3 uColMid;   //饱和主色（中带）
float3 uColGlint; //玻白闪色（离散闪点与头端小热芯）

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
    float across = abs(uv.y * 2.0 - 1.0); //0 中心 1 边缘

    //磨光硬边剖面：比火焰带利落的切石缘
    float profile = smoothstep(1.0, 0.62, across);

    //尾端蚀断：门限向尾收紧，噪声决定碎晶断齿形状
    float n1 = tex2D(noiseSamp, float2(along * 1.8 - uTime * 0.7, uv.y * 0.4 + 0.71)).r;
    float erode = smoothstep(along - 0.32, along + 0.06, n1 * 0.86 + 0.07);

    //沿带离散棱面闪点：稀疏噪声胞元锐化成点状反光，缓缓向尾漂移
    float cells = tex2D(noiseSamp, float2(along * 3.1 + uTime * 0.45, uv.y * 0.55 + 0.19)).r;
    float glint = pow(saturate(cells), 7.0);

    float headBoost = smoothstep(0.45, 1.0, along);
    float body = profile * erode;

    //色阶：深宝石外缘 → 饱和中带 → 头端小热芯（玻白只出现在小面积）
    float coreT = pow(profile, 3.0);
    float3 col = lerp(uColDark, uColMid, saturate(profile * 1.55));
    col = lerp(col, uColGlint, saturate(coreT * headBoost * 0.42));
    //离散闪点提亮：只落在带体内，偏头端更亮
    col += uColGlint * (glint * body * (0.35 + 0.55 * headBoost));

    float alpha = body * (0.26 + 0.74 * headBoost);

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
