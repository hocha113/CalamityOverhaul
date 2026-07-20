// ============================================================================
//FishMudGlob.fx 泥球液团：受速度与重力持续变形的软体湿泥，非刚体贴图
//quad由C#沿速度轴摆放：uv.x=0头部/1尾部，uv.y横向0..1中线0.5
//uStretch控制沿轴拉长，uWobble软体蠕动相位，尾侧噪声撕裂成甩泥须
//全笛卡尔噪声输入，无极角无缝隙；哑光无加色，头缘一点窄水光
//预乘alpha，配BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;    //秒
float uSeed;    //实例随机相位
float uStretch; //0..1随速度拉伸量
float uWobble;  //软体蠕动相位
float uFade;    //整体不透明度
float3 uLight;  //世界光照调制，泥面哑光必须吃环境光

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

static const float3 ColMurk = float3(0.157, 0.118, 0.094);
static const float3 ColDeep = float3(0.235, 0.173, 0.129);
static const float3 ColBase = float3(0.369, 0.275, 0.192);
static const float3 ColWet = float3(0.502, 0.392, 0.267);
static const float3 ColSheen = float3(0.627, 0.643, 0.580);

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
    //p.x沿速度轴：-1头..1尾，p.y横向-1..1
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //软体形变：拉伸时沿轴变长变窄，慢速时蠕动呼吸
    float ax = 1.0 / (1.0 + uStretch * 1.15);
    float squash = 1.0 + sin(uWobble) * 0.15 * (1.0 - uStretch * 0.6);
    float2 q = float2(p.x * ax, p.y * squash);

    //边缘蠕动噪声：尾侧振幅加大撕成甩泥须
    float tailness = saturate(p.x);
    float n1 = tex2D(noiseSamp, q * 0.5 + float2(uSeed * 9.0, uTime * 0.4 + uSeed)).r;
    float r = length(q) + (n1 - 0.5) * (0.26 + tailness * 0.55);

    float body = smoothstep(1.0, 0.62 - tailness * 0.16, r);
    if (body < 0.004)
        return float4(0, 0, 0, 0);

    //内部浑浊流动
    float n2 = tex2D(noiseSamp, q * 1.4 + float2(uSeed * 3.0, -uTime * 0.55)).r;

    //外缘暗、芯部实、最里带湿亮：浑浊液团的体积读法
    float3 col = lerp(ColDeep, ColBase, saturate(body * 1.25));
    col = lerp(col, ColWet, smoothstep(0.72, 1.0, body) * 0.55);
    col *= 0.86 + n2 * 0.28;

    //尾须转向暗泥
    col = lerp(col, ColMurk, tailness * 0.4);

    //头缘窄水光：飞行前脸的湿反光，极低幅
    float rim = smoothstep(0.42, 0.6, r) * body;
    float sheen = rim * smoothstep(0.0, -0.55, p.x) * (0.28 + 0.18 * sin(uWobble * 0.7 + uSeed * 6.0));
    col = lerp(col, ColSheen, sheen * 0.5);

    float alpha = body * uFade;
    return float4(col * uLight * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
