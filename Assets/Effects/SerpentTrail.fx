// ============================================================================
// SerpentTrail.fx 神圣之蛇拖尾
// Trail 条带蛇鳞+圣光；UV.x 沿身 UV.y 径向；ps_3_0/vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;         //整体透明度
float glowIntensity;     //发光强度(攻击时增强)

//颜色参数
float3 holyGold;         //神圣金(主色)
float3 scaleGreen;       //蛇鳞绿(尾部)
float3 pureWhite;        //纯净白(高光)
float3 mysticPurple;     //神秘紫(眼/装饰)

//噪声纹理(s1)
texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

struct VSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position  = mul(v.Position, transformMatrix);
    o.Color     = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

#define PI  3.14159265
#define TAU 6.28318530

// SerpentBody — 蛇身主体
float4 SerpentBodyPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;          //0=尾 1=头
    float cross_ = uv.y;
    float crossDist = abs(cross_ - 0.5) * 2.0; //0=中心 1=边缘

    float3 col = 0;

    //基础色：沿蛇身从暗绿渐变到金色
    float3 baseColor = lerp(scaleGreen * 0.5, holyGold * 0.7, along * along);

    // =
    // 1. 圆柱体底色(明暗立体感)
    // =
    //模拟圆柱截面光照：中心亮两侧暗
    float cylinder = 1.0 - crossDist * crossDist;
    float shade = 0.3 + cylinder * 0.7;
    col = baseColor * shade * 0.3;

    // =
    // 2. 鳞片纹理(加大对比度)
    // =
    //鳞片密度：适中，让每片鳞清晰可见
    float scaleU = along * 18.0;
    float scaleV = cross_ * 3.0;

    //交错排列(奇数行偏移半格)
    float row = floor(scaleV);
    float offsetU = fmod(row, 2.0) * 0.5;
    float cellU = frac(scaleU + offsetU);
    float cellV = frac(scaleV);

    //菱形距离场
    float diamond = abs(cellU - 0.5) + abs(cellV - 0.5);

    //鳞片暗缝：鳞片边界处变暗(关键对比度)
    float scaleGap = smoothstep(0.38, 0.5, diamond);
    col *= (1.0 - scaleGap * 0.6);

    //鳞片内部渐变高光：每片中心向边缘递减
    float scaleBody = saturate(1.0 - diamond * 1.8);
    float3 scaleFillCol = lerp(scaleGreen * 0.4, holyGold * 0.6, along);
    //鳞片内有轻微色彩变化
    float scaleHue = sin(scaleU * 3.0 + row * 1.7) * 0.5 + 0.5;
    scaleFillCol = lerp(scaleFillCol, holyGold * 0.8, scaleHue * 0.3);
    col += scaleFillCol * scaleBody * 0.2 * shade;

    //鳞片亮边：每片鳞的顶部(朝头方向)有反光弧线
    float scaleTopEdge = smoothstep(0.35, 0.42, diamond) * smoothstep(0.5, 0.43, diamond);
    float topBias = smoothstep(0.5, 0.3, cellU); //偏向鳞片上游
    col += holyGold * scaleTopEdge * topBias * 0.35;

    // =
    // 3. 腹部与背部色差
    // =
    //蛇腹(UV.y中心区域)略浅偏金
    float bellyMask = exp(-crossDist * crossDist * 8.0);
    col += holyGold * 0.06 * bellyMask;

    //脊背线(中线微亮)
    float spine = exp(-crossDist * crossDist * 100.0);
    col += lerp(scaleGreen * 0.3, holyGold * 0.5, along) * spine * 0.15;

    // =
    // 4. 边缘轮廓光(有色，非白)
    // =
    float edgeGlow = smoothstep(0.55, 0.92, crossDist);
    float edgePulse = 0.8 + 0.2 * sin(uTime * 2.0 + along * 6.0);
    float3 edgeCol = lerp(scaleGreen * 0.5, holyGold * 0.7, along);
    col += edgeCol * edgeGlow * edgePulse * 0.15;

    // =
    // 5. 圣光流转(克制的光波)
    // =
    float wave1 = sin((along - uTime * 0.35) * TAU * 2.0) * 0.5 + 0.5;
    wave1 = pow(wave1, 6.0);
    col += holyGold * wave1 * 0.08 * cylinder;

    // =
    // 6. 头部渐亮
    // =
    float headFactor = smoothstep(0.8, 1.0, along);
    col += holyGold * headFactor * 0.12 * cylinder;

    // =
    // 边缘衰减 + 头尾淡出
    // =
    float crossFade = smoothstep(1.0, 0.75, crossDist);
    float tailFade = smoothstep(0.0, 0.1, along);

    col *= crossFade * tailFade * fadeAlpha * glowIntensity;

    float alpha = crossFade * tailFade * fadeAlpha;
    return float4(col, alpha);
}

