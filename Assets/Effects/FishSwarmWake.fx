// ============================================================================
//FishSwarmWake.fx 鱼形换影群体流线束（鱼群整体共享的水流缎带，非单鱼尾流）
//uv.x: 0=头端(最新，玩家当前位置侧) 1=尾端(最旧)；像素内翻转为 along(1=头)
//uv.y: 0..1 跨带。顶点色承载 C# 端速度/退场包络
//亮度结构：uColDeep 暗外圈 → uColFlow 饱和中层 → 内部细流线与水纹线 → uColSpec 碎鳞光斑瞬现
//水纹折射暗示：跨带 sin 细线；全部笛卡尔 uv 输入，无 atan2/theta/phi，无极角缝隙风险
//Additive 输出（调用方设 BlendState.Additive）
// ============================================================================

float4x4 transformMatrix;
float uTime;      //流动相位（含每实例相位偏移）
float3 uColDeep;  //深水暗蓝（外缘/尾端压底）
float3 uColFlow;  //水流蓝（饱和中层）
float3 uColSpec;  //鳞银碎光（窄点高光，随流瞬现）

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
    float along = 1.0 - uv.x;          //1=头端 0=尾端
    float across = uv.y * 2.0 - 1.0;   //-1..1 跨带

    float body = saturate(1.0 - across * across);

    //低频水体（顺流）与高频碎光（逆流）双噪声，异速产生水层错动
    float n1 = tex2D(noiseSamp, float2(along * 1.7 - uTime * 0.70, uv.y * 0.55 + 0.21)).r;
    float n2 = tex2D(noiseSamp, float2(along * 4.6 + uTime * 0.38, uv.y * 1.60 + 0.63)).r;

    //共享流线：个体沿其游动的内部细亮线，被低频噪声扰弯
    float lineWave = sin((uv.y * 4.0 + (n1 - 0.5) * 0.9) * 6.2831853 + uTime * 2.1);
    float streamline = smoothstep(0.82, 0.97, lineWave) * body;

    //更细更弱的逆向水纹线（折射暗示）
    float rippleWave = sin((uv.y * 9.0 - (n2 - 0.5) * 0.6) * 6.2831853 - uTime * 3.0);
    float ripple = smoothstep(0.88, 0.99, rippleWave) * body * 0.5;

    //尾端噪声侵蚀，禁平滑收口
    float erode = smoothstep(along - 0.38, along + 0.05, n1 * 0.9 + 0.05);
    float headT = smoothstep(0.35, 1.0, along);

    //鳞银碎光：高频噪声过阈的小斑，仅偏头端瞬现
    float glint = smoothstep(0.80, 0.93, n2) * body * erode * headT;

    float3 col = lerp(uColDeep, uColFlow, saturate(body * (0.45 + 0.55 * n1)) * erode);
    col += uColFlow * (streamline * 0.55 + ripple * 0.30) * erode;
    col = lerp(col, uColSpec, saturate(glint * 0.85));

    float alpha = (body * 0.40 + streamline * 0.28 + ripple * 0.14 + glint * 0.5) * erode * (0.20 + 0.55 * headT);
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
