// ============================================================================
//GlacierSpike.fx 冰川晶柱
//UV.x 0左→1右(0.5中轴) UV.y 0尖→1底
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uSeed;    //每根冰柱的随机种子，错开晶面与流光
float uGlow;    //内芯发光强度 0~1，生长瞬间最亮

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
    float axisDist = abs(uv.x - 0.5);

    //冰柱轮廓：尖端收窄、底部展宽，噪声啃出参差冰棱
    float edgeNoise = tex2D(noiseSamp, float2(uv.y * 1.7 + uSeed, uSeed * 0.37)).r;
    float halfWidth = lerp(0.045, 0.46, pow(uv.y, 1.35));
    halfWidth += (edgeNoise - 0.5) * 0.16 * uv.y;

    float body = smoothstep(halfWidth, halfWidth - 0.07, axisDist);
    if (body <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    //晶面：量化噪声形成棱面明暗切割
    float facetRaw = tex2D(noiseSamp, float2(uv.x * 2.6 + uSeed * 3.1, uv.y * 1.9 + uSeed)).r;
    float facet = floor(facetRaw * 5.0) / 5.0;

    //内部缓流光：冰芯中缓慢上涌的寒光
    float flow = tex2D(noiseSamp, float2(uv.x * 1.3 + uSeed, uv.y * 1.1 - uTime * 0.22)).r;
    float innerGlow = smoothstep(0.40, 0.85, flow) * smoothstep(0.34, 0.05, axisDist);

    //棱缘白边：轮廓边缘的菲涅尔式高光
    float rim = smoothstep(halfWidth - 0.11, halfWidth - 0.025, axisDist);

    //尖端炽亮
    float tip = smoothstep(0.30, 0.0, uv.y);

    //颜色：深渊蓝 → 冰川青 → 白炽
    float3 cDeep = float3(0.05, 0.16, 0.38);
    float3 cIce = float3(0.30, 0.62, 0.92);
    float3 cGlow = float3(0.85, 0.97, 1.00);

    float3 color = lerp(cDeep, cIce, facet * 0.75 + 0.15);
    color += cIce * innerGlow * (0.45 + uGlow * 0.55);
    color = lerp(color, cGlow, rim * 0.65);
    color += cGlow * tip * (0.35 + uGlow * 0.4);

    float alpha = body * (0.62 + facet * 0.18 + innerGlow * 0.25);
    alpha = saturate(alpha + rim * 0.3 + tip * 0.25);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass GlacierSpikePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
