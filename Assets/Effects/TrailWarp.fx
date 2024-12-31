texture uBaseImage;
texture uFlow;
matrix uTransform;
float uTime;

sampler2D baseTex = sampler_state
{
    texture = <uBaseImage>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

sampler2D flowTex = sampler_state
{
    texture = <uFlow>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VertexShaderInput
{
    float4 Position : POSITION;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    output.Position = mul(input.Position, uTransform);

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float4 c = tex2D(baseTex, input.TexCoords);
    float4 c2 = tex2D(flowTex, float2(input.TexCoords.x *3 + uTime, input.TexCoords.y));
    
    float a = c.r - c.r* c2.r;
    
    return float4(input.Color.r, input.Color.g * a, input.Color.b, input.Color.a * a);
}

technique Technique1
{
    pass TrailWarpPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}