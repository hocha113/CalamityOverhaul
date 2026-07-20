// ============================================================================
//FishDemonicHellComet.fx 地狱火球彗尾条带（沿 oldPos 轨迹的 TriangleStrip）
//uv.x：0=弹头 → 1=尾梢；uv.y：0..1 横跨条带，0.5=中线
//全笛卡尔条带坐标，无极角 → 接缝协议天然合规
//
//相比 OniMacheteComet：黑烟提前接管（烟雾尾占比更重）+ 高频噪声阈值撕出
//离散余烬斑（余烬顺尾流散），头段热芯更短更收敛（弹头主体另有剪影核绘制）
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uFade;   //整体不透明度（出生淡入/爆后速灭）

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

//暖金热芯（偏金，禁冷白）/ 地狱橙红 / 深红 / 黑烟
static const float3 ColHot = float3(1.30, 0.84, 0.34);
static const float3 ColFire = float3(1.06, 0.25, 0.05);
static const float3 ColDeep = float3(0.37, 0.055, 0.030);
static const float3 ColSmoke = float3(0.050, 0.017, 0.014);

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
    float wob1 = tex2D(noiseSamp, float2(t * 2.1 - uTime * 1.5 + uSeed * 7.0, uSeed)).r - 0.5;
    float wob2 = tex2D(noiseSamp, float2(t * 4.6 - uTime * 2.4 + uSeed * 3.0, 0.37 + uSeed)).r - 0.5;
    y += (wob1 * 0.55 + wob2 * 0.30) * t * 0.60;

    float across = saturate(abs(y) * 2.0);
    float body = 1.0 - across;

    //焰舌纹理：沿带向尾流动
    float flame = tex2D(noiseSamp, float2(t * 3.0 - uTime * 1.9 + uSeed * 11.0, y * 1.5 + 0.5 + uSeed)).r;

    //边缘/尾部撕碎：阈值随 t 收紧，尾梢读作飞散烟屑
    float ragged = smoothstep(0.14 + t * 0.50, 0.80, body + (flame - 0.5) * (0.40 + t * 0.90));
    if (ragged < 0.004)
        return float4(0, 0, 0, 0);

    //强度包络：头亮尾灭
    float head = pow(saturate(1.0 - t), 1.9);
    //黑烟提前接管：t≈0.06 起烟，中后段以烟为主
    float smoke = (1.0 - t) * smoothstep(0.06, 0.32, t) * 0.72;

    //色程：地狱橙红 → 深红 → 黑烟（比 OniMachete 更早入烟）
    float3 col = lerp(ColFire, ColDeep, smoothstep(0.10, 0.42, t + (0.5 - flame) * 0.12));
    col = lerp(col, ColSmoke, smoothstep(0.34, 0.78, t));

    //离散余烬斑：高频噪声阈值，只在中段窗口，随尾流闪散
    float emberN = tex2D(noiseSamp, float2(t * 9.0 - uTime * 3.1 + uSeed * 17.0, y * 4.0 + 0.5 + uSeed * 29.0)).r;
    float emberWin = smoothstep(0.05, 0.18, t) * (1.0 - smoothstep(0.45, 0.80, t));
    float embers = smoothstep(0.74, 0.88, emberN) * emberWin;
    col += ColFire * embers * 1.3 + ColHot * embers * head * 0.6;

    //短热芯：仅头段贴中线（弹头另有剪影核，条带只接管尾）
    float core = pow(body, 6.0) * pow(saturate(1.0 - t), 3.2);
    col += ColHot * core * 1.05;
    //焰舌高光
    col += ColFire * smoothstep(0.62, 0.92, flame) * head * 0.50;

    float alpha = saturate(head * 0.85 + smoke) * ragged * uFade;
    return float4(col * alpha + ColHot * core * 0.18 * uFade, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
