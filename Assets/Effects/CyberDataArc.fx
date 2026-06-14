// ============================================================================
//CyberDataArc.fx 赛博数据电弧
//Trail 条带 Additive；采样 uNoiseTex
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;       //整体透明度
float3 coreColor;      //主光色
float3 glowColor;      //辉光色

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
    float along = uv.x;             //0=起点 1=末端
    float crossPos = uv.y;          //0=上边 1=下边
    float crossDist = abs(crossPos - 0.5) * 2.0;

    //噪声驱动的横向偏移：让弧体"颤抖"
    float n1 = tex2D(noiseSamp, frac(float2(along * 6.0 + uTime * 3.0, 0.0))).r;
    float jitter = (n1 - 0.5) * 0.18;
    float effDist = saturate(crossDist + jitter);

    //核心白热芯
    float coreW = 0.10;
    float core = 1.0 - smoothstep(0.0, coreW, effDist);
    core = pow(saturate(core), 1.4);
    float coreFlicker = 0.7 + 0.3 * sin(uTime * 28.0 + along * 40.0);
    core *= coreFlicker;

    //中层辉光
    float glow = 1.0 - smoothstep(0.05, 0.55, effDist);
    glow *= 0.85;

    //外层柔光
    float outer = 1.0 - smoothstep(0.4, 1.0, effDist);
    outer *= 0.4;

    //沿弧流动的数据条纹（白色高亮的小光点）
    float streamUV = frac(along * 14.0 - uTime * 4.0);
    float stream = smoothstep(0.0, 0.06, streamUV) * smoothstep(0.30, 0.10, streamUV);
    stream *= (1.0 - crossDist * 0.7);

    //端点收尾：让贴图在 along=0 与 along=1 处自然淡出
    float endsTaper = smoothstep(0.0, 0.06, along) * smoothstep(1.0, 0.94, along);

    float3 color = float3(0, 0, 0);
    color += coreColor * core * 1.4;
    color += glowColor * glow;
    color += glowColor * outer * 0.6;
    color += float3(1.0, 1.0, 1.0) * stream * 0.6;

    float alpha = saturate(core + glow * 0.6 + outer * 0.35 + stream * 0.4);
    alpha *= fadeAlpha * endsTaper;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DataArcPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
