// ============================================================================
//FishCrimsonTigerWake.fx 猩红虎鱼冲刺尾波绸带（沿尾锚轨迹的 TriangleStrip）
//uv.x：0=鱼尾根 → 1=尾波末梢；uv.y：0..1 横跨条带，0.5=中线
//全笛卡尔条带坐标，无极角，接缝协议天然合规
//
//层次：暗红外缘 → 饱和猩红中层 → 暖亮窄芯（仅头段，非白）；
//水痕质感 = 流动噪声沿带向后冲刷，边缘噪声撕裂且越近尾梢越碎，
//尾梢完全撕散成水沫，禁平滑收口。预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uFade;   //整体不透明度，外部按速度与出生淡入折算

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

//暗红外缘 / 饱和猩红 / 暖亮芯（偏橙红，禁冷白）
static const float3 ColDeep = float3(0.26, 0.030, 0.045);
static const float3 ColCrim = float3(0.80, 0.095, 0.125);
static const float3 ColHot = float3(1.12, 0.40, 0.30);

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
    float t = input.TexCoords.x;          //0 根 → 1 梢
    float y = input.TexCoords.y - 0.5;    //-0.5..0.5

    //水流扰动：单层流动噪声轻推横向坐标，水痕比火焰绷直，幅度压小且尾段才散
    float wob = tex2D(noiseSamp, float2(t * 2.1 - uTime * 2.4 + uSeed * 9.0, uSeed)).r - 0.5;
    y += wob * 0.22 * t;

    float across = saturate(abs(y) * 2.0);   //0 中线 → 1 边缘
    float body = 1.0 - across;

    //冲刷纹理：沿带向后流动，读作被撕开的水面
    float flow = tex2D(noiseSamp, float2(t * 3.6 - uTime * 3.1 + uSeed * 5.0, y * 1.3 + 0.5 + uSeed)).r;

    //边缘撕裂：阈值随 t 收紧，尾梢撕成断续水沫
    float ragged = smoothstep(0.14 + t * 0.58, 0.72, body + (flow - 0.5) * (0.28 + t * 0.95));
    if (ragged < 0.004)
        return float4(0, 0, 0, 0);

    //强度包络：根亮梢灭
    float head = pow(saturate(1.0 - t), 1.5);

    //色程：猩红中层 → 暗红外缘与尾段
    float3 col = lerp(ColCrim, ColDeep, max(smoothstep(0.18, 0.75, t), across * 0.8));

    //暖亮窄芯：只贴头段中线，极窄
    float core = pow(body, 6.0) * pow(saturate(1.0 - t), 3.0);
    col += ColHot * core * 0.9;

    //冲刷高光：流动噪声亮斑，只在前半段
    col += ColCrim * smoothstep(0.66, 0.94, flow) * head * 0.5;

    float alpha = saturate(head * 0.85) * ragged * uFade;
    return float4(col * alpha + ColHot * core * 0.15 * uFade, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
