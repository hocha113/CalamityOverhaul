// ============================================================================
//EntrustGuideCard.fx 委托引导卡片背景
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float  uTime;
float  uAlpha;
float2 uResolution;
float  uEdgePad;
float  uVariant;  //0暖琥珀 1冷青

//─── 工具函数 ───────────────────────────────────────────────────────────────

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//─── 主片段函数 ──────────────────────────────────────────────────────────────

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 halfSize = (innerMax - innerMin) * 0.5;
    float2 center   = uResolution * 0.5;

    //═══ 圆角矩形 SDF（控制卡片外形和边缘淡出） ═══
    float cornerR = 4.0;
    float2 dd = abs(pixelPos - center) - halfSize;
    float sdf = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    if (sdf > uEdgePad + 2.0) return float4(0, 0, 0, 0);
    float edgeMask = 1.0 - smoothstep(-1.5, 2.5, sdf);
    if (edgeMask < 0.005) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / (innerMax - innerMin));
    float t = saturate(uVariant);

    //═══ 色板（variant插值：0=琥珀暖色  1=青色冷色） ═══
    float3 colBorder = lerp(float3(1.00, 0.72, 0.18), float3(0.20, 0.85, 0.95), t);
    float3 colCorner = lerp(float3(1.00, 0.90, 0.55), float3(0.55, 1.00, 1.00), t);
    float3 colScan   = lerp(float3(0.55, 0.35, 0.06), float3(0.06, 0.35, 0.52), t);

    //═══ 1. 背景底色渐变（顶部略暖，底部略深） ═══
    float3 bgTop = lerp(float3(0.052, 0.040, 0.024), float3(0.018, 0.038, 0.060), t);
    float3 bgBot = lerp(float3(0.024, 0.018, 0.010), float3(0.008, 0.018, 0.032), t);
    float3 bg    = lerp(bgTop, bgBot, uv.y * uv.y);

    //═══ 2. 边框流光 ═══
    //计算边框线（内侧SDF为0处）的光晕
    float2 tightHalf = halfSize - 1.0;
    float2 dd2 = abs(pixelPos - center) - tightHalf;
    float innerSDF = length(max(dd2, 0.0)) + min(max(dd2.x, dd2.y), 0.0) - cornerR;
    float ringGlow = exp(-abs(innerSDF + 0.8) * 0.85);

    //对角线流光扫过（uv.x+uv.y 方向，形成彗星尾效果）
    float diagCoord = uv.x + uv.y;
    float sweep = frac(diagCoord * 0.5 - uTime * 0.09);
    float sweepTail = pow(saturate(1.0 - sweep * 1.6), 4.0);

    //顶边固定高光（加强顶部存在感）
    float topGlow  = saturate(1.0 - abs(pixelPos.y - innerMin.y) * 0.20) * 0.55;
    //左边竖光（弱）
    float leftGlow = saturate(1.0 - abs(pixelPos.x - innerMin.x) * 0.20) * 0.22;

    float borderIntensity = ringGlow * (0.55 + sweepTail * 0.45) + topGlow + leftGlow;
    bg += colBorder * borderIntensity;

    //═══ 3. 角落光斑（左上+右下对角呼应，随时间脉冲） ═══
    float pulse = 0.72 + sin(uTime * 2.0) * 0.28;
    float2 cornerTL = innerMin + 5.0;
    float2 cornerBR = innerMax - 5.0;
    float cgTL = exp(-length(pixelPos - cornerTL) * 0.28);
    float cgBR = exp(-length(pixelPos - cornerBR) * 0.28);
    bg += colCorner * (cgTL + cgBR) * pulse * 0.30;

    //═══ 4. 斜扫描线（极细微，增加屏显质感） ═══
    float scanPhase = frac(uv.x * 0.80 - uv.y * 0.55 - uTime * 0.08);
    float scanLine  = exp(-scanPhase * 22.0) * 0.044;
    bg += colScan * scanLine;

    //═══ 5. 内部暗角（四边压暗，使中央内容区更突出） ═══
    float2 vigUV = uv * 2.0 - 1.0;
    float vig = saturate(1.0 - dot(vigUV, vigUV) * 0.28);
    bg *= vig;

    float a = edgeMask * uAlpha;
    return float4(bg * a, a);
}

//─── Technique ──────────────────────────────────────────────────────────────

technique Technique1
{
    pass EntrustGuideCardPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
