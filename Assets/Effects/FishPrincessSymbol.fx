// ============================================================================
//FishPrincessSymbol.fx 公主鱼绘本符号弹体（心/星 SDF 平涂+描边+高光点）
//四边形 uv 0..1，p = uv*2-1，y 向下（屏幕系）；符号形状即弹体几何，非贴纸叠加
//绘本语言：粉彩平涂纵向渐变 + 深玫瑰描边 + 奶油金高光点，无泛光无纯白
//星形折角按 2π/5 整数折叠，atan2 跳变被 wrap 吃掉（接缝协议合规）
//预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uShape;     //0 心 1 星
float uSigil;     //0 实体弹 1 描边符印（平涂压到两成）
float uFade;      //整体不透明度
float uPulse;     //呼吸亮度 0..1
float3 uColFill;  //粉彩平涂主色（顶部亮端）
float3 uColDeep;  //底部深一档同族色
float3 uColInk;   //描边墨色（深玫瑰）
float3 uColGloss; //高光点奶油金

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

float dot2(float2 v)
{
    return dot(v, v);
}

//IQ 2D 心形 SDF：心占 y 0..1，尖端在原点，圆瓣朝上（输入已翻转屏幕 y）
float sdHeart(float2 p)
{
    p.x = abs(p.x);
    if (p.y + p.x > 1.0)
        return sqrt(dot2(p - float2(0.25, 0.75))) - 0.35355;
    return sqrt(min(dot2(p - float2(0.0, 1.0)), dot2(p - 0.5 * max(p.x + p.y, 0.0)))) * sign(p.x - p.y);
}

//五角星 SDF：角度按 72° 折叠后回笛卡尔求边线符号距离，负值在星内
float sdStar(float2 p)
{
    const float seg = 1.2566371; //2π/5
    float a = atan2(p.x, -p.y);  //0 对准屏幕上方
    a = a - seg * floor(a / seg);
    a = abs(a - seg * 0.5);
    float2 q = float2(cos(a), sin(a)) * length(p);
    //外顶点 R=0.92，内凹点 r=0.42（B = (cos36°, sin36°)*0.42）
    float2 A = float2(0.92, 0.0);
    float2 w = q - A;
    //e = B-A = (-0.58021, 0.24687)，|e| = 0.63054
    return -((-0.58021) * w.y - 0.24687 * w.x) / 0.63054;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 p = input.TexCoords * 2.0 - 1.0;

    float sd;
    if (uShape < 0.5)
        sd = sdHeart(float2(p.x * 0.80, 0.52 - p.y * 0.62));
    else
        sd = sdStar(p);

    float fillA = smoothstep(0.05, -0.05, sd);
    float outlineA = 1.0 - smoothstep(0.09, 0.15, abs(sd));

    //纵向粉彩渐变：顶亮底深
    float g = saturate(p.y * 0.55 + 0.45);
    float3 body = lerp(uColFill, uColDeep, g);

    //高光点：左上小椭圆，奶油金非纯白，只落在填充内
    float gloss = 1.0 - smoothstep(0.08, 0.22, length((p - float2(-0.26, -0.33)) * float2(1.0, 1.4)));
    gloss *= smoothstep(0.02, -0.06, sd);
    body += uColGloss * gloss * 0.5;
    body *= 1.0 + uPulse * 0.10;

    float3 col = lerp(body, uColInk, outlineA);
    float a = saturate(max(fillA * lerp(1.0, 0.20, uSigil), outlineA)) * uFade;
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
