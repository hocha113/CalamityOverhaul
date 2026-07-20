// ============================================================================
//HeartcarverRibbon.fx 刻心者冲刺血刃条带
//TriangleStrip Additive：宽度沿尾递减的位置条带，替代 sprite 残影
//UV.x 0=最新头部 1=尾端 UV.y 0~1 横截面
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade; //整体透明度 0~1

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
    float cross = abs(uv.y - 0.5) * 2.0;

    //双层流动噪声：血浆沿冲刺方向回卷
    float n1 = tex2D(noiseSamp, float2(uv.x * 1.8 - uTime * 1.3, uv.y * 0.7 + uTime * 0.11)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 3.4 + uTime * 0.5, uv.y * 1.6 - uTime * 0.8)).r;
    float flow = n1 * 0.65 + n2 * 0.35;

    //边缘被噪声啃出撕痕
    float edgeBite = (flow - 0.5) * 0.30;
    float band = smoothstep(1.0 + edgeBite, 0.45 + edgeBite, cross);

    //头部亮、尾部枯：尾端额外被噪声蒸发
    float ageMask = pow(saturate(1.0 - uv.x), 1.5);
    float tailErode = smoothstep(0.35, 0.9, uv.x) * (1.0 - flow);
    float intensity = band * ageMask * saturate(1.0 - tailErode);

    //白热芯：只活在头部中线
    float hotCore = smoothstep(0.40, 0.04, cross) * smoothstep(0.42, 0.0, uv.x);

    //血浆丝线
    float filament = smoothstep(0.56, 0.84, flow) * intensity;

    //颜色：干涸深红 → 动脉暗红 → 心肌粉白芯
    float3 cDeep = float3(0.22, 0.012, 0.04);
    float3 cMain = float3(0.66, 0.05, 0.10);
    float3 cGlow = float3(1.00, 0.82, 0.85);

    float3 color = cDeep * intensity * 1.2;
    color += cMain * intensity * 0.6;
    color = lerp(color, cMain * 1.3, filament * 0.8);
    color += cGlow * hotCore * 0.9;

    float alpha = saturate(intensity * 0.8 + filament * 0.3 + hotCore * 0.65) * uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass HeartcarverRibbonPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
