// ============================================================================
//OniMacheteCrack.fx 熔金裂纹（双 technique）
//GroundTech：鬼手砸地的地面熔金裂缝 decal（横宽 quad，中心热白 → 熔金 → 硫火暗红）
//  裂纹 = 双八度脊线噪声（1-|n-0.5| 阈值），全部输入为 quad uv → 无极角，无缝
//  uLife 0..1：出生整片过曝 → 裂纹定形 → 侵蚀冷却（阈值收紧 + 明度衰减）
//OverlayTech：受创 NPC 的熔金裂纹覆盖层（Immediate 批次重绘 NPC 贴图帧）
//  s0 = SpriteBatch 自动绑定的 NPC 贴图；裂纹在贴图 uv 空间生成，乘 sprite alpha
//预乘 alpha 输出（Ground 配 AlphaBlend；Overlay 输出加色项 alpha=0）
//ps_3_0 / vs_3_0（OverlayTech 仅 ps）
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uLife;   //0..1 decal 生命进度（Ground）
float uSeed;   //实例随机
float uCrack;  //0..1 裂纹强度（Overlay）

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

sampler spriteSamp : register(s0);

static const float3 ColHot = float3(1.60, 1.40, 0.95);
static const float3 ColGold = float3(1.15, 0.75, 0.20);
static const float3 ColBrim = float3(1.05, 0.28, 0.05);
static const float3 ColDark = float3(0.16, 0.04, 0.02);

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

//脊线裂纹场：两八度 1-|n-0.5| 阈值，返回 0..1 裂纹强度
float CrackField(float2 uv, float thin)
{
    float n1 = tex2D(noiseSamp, uv * 1.6 + uSeed).r;
    float n2 = tex2D(noiseSamp, uv * 3.7 + uSeed * 2.3 + 0.31).r;
    float ridge1 = 1.0 - smoothstep(0.0, thin, abs(n1 - 0.5));
    float ridge2 = 1.0 - smoothstep(0.0, thin * 1.6, abs(n2 - 0.5));
    return saturate(ridge1 + ridge2 * 0.55);
}

float4 GroundPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * 2.0;

    //横宽椭圆域：x 沿地面展开，y 压扁
    float dome = length(p * float2(1.0, 1.85));
    if (dome > 1.0)
        return float4(0, 0, 0, 0);
    float domeFade = smoothstep(1.0, 0.55, dome);

    //出生冲击（前 12%）：整片过曝白金
    float birth = 1.0 - smoothstep(0.0, 0.12, uLife);
    //冷却（后 55%）：裂纹收窄 + 压暗
    float cool = smoothstep(0.45, 1.0, uLife);

    //裂纹场：中心粗、边缘细
    float thin = lerp(0.16, 0.055, dome) * (1.0 - cool * 0.55);
    float crack = CrackField(uv * float2(2.2, 1.1), thin) * domeFade;

    //熔浆呼吸：低频噪声缓慢流动（非节拍，单向流）
    float magma = tex2D(noiseSamp, uv * float2(1.3, 0.9) + float2(uTime * 0.05, uSeed)).r;
    float heat = crack * (0.75 + magma * 0.45) * (1.0 - cool * 0.7);

    //色带：裂纹核心热白金 → 熔金 → 硫火 → 焦黑
    float3 col = lerp(ColDark, ColBrim, saturate(heat * 1.6));
    col = lerp(col, ColGold, saturate(heat * 1.25 - 0.35));
    col = lerp(col, ColHot, saturate(heat * 1.1 - 0.72));

    //出生过曝：整片域抬亮
    col += ColHot * birth * domeFade * (0.5 + crack * 0.8);

    //中心余温辉光垫底
    float ember = smoothstep(0.75, 0.0, dome) * (1.0 - cool) * 0.30;
    col += ColBrim * ember;

    float alpha = saturate(crack * (1.0 - cool * 0.85) + birth * domeFade * 0.75 + ember * 0.8);
    alpha *= smoothstep(1.0, 0.80, uLife);   //末段整体淡出
    return float4(col * alpha, alpha);
}

float4 OverlayPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 sprite = tex2D(spriteSamp, coords);
    if (sprite.a < 0.05)
        return float4(0, 0, 0, 0);

    //裂纹在贴图 uv 空间；随 uTime 缓慢换相避免完全静态
    float crack = CrackField(coords * 5.5 + float2(0.0, uTime * 0.02), 0.10);

    //熔金流脉：沿裂纹的亮度起伏
    float magma = tex2D(noiseSamp, coords * 2.8 + float2(uTime * 0.07, uSeed)).r;
    float heat = crack * (0.6 + magma * 0.6) * uCrack;

    float3 col = lerp(ColBrim, ColGold, saturate(heat * 1.4));
    col = lerp(col, ColHot, saturate(heat * 1.2 - 0.65));

    //加色输出（alpha=0），只提亮裂纹处，身体轮廓由 sprite.a 约束
    return float4(col * heat * sprite.a * vertexColor.a, 0.0);
}

technique GroundTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 GroundPS();
    }
}

technique OverlayTech
{
    pass P0
    {
        PixelShader = compile ps_3_0 OverlayPS();
    }
}
