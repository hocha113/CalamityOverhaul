// ============================================================================
//ThermalBar.fx 热力发电机指示条
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uFillRatio;  //填充 0~1 底→顶
float uBarMode;    //0温度条红橙 1电力条琥珀

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 uv = coords;
    float fill = uFillRatio;
    float mode = uBarMode;

    //═══ 圆角矩形SDF ═══
    float2 center = uResolution * 0.5;
    float2 halfSize = (uResolution - 8.0) * 0.5;
    float2 d = abs(pixelPos - center) - halfSize;
    float cornerR = 3.0;
    float barSDF = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - cornerR;

    if (barSDF > 3.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, barSDF);
    if (edgeAlpha < 0.01) return float4(0, 0, 0, 0);

    //═══ 1. 深色背景 ═══
    float3 bg = float3(0.022, 0.015, 0.012);

    //金属质感
    float brush = valueNoise(pixelPos * float2(0.08, 0.2));
    bg *= 0.8 + brush * 0.4;

    //═══ 2. 填充区域 ═══
    //填充从底部(uv.y=1)向顶部(uv.y=0)增长
    float fillLine = 1.0 - fill;  //uv坐标中的填充边界线
    float inFill = smoothstep(fillLine - 0.005, fillLine + 0.005, uv.y);

    if (inFill > 0.01) {
        //填充内的归一化位置 (0=填充底部, 1=填充顶部)
        float fillUV = saturate((1.0 - uv.y) / max(fill, 0.001));

        //温度模式色系（深红→橙→亮黄）
        float3 tempLow  = float3(0.22, 0.06, 0.02);
        float3 tempMid  = float3(0.55, 0.18, 0.04);
        float3 tempHigh = float3(0.85, 0.45, 0.12);

        //电力模式色系（深琥珀→金→亮金）
        float3 powLow  = float3(0.18, 0.12, 0.04);
        float3 powMid  = float3(0.42, 0.30, 0.08);
        float3 powHigh = float3(0.72, 0.55, 0.18);

        float3 cLow  = lerp(tempLow,  powLow,  mode);
        float3 cMid  = lerp(tempMid,  powMid,  mode);
        float3 cHigh = lerp(tempHigh, powHigh, mode);

        //三段渐变
        float3 fillColor = fillUV < 0.5
            ? lerp(cLow, cMid, fillUV * 2.0)
            : lerp(cMid, cHigh, (fillUV - 0.5) * 2.0);

        //填充比例影响亮度（填充越满越亮）
        fillColor *= 0.7 + fill * 0.5;

        //脉冲波动
        float pulse = sin(uTime * 2.2 + fillUV * 6.0) * 0.15 + 0.85;
        fillColor *= pulse;

        //流动光效（从底向上移动的亮带）
        float flowPos = frac(uTime * 0.08);
        float flowDist = abs(fillUV - flowPos);
        float flowLine = exp(-flowDist * 12.0);
        float3 flowColor = lerp(float3(0.9, 0.5, 0.2), float3(0.9, 0.7, 0.3), mode);
        fillColor += flowColor * flowLine * 0.35 * fill;

        //第二流动线（反向）
        float flowPos2 = frac(uTime * 0.055 + 0.5);
        float flowDist2 = abs(fillUV - flowPos2);
        fillColor += flowColor * 0.6 * exp(-flowDist2 * 15.0) * 0.2 * fill;

        //热浪噪声（仅温度模式）
        float heatNoise = valueNoise(pixelPos * 0.08 + float2(0, uTime * 0.8));
        fillColor += float3(0.15, 0.05, 0.02) * heatNoise * 0.15 * (1.0 - mode) * fill;

        //混合到背景
        bg = lerp(bg, fillColor, inFill);

        //填充顶部边界发光
        float topGlow = exp(-abs(uv.y - fillLine) * uResolution.y * 0.12);
        float3 glowColor = lerp(float3(0.8, 0.35, 0.12), float3(0.7, 0.55, 0.15), mode);
        bg += glowColor * topGlow * 0.5 * fill;
    }

    //═══ 3. 刻度线 ═══
    float scaleCount = 10.0;
    float scaleY = frac(uv.y * scaleCount);
    float scaleLine = 1.0 - smoothstep(0.0, 0.02, abs(scaleY - 0.5) - 0.48);
    float3 scaleColor = float3(0.12, 0.08, 0.05);
    bg += scaleColor * scaleLine * 0.15;

    //═══ 4. 边框 ═══
    float edgeDist = -barSDF;
    float edgeGlow = exp(-edgeDist * 0.35);
    float3 edgeColor = lerp(float3(0.30, 0.15, 0.08), float3(0.22, 0.18, 0.08), mode);
    edgeColor = lerp(edgeColor, edgeColor * 1.5, fill * 0.5);
    bg += edgeColor * edgeGlow * 0.55;

    //内框细线
    float innerEdge = exp(-abs(edgeDist - 4.0) * 0.8);
    bg += edgeColor * innerEdge * 0.08;

    //═══ 5. 空槽暗色调 ═══
    if (fill < 0.01) {
        bg *= 0.6;
    }

    float alpha = edgeAlpha * uAlpha;
    return float4(bg * alpha, alpha);
}

technique Technique1
{
    pass ThermalBarPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
