// ============================================================================
//GraniteArc.fx 花岗青蓝电弧拖尾
//Trail 条带 Additive；花岗系飞刀/能量球/碎片共用签名拖尾
//UV.x 0=最新端(oldPos[0]侧) 1=尾端  UV.y 0/1=两缘 0.5=中轴
//读感=细锐电弧：噪声位移的中轴亮线 + 偶发侧枝 + 稀薄青辉包络，
//带体大部分透明，绝不允许读成实心光锥；颜色内置，顶点色作整体调制
//vs_3_0 / ps_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;    //流动相位（GlobalTimeWrappedHourly）
float uFade;    //整体透明度 0~1

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
    float head = 1.0 - uv.x;        //1=最新端
    float across = uv.y - 0.5;      //-0.5~0.5 中轴0

    //主弧路径：双频噪声位移中轴（头端钉回中轴，保证从弹体中心发出）
    float n1 = tex2D(noiseSamp, float2(uv.x * 1.7 + uTime * 2.3, 0.31)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 3.9 - uTime * 1.6, 0.67)).r;
    float wander = ((n1 - 0.5) * 0.36 + (n2 - 0.5) * 0.16) * smoothstep(0.85, 0.6, head);

    //主电弧：距位移中轴的细亮线 + 更细的白芯
    float d = abs(across - wander);
    float bolt = smoothstep(0.16, 0.02, d);
    float core = smoothstep(0.055, 0.0, d);

    //侧枝：位移相位不同的更细弱弧，只在头段偶现
    float d2 = abs(across - (n2 - 0.5) * 0.72);
    float branch = smoothstep(0.09, 0.012, d2) * smoothstep(0.35, 0.85, head);

    //沿带能量衰减 + 电弧断续（尾段被噪声咬断成节段）
    float lenFade = smoothstep(0.0, 0.5, head);
    float breakN = tex2D(noiseSamp, float2(uv.x * 5.7 + uTime * 3.1, 0.05)).r;
    float continuity = smoothstep(0.46 - head * 0.5, 0.66 - head * 0.5, breakN);
    bolt *= lenFade * continuity;
    core *= lenFade * continuity;
    branch *= lenFade * continuity;

    //稀薄青辉包络：只给电弧一点氛围底色，绝不构成实心带
    float haze = smoothstep(0.5, 0.0, abs(across)) * lenFade * (0.10 + 0.08 * n1);

    //色带：深蓝底辉 → 青主弧 → 白蓝弧心
    float3 cDeep = float3(0.10, 0.20, 0.55);
    float3 cCyan = float3(0.30, 0.80, 1.00);
    float3 cCore = float3(0.88, 0.98, 1.00);

    float3 color = cDeep * haze * 2.2;
    color += cCyan * (bolt * 0.85 + branch * 0.5);
    color += cCore * core * (0.6 + 0.4 * head);

    float alpha = saturate(haze + bolt * 0.75 + branch * 0.4 + core * 0.9) * uFade;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass GraniteArcPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
