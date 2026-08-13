// ============================================================================
//EocBloodTrail.fx 克眼冲刺血带
//三层液体截面：静脉暗鞘/动脉主体/鲜血芯线，噪声撕边+尾部先蚀+血珠斑
//uv.x: 0=尾 1=头；顶点色A=CPU侧宽度渐隐
//血是暗的：无白热常驻，最亮只到鲜血红
//AlphaBlend 预乘输出
// ============================================================================

matrix transformMatrix;
float uTime;
float uIntensity;
texture uNoise;
sampler2D noiseTex = sampler_state
{
    texture = <uNoise>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

static const float3 VenousDark = float3(0.239, 0.024, 0.043);
static const float3 Arterial   = float3(0.557, 0.059, 0.102);
static const float3 Bright     = float3(0.831, 0.129, 0.180);

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
    float x = input.TexCoords.x;   //沿带 0尾→1头
    float y = input.TexCoords.y;   //横截 0~1
    float centered = abs(y - 0.5) * 2.0;   //0中脊→1边缘

    //向尾滚动的液流噪声，两个频段
    float flowA = tex2D(noiseTex, float2(x * 2.6 - uTime * 1.15, y * 0.9)).r;
    float flowB = tex2D(noiseTex, float2(x * 6.5 - uTime * 1.9, y * 2.3 + 0.37)).r;

    //边缘噪声撕裂：静脉鞘的毛口
    float edgeTear = centered + (flowA - 0.5) * 0.42 + (flowB - 0.5) * 0.2;
    float sheath = 1.0 - smoothstep(0.55, 1.0, edgeTear);

    //尾部先蚀：越靠尾越被噪声吃掉
    float tailErode = smoothstep(0.0, 0.55, x + (flowB - 0.5) * 0.35);
    sheath *= tailErode;

    //动脉主体：更窄更饱和
    float body = 1.0 - smoothstep(0.28, 0.72, edgeTear);
    body *= smoothstep(0.05, 0.6, x);

    //鲜血芯线：细，头端才最亮，仍是血不是光
    float core = 1.0 - smoothstep(0.0, 0.24, centered + (flowA - 0.5) * 0.12);
    core *= smoothstep(0.25, 0.95, x);

    //血珠斑：高频噪声阈值挑亮点，沿带滚动
    float speck = tex2D(noiseTex, float2(x * 9.0 - uTime * 2.4, y * 4.1 + 0.71)).r;
    float droplets = smoothstep(0.78, 0.9, speck) * sheath * 0.8;

    float3 col = VenousDark * sheath
               + lerp(VenousDark, Arterial, body) * body * 0.9
               + Bright * core * 0.85
               + Bright * droplets * 0.5;

    float a = saturate(sheath * 0.62 + body * 0.5 + core * 0.55 + droplets * 0.3);
    a *= input.Color.a * saturate(uIntensity);

    return float4(col * a, a);
}

technique Technique1
{
    pass EocBloodTrailPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
