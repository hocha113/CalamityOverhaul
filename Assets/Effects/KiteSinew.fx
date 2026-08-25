// ============================================================================
//KiteSinew.fx 缚瞳风筝 红白螺旋筋腱线
//材质=血浸筋腱绳：红白螺旋缠绕、圆柱实体、纤维毛口、湿面各向异性窄亮
//不是光带/鱼线。白=筋腱本色，红=血浸缠绕。绷紧收窄拉直，回弹在 uTwangPos 挤一圈血
//uv.x 0玩家结→1眼球附着；uv.y 横截。预乘 AlphaBlend
//噪声固定 s1。无 atan2、无动态分支、无 tex2Dlod
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uTension;   //0松弛 1绷直
float uTwang;     //0~1 回弹挤波
float uTwangPos;  //挤波沿绳 0结→1眼
float uFade;
float seed;

sampler noiseSamp : register(s1);

static const float3 ColDeep  = float3(0.165, 0.027, 0.035);
static const float3 ColBlood = float3(0.659, 0.086, 0.125);
static const float3 ColArter = float3(0.820, 0.141, 0.176);
static const float3 ColSinew = float3(0.910, 0.855, 0.800);
static const float3 ColWet   = float3(0.780, 0.310, 0.290);

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
    float along = input.TexCoords.x;
    float y = input.TexCoords.y;
    float cross_ = (y - 0.5) * 2.0;

    float nA = tex2D(noiseSamp, float2(along * 3.4 + seed, seed * 2.1 + uTime * 0.05)).r;
    float nB = tex2D(noiseSamp, float2(along * 9.2 - uTime * 0.11, y * 2.4 + seed)).r;
    float nC = tex2D(noiseSamp, float2(along * 16.0 + seed * 4.0, y * 5.1 + 0.37)).r;

    //松弛时轴微蜿蜒，绷直拉平
    float axis = (nA - 0.5) * 0.28 * (1.0 - uTension);
    float d = abs(cross_ - axis);

    //宽度：两端收成附着点，中段随张力收窄；毛口走噪声
    float endCap = smoothstep(0.00, 0.08, along) * smoothstep(1.00, 0.90, along);
    endCap = 0.55 + 0.45 * endCap;
    float taper = lerp(1.0, 0.62, uTension) * endCap;
    float edgeN = nB * 0.22 + nC * 0.10;
    float bodyEdge = taper * (0.72 + edgeN);
    float body = smoothstep(bodyEdge, bodyEdge - 0.22, d);

    //圆柱明暗+沿绳纤维
    float lam = 1.0 - saturate(d / max(bodyEdge, 0.001));
    float shade = 0.38 + 0.62 * pow(lam, 0.70);
    float fiber = 0.82 + 0.28 * nC;
    shade *= fiber;

    //红白螺旋缠绕：沿绳推进 + 横截相位，读成绕柱的筋腱
    float turns = lerp(5.4, 3.6, uTension);
    float helix = frac(along * turns + y * 0.58 + seed * 0.17 - uTime * 0.04);
    float sinewBand = smoothstep(0.06, 0.14, helix) * (1.0 - smoothstep(0.40, 0.48, helix));
    sinewBand *= 0.55 + 0.45 * lam;

    float3 col = lerp(ColDeep, ColBlood, shade);
    col = lerp(col, ColArter, lam * 0.35 * (1.0 - sinewBand));
    col = lerp(col, ColSinew, sinewBand * (0.72 + 0.28 * lam));

    //各向异性湿亮：沿绳窄带，不是圆高光
    float sheen = pow(saturate(lam), 7.0) * (0.18 + 0.16 * nB);
    sheen *= 0.55 + 0.45 * sinewBand;
    col += ColWet * sheen;

    //回弹挤波：那一圈血被掐亮，筋腱被拉白一点
    float twangD = abs(along - uTwangPos);
    float twangBand = exp(-twangD * twangD * 90.0) * uTwang;
    col = lerp(col, ColArter, twangBand * 0.55);
    col = lerp(col, ColSinew, twangBand * sinewBand * 0.35);

    //血珠斑，松弛时更多
    float speck = smoothstep(0.82, 0.93, nC) * body * (0.22 + 0.35 * (1.0 - uTension));
    col += ColArter * speck * 0.45;

    float alpha = saturate(body) * uFade * input.Color.a;
    col *= lerp(float3(1.0, 1.0, 1.0), input.Color.rgb, 0.55);

    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass KiteSinewPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
