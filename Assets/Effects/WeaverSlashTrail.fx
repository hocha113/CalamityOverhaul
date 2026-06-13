// ============================================================================
// WeaverSlashTrail.fx 纠缠之怨刀光
// Trail 条带 Additive
// UV.x 1=最新 0=尾 UV.y 0=外缘 1=内缘
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //炽热度 0~1，终结斩时提升白芯与丝线亮度

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
    float age = uv.x; //1=最新 越大越亮

    //双层流动噪声：怨魂气流沿挥砍方向滚动
    float n1 = tex2D(noiseSamp, float2(uv.x * 1.6 - uTime * 1.1, uv.y * 0.8 + uTime * 0.13)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 3.1 + uTime * 0.45, uv.y * 1.7 - uTime * 0.7)).r;
    float flow = n1 * 0.65 + n2 * 0.35;

    //刀尖外缘：锐利但被噪声啃出撕痕
    float edgeBite = (flow - 0.5) * 0.24;
    float outerMask = smoothstep(0.02 + edgeBite, 0.18 + edgeBite, uv.y);
    //内缘朝持握者渐隐
    float innerMask = smoothstep(1.0, 0.30, uv.y);

    //尾迹老化
    float ageMask = smoothstep(0.0, 0.55, age);
    ageMask *= ageMask;

    float intensity = outerMask * innerMask * ageMask;

    //灵魂丝线：噪声脊线形成的细丝高光
    float filament = smoothstep(0.56, 0.86, flow) * intensity;

    //刀口白热芯：仅最新边缘的刀尖侧
    float hotCore = smoothstep(0.72, 1.0, age) * smoothstep(0.32, 0.05, uv.y)
                  * outerMask * (0.55 + uHeat * 0.45);

    //颜色：暗怨红 → 魂粉 → 亮白粉
    float3 cDark = float3(0.30, 0.10, 0.20);
    float3 cMain = float3(0.78, 0.33, 0.50);
    float3 cGlow = float3(1.00, 0.78, 0.88);

    float3 color = cDark * intensity * 1.2;
    color += cMain * intensity * 0.45;
    color = lerp(color, cMain, filament * 0.85);
    color += cGlow * hotCore;
    color += cGlow * filament * (0.25 + uHeat * 0.45);

    float alpha = saturate(intensity * 0.85 + filament * 0.35 + hotCore * 0.6);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass WeaverSlashTrailPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
