// ============================================================================
//OniMacheteComet.fx 鬼手之火彗尾条带（沿 oldPos 轨迹的 TriangleStrip）
//uv.x：0=弹头 → 1=尾梢；uv.y：0..1 横跨条带，0.5=中线
//全笛卡尔条带坐标，无极角 → 接缝协议天然合规
//
//层次：暖金白热中脊（仅头段，非冷白）→ 硫磺橙红焰体 → 深红 → 黑烟尾；
//热扰动 = 流动噪声推挤横向坐标（越近尾越大），边缘被噪声撕出飞散焰屑，
//头亮尾灭 + 尾段只剩半透明黑烟。预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uFade;   //整体不透明度（出生淡入）

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

//暖金白热（刻意偏金，禁冷白）/ 硫磺橙红 / 深红 / 黑烟
static const float3 ColHot = float3(1.42, 1.05, 0.48);
static const float3 ColBrim = float3(1.15, 0.30, 0.06);
static const float3 ColDeep = float3(0.42, 0.07, 0.03);
static const float3 ColSmoke = float3(0.05, 0.018, 0.014);

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
    float t = input.TexCoords.x;          //0 头 → 1 尾
    float y = input.TexCoords.y - 0.5;    //-0.5..0.5

    //热扰动：双八度流动噪声推挤横向坐标，越靠尾抖得越散
    float wob1 = tex2D(noiseSamp, float2(t * 2.4 - uTime * 1.7 + uSeed * 7.0, uSeed)).r - 0.5;
    float wob2 = tex2D(noiseSamp, float2(t * 5.1 - uTime * 2.6 + uSeed * 3.0, 0.37 + uSeed)).r - 0.5;
    y += (wob1 * 0.50 + wob2 * 0.28) * t * 0.55;

    float across = saturate(abs(y) * 2.0);   //0 中线 → 1 边缘
    float body = 1.0 - across;

    //焰舌纹理：沿带向尾流动
    float flame = tex2D(noiseSamp, float2(t * 3.3 - uTime * 2.1 + uSeed * 11.0, y * 1.6 + 0.5 + uSeed)).r;

    //边缘/尾部撕碎：噪声阈值随 t 收紧，尾梢读作飞散焰屑
    float ragged = smoothstep(0.16 + t * 0.50, 0.78, body + (flame - 0.5) * (0.35 + t * 0.85));
    if (ragged < 0.004)
        return float4(0, 0, 0, 0);

    //强度包络：头亮尾灭
    float head = pow(saturate(1.0 - t), 1.7);
    //黑烟余量：中后段残留的半透明烟带
    float smoke = (1.0 - t) * smoothstep(0.10, 0.45, t) * 0.55;

    //色程：暖金白热（仅头段贴中线）→ 硫磺橙红 → 深红 → 黑烟
    float3 col = lerp(ColBrim, ColDeep, smoothstep(0.16, 0.52, t + (0.5 - flame) * 0.12));
    col = lerp(col, ColSmoke, smoothstep(0.46, 0.88, t));

    //暖金中脊：头段中线一条熔金热芯（额头与弹体辉光融为一体）
    float core = pow(body, 5.0) * pow(saturate(1.0 - t), 2.4);
    col += ColHot * core * 1.25;
    //焰舌高光
    col += ColBrim * smoothstep(0.62, 0.92, flame) * head * 0.55;

    float alpha = saturate(head * 0.92 + smoke) * ragged * uFade;
    return float4(col * alpha + ColHot * core * 0.22 * uFade, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
