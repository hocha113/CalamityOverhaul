// ============================================================================
//FishIchornJet.fx 灵流蚀甲液柱条带（沿 oldPos 轨迹的 TriangleStrip）
//uv.x：0=头端(最新，oldPos[0] 侧) 1=尾端(最旧)；像素内翻转为 along（1=头 0=尾）。uv.y：0..1 跨带
//全笛卡尔条带坐标 + wrap 贴图采样，无 atan2/theta/phi，无极角接缝风险
//
//层次：暗金外缘 → 深金 → 高饱和金黄液体（流动噪声调制粘稠不均）→ 偏离中线的窄湿面高光
//→ 头端液锋极小亮芯。尾段 Plateau-Rayleigh 失稳：噪声阈值随 along 降低收紧，
//液柱被撕成滴串渐散，绝非平滑收口
//预乘 alpha 输出，配 BlendState.AlphaBlend：暗金外缘真正压暗背景，读作有体积的液体
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //流动相位秒
float uSeed;        //实例相位（whoAmI 派生），避免多股射流同相
float uFade;        //整体包络 0..1（出生淡入/消散）
float3 uColDark;    //暗金基底
float3 uColDeep;    //深金
float3 uColGold;    //高饱和金黄
float3 uColBright;  //亮芯，仅小面积

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
    //along: 1=头端(最新) 0=尾端(最旧)
    float along = 1.0 - input.TexCoords.x;
    float y = input.TexCoords.y - 0.5;

    //液柱摆动：双频噪声推挤横向坐标，尾段振幅增大（失稳前兆）
    float wob1 = tex2D(noiseSamp, float2(along * 1.6 + uTime * 1.1 + uSeed, uSeed * 0.7)).r - 0.5;
    float wob2 = tex2D(noiseSamp, float2(along * 4.2 + uTime * 1.9 + uSeed * 3.0, 0.41)).r - 0.5;
    y += (wob1 * 0.42 + wob2 * 0.22) * (1.0 - along) * 0.55;

    float across = saturate(abs(y) * 2.0);
    float body = 1.0 - across;

    //表面流动：沿柱向尾滑动的粘稠条纹，比水慢
    float flow = tex2D(noiseSamp, float2(along * 2.6 + uTime * 0.8 + uSeed * 7.0, y * 1.3 + 0.5)).r;

    //尾段珠化断裂：阈值随 along 降低收紧，液柱被撕成滴串
    float beadGate = smoothstep(0.30 - along * 0.26, 0.66 - along * 0.30, body * (0.52 + flow * 0.72));
    if (beadGate < 0.004)
        return float4(0, 0, 0, 0);

    //亮度骨架：头亮尾灭
    float head = smoothstep(0.15, 0.95, along);

    //色程：暗金缘 → 深金 → 饱和金；flow 调制出粘稠不均
    float3 col = lerp(uColDark, uColDeep, saturate(body * 1.7));
    col = lerp(col, uColGold, saturate(pow(body, 2.0) * (0.45 + 0.55 * flow) * (0.55 + 0.45 * head)));

    //湿面高光：偏离中线的一条窄反光带，随摆动起伏（液体镜面）
    float spec = saturate(1.0 - abs(y - 0.14) * 7.5);
    col += uColBright * pow(spec, 3.0) * 0.30 * (0.35 + 0.65 * head);

    //头端液锋亮芯：极小面积
    float core = pow(body, 5.0) * smoothstep(0.75, 1.0, along);
    col += uColBright * core * 0.85;

    float alpha = beadGate * (0.30 + 0.70 * body) * (0.25 + 0.75 * head) * uFade;
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
