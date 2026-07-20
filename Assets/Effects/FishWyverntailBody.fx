// ============================================================================
//FishWyverntailBody.fx 云蛟白龙蛇形体节条带（沿重采样轨迹的 TriangleStrip）
//uv.x：0=颈根(头端) → 1=尾梢；uv.y：0..1 横跨条带，0.5=中脊
//顶点色 r：uv.y=0 侧朝天权重(0..1)，珍珠白受光面与灰蓝腹影的插值轴
//全笛卡尔条带坐标，无极角，接缝协议天然合规
//
//珍珠白靠暗部塑形：灰蓝腹影坐底、受光面才到珍珠白、背脊窄条金鬃点缀；
//体节暗纹沿体向尾传播游动相位，鳞噪只压背光侧，尾梢噪声撕裂化云。
//预乘 alpha 配 BlendState.AlphaBlend，暗部真正能压暗背景
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //秒
float uSeed;      //实例随机相位
float uFade;      //整体不透明度（出生淡入）
float uSwimPhase; //体节游动相位，随速度增速
float uDissolve;  //0..1 化云侵蚀，从尾向头推进

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

//灰蓝腹影 / 珍珠白（非纯白）/ 金鬃
static const float3 ColShadow = float3(0.33, 0.40, 0.53);
static const float3 ColPearl = float3(0.93, 0.95, 0.99);
static const float3 ColMane = float3(0.87, 0.68, 0.34);

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
    float t = input.TexCoords.x;        //0 颈根 → 1 尾梢
    float y = input.TexCoords.y - 0.5;  //-0.5..0.5
    float across = saturate(abs(y) * 2.0);  //0 中脊 → 1 边缘
    float body = 1.0 - across;

    //受光面：uv.y=0 侧受光度=顶点r，另一侧取反，转弯时沿体平滑过渡
    float faceLight = lerp(input.Color.r, 1.0 - input.Color.r, input.TexCoords.y);

    //体节暗纹：sin 沿体向尾传播（9节），节沟微暗读作环节
    float seg = sin(t * 56.5 - uSwimPhase * 2.0);
    float segShade = smoothstep(0.25, 0.9, seg) * 0.16;

    //鳞纹噪声：只压背光侧，亮面保持珍珠光洁
    float scaleN = tex2D(noiseSamp, float2(t * 5.0 - uTime * 0.35 + uSeed * 9.0, y * 2.2 + 0.5 + uSeed)).r;

    //明度组装：腹影 → 珍珠白，受光+中脊提亮，节沟与鳞纹压暗
    float lum = saturate(faceLight * 0.72 + body * 0.34 - segShade - scaleN * 0.22 * (1.0 - faceLight));
    float3 col = lerp(ColShadow, ColPearl, lum);

    //背脊金鬃：受光侧最外缘窄条，噪声撕成鬃束，颈段最盛尾段收没
    float maneSide = smoothstep(0.35, 0.65, input.Color.r);
    float edgeUp = lerp(input.TexCoords.y, 1.0 - input.TexCoords.y, maneSide);
    float maneBand = smoothstep(0.78, 0.97, edgeUp);
    float maneN = tex2D(noiseSamp, float2(t * 9.0 - uSwimPhase * 0.35 + uSeed * 5.0, 0.13 + uSeed)).r;
    float mane = maneBand * smoothstep(0.38, 0.82, maneN) * pow(saturate(1.0 - t), 0.6);
    col = lerp(col, ColMane, mane * 0.85);

    //尾梢噪声撕裂：末端读作散开的云缕，禁平滑收口
    float erodeN = tex2D(noiseSamp, float2(t * 3.2 + uSeed * 13.0, y * 1.4 + 0.5 - uSeed)).r;
    float tailFade = smoothstep(1.02, 0.55, t + (0.5 - erodeN) * 0.30);

    //化云侵蚀：uDissolve 推进时蚀线从尾(t=1)向头(t=0)扫过，边缘吃同一噪声
    float front = uDissolve * 1.30;
    float dissolveMask = smoothstep(-0.02, 0.16, (1.0 - front) - t + (erodeN - 0.5) * 0.34);

    //横向轮廓：圆柱边缘收透明，鳞噪撕一点毛边
    float edgeA = smoothstep(1.0, 0.62, across + (scaleN - 0.5) * 0.18);
    //颈根淡入：接缝藏进头贴图之下
    float neckIn = smoothstep(0.0, 0.045, t);

    float alpha = edgeA * tailFade * dissolveMask * neckIn * uFade;
    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    //化云蚀线泛灰蓝云色：残影消散处偏冷偏淡而非发亮
    float edgeGlow = saturate(1.0 - dissolveMask) * uDissolve;
    col = lerp(col, float3(0.62, 0.70, 0.82), edgeGlow * 0.6);

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
