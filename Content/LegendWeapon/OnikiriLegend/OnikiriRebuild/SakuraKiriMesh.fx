float4x4 Transform;
sampler Source : register(s0);
float2 TextureScale;
float2 TextureOffset;

float4 tint; 
float GlowMask;
float GlowPower;

struct Input { float3 Position : POSITION0; float2 UV : TEXCOORD0; };
struct Output { float4 Position : POSITION0; float2 UV : TEXCOORD0; };

Output VS(Input input)
{
    Output output;
    output.Position = mul(float4(input.Position, 1), Transform);
    output.UV = input.UV;
    return output;
}

float4 PS(Output input) : COLOR0
{
    float2 uv = (input.UV - 0.5) * TextureScale + 0.5 + TextureOffset;
    float4 color = tex2D(Source, uv);
    float edge = saturate(1 - abs(input.UV.y * 2 - 1));
    if (GlowMask > 1.5)
    {
        float halo = pow(edge, GlowPower);
        return float4(tint.rgb, halo * tint.a);
    }
    if (GlowMask > .5)
    {
        float glow = pow(edge, GlowPower);
        return float4(tint.rgb, glow * tint.a);
    }
    return color * tint;
}

technique SakuraKiri
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}
