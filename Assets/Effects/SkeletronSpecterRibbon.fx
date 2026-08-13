// ============================================================================
//SkeletronSpecterRibbon.fx 灵息绸带（预警线/骨链筋络/旋杀轨迹通用顶点带）
//UV.x 0尾→1头（沿带） UV.y 0~1 横截面；顶点色 R=芯线增益 A=透明度
//材质：灵息绸带。签名行为：①中脊亮芯+横向羽化 ②纵向噪声流动、边缘撕散
//③两端包络可调（uFadeIn 尾端起弧 / uFadeOut 头端收弧）
//加色批输出 (SourceAlpha, One)；无极角运算
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;      //实例相位
float uFadeIn;    //尾端淡入区宽 0~1
float uFadeOut;   //头端淡出区宽 0~1
float uFlowSpeed; //流速
float3 uCoreColor; //芯线
float3 uBodyColor; //带体
float3 uEdgeColor; //暗缘

// 噪声固定 s1：sampler_state 自动分配会落 s0，图元路径今日侥幸、批次路径必坏；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

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
    float along = input.TexCoords.x;              //0 尾 → 1 头
    float y = (input.TexCoords.y - 0.5) * 2.0;    //-1~1 横
    float coreBoost = input.Color.r;
    float opacity = input.Color.a;

    //---- 纵向流动：双频噪声 ----
    float n = tex2D(noiseSamp, float2(along * 2.4 - uTime * uFlowSpeed + uSeed, y * 0.35 + uSeed * 3.0)).r;
    float n2 = tex2D(noiseSamp, float2(along * 5.0 + uTime * uFlowSpeed * 0.35 + uSeed * 2.0, y * 0.8)).r;

    //---- 带体：横向羽化 + 边缘撕散 ----
    float edge = saturate(1.0 - abs(y));
    float fray = saturate((n - abs(y) * 0.85) * 3.6 + 0.42);
    float body = pow(edge, 1.7) * (0.55 + 0.45 * n) * fray;

    //---- 中脊亮芯 ----
    float core = pow(edge, 7.0) * (0.8 + 0.4 * n2) * coreBoost;

    //---- 两端包络（噪声打散前沿，非平切）----
    float fadeIn = smoothstep(0.0, max(uFadeIn, 0.001), along + (n - 0.5) * 0.08);
    float fadeOut = 1.0 - smoothstep(1.0 - max(uFadeOut, 0.001), 1.0, along - (n - 0.5) * 0.08);
    float envelope = fadeIn * fadeOut;

    float3 col = uEdgeColor * body * 0.6
        + uBodyColor * body * (0.5 + n * 0.4)
        + uCoreColor * core;

    float alpha = saturate(body + core) * envelope * opacity;
    return float4(col, alpha);
}

technique Technique1
{
    pass SkeletronSpecterRibbonPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
