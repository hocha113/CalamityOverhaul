// ============================================================================
//FishBrimlishComet.fx 硫火球彗尾条带（沿 oldPos 轨迹的 TriangleStrip）
//uv.x：0=弹头 → 1=尾梢；uv.y：0..1 横跨条带，0.5=中线
//全笛卡尔条带坐标，无极角 → 接缝协议天然合规
//
//层次：焰心亮橙中脊（仅头段贴中线，禁纯白）→ 硫磺橙红焰体 → 深红 → 黑烟尾；
//尾流内嵌余烬火星（高频噪声阈值闪点，向尾漂移）；热扰动推挤横向坐标，
//边缘噪声撕裂；弹幕群定位：比 OniMacheteComet 更暗、更短碎
//uBurn 燃尽进度：走高时头光衰减、色程前移向深红、撕裂提前 → 飞行期量在演化
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uFade;   //整体不透明度（出生淡入）
float uBurn;   //燃尽进度 0..1

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

//焰心亮橙（刻意压离纯白）/ 硫磺橙红 / 深红 / 黑烟
static const float3 ColHot = float3(1.28, 0.62, 0.16);
static const float3 ColBrim = float3(1.02, 0.24, 0.05);
static const float3 ColDeep = float3(0.34, 0.05, 0.025);
static const float3 ColSmoke = float3(0.045, 0.017, 0.013);

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
    float wob1 = tex2D(noiseSamp, float2(t * 3.0 - uTime * 2.2 + uSeed * 7.0, uSeed)).r - 0.5;
    float wob2 = tex2D(noiseSamp, float2(t * 6.3 - uTime * 3.4 + uSeed * 3.0, 0.37 + uSeed)).r - 0.5;
    y += (wob1 * 0.46 + wob2 * 0.30) * t * 0.60;

    float across = saturate(abs(y) * 2.0);   //0 中线 → 1 边缘
    float body = 1.0 - across;

    //焰舌纹理：沿带向尾流动
    float flame = tex2D(noiseSamp, float2(t * 3.8 - uTime * 2.6 + uSeed * 11.0, y * 1.7 + 0.5 + uSeed)).r;

    //边缘/尾部撕碎：噪声阈值随 t 收紧，燃尽时提前收紧 → 尾梢读作飞散焰屑
    float ragged = smoothstep(0.18 + t * (0.52 + uBurn * 0.30), 0.80, body + (flame - 0.5) * (0.32 + t * 0.90));
    if (ragged < 0.004)
        return float4(0, 0, 0, 0);

    //强度包络：头亮尾灭，燃尽时整体压暗
    float head = pow(saturate(1.0 - t), 1.8) * (1.0 - uBurn * 0.45);
    //黑烟余量：中后段残留的半透明烟带
    float smoke = (1.0 - t) * smoothstep(0.08, 0.42, t) * 0.50;

    //色程：硫磺橙红 → 深红 → 黑烟，燃尽把色程整体前移
    float3 col = lerp(ColBrim, ColDeep, smoothstep(0.12, 0.48, t + (0.5 - flame) * 0.12 + uBurn * 0.25));
    col = lerp(col, ColSmoke, smoothstep(0.40, 0.85, t));

    //焰心中脊：头段中线一条亮橙热芯（与弹头焰核辉光融为一体）
    float core = pow(body, 5.0) * pow(saturate(1.0 - t), 2.6) * (1.0 - uBurn * 0.50);
    col += ColHot * core * 1.15;

    //余烬火星：中后段尾流内嵌高频噪声闪点，向尾漂移
    float sparkN = tex2D(noiseSamp, float2(t * 9.0 - uTime * 3.8 + uSeed * 17.0, y * 3.2 + 0.5 + uSeed * 5.0)).r;
    float spark = smoothstep(0.80, 0.94, sparkN) * smoothstep(0.10, 0.38, t) * saturate(1.2 - t) * (1.0 - uBurn * 0.6);
    col += ColHot * spark * 0.75;

    //焰舌高光
    col += ColBrim * smoothstep(0.64, 0.92, flame) * head * 0.50;

    float alpha = saturate(head * 0.90 + smoke + spark * 0.35) * ragged * uFade;
    return float4(col * alpha + ColHot * core * 0.16 * uFade, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
