// ============================================================================
// FrostJudgment.fx 冬至审判光束
// UV.x 0枪口→1末端 UV.y 0.5中轴
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1（光束渐隐）
float uCharge;  //蓄力等级 0~1，提升冰芯宽度与色温
float uSeed;    //随机种子

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
    float axisDist = abs(uv.y - 0.5);

    //光束外缘被寒流噪声轻微侵蚀，呈现冻气缭绕感
    float erode = tex2D(noiseSamp, float2(uv.x * 2.8 - uTime * 1.6, uv.y * 1.4 + uSeed)).r;
    float edge = 0.5 - (erode - 0.5) * 0.16;

    float body = smoothstep(edge, edge - 0.22, axisDist);
    if (body <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    //首尾衰减：枪口快速亮起，末端羽散
    float head = smoothstep(0.0, 0.025, uv.x);
    float tail = smoothstep(1.0, 0.86, uv.x);
    float along = head * tail;

    //白炽冰芯：蓄力越满芯越宽
    float coreWidth = 0.10 + uCharge * 0.14;
    float core = smoothstep(coreWidth, 0.0, axisDist);

    //冰晶迸闪：高频噪声打点，沿光束高速流动
    float sparkle = tex2D(noiseSamp, float2(uv.x * 7.0 - uTime * 3.2 + uSeed, uv.y * 2.6 + uSeed * 1.7)).r;
    sparkle = smoothstep(0.78, 0.96, sparkle) * body;

    //寒气外晕的缓慢呼吸
    float breath = 0.85 + 0.15 * sin(uTime * 4.0 + uv.x * 9.0);

    //颜色：暗霜紫外缘 → 冰蓝中层 → 白炽核心
    float3 cOuter = float3(0.22, 0.18, 0.55);
    float3 cMid = float3(0.35, 0.65, 1.00);
    float3 cCore = float3(0.92, 0.98, 1.00);

    float3 color = cOuter * body;
    color = lerp(color, cMid, smoothstep(0.40, 0.12, axisDist));
    color = lerp(color, cCore, core * (0.75 + uCharge * 0.25));
    color += cCore * sparkle * 0.8;

    float alpha = saturate(body * 0.70 * breath + core * 0.45 + sparkle * 0.5);
    alpha *= along * uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass FrostJudgmentPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
