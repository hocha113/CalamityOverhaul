// 无头鬼影本体：Shutter Alpha 提供无头人形，噪声只负责边缘剥落与内部阴影流动。
// 输出为预乘 Alpha，配合 BlendState.AlphaBlend 形成吸光黑影，而不是发光叠层。

float4x4 transformMatrix;
float uTime;
float uOpacity;
float uDissolve;
float uPhase;
float uSeed;

texture uShutterTex;
sampler shutterSamp = sampler_state
{
    texture = <uShutterTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

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
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput input)
{
    PSInput output;
    output.Position = mul(input.Position, transformMatrix);
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    return output;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float shape = tex2D(shutterSamp, uv).a;

    float n0 = tex2D(noiseSamp, float2(uv.x * 2.10 + uSeed, uv.y * 1.55 - uTime * 0.10)).r;
    float n1 = tex2D(noiseSamp, float2(uv.x * 4.70 - uTime * 0.16 + uSeed * 2.73,
        uv.y * 3.40 + uTime * 0.07)).r;
    float noise = n0 * 0.67 + n1 * 0.33;

    float lowerFray = smoothstep(0.43, 0.98, uv.y);
    float sideFray = smoothstep(0.18, 0.48, abs(uv.x - 0.5));
    float erosion = uDissolve * (0.10 + lowerFray * 0.52 + sideFray * 0.16)
        * (0.30 + noise * 0.70);
    float field = shape - erosion;
    float body = smoothstep(0.035, 0.33, field);

    float inner = smoothstep(0.34, 0.72, field);
    float tornEdge = saturate(body - inner);
    float grain = saturate((noise - 0.30) * 1.45) * body;
    float phaseBeat = saturate(uPhase);

    float3 shadowCore = float3(0.010, 0.008, 0.018);
    float3 shadowGrain = float3(0.036, 0.030, 0.070);
    float3 edgeDormant = float3(0.075, 0.060, 0.135);
    float3 edgeStriking = float3(0.175, 0.105, 0.255);
    float3 edgeColor = lerp(edgeDormant, edgeStriking, phaseBeat);

    float3 color = shadowCore;
    color = lerp(color, shadowGrain, grain * 0.30);
    color += edgeColor * tornEdge * (0.42 + phaseBeat * 0.58);

    float opacity = saturate(uOpacity * input.Color.a);
    float alpha = saturate(body * opacity * (0.74 + grain * 0.18));
    return float4(color * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}