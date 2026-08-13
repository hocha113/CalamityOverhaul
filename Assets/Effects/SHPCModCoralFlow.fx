// ============================================================================
//SHPCModCoralFlow.fx 礁间水流带
//Trail 条带 预乘输出+AlphaBlend;uv.x 0=本锚 1=对端锚
//s1=PerlinNoise(LinearWrap),消费端 Textures[1] 于 Apply 前绑定
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;      //整体透明度
float3 coreColor;     //浅水青
float3 glowColor;     //深海青

sampler noiseSamp : register(s1);   //PerlinNoise,消费端绑定

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
    float along = uv.x;
    float crossPos = uv.y;
    float crossDist = abs(crossPos - 0.5) * 2.0;

    //双频水流噪声,慢速异向
    float n1 = tex2D(noiseSamp, float2(along * 1.6 - uTime * 0.09, crossPos * 0.5 + 0.21)).r;
    float n2 = tex2D(noiseSamp, float2(along * 3.3 + uTime * 0.05, crossPos * 0.8 + 0.66)).g;

    //水带轮廓,边缘轻噪
    float band = 1.0 - smoothstep(0.55, 1.0, crossDist + (n1 - 0.5) * 0.3);

    //两端收进锚点
    float ends = smoothstep(0.0, 0.07, along) * smoothstep(1.0, 0.93, along);

    //内部流纹,沿向漂移亮带
    float streak = smoothstep(0.45, 0.85, n1) * (1.0 - crossDist * 0.6);

    //表面张力边,近缘深色加重
    float meniscus = smoothstep(0.45, 0.95, crossDist);

    //稀疏白沫,靠边聚集
    float foam = step(0.90, n2) * smoothstep(0.25, 0.75, crossDist);

    float3 col = lerp(coreColor, glowColor, meniscus * 0.85);
    col += coreColor * streak * 0.45;
    col += float3(0.95, 0.98, 0.96) * foam * 0.6;

    float alpha = band * ends * (0.26 + streak * 0.30 + foam * 0.5 + meniscus * 0.12);
    alpha *= fadeAlpha;

    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass CoralFlowPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