// SerpentHead — 蛇头(在Trail头端额外覆盖的圆形画布)
float4 SerpentHeadPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 c = uv - 0.5;
    float dist = length(c);
    float ang = atan2(c.y, c.x);

    float3 col = 0;

    // =
    // 1. 蛇头轮廓(水滴/三角形)
    // =
    //用极坐标构造前尖后圆的形状
    float headShape = 0.18 + 0.08 * cos(ang); //朝右的水滴形
    float headMask = smoothstep(headShape + 0.02, headShape - 0.01, dist);
    float headEdge = smoothstep(headShape - 0.03, headShape, dist) * headMask;

    float3 headColor = lerp(holyGold, pureWhite, 0.2);
    col += headColor * headMask * 0.5;
    col += pureWhite * headEdge * 0.4;

    // =
    // 2. 双眼
    // =
    float2 eyeOffset = float2(0.06, 0.055);
    float eye1Dist = length(c - eyeOffset);
    float eye2Dist = length(c - float2(eyeOffset.x, -eyeOffset.y));

    float eyeGlow = exp(-eye1Dist * eye1Dist * 3000.0) + exp(-eye2Dist * eye2Dist * 3000.0);
    col += mysticPurple * eyeGlow * 1.5;

    //瞳孔(更亮的核心)
    float pupil1 = exp(-eye1Dist * eye1Dist * 8000.0);
    float pupil2 = exp(-eye2Dist * eye2Dist * 8000.0);
    col += pureWhite * (pupil1 + pupil2) * 0.8;

    // =
    // 3. 头冠十字架
    // =
    //旋转坐标用于十字架
    float2 crownC = c - float2(0.12, 0.0);
    float crownDist = length(crownC);

    float crossH = exp(-crownC.y * crownC.y * 2000.0);
    float crossV = exp(-crownC.x * crownC.x * 3000.0);
    float crownFade = exp(-crownDist * 6.0);
    col += holyGold * (crossH + crossV * 1.2) * crownFade * 0.5;

    // =
    // 4. 头部神圣光晕
    // =
    float halo = exp(-dist * dist * 15.0);
    float haloPulse = 0.7 + 0.3 * sin(uTime * 3.0);
    col += holyGold * halo * 0.2 * haloPulse * glowIntensity;

    // =
    // 5. 鳞片暗示(头部前端)
    // =
    float headScaleU = (c.x + 0.2) * 10.0;
    float headScaleV = c.y * 6.0;
    float hRow = floor(headScaleV);
    float hOffU = fmod(hRow, 2.0) * 0.5;
    float hDiamond = abs(frac(headScaleU + hOffU) - 0.5) + abs(frac(headScaleV) - 0.5);
    float hScaleEdge = smoothstep(0.46, 0.5, hDiamond);
    col += holyGold * hScaleEdge * headMask * 0.15;

    // =
    // 边缘衰减
    // =
    col *= smoothstep(0.3, 0.2, dist) * fadeAlpha * glowIntensity;

    float alpha = smoothstep(0.3, 0.15, dist) * fadeAlpha;
    return float4(col, alpha);
}

// Technique定义

technique SerpentBody
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 SerpentBodyPS();
    }
};

technique SerpentHead
{
    pass P0
    {
        PixelShader = compile ps_3_0 SerpentHeadPS();
    }
};
