// ============================================================================
//BreachMatrixAxisHighlight.fx 破译小游戏行列高亮条
//AlphaBlend
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uIntensity;     //总强度，点击脉冲衰减
float4 uColor;        //主色 RGB
float2 uResolution;   //矩形像素尺寸
float uOrientation;   //0水平 1垂直
float uCoreWeight;    //中心光亮度权重

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //u沿带方向 v横切方向
    float u = lerp(coords.x, coords.y, uOrientation);
    float v = lerp(coords.y, coords.x, uOrientation);
    float across = lerp(coords.y * uResolution.y, coords.x * uResolution.x, uOrientation);
    float height = lerp(uResolution.y, uResolution.x, uOrientation);

    //横切方向距离中心归一化（0中心，1边缘）
    float vDist = abs(v - 0.5) * 2.0;

    //柔和高斯面光
    float band = exp(-vDist * vDist * 3.2);
    //宽外光晕，制造玻璃折射感
    float glow = exp(-vDist * vDist * 1.0) * 0.35;

    //中央细芯：仅在横切方向距离中心1~1.5像素内出现
    float centerPx = abs(across - height * 0.5);
    float core = 1.0 - smoothstep(0.0, 1.4, centerPx);
    core *= 0.9;

    //沿带方向两端羽化，避免梁硬切
    float uEdge = smoothstep(0.0, 0.03, u) * smoothstep(1.0, 0.97, u);

    //极慢呼吸（0.85~1.0之间微幅），保持干净感
    float breath = 0.9 + 0.1 * sin(uTime * 1.4);

    float3 col = uColor.rgb;
    //最终色：底色面光 + 中央细芯轻微偏白增强"玻璃刀锋"感
    float3 result = col * band + col * glow + lerp(col, float3(1.0, 1.0, 1.0), 0.35) * core;

    //alpha：主要由面光贡献，中央细芯额外提亮
    float alpha = (band * 0.55 + glow * 0.35 + core * 0.7) * uEdge * uIntensity * breath;
    alpha = saturate(alpha);

    return float4(result * alpha, alpha) * vertexColor * uCoreWeight;
}

technique Technique1
{
    pass AxisHighlightPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
