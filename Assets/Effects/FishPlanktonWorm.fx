// ============================================================================
//FishPlanktonWorm.fx 腐虫体节条带（沿虫中脊线的 TriangleStrip）
//uv.x：0=头 → 1=尾梢；uv.y：0..1 横跨条带，0.5=中脊
//全笛卡尔条带坐标，无极角，接缝协议天然合规
//
//质感：暗腐绿哑光体节，环纹相位随 uCrawlPhase 沿体后传（与 CPU 推进波同源），
//节间沟槽露肉粉膜，低频噪声腐斑压暗，中脊窄湿光带（淡绿白，非纯白）；
//uDissolve 尾向头噪声蚀散（腐解退场，撕裂边界禁平滑收口）。
//顶点色 = 环境光，全程相乘，零自发光。预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uSeed;        //实例随机相位
float uFade;        //整体不透明度
float uCrawlPhase;  //蠕动推进相位，弧度，与 CPU 波同源
float uDissolve;    //0..1 腐解进度，尾向头蚀散
float uBiteHeat;    //0..1 咬击充血，头段短暂泛肉红

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

static const float SegN = 9.0;  //可见环节数

//暗腐绿黑 / 腐绿褐 / 节间肉粉 / 湿光淡绿白
static const float3 ColDark = float3(0.102, 0.118, 0.055);
static const float3 ColBody = float3(0.345, 0.400, 0.157);
static const float3 ColFlesh = float3(0.674, 0.463, 0.440);
static const float3 ColWet = float3(0.776, 0.815, 0.660);

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
    float t = input.TexCoords.x;          //0 头 → 1 尾梢
    float y = input.TexCoords.y - 0.5;    //-0.5..0.5
    float across = saturate(abs(y) * 2.0);

    //体节环纹：相位沿体向尾传递，读作蠕动的推进波掠过体表
    float ring = sin(t * SegN * 6.2831853 - uCrawlPhase);

    //节沟：环纹负半程收成沟槽
    float groove = smoothstep(0.15, -0.6, ring);

    //腐斑：低频噪声暗斑，实例相位错开
    float blotch = tex2D(noiseSamp, float2(t * 2.4 + uSeed * 7.0, y * 0.9 + uSeed * 3.0)).r;

    //横向体色：中脊腐绿 → 侧腹压暗收成剪影
    float3 col = lerp(ColBody, ColDark, across * across * 0.85);
    //节沟露肉粉膜，只在中脊附近可见
    col = lerp(col, ColFlesh, groove * 0.40 * (1.0 - across));
    //节腹微鼓：环纹正半程轻提亮度，塑出体节起伏
    col += ColBody * saturate(ring) * 0.14 * (1.0 - across);
    //腐斑压暗
    col = lerp(col, ColDark, smoothstep(0.60, 0.95, blotch) * 0.5);
    //中脊湿光带：极窄、随环节起伏闪动、淡绿白非纯白
    float wet = pow(1.0 - across, 6.0) * (0.30 + 0.35 * saturate(ring));
    col += ColWet * wet * 0.28;
    //咬击充血：头段短暂泛肉红
    col = lerp(col, ColFlesh, uBiteHeat * saturate(1.0 - t * 2.6) * 0.45);

    //轮廓收形：边缘快速压掉
    float edge = smoothstep(1.0, 0.80, across);

    //腐解蚀散：尾向头推进，噪声撕裂边界
    float erode = tex2D(noiseSamp, float2(t * 4.6 + uSeed * 5.0, y * 2.0 + uSeed * 9.0)).r;
    float front = 1.05 - uDissolve * 1.25;
    float alive = smoothstep(-0.10, 0.10, front - t + (erode - 0.5) * 0.30);

    float alpha = edge * alive * uFade;
    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    //乘环境光：洞穴里就该是暗的
    col *= input.Color.rgb;
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
