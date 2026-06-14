// ============================================================================
//ThermalPanel.fx 热力发电机面板
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;
float uTemperature;   //温度比例 0~1
float uBurnIntensity; //燃烧强度 0~1

//─── 噪声工具 ───
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

float fbm2(float2 p) {
    float v = 0.0;
    v += valueNoise(p) * 0.5;
    v += valueNoise(p * 2.03 + 17.0) * 0.25;
    v += valueNoise(p * 4.01 + 43.0) * 0.125;
    return v / 0.875;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;

    //═══ 圆角矩形SDF ═══
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;
    float2 d = abs(pixelPos - center) - halfSize;
    float cornerR = 5.0;
    float panelSDF = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - cornerR;

    if (panelSDF > uEdgePad + 2.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.5, panelSDF);
    if (edgeAlpha < 0.01) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTemperature;
    float burn = uBurnIntensity;

    //═══ 1. 底色渐变（冷工业→热锻造） ═══
    float3 coldTop = float3(0.055, 0.042, 0.038);
    float3 coldBot = float3(0.028, 0.022, 0.018);
    float3 hotTop  = float3(0.10, 0.055, 0.032);
    float3 hotBot  = float3(0.065, 0.035, 0.022);

    float3 top = lerp(coldTop, hotTop, t);
    float3 bot = lerp(coldBot, hotBot, t);
    float3 bg = lerp(top, bot, uv.y);

    //═══ 2. 拉丝金属纹理 ═══
    float brush = valueNoise(pixelPos * float2(0.05, 0.16));
    float brushCoarse = valueNoise(pixelPos * 0.022 + 150.0);
    bg *= 0.82 + (brush * 0.35 + brushCoarse * 0.18) * 0.38;

    //热色偏移
    float3 warmTint = float3(0.014, 0.005, -0.003);
    bg += warmTint * t * (brush - 0.5);

    //═══ 3. 锈蚀噪声花纹（淡化以保持清洁感） ═══
    float rust = fbm2(pixelPos * 0.015 + float2(uTime * 0.15, uTime * 0.06));
    float3 rustColor = float3(0.10, 0.05, 0.025);
    bg += rustColor * rust * 0.06 * (0.3 + t * 0.7);

    //═══ 4. 热能脉络网（两条水平脉冲线） ═══
    float circuitAccum = 0.0;
    float lineY1 = innerMin.y + innerSize.y * 0.35;
    float lineY2 = innerMin.y + innerSize.y * 0.72;
    float distY1 = abs(pixelPos.y - lineY1);
    float distY2 = abs(pixelPos.y - lineY2);
    float line1 = 1.0 - smoothstep(0.0, 2.5, distY1);
    float line2 = 1.0 - smoothstep(0.0, 2.5, distY2);
    float pulse1 = sin((pixelPos.x * 0.025 - uTime * 1.5)) * 0.5 + 0.5;
    float pulse2 = sin((pixelPos.x * 0.025 + uTime * 1.2)) * 0.5 + 0.5;
    circuitAccum += line1 * pulse1 + line2 * pulse2;

    float3 pulseColor = lerp(float3(0.12, 0.06, 0.03), float3(0.35, 0.14, 0.06), t);
    bg += pulseColor * circuitAccum * 0.18 * (0.4 + t * 0.6);

    //═══ 5. 底部热浪光晕（受温度和燃烧强度影响） ═══
    float heatGlow = pow(max(uv.y, 0.0), 2.5) * t;
    float3 heatColor = float3(0.45, 0.18, 0.06);
    bg += heatColor * heatGlow * 0.35;

    //燃烧脉冲
    float burnPulse = sin(uTime * 3.5) * 0.5 + 0.5;
    float burnPulse2 = sin(uTime * 5.8 + 1.7) * 0.5 + 0.5;
    float3 burnColor = float3(0.55, 0.22, 0.06);
    bg += burnColor * burn * burnPulse * 0.10;
    bg += float3(0.3, 0.08, 0.02) * burn * burnPulse2 * 0.06 * (1.0 - uv.y);

    //═══ 6. 网格线（更细更淡） ═══
    float gridSize = 48.0;
    float gx = abs(frac(pixelPos.x / gridSize) - 0.5) * 2.0;
    float gy = abs(frac(pixelPos.y / gridSize) - 0.5) * 2.0;
    float gridLineX = 1.0 - smoothstep(0.0, 0.022, 1.0 - gx);
    float gridLineY = 1.0 - smoothstep(0.0, 0.022, 1.0 - gy);
    float gridLine = max(gridLineX, gridLineY);

    float3 gridColor = lerp(float3(0.05, 0.035, 0.025), float3(0.12, 0.06, 0.035), t);
    bg += gridColor * gridLine * 0.04;

    //═══ 7. 扫描线 ═══
    float scanPos = frac(uTime * 0.035) * innerSize.y;
    float scanDist = abs((pixelPos.y - innerMin.y) - scanPos);
    float scanLine = exp(-scanDist * 0.12);
    float3 scanColor = lerp(float3(0.25, 0.12, 0.06), float3(0.55, 0.25, 0.10), t);
    bg += scanColor * scanLine * 0.14;

    //第二扫描线（反向，更弱）
    float scanPos2 = frac(uTime * 0.022 + 0.5) * innerSize.y;
    float scanDist2 = abs((pixelPos.y - innerMin.y) - scanPos2);
    bg += scanColor * 0.5 * exp(-scanDist2 * 0.18) * 0.08;

    //═══ 8. CRT水平线（微弱，仅提供质感） ═══
    float crtLine = abs(frac(pixelPos.y * 0.25) - 0.5) * 2.0;
    crtLine = smoothstep(0.45, 0.5, crtLine);
    bg *= 1.0 - crtLine * 0.018;

    //═══ 9. 边框辉光（加强，轮廓分明） ═══
    float edgeDist = -panelSDF;
    float edgeGlowStr = exp(-edgeDist * 0.18);
    float3 edgeColor = lerp(float3(0.35, 0.18, 0.09), float3(0.60, 0.28, 0.10), t + burn * 0.3);
    bg += edgeColor * edgeGlowStr * (0.30 + t * 0.50 + burn * 0.18);

    //内框细线
    float innerEdge = exp(-abs(edgeDist - 8.0) * 0.5);
    bg += edgeColor * innerEdge * 0.16;

    //═══ 10. (removed corner markers) ═══

    //═══ 11. 暗角 ═══
    float2 vigUV = uv * 2.0 - 1.0;
    float vig = dot(vigUV, vigUV);
    bg *= 1.0 - vig * 0.22;

    //═══ 12. 顶部反光条 ═══
    float topHighlight = 1.0 - smoothstep(0.0, 0.08, uv.y);
    bg += float3(0.18, 0.12, 0.07) * topHighlight * 0.18;

    float alpha = edgeAlpha * uAlpha;
    return float4(bg * alpha, alpha);
}

technique Technique1
{
    pass ThermalPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
