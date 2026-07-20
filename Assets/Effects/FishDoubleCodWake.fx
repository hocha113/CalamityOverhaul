// ============================================================================
//FishDoubleCodWake.fx 双鳕伴飞细水尾流条带（两条带随双鱼螺旋交织成 DNA）
//uv.x: 0=头端(最新，DrawWakeTrail 的 oldPos[0] 侧) 1=尾端(最旧)；像素内翻转为 along(1=头)
//uv.y: 0..1 跨带。顶点色承载 C# 端速度/透明度包络
//亮度结构：uColDeep 暗外圈 → uColFlow 饱和中层 → uColSpec 银鳞碎光只在噪声窄条上瞬现（非常驻）
//极角审计：无 atan2/theta/phi 消费，全部笛卡尔 uv + wrap 贴图采样，无缝隙风险
//Additive 输出（调用方 DrawWakeTrail 设 BlendState.Additive）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //流动相位（含每弹幕相位偏移）
float3 uColDeep;  //深水暗蓝（外缘/尾端）
float3 uColFlow;  //水流蓝（中层）
float3 uColSpec;  //银鳞碎光（窄条高光，随流移动即逝）

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
    float along = 1.0 - uv.x;          //1=头端(鱼尾根) 0=尾端(最旧)
    float across = uv.y * 2.0 - 1.0;   //-1..1 跨带

    //跨带剖面：中心水体 + 两缘细波纹线（水尾流的 V 形涟漪）
    float body = saturate(1.0 - across * across);
    float rim = smoothstep(0.40, 0.72, abs(across)) * smoothstep(1.0, 0.80, abs(across));

    //低频水体噪声（顺流） + 高频碎光噪声（逆流），异速制造水面错动感
    float n1 = tex2D(noiseSamp, float2(along * 2.1 - uTime * 0.85, uv.y * 0.55 + 0.13)).r;
    float n2 = tex2D(noiseSamp, float2(along * 5.2 + uTime * 0.50, uv.y * 1.35 + 0.61)).r;

    //尾端噪声侵蚀：波纹碎成断丝，禁平滑收口
    float erode = smoothstep(along - 0.34, along + 0.06, n1 * 0.86 + 0.07);
    float headT = smoothstep(0.45, 1.0, along);

    //银鳞碎光：高频噪声过阈的窄条，随流动瞬现瞬灭
    float glint = smoothstep(0.62, 0.78, n2) * body * erode;

    //暗外圈 → 饱和中层，被 n1 调制出水体不均
    float3 col = lerp(uColDeep, uColFlow, saturate(body * (0.55 + 0.65 * n1)) * erode);
    col += uColFlow * rim * 0.5 * erode;                       //缘波纹线
    col = lerp(col, uColSpec, glint * (0.30 + 0.40 * headT));  //碎光偏头端

    float alpha = (body * 0.55 + rim * 0.45) * erode * (0.30 + 0.62 * headT);
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
