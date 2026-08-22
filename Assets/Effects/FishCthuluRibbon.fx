// ============================================================================
//FishCthuluRibbon.fx 深渊凝视之瞳冲刺暗绸带（沿 oldPos 轨迹的 TriangleStrip）
//uv.x：0=弹头 → 1=尾梢；uv.y：0..1 横跨条带，0.5=中线
//全笛卡尔条带坐标，无极角 → 接缝协议天然合规
//
//调性与彗尾相反：不是发光的火，是遮暗背景的绸，暗紫黑边缘 → 暗血肉中层，
//缎面流纹只做 ±15% 明暗游动，唯一暖点 = 中线极细暗红芯（仅头段，禁白）。
//预乘 alpha，配 BlendState.AlphaBlend：高 alpha 暗色真正压暗画面
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uFade;   //整体不透明度（冲刺起淡入、冲刺后残迹独立衰减）

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

//暗紫黑虚空 / 暗血肉 / 虹膜暗红（芯，非白非亮橙）
static const float3 ColVoid = float3(0.055, 0.030, 0.075);
static const float3 ColFlesh = float3(0.165, 0.052, 0.070);
static const float3 ColIris = float3(0.60, 0.095, 0.095);

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

    //绸带摆动：低频缎波 + 细波推挤横向坐标，越近尾摆幅越大（绸尾飘）
    float wob1 = tex2D(noiseSamp, float2(t * 1.6 - uTime * 0.9 + uSeed * 5.0, uSeed)).r - 0.5;
    float wob2 = tex2D(noiseSamp, float2(t * 4.2 - uTime * 1.5 + uSeed * 2.0, 0.41 + uSeed)).r - 0.5;
    y += (wob1 * 0.42 + wob2 * 0.18) * (0.25 + t * 0.75) * 0.55;

    float across = saturate(abs(y) * 2.0);   //0 中线 → 1 边缘
    float body = 1.0 - across;

    //缎面流纹：沿带向尾流动的低频明暗（绸的光泽=明暗游动，不是提亮）
    float sheen = tex2D(noiseSamp, float2(t * 2.6 - uTime * 1.3 + uSeed * 9.0, y * 1.2 + 0.5 + uSeed)).r;

    //边缘/尾梢撕碎：噪声阈值随 t 收紧，末端读作散开的绸缕
    float ragged = smoothstep(0.14 + t * 0.42, 0.72, body + (sheen - 0.5) * (0.30 + t * 0.70));
    if (ragged < 0.004)
        return float4(0, 0, 0, 0);

    //强度包络：头实尾散
    float head = pow(saturate(1.0 - t), 1.35);

    //色程：暗紫黑边缘 → 暗血肉中层，缎光 ±15%
    float3 col = lerp(ColVoid, ColFlesh, smoothstep(0.25, 0.90, body));
    col *= 0.85 + sheen * 0.30;

    //暗红芯：中线极细、仅头段前 40%，read 作瞳孔拖出的一线血光
    float core = pow(body, 7.0) * pow(saturate(1.0 - t), 2.6);
    col += ColIris * core;

    float alpha = head * 0.88 * ragged * uFade;
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
