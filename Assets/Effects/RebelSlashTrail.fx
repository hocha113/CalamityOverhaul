// ============================================================================
// RebelSlashTrail.fx 叛逆之刃刀光
// Trail 条带 Additive
// UV.x 1=最新 0=尾 UV.y 0=外缘 1=内缘
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //狂暴度 0~1，终结回旋斩时提升

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

    //叛逆气流：逆向滚动的双层噪声，桀骜不驯
    float n1 = tex2D(noiseSamp, float2(uv.x * 1.8 - uTime * 1.5, uv.y * 1.1 - uTime * 0.3)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 3.6 + uTime * 0.8, uv.y * 2.0 + uTime * 0.9)).r;
    float flow = n1 * 0.62 + n2 * 0.38;

    //外缘被狂野撕裂：撕裂幅度随狂暴度增长
    float biteAmp = 0.26 + uHeat * 0.14;
    float edgeBite = (flow - 0.5) * biteAmp;
    float outerMask = smoothstep(0.02 + edgeBite, 0.20 + edgeBite, uv.y);
    //内缘渐隐
    float innerMask = smoothstep(1.0, 0.32, uv.y);

    //尾迹老化
    float ageMask = smoothstep(0.0, 0.52, age);
    ageMask *= ageMask;

    float intensity = outerMask * innerMask * ageMask;

    //飞溅星火：高频噪声阈值闪点，沿尾迹散落
    float sparkNoise = tex2D(noiseSamp, float2(uv.x * 7.0 - uTime * 2.2, uv.y * 5.0 + uTime * 0.6)).r;
    float spark = smoothstep(0.78 - uHeat * 0.08, 0.92, sparkNoise) * ageMask * innerMask;

    //能量丝缕：中阈值噪声脊线
    float filament = smoothstep(0.55, 0.84, flow) * intensity;

    //刃口青白芯
    float hotCore = smoothstep(0.70, 1.0, age) * smoothstep(0.34, 0.05, uv.y)
                  * outerMask * (0.50 + uHeat * 0.50);

    //颜色：深海蓝 → 叛逆蓝 → 青白
    float3 cDark = float3(0.06, 0.13, 0.36);
    float3 cMain = float3(0.30, 0.55, 1.00);
    float3 cGlow = float3(0.72, 0.93, 1.00);

    float3 color = cDark * intensity * 1.25;
    color += cMain * intensity * 0.5;
    color = lerp(color, cMain, filament * 0.85);
    color += cGlow * hotCore;
    color += cGlow * spark * (0.8 + uHeat * 0.5);
    color += cGlow * filament * (0.2 + uHeat * 0.4);

    float alpha = saturate(intensity * 0.85 + filament * 0.3 + hotCore * 0.6 + spark * 0.5);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass RebelSlashTrailPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
