// ============================================================================
//GraniteArc.fx 花岗青蓝能量弧光带
//Trail 条带 Additive；花岗系飞刀/能量球/碎片共用签名拖尾
//UV.x 0=最新端(oldPos[0]侧) 1=尾端  UV.y 0/1=两缘 0.5=中轴
//颜色内置（深蓝底/青主体/白蓝弧心），顶点色作整体调制
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
    float head = 1.0 - uv.x;              //1=最新端
    float across = abs(uv.y - 0.5) * 2.0; //0=中轴 1=边缘

    //双层流动噪声：电荷沿带身向尾端泄流
    float n1 = tex2D(noiseSamp, float2(uv.x * 2.3 + uTime * 1.7, uv.y * 0.7 + uTime * 0.13)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 5.1 - uTime * 0.9, uv.y * 1.4 - uTime * 0.41)).r;
    float flow = n1 * 0.62 + n2 * 0.38;

    //带身：硬边（花岗=棱角），噪声轻咬边缘
    float bodyEdge = 0.88 + (flow - 0.5) * 0.14;
    float body = smoothstep(bodyEdge, bodyEdge - 0.10, across);

    //沿带渐隐：头亮尾淡 + 尾端颗粒崩解
    float lenFade = smoothstep(0.0, 0.55, head);
    float grain = tex2D(noiseSamp, float2(uv.x * 6.3 + uTime * 0.23, uv.y * 3.1)).r;
    float thresh = 0.75 - head * 1.15;
    body *= lenFade * smoothstep(thresh, thresh + 0.18, grain);

    //中轴电弧：噪声位移的锯齿亮线
    float wob = (flow - 0.5) * 0.62;
    float boltDist = abs(uv.y - 0.5 - wob * 0.42);
    float bolt = smoothstep(0.11, 0.015, boltDist) * smoothstep(0.0, 0.30, head) * lenFade;

    //晶面碎闪：硬阈值给出棱角感块斑
    float facet = step(0.74, n2) * body;

    //色带：深蓝底 → 青主体 → 白蓝弧心
    float3 cDeep = float3(0.10, 0.20, 0.55);
    float3 cCyan = float3(0.28, 0.80, 1.00);
    float3 cCore = float3(0.85, 0.97, 1.00);

    float3 color = cDeep * body * 1.15;
    color += cCyan * body * flow * 0.55;
    color += cCyan * facet * 0.45;
    color += cCyan * bolt * 0.9;
    color += cCore * bolt * smoothstep(0.45, 1.0, head);

    float alpha = saturate(body * 0.62 + bolt * 0.85) * uFade;
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
