// ============================================================================
//CyberGlitchBolt.fx 赛博空间故障闪电
//Trail 条带 Additive
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;        //整体透明度 0~1
float visibleStart;     //可见段起点 0~1（收缩时从0上升）
float visibleEnd;       //可见段终点 0~1（延伸时从0→1）
float glitchSeed;       //本实例随机种子

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

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;          //0=起点 1=末端
    float cross_ = uv.y;         //0=上边 1=下边
    float crossDist = abs(cross_ - 0.5) * 2.0; //0=中心 1=边缘

    //可见区域遮罩
    float headMask = smoothstep(visibleEnd + 0.04, visibleEnd - 0.02, along);
    float tailMask = smoothstep(visibleStart - 0.02, visibleStart + 0.04, along);
    float visMask = headMask * tailMask;
    if (visMask < 0.001)
        return float4(0, 0, 0, 0);

    //噪声采样
    float n1 = tex2D(noiseSamp, frac(float2(along * 3.0 + uTime * 1.2, cross_ * 0.6 + glitchSeed))).r;
    float n2 = tex2D(noiseSamp, frac(float2(along * 7.0 - uTime * 2.0, cross_ * 1.3 + 0.37))).g;
    float n3 = tex2D(noiseSamp, frac(float2(along * 1.8 + uTime * 0.7, cross_ * 2.5 + 0.61))).b;

    //=
    //A. 核心裂缝：白热闪电芯（现实被撕裂的缝隙）
    //=
    float coreW = 0.10 + n1 * 0.06;
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.3);
    float coreFlicker = 0.75 + 0.25 * sin(uTime * 22.0 + along * 50.0 + glitchSeed * 10.0);
    core *= coreFlicker;

    //=
    //B. 中层红色辉光
    //=
    float midW = 0.32 + n2 * 0.1;
    float mid = 1.0 - smoothstep(coreW * 0.5, midW, crossDist);
    mid *= 0.65;

    //=
    //C. 外层暗红光晕
    //=
    float outer = 1.0 - smoothstep(0.2, 1.0, crossDist);
    outer *= 0.3;

    //=
    //D. 数字故障方块：黑墙数据入侵的核心视觉
    //=
    //大方块层（低频闪烁：整块出现/消失的数据损坏区域）
    float bx = floor(along * 18.0 + glitchSeed * 5.0);
    float by = floor(cross_ * 4.0);
    float blockTime = floor(uTime * 8.0);
    float bHash = hash21(float2(bx + blockTime * 7.1, by + glitchSeed * 13.0));
    float blockOn = step(0.60, bHash);
    float blockFill = bHash * blockOn;
    //方块内边距（硬边矩形，不是渐变）
    float bxFrac = frac(along * 18.0 + glitchSeed * 5.0);
    float byFrac = frac(cross_ * 4.0);
    float blockEdge = step(0.05, bxFrac) * step(bxFrac, 0.95)
                    * step(0.06, byFrac) * step(byFrac, 0.94);
    blockFill *= blockEdge;

    //小方块层（高频细节：更密集的数据碎片）
    float sbx = floor(along * 48.0);
    float sby = floor(cross_ * 8.0);
    float sTime = floor(uTime * 14.0);
    float sHash = hash21(float2(sbx + sTime * 11.3, sby + glitchSeed * 7.7));
    float subBlock = step(0.78, sHash) * sHash;
    subBlock *= (1.0 - crossDist * 0.6);

    //=
    //E. 水平数据条纹：故障撕裂横纹
    //=
    float stripIdx = floor(cross_ * 10.0);
    float stripTime = floor(uTime * 6.0);
    float stripHash = hash21(float2(stripIdx + stripTime * 3.7, glitchSeed * 19.0));
    float stripOn = step(0.72, stripHash);
    float stripNoise = tex2D(noiseSamp, frac(float2(along * 15.0 + uTime * 4.0, stripIdx * 0.17))).r;
    float strip = stripOn * smoothstep(0.30, 0.70, stripNoise) * (1.0 - crossDist * 0.4);

    //=
    //F. 纵向数据流纹：沿闪电方向的流动痕迹
    //=
    float streamUV = frac(along * 12.0 - uTime * 3.5 + glitchSeed * 4.0);
    float stream = smoothstep(0.0, 0.08, streamUV) * smoothstep(0.3, 0.12, streamUV);
    stream *= (1.0 - crossDist * 0.8) * 0.4;

    //=
    //G. 边缘腐蚀：噪声驱动的不规则撕裂边界
    //=
    float edgeNoise = n2 * 0.22 + n3 * 0.18;
    float edgeMask = 1.0 - smoothstep(0.50 - edgeNoise, 0.96, crossDist);

    //=
    //H. 尖端光斑：延伸前端的高亮
    //=
    float tipDist = abs(along - visibleEnd);
    float tipFlare = 1.0 - smoothstep(0.0, 0.06, tipDist);
    tipFlare *= (1.0 - crossDist * 0.7);
    tipFlare *= 0.4 + 0.6 * sin(uTime * 30.0 + glitchSeed * 7.0);

    //=
    //颜色合成
    //=
    float3 cWhiteHot  = float3(1.0, 0.93, 0.85);
    float3 cBrightRed = float3(0.95, 0.10, 0.06);
    float3 cDarkRed   = float3(0.38, 0.025, 0.035);
    float3 cBlockRed  = float3(0.72, 0.06, 0.04);
    float3 cSubBlock  = float3(1.0, 0.38, 0.20);
    float3 cStrip     = float3(0.55, 0.035, 0.025);
    float3 cStream    = float3(0.80, 0.05, 0.04);
    float3 cTip       = float3(1.0, 0.60, 0.42);

    float3 color = float3(0, 0, 0);
    color += cWhiteHot  * core;                             //A: 白热芯
    color += cBrightRed * mid;                              //B: 红色辉光
    color += cDarkRed   * outer;                            //C: 暗红光晕
    color += cBlockRed  * blockFill * edgeMask * 0.55;      //D: 大故障方块
    color += cSubBlock  * subBlock * 0.30;                  //D: 小碎片方块
    color += cStrip     * strip * 0.40;                     //E: 横向撕裂纹
    color += cStream    * stream;                           //F: 纵向数据流
    color += cTip       * tipFlare;                         //H: 尖端光斑

    float alpha = saturate(
        edgeMask
        + core * 0.5
        + blockFill * 0.25
        + subBlock * 0.15
        + strip * 0.20
        + stream * 0.15
    );
    alpha *= fadeAlpha * visMask;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass GlitchBoltPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
