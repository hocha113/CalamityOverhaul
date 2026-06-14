// ============================================================================
//CyberFreezeWave.fx 赛博领域冻结黑墙能量波
//s0+s1 Additive；领域中心方形 quad
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float ringProgress;     //0~1 波前扩张进度
float ringThickness;    //波前厚度（归一化）
float fadeAlpha;        //整体淡出 0~1

//六角网格工具函数

//将直角坐标转换为六角网格坐标
float2 hexCenter(float2 p, float size)
{
    float sqrt3 = 1.7320508;
    float2 q;
    q.x = (2.0 / 3.0 * p.x) / size;
    q.y = (-1.0 / 3.0 * p.x + sqrt3 / 3.0 * p.y) / size;

    //四舍五入到最近的六角中心
    float rz = -q.x - q.y;
    float3 rounded = float3(round(q.x), round(q.y), round(rz));
    float3 diff = abs(rounded - float3(q.x, q.y, rz));

    if (diff.x > diff.y && diff.x > diff.z)
        rounded.x = -rounded.y - rounded.z;
    else if (diff.y > diff.z)
        rounded.y = -rounded.x - rounded.z;

    //转回直角坐标
    float2 center;
    center.x = size * (3.0 / 2.0 * rounded.x);
    center.y = size * (sqrt3 / 2.0 * rounded.x + sqrt3 * rounded.y);
    return center;
}

//计算到最近六角边缘的距离
float hexEdgeDist(float2 p, float size)
{
    float2 hc = hexCenter(p, size);
    float2 d = abs(p - hc);
    float sqrt3 = 1.7320508;
    //六角形的边缘距离
    float hexDist = max(d.x * 2.0 / 3.0, (d.x / 3.0 + d.y * sqrt3 / 3.0));
    return size - hexDist;
}

//哈希函数
float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    //噪声扰动波前边缘
    float n1 = tex2D(noiseTex, float2(normAngle * 3.0 + uTime * 2.5, ringProgress * 1.5)).r;
    float n2 = tex2D(noiseTex, float2(normAngle * 6.0 - uTime * 1.8, ringProgress + 0.3)).g;
    float noiseDisp = (n1 * 0.55 + n2 * 0.45 - 0.5) * 0.06;

    float adjDist = dist + noiseDisp;

    //主波前环形遮罩
    float ringDist = abs(adjDist - ringProgress);
    float ringMask = 1.0 - smoothstep(0.0, ringThickness, ringDist);

    //内侧压缩面更亮
    float innerBias = smoothstep(ringProgress, ringProgress - ringThickness * 0.5, adjDist);

    //=
    //六角网格效果 ： 黑墙风格
    //=

    //将UV映射到世界空间比例的六角网格
    float hexSize = 0.045;
    float2 hexUV = centered * 8.0; //放大到合适网格密度
    float2 hc = hexCenter(hexUV, hexSize);
    float edgeDist = hexEdgeDist(hexUV, hexSize);

    //六角边缘线
    float edgeWidth = 0.008;
    float hexEdge = 1.0 - smoothstep(0.0, edgeWidth, edgeDist);

    //每个六角格子的唯一ID和哈希
    float hexId = hash21(hc * 100.0 + 0.5);

    //六角格子距离波前的程度 → 激活状态
    float hexCenterDist = length(hc) / 8.0; //归一化回0~1范围
    float hexActivation = smoothstep(ringProgress + ringThickness * 1.5, ringProgress - ringThickness * 2.0, hexCenterDist);

    //波前经过时六角格子的闪烁激活
    float waveFrontHit = 1.0 - smoothstep(0.0, ringThickness * 3.0, abs(hexCenterDist - ringProgress));
    float hexFlash = waveFrontHit * step(0.3, hexId);

    //=
    //伪3D立体感：根据位置给六角格子添加深度错觉
    //=

    //越靠外的六角格子有轻微透视偏移感
    float depth = hexCenterDist * 0.3;
    float depthShading = 1.0 - depth * 0.5;

    //格子内部的渐变模拟立体厚度
    float cellFill = smoothstep(edgeWidth * 4.0, edgeWidth * 12.0, edgeDist);
    float cellDepth = cellFill * depthShading;

    //选择性格子凸起效果（部分格子看起来比其他高一层）
    float raised = step(0.65, hexId);
    float raiseGlow = raised * cellFill * 0.4;

    //=
    //颜色合成 ： 赛博黑墙风格
    //=

    //黑墙主色调：深暗红黑底 + 红晶能量边缘（偏冷红/品红，避开领域的暖橙红）
    float3 blackWallBase = float3(0.06, 0.01, 0.04);
    float3 edgeCrimson  = float3(0.95, 0.08, 0.25);
    float3 edgeMaroon   = float3(0.65, 0.04, 0.18);
    float3 flashWhite   = float3(1.0, 0.55, 0.55);
    float3 innerDark    = float3(0.08, 0.01, 0.05);

    //波前主亮度
    float brightness = ringMask * (0.5 + innerBias * 0.8);

    //六角边缘光：在波前和已激活区域显示
    float edgeIntensity = hexEdge * (hexActivation * 0.6 + waveFrontHit * 1.2);
    float3 edgeColor = lerp(edgeMaroon, edgeCrimson, waveFrontHit) * edgeIntensity;

    //格子内部暗色填充（已激活区域）
    float3 cellColor = blackWallBase * cellDepth * hexActivation * 0.4;

    //凸起格子的额外光泽
    float3 raiseColor = edgeCrimson * raiseGlow * hexActivation * 0.3;

    //波前经过时的闪烁白光
    float3 flashColor = flashWhite * hexFlash * 0.8;

    //波前环的核心发光
    float3 ringGlow = lerp(edgeMaroon, edgeCrimson, innerBias * 0.6) * brightness;

    //外侧数字碎片拖尾
    float trailing = smoothstep(ringProgress + ringThickness * 2.5, ringProgress, adjDist);
    trailing *= trailing;
    float trailNoise = tex2D(noiseTex, float2(normAngle * 14.0 + uTime * 1.2, dist * 2.5)).r;
    trailing *= step(0.45, trailNoise) * 0.4;
    float3 trailColor = edgeMaroon * 0.3 * trailing;

    //内侧已冻结区域暗色余波
    float innerWave = smoothstep(ringProgress - ringThickness * 0.5, ringProgress - ringThickness * 4.0, adjDist);
    innerWave *= (1.0 - adjDist) * 0.1;
    float innerNoise = tex2D(noiseTex, float2(normAngle * 8.0, dist * 3.0 - uTime * 1.5)).r;
    innerWave *= smoothstep(0.3, 0.65, innerNoise);
    float3 innerColor = innerDark * innerWave;

    //最终合成
    float3 finalColor = (ringGlow + edgeColor + cellColor + raiseColor + flashColor + trailColor + innerColor) * fadeAlpha;
    float alpha = saturate(brightness + edgeIntensity * 0.7 + trailing * 0.5 + hexFlash * 0.5 + innerWave) * fadeAlpha;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass CyberFreezeWavePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
