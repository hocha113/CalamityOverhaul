// ============================================================================
//FishCursedFlame.fx 诅咒绿火拖尾条带（冥焰迸发弹体尾迹）
//uv.x：0=头端(最新，DrawTrailFromOldPos 的 oldPos[0] 侧) → 1=尾端(最旧)；uv.y：0..1 跨带
//层次：墨绿烟外缘 → 暗绿 → 饱和中绿 → 头段细黄绿焰芯；亮度=暗外圈/饱和中层/极小热芯，无纯白
//焰舌撕裂：流动噪声推挤横向坐标（越近尾越散），噪声阈值把边缘与尾端撕成焰屑
//上飘由 C# 轨迹承担；全笛卡尔条带坐标，无 atan2/theta/phi，接缝协议天然合规
//Additive 输出（GraniteMarbleVFX.DrawTrailFromOldPos 设 BlendState.Additive），顶点色承载包络
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //流动相位（每弹幕加相位偏移）
float uFade;   //整体包络 0..1

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

//墨绿烟 / 暗绿 / 饱和中绿 / 亮黄绿焰心
static const float3 ColSmoke = float3(0.047, 0.10, 0.055);
static const float3 ColDeep = float3(0.10, 0.35, 0.13);
static const float3 ColMid = float3(0.25, 0.66, 0.23);
static const float3 ColCore = float3(0.67, 0.85, 0.35);

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

    //焰舌撕摆：双八度流动噪声推挤横向坐标，越近尾越散
    float wob1 = tex2D(noiseSamp, float2(t * 2.1 - uTime * 1.1, 0.21)).r - 0.5;
    float wob2 = tex2D(noiseSamp, float2(t * 4.7 - uTime * 1.9, 0.63)).r - 0.5;
    y += (wob1 * 0.46 + wob2 * 0.26) * (0.25 + t * 0.75);

    float across = saturate(abs(y) * 2.0);   //0 中线 → 1 边缘
    float profile = 1.0 - across;

    //焰纹：沿带流动
    float flame = tex2D(noiseSamp, float2(t * 3.2 - uTime * 1.5, y * 1.4 + 0.5)).r;

    //撕裂 gate：尾端阈值收紧，边缘被噪声撕成焰屑
    float ragged = smoothstep(0.15 + t * 0.52, 0.80, profile + (flame - 0.5) * (0.32 + t * 0.85));
    if (ragged < 0.004)
        return float4(0, 0, 0, 0);

    //强度包络：头亮尾灭
    float head = pow(saturate(1.0 - t), 1.55);
    //中后段残留的墨绿烟带
    float smoke = (1.0 - t) * smoothstep(0.12, 0.5, t) * 0.5;

    //色程：暗绿 →（头段）饱和中绿，中后段沉入墨绿烟
    float3 col = lerp(ColDeep, ColMid, saturate(head * (0.5 + 0.5 * flame)));
    col = lerp(col, ColSmoke, smoothstep(0.42, 0.9, t));

    //焰芯：头段中线一条细黄绿热芯
    float core = pow(profile, 5.0) * pow(saturate(1.0 - t), 2.2);
    col += ColCore * core * 0.9;

    float alpha = saturate(head * 0.8 + smoke) * ragged * uFade;
    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
