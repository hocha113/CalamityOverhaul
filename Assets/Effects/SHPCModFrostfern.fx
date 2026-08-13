// ============================================================================
//SHPCModFrostfern.fx 霜蕨枪管冰晶脉络
//Trail 条带 Additive 批；uv.x=along 0根→1梢，uv.y=cross
//全笛卡尔无极角；直线算术无分支，单次 tex2D
// ============================================================================

sampler noiseSamp : register(s1);   //Voronoi 晶粒，消费端 Textures[1]+LinearWrap 绑定

float4x4 transformMatrix;
float uTime;
float fadeAlpha;       //整体透明度 0~1
float uGrow;           //结晶前沿 0~1，along 超过即未生成
float uDissolve;       //消融进度 0~1，自梢向根蚀散
float uSeed;           //每株随机相位
float3 coreColor;      //冰脊白芯
float3 glowColor;      //晶面青辉

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
    float along = input.TexCoords.x;
    float crossDist = abs(input.TexCoords.y - 0.5) * 2.0;

    //静态晶粒，位置冻结不随时间流动（冰=凝固介质）
    float vor = tex2D(noiseSamp, float2(along * 4.5 + uSeed, input.TexCoords.y * 1.4 + uSeed * 0.63)).r;

    //结晶门：前沿之外未生成；前沿一点白热亮尖
    float grown = step(along, uGrow);
    float front = (1.0 - smoothstep(0.0, 0.10, abs(along - uGrow))) * step(uGrow, 0.995);

    //消融自梢向根，晶粒咬出碎口
    float erode = uDissolve * (0.4 + along * 1.05);
    float alive = saturate(1.0 - (erode * 1.5 - vor * 0.6));

    //锐利冰脊
    float spine = pow(saturate(1.0 - smoothstep(0.0, 0.18, crossDist)), 2.2);
    //晶棱硬边，晶粒咬边成碎棱
    float facet = 1.0 - smoothstep(0.5, 0.86, crossDist + (vor - 0.5) * 0.24);
    //棱面霜砂
    float frost = facet * (0.55 + vor * 0.45);

    //晶面闪，分段 hash 偶发
    float band = floor(along * 9.0 + uSeed * 13.0);
    float tw = frac(sin(band * 91.17) * 43758.55);
    float glint = step(0.93, frac(tw + uTime * 0.36)) * (1.0 - smoothstep(0.0, 0.42, crossDist));

    float3 color = coreColor * spine * 1.35
                 + glowColor * frost * 0.6
                 + float3(1.0, 1.0, 1.0) * (front * 0.95 + glint * 0.7);
    float alpha = saturate(spine + frost * 0.55 + front * 0.85 + glint * 0.55);
    alpha *= alive * grown * fadeAlpha;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass FrostfernPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
