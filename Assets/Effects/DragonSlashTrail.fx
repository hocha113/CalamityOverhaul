// ============================================================================
//DragonSlashTrail.fx 龙藻巨刃刀光
//Trail 条带 Additive
//UV.x 1=最新 0=尾 UV.y 0=外缘 1=内缘
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //龙威炽热度 0~1，终结斩时提升鎏金刀口与丝线亮度

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

    //双层流动噪声：藻流沿挥砍方向滚动
    float n1 = tex2D(noiseSamp, float2(uv.x * 1.5 - uTime * 1.0, uv.y * 0.9 + uTime * 0.16)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 3.4 + uTime * 0.5, uv.y * 1.8 - uTime * 0.65)).r;
    float flow = n1 * 0.6 + n2 * 0.4;

    //龙鳞锯齿：高频三角波叠加噪声，把外缘啃成鳞片状
    float serration = abs(frac(uv.x * 9.0 + n1 * 0.8) - 0.5) * 2.0;
    float edgeBite = (flow - 0.5) * 0.20 + serration * 0.10;
    float outerMask = smoothstep(0.02 + edgeBite, 0.20 + edgeBite, uv.y);
    //内缘朝持握者渐隐
    float innerMask = smoothstep(1.0, 0.32, uv.y);

    //尾迹老化
    float ageMask = smoothstep(0.0, 0.55, age);
    ageMask *= ageMask;

    float intensity = outerMask * innerMask * ageMask;

    //叶绿丝线：噪声脊线形成的藻丝高光
    float filament = smoothstep(0.55, 0.85, flow) * intensity;

    //孢子光点：高频噪声阈值化成点状荧光，沿尾迹剥离
    float spore = tex2D(noiseSamp, float2(uv.x * 6.5 - uTime * 0.35, uv.y * 5.0 + uTime * 0.9)).r;
    float sporeDot = smoothstep(0.78, 0.92, spore) * ageMask * innerMask * smoothstep(0.0, 0.35, uv.y);

    //鎏金白热刀口：仅最新边缘的刀尖侧
    float hotCore = smoothstep(0.70, 1.0, age) * smoothstep(0.34, 0.05, uv.y)
                  * outerMask * (0.55 + uHeat * 0.45);

    //颜色：深藻绿 → 叶绿 → 鎏金亮白
    float3 cDark = float3(0.05, 0.20, 0.10);
    float3 cMain = float3(0.28, 0.76, 0.36);
    float3 cGold = float3(0.95, 0.88, 0.45);
    float3 cGlow = float3(0.88, 1.00, 0.66);

    float3 color = cDark * intensity * 1.25;
    color += cMain * intensity * 0.45;
    color = lerp(color, cMain, filament * 0.85);
    color += cGlow * hotCore;
    color += lerp(cGlow, cGold, uHeat) * filament * (0.22 + uHeat * 0.5);
    color += cGlow * sporeDot * 0.85;

    float alpha = saturate(intensity * 0.85 + filament * 0.35 + hotCore * 0.6 + sporeDot * 0.4);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DragonSlashTrailPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
