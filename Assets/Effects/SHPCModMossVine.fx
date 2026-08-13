// ============================================================================
//SHPCModMossVine.fx 苔藓藤蔓纤维
//Trail 条带 预乘输出+AlphaBlend;uv.x 0=根 1=梢
//s1=PerlinNoise(LinearWrap),消费端 Textures[1] 于 Apply 前绑定
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;      //整体透明度
float3 coreColor;     //饱和苔绿
float3 glowColor;     //暗湿绿

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
    float along = uv.x;                 //0=根 1=梢
    float crossPos = uv.y;
    float crossDist = abs(crossPos - 0.5) * 2.0;

    //低频形体噪声+高频纤维噪声
    float n1 = tex2D(noiseSamp, float2(along * 1.7 + uTime * 0.06, crossPos * 0.35 + 0.13)).r;
    float n2 = tex2D(noiseSamp, float2(along * 4.6 - uTime * 0.04, crossPos * 0.9 + 0.57)).g;

    //边缘毛口,噪声咬开轮廓
    float band = 1.0 - smoothstep(0.42, 1.0, crossDist + (n1 - 0.5) * 0.55);

    //梢端蚀散,根部完整
    float tipErode = 1.0 - smoothstep(0.62, 1.02, along + (n2 - 0.5) * 0.3);

    //双股纤维,沿带两条亚股,噪声断续
    float dA = (crossPos - 0.30) * 7.5;
    float dB = (crossPos - 0.68) * 7.5;
    float strandA = exp2(-dA * dA);
    float strandB = exp2(-dB * dB);
    float breakup = 0.55 + 0.45 * n2;
    float fiber = saturate(strandA + strandB) * breakup;

    //湿光斑,稀疏微亮非常驻白
    float moist = step(0.88, n1 * 0.5 + n2 * 0.5) * band * 0.55;

    //叶绿渐变,暗缘到饱和绿芯
    float3 col = lerp(glowColor * 0.55, coreColor, saturate(fiber * 0.85 + 0.18));
    col += coreColor * moist * 0.8;
    col *= 0.85 + 0.3 * (1.0 - along);  //根部略实

    float alpha = band * tipErode * (0.30 + 0.62 * fiber + moist * 0.4);
    alpha *= fadeAlpha;

    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass MossVinePass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
