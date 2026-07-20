// ============================================================================
//PallbearerTrail.fx 抬棺人血红拖尾条带（棺钉 / 掷棺回旋共用）
//色彩纪律 v2：焦黑 uColDark + 深红 uColEdge + 血色 uColCore；头端小面积暖色过曝（瞬时，
//随条带头移动即逝），无青/绿/蓝、无大面积常驻纯白。
//头亮尾灭：亮度集中在头端，尾端被噪声侵蚀成余烬断丝。
//uv.x: 0=头端(最新，GraniteMarbleVFX.DrawTrailFromOldPos 的 oldPos[0] 侧) 1=尾端(最旧)；
//像素内先翻转为 along（1=头 0=尾）再做侵蚀/提亮。uv.y: 0..1 跨带。顶点色承载 C# 端透明度包络。
//极角审计：无 atan2/theta/phi 消费，全部为笛卡尔 uv + 贴图采样，无缝隙风险。
//Additive 输出（调用方 GraniteMarbleVFX.DrawTrailFromOldPos 设 BlendState.Additive）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;          //流动相位（含每弹幕相位偏移）
float3 uColCore;      //血色（核心）
float3 uColEdge;      //深红（过渡）
float3 uColDark;      //焦黑（外缘）

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
    float2 uv = input.TexCoords;
    //along: 1=头端(最新) 0=尾端(最旧)
    float along = 1.0 - uv.x;

    //跨带抛物线剖面：中心 1 → 两缘 0
    float across = uv.y * 2.0 - 1.0;
    float profile = saturate(1.0 - across * across);

    //沿带流动噪声：两个不同频率相位反向，制造血焰撕扯感
    float n1 = tex2D(noiseSamp, float2(along * 1.7 - uTime, uv.y * 0.6)).r;
    float n2 = tex2D(noiseSamp, float2(along * 3.4 + uTime * 0.6, uv.y * 1.1 + 0.37)).r;

    //尾端侵蚀：越靠尾侵蚀阈值越高，噪声决定余烬断丝形状
    float erodeGate = smoothstep(along - 0.30, along + 0.08, n1 * 0.82 + 0.10);

    //亮度骨架：剖面 × 侵蚀 × 头端增强（头亮尾灭）
    float body = profile * erodeGate;
    float headBoost = smoothstep(0.5, 1.0, along);

    //调色：外缘焦黑 → 深红 → 核心血色；核心权重被 n2 调制出血焰不均
    float coreT = pow(profile, 3.0) * (0.55 + 0.45 * n2);
    float3 col = lerp(uColDark, uColEdge, saturate(profile * 1.5));
    col = lerp(col, uColCore, saturate(coreT * (0.6 + 0.6 * headBoost)));

    //头端暖色过曝：小面积、随头移动即逝的血橙热芯
    col = lerp(col, float3(1.02, 0.46, 0.24), headBoost * coreT * 0.6);

    float alpha = body * (0.3 + 0.7 * headBoost);

    //Additive：预乘颜色即可，顶点色承载包络
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
