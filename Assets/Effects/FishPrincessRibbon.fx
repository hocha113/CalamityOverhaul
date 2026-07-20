// ============================================================================
//FishPrincessRibbon.fx 公主鱼缎带条带（沿轨迹的 TriangleStrip）
//uv.x：0=头（弹体端）→ 1=尾梢；uv.y：0..1 横跨条带，0.5=中线
//全笛卡尔条带坐标，无极角，无采样器
//横向结构：奶油芯线（窄，仅头段）→ 粉中带 → 薰衣草边 → 深紫暗缘（暗外圈压制过曝）
//缎面流光沿带缓扫；尾段先沉入暗紫再收梢；uErode 供残迹实体尾部先蚀
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //秒
float uSeed;      //实例随机相位
float uFade;      //整体不透明度
float uErode;     //0..1 尾部先蚀进度，0 完整 1 全蚀
float3 uColMid;   //中带粉
float3 uColEdge;  //边缘薰衣草
float3 uColSheen; //缎面流光奶油金
float3 uColDark;  //暗缘深紫灰

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
    float t = input.TexCoords.x;
    float across = abs(input.TexCoords.y - 0.5) * 2.0;

    //横向色程：粉 → 薰衣草 → 深紫暗缘
    float3 col = lerp(uColMid, uColEdge, smoothstep(0.15, 0.70, across));
    col = lerp(col, uColDark, smoothstep(0.70, 1.0, across) * 0.85);

    //奶油芯线：仅头段中线，非白
    float core = (1.0 - smoothstep(0.0, 0.22, across)) * saturate(1.0 - t * 1.8);
    col = lerp(col, uColSheen, core * 0.40);

    //缎面流光：沿带滑动的高光段
    float sheenPos = frac(uTime * 0.55 + uSeed);
    float sheen = exp(-pow((t - sheenPos) * 6.0, 2.0));
    col += uColSheen * sheen * 0.28;

    //尾段沉入暗紫：先暗后隐
    col = lerp(col, uColDark, smoothstep(0.55, 0.95, t) * 0.60);

    //缎纹微调制：笛卡尔 t，无极角
    col *= 1.0 + 0.06 * sin(t * 22.0 - uTime * 7.0 + uSeed * 6.2832);

    //alpha：边缘软化 + 头淡入 + 尾收梢
    float a = 1.0 - smoothstep(0.55, 1.0, across);
    a *= smoothstep(0.0, 0.06, t);
    a *= 1.0 - smoothstep(0.60, 1.0, t);

    //尾部先蚀：cut 从尾梢向头推进
    float cut = 1.05 - uErode * 1.25;
    a *= 1.0 - smoothstep(cut - 0.14, cut, t);
    a *= uFade;

    return float4(col * a, a);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
