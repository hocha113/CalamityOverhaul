// ============================================================================
//LonginusHelix.fx 朗基努斯双螺旋尾迹
//TriangleStrip 条带 Additive，两股缠绕由 C# 侧相位差 π 生成
//uv.x 0=枪头端 1=尾端；uv.y 0~1 横截；顶点 z=螺旋深度(0远侧 1近侧)
//uErode 0~1 尾先碎的侵蚀前沿；顶点色=股色调制
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uErode;
float uHot;

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
    float Depth : TEXCOORD1;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    float4 pos = float4(v.Position.x, v.Position.y, 0.0, 1.0);
    o.Position = mul(pos, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    o.Depth = v.Position.z;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float u = uv.x;
    float cross = abs(uv.y - 0.5) * 2.0;

    float n = tex2D(noiseSamp, float2(u * 2.1 - uTime * 0.55, uv.y * 0.6 + 0.29)).r;

    //宽度包络：头饱满向尾收细
    float widthEnv = lerp(0.85, 0.20, u);
    float core = smoothstep(widthEnv * 0.55, widthEnv * 0.08, cross);
    float sheath = smoothstep(widthEnv * 1.4, widthEnv * 0.28, cross);

    //头部软入与尾部衰减
    float headIn = smoothstep(0.0, 0.06, u);
    float tailFade = pow(saturate(1.0 - u), 0.5);

    //侵蚀：尾端先碎，噪声毛口
    float front = 1.0 - uErode * 1.18;
    float keep = 1.0 - smoothstep(front - 0.12, front + 0.10, u + (n - 0.5) * 0.22);

    //螺旋深度明暗：远侧压暗近侧提亮，立体缠绕线索
    float depthLum = lerp(0.40, 1.15, input.Depth);

    //沿带速度丝
    float filament = smoothstep(0.58, 0.86, n) * sheath * 0.35;

    float3 cHot = float3(1.28, 1.05, 0.85);

    float fade = headIn * tailFade * keep;
    float3 color = input.Color.rgb * (sheath * 0.95 + filament);
    color += cHot * core * (0.55 + uHot * 0.5);

    float alpha = saturate(sheath * 0.6 + core * 0.9) * fade * depthLum * input.Color.a;

    return float4(color * alpha, alpha);
}

technique Technique1
{
    pass LonginusHelixPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
