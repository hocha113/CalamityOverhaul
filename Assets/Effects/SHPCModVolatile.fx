// ============================================================================
//SHPCModVolatile.fx 不稳定机匣：变异光束故障覆层
//Trail 条带 Additive，叠在 CyberTraceBeam 之上；
//色相随机跳变 + 行撕裂 + 数字坏块 + RGB 通道分离，变异色由 C# 传入
//uv.x=along(0头部 1尾端) uv.y=cross；无 atan2/极坐标，无接缝审计项
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;            //每束独立随机种子，错开跳变节奏
float fadeAlpha;        //整体透明度 0~1
float uGlitch;          //0~1 事件故障尖峰（急转/抖折/命中时拉高后衰减）
float3 baseColor;       //变异主色（裂变青/过载红/失稳橙/畸变紫）
float3 accentColor;     //变异强调色（坏块与描边）

sampler noiseSamp : register(s1); //Extra_193 Voronoi，消费端 Textures[1]+LinearWrap

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

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//绕灰轴的 Rodrigues 旋转做色相偏移，无需极坐标
float3 hueRotate(float3 c, float a)
{
    const float3 k = float3(0.57735, 0.57735, 0.57735);
    float ca = cos(a);
    float sa = sin(a);
    return c * ca + cross(k, c) * sa + k * dot(k, c) * (1.0 - ca);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;      //0=头部 1=尾端
    float cross_ = uv.y;

    //=
    //A. 色相跳变 ： 阶梯时间量化，每步随机转一个色相角；
    //   慢步大幅 + 快步小幅两层叠加，故障尖峰时跳变幅度放大
    //=
    float stepSlow = floor(uTime * 5.0 + uSeed * 13.7);
    float stepFast = floor(uTime * 17.0 + uSeed * 5.3);
    float hueOff = (hash21(float2(stepSlow, uSeed)) - 0.5) * 2.6;
    hueOff += (hash21(float2(stepFast, uSeed + 4.7)) - 0.5) * 0.9;
    float3 effColor = hueRotate(baseColor, hueOff * (0.55 + uGlitch * 0.65));
    float3 effAccent = hueRotate(accentColor, hueOff * 0.35);

    //=
    //B. 行撕裂 ： 横向切成数据行，随机行沿 along 方向错位
    //=
    float rowID = floor(cross_ * 22.0);
    float rowHash = hash21(float2(rowID, floor(uTime * 9.0 + uSeed)));
    float rowOn = step(0.74 - uGlitch * 0.30, rowHash);
    float rowShift = rowOn * (rowHash - 0.5) * (0.09 + uGlitch * 0.24);
    float rAlong = along + rowShift;
    float rCross = cross_;
    float crossDist = abs(rCross - 0.5) * 2.0;   //0=中心 1=边缘

    //=
    //C. 核心缎带 ： 覆层核心比主光束更细更幽灵，噪声蚀边
    //=
    float n1 = tex2D(noiseSamp, frac(float2(rAlong * 3.0 - uTime * 1.8, rCross * 0.9 + uSeed))).r;
    float coreW = 0.16 + n1 * 0.06 + uGlitch * 0.05;
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.5);

    //RGB 通道分离：R/B 各取一个横向偏移的核心蒙版
    float chrom = 0.06 + uGlitch * 0.10;
    float coreR = 1.0 - smoothstep(0.0, coreW, abs(rCross - 0.5 + chrom) * 2.0);
    float coreB = 1.0 - smoothstep(0.0, coreW, abs(rCross - 0.5 - chrom) * 2.0);

    //=
    //D. 数字坏块 ： 网格化，部分块挖空（数据缺口）部分块炸亮（坏块）
    //=
    float2 blockID = float2(floor(rAlong * 20.0), floor(rCross * 5.0));
    float bHash = hash21(blockID + floor(uTime * 11.0 + uSeed) * 0.37);
    float holeOn = step(0.86 - uGlitch * 0.12, bHash);            //挖空块
    float hotOn = step(0.90 - uGlitch * 0.14, frac(bHash * 7.31)); //炸亮块
    float holeMask = 1.0 - holeOn * 0.85;

    //=
    //E. 扫描细线 ： 沿 cross 高频亮线，低速滚动
    //=
    float scan = step(0.92, frac(cross_ * 26.0 - uTime * 2.2));

    //=
    //F. 包络 ： 头部微收、尾端渐隐、边缘收口
    //=
    float headRise = smoothstep(0.0, 0.05, along);
    float tailFade = 1.0 - smoothstep(0.60, 1.0, along);
    float edgeMask = 1.0 - smoothstep(0.55, 1.0, crossDist);

    //闪断：整条缎带偶发瞬灭一帧，制造"信号不稳"
    float blink = step(hash21(float2(floor(uTime * 13.0), uSeed * 3.1)), 0.93);

    //=
    //颜色合成 ： 通道分离三层 + 坏块 + 扫描线
    //=
    float3 color = float3(0.0, 0.0, 0.0);
    color.r += effColor.r * coreR * 0.85;
    color.g += effColor.g * core * 0.85;
    color.b += effColor.b * coreB * 0.85;
    color += effAccent * hotOn * (0.45 + uGlitch * 0.55);
    color += effColor * scan * 0.22;
    color += effAccent * n1 * core * 0.30;

    float alpha = saturate(core * 0.55 + (coreR + coreB) * 0.10 + hotOn * 0.30 + scan * 0.08);
    alpha *= holeMask * edgeMask * headRise * tailFade * blink * fadeAlpha;
    color *= holeMask * blink;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass SHPCModVolatilePass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
