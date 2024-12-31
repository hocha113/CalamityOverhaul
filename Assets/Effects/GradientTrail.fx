matrix transformMatrix;
float uTime;
float uTimeG;
float udissolveS;
texture uBaseImage;
texture uFlow;
texture uGradient;
texture uDissolve;

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

sampler2D gradientTex = sampler_state
{
    texture = <uGradient>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

sampler2D dissolveTex = sampler_state
{
    texture = <uDissolve>;
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
    output.Position = mul(input.Position, transformMatrix);

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float xcoord = input.TexCoords.x;
    float ycoord = input.TexCoords.y;

    float2 st = float2((xcoord + uTime) % 1.0, ycoord);
    
    float4 baseC = tex2D(baseTex, input.TexCoords).xyzw;
    float4 flowC = tex2D(flowTex, st).xyzw;
    
    float a = baseC.r + baseC.r * flowC.r;
    
    float4 gradientC = tex2D(gradientTex, float2(clamp(baseC.r, 0.01, 0.99), 0.5)).xyzw;
    float4 gradientC2 = tex2D(gradientTex, float2(clamp(1 - a, 0.01, 0.99), 0.5)).xyzw;

    st.r = (xcoord * 3 + 0.5 + uTime) % 1.0;
    float4 dissolveC = tex2D(dissolveTex, st).xyzw;
    
    float f = input.TexCoords.x;
    f = f * f;
    
    a = lerp(a, 0, (1 - f) * dissolveC.r);
    
    gradientC = (gradientC + gradientC2) / 2;
    gradientC.a *= a * input.Color.a;
    
    return gradientC;
}

technique Technique1
{
    pass GradientTrailPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}