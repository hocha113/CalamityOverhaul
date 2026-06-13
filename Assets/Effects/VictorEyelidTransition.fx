// ============================================================================
// VictorEyelidTransition.fx —— Victor 手术过场"闭眼陷入黑暗 / 睁眼"全屏覆盖
// 设计：上下眼睑带弧形软边合拢到全黑；睁眼瞬间中央一道手术灯横向眩光
// 输入参数：
//   uClose       眼睑闭合 0(全睁)→1(全黑)
//   uGlow        睁眼手术灯眩光 0~1
//   uTime        累计时间（秒）
//   uResolution  屏幕像素尺寸
// 渲染方式：sb.Begin(Immediate, AlphaBlend, ..., Matrix.Identity)，全屏单 quad
// ============================================================================

sampler uImage0 : register(s0);

float uClose;
float uGlow;
float uTime;
float2 uResolution;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float cx = coords.x;

    // 眼睑弧形：中央比两侧晚一点闭合，更像真实眨眼
    float curve = sin(cx * 3.14159265) * uResolution.y * 0.075;
    float halfH = uResolution.y * 0.5;
    float reach = uClose * (halfH + curve);

    float topEdge = reach;
    float botEdge = uResolution.y - reach;

    float soft = 12.0;
    float topCover = 1.0 - smoothstep(topEdge - soft, topEdge, px.y);
    float botCover = smoothstep(botEdge, botEdge + soft, px.y);
    float lid = saturate(topCover + botCover);

    // 眼睑内缘的细微暗纹（睫毛感）
    float topLash = saturate(1.0 - abs(px.y - topEdge) / 6.0) * step(0.001, uClose) * 0.25;
    float botLash = saturate(1.0 - abs(px.y - botEdge) / 6.0) * step(0.001, uClose) * 0.25;

    // 手术灯：中央横向亮带
    float bandWidth = uResolution.y * 0.18;
    float band = 1.0 - smoothstep(0.0, bandWidth, abs(px.y - halfH));
    float flick = 0.85 + 0.15 * sin(uTime * 30.0);
    float glow = band * uGlow * flick;
    float3 surgical = float3(1.0, 0.96, 0.86);

    float alpha = saturate(lid + topLash + botLash + glow * 0.9);
    float3 rgb = lerp(float3(0.0, 0.0, 0.0), surgical, saturate(glow));

    return float4(rgb, alpha) * vertexColor;
}

technique Technique1
{
    pass EyelidPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
