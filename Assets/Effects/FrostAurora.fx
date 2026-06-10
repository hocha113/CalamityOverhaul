// ============================================================================
// FrostAurora.fx —— 万象霜天·极光霜幕着色器
// 在天空展开的极光垂帘：噪声驱动的光柱列 + 顶亮底羽化 + 青绿向霜紫渐变
// uv.x: 0=幕左缘 → 1=幕右缘
// uv.y: 0=幕顶 → 1=幕底（羽尾）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1（展开/消散渐变）
float uSeed;    //随机种子，错开多次召唤的光柱分布

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

    //幕体横向波动：整面光幕像绸缎一样缓慢摆动
    float sway = tex2D(noiseSamp, float2(uv.x * 0.9 + uTime * 0.05, uSeed)).r;
    float xWave = uv.x + (sway - 0.5) * 0.10;

    //光柱列：低频噪声决定每列亮度，缓慢横向漂移
    float column = tex2D(noiseSamp, float2(xWave * 2.3 - uTime * 0.06, uSeed * 0.61)).r;
    column = smoothstep(0.18, 0.85, column);

    //细丝微光：高频噪声的流动丝缕
    float filament = tex2D(noiseSamp, float2(xWave * 6.5 + uTime * 0.18, uv.y * 0.8 - uTime * 0.10)).r;
    filament = smoothstep(0.55, 0.95, filament);

    //纵向衰减：顶部最亮，向下羽化消散，羽尾边界被噪声扰动
    float fray = tex2D(noiseSamp, float2(xWave * 3.4 + uSeed, uTime * 0.07)).r;
    float bottomEdge = 0.62 + (fray - 0.5) * 0.42;
    float vertical = smoothstep(bottomEdge, 0.06, uv.y);
    float topCap = smoothstep(0.0, 0.08, uv.y);

    //横向羽化边缘
    float xFeather = smoothstep(0.0, 0.10, uv.x) * smoothstep(1.0, 0.90, uv.x);

    float intensity = column * vertical * topCap * xFeather;
    if (intensity <= 0.002)
    {
        return float4(0, 0, 0, 0);
    }

    //颜色：顶部冰青绿 → 中部霜蓝 → 羽尾霜紫
    float3 cTop = float3(0.45, 1.00, 0.85);
    float3 cMid = float3(0.35, 0.65, 1.00);
    float3 cTail = float3(0.60, 0.40, 0.95);

    float3 color = lerp(cTop, cMid, smoothstep(0.05, 0.45, uv.y));
    color = lerp(color, cTail, smoothstep(0.35, 0.85, uv.y));
    color += float3(0.8, 0.95, 1.0) * filament * vertical * 0.5;

    float alpha = saturate(intensity * (0.55 + filament * 0.35));
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass FrostAuroraPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
