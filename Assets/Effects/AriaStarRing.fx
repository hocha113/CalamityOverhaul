// ============================================================================
//AriaStarRing.fx 寰宇咏叹调·Q技能星流环带
//单quad：金橙星流环 + 6个亮结点(星卫)；Additive
//接缝纪律：角度进噪声一律整数倍(采样器wrap)，结点用整数6倍cos
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;        //整体透明度
float uRingN;       //环半径(quad归一化)
float uRingW;       //环带半宽(quad归一化)
float uNodePhase;   //结点星座旋转相位
float uSpin;        //星流流动相位
float seed;

texture noiseTexture;
sampler noiseTex = sampler_state
{
    texture = <noiseTexture>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

#define TAU 6.28318530

static const float3 ColHot = float3(1.00, 0.973, 0.910);
static const float3 ColGold = float3(1.00, 0.702, 0.278);
static const float3 ColRose = float3(1.00, 0.369, 0.478);

struct VertexShaderInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    output.Position = mul(input.Position, transformMatrix);
    return output;
}

float4 RingPS(VertexShaderOutput input) : COLOR0
{
    float2 c = input.TexCoords - 0.5;
    float dist = length(c);
    float ang = atan2(c.y, c.x + 1e-6);

    float circleFade = 1.0 - smoothstep(0.44, 0.5, dist);

    //---- 主环带 ----
    float band = exp(-pow((dist - uRingN) / uRingW, 2.0));
    //外侧多一层松散星尘晕
    float fringe = exp(-pow((dist - uRingN) / (uRingW * 2.6), 2.0)) * 0.30;

    //---- 星流：沿环流动的碎星纹理(整数倍角→wrap无缝) ----
    float s1 = tex2D(noiseTex, float2(ang / TAU * 3.0 - uSpin * 0.5, (dist - uRingN) * 9.0 + seed)).r;
    float s2 = tex2D(noiseTex, float2(ang / TAU * 5.0 + uSpin * 0.23, (dist - uRingN) * 6.0 - seed)).g;
    float stream = (s1 * 0.6 + s2 * 0.4);
    //碎星脊线：亮点断续
    float sparkle = smoothstep(0.55, 0.85, stream);
    stream = stream * 0.55 + 0.45;

    //---- 6结点星卫(整数6倍cos连续) ----
    float nodeWave = cos(6.0 * ang - uNodePhase * 6.0) * 0.5 + 0.5;
    float nodes = pow(nodeWave, 26.0);
    //结点呼吸
    nodes *= 0.85 + 0.15 * sin(uTime * 6.0);

    //---- 环内轨道细线(第二圈更细的内环) ----
    float rail = exp(-pow((dist - uRingN) / (uRingW * 0.22), 2.0)) * 0.5;

    //---- 合成 ----
    float3 col = float3(0.0, 0.0, 0.0);
    col += ColGold * band * stream * 0.85;
    col += ColHot * band * sparkle * 0.7;
    col += ColRose * fringe * stream;
    col += ColGold * rail;
    col += ColHot * nodes * band * 2.2;

    col *= uFade * circleFade;
    float a = saturate(max(col.r, max(col.g, col.b)));
    return float4(col, a) * input.Color;
}

technique Ring
{
    pass RingPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 RingPS();
    }
}
