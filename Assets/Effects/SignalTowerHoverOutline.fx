// ============================================================================
// SignalTowerHoverOutline.fx 信号塔悬停描边
// 采样 uImage0 alpha 8 邻域描边
// ============================================================================

sampler uImage0 : register(s0);

float uTime;          //全局时间
float intensity;      //悬停强度 0~1
float2 texelSize;     //1/纹理宽高
float seed;           //实例化扰动种子

//邻域alpha扩张：得到外轮廓mask
float edgeMask(float2 uv) {
    float alpha = tex2D(uImage0, uv).a;
    //自身透明才有可能成为边缘
    if (alpha > 0.02) return 0.0;

    float total = 0.0;
    //3x3邻域采样，中心不用
    [unroll]
    for (int oy = -1; oy <= 1; oy++) {
        [unroll]
        for (int ox = -1; ox <= 1; ox++) {
            if (ox == 0 && oy == 0) continue;
            float2 offset = float2(ox, oy) * texelSize * 1.5;
            total += tex2D(uImage0, uv + offset).a;
        }
    }
    //只要附近存在不透明就算边
    return saturate(total * 1.2);
}

//水平扫描条：高对比带相对uv.y周期推进
float scanBar(float y, float phase, float width) {
    float band = frac(y * 6.0 - phase);
    return smoothstep(1.0 - width, 1.0 - width * 0.2, band)
         * (1.0 - smoothstep(1.0 - width * 0.2, 1.0, band));
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float baseAlpha = tex2D(uImage0, coords).a;
    float edge = edgeMask(coords);

    //描边厚度随intensity变化
    float edgeBoost = smoothstep(0.15, 0.75, edge);

    //主描边色：橙金属 → 热白；被内部的暗纹切出层次
    float3 rust    = float3(0.95, 0.55, 0.18);//废土锈橙
    float3 hotEdge = float3(1.00, 0.92, 0.65);//金属热白
    float3 cyberCy = float3(0.40, 0.90, 1.00);//2077冷青
    float3 deepInk = float3(0.10, 0.02, 0.14);//暗底

    //描边颜色：主体橙红 + 内测青色勾线
    float pulse = 0.65 + sin(uTime * 4.0 + seed) * 0.35;
    float3 edgeCol = lerp(rust, hotEdge, pow(edgeBoost, 3.0) * pulse);
    edgeCol += cyberCy * edgeBoost * 0.25;

    //外发光扩散：第二层较软的轮廓
    float glow = 0.0;
    [unroll]
    for (int i = 1; i <= 3; i++) {
        float k = float(i);
        float a = 0.0;
        [unroll]
        for (int oy = -1; oy <= 1; oy++) {
            [unroll]
            for (int ox = -1; ox <= 1; ox++) {
                a += tex2D(uImage0, coords + float2(ox, oy) * texelSize * (1.5 + k * 1.5)).a;
            }
        }
        glow = max(glow, saturate(a * 0.15) * (1.0 - k * 0.25));
    }
    //外发光只在本身透明区域显示
    glow *= step(baseAlpha, 0.02);
    float3 glowCol = rust * glow * 0.5 + cyberCy * glow * 0.2;

    //本体内部的扫描条：让塔看起来正在被定位/锁定
    float scan = scanBar(coords.y + uTime * 0.15, uTime * 0.6, 0.05);
    scan *= step(0.1, baseAlpha);//仅实体区域
    //再叠一道反向慢扫描
    float scan2 = scanBar(coords.y - uTime * 0.08, uTime * 0.25 + 0.5, 0.03);
    scan2 *= step(0.1, baseAlpha);

    float3 scanCol = cyberCy * scan * 0.45 + rust * scan2 * 0.25;

    //亮斑沿描边走：用coords.y做相位，形成上下巡游的光点
    float runPhase = frac(uTime * 0.35 + coords.y * 0.8 + seed * 0.17);
    float runSpot = smoothstep(0.05, 0.0, abs(runPhase - 0.5)) * edgeBoost;
    float3 runCol = hotEdge * runSpot * 1.2;

    //最终合成（Additive叠在本体之上）
    float3 finalRGB = (edgeCol * edgeBoost + glowCol + scanCol + runCol) * intensity;
    float finalA = saturate(edgeBoost * 0.9 + glow * 0.45 + scan * 0.2 + scan2 * 0.12 + runSpot * 0.6) * intensity;

    //预乘alpha以适配Additive
    return float4(finalRGB * finalA, finalA) * vertexColor;
}

technique Technique1
{
    pass SignalTowerHoverOutlinePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
