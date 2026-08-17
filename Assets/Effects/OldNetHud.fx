// ============================================================================
//OldNetHud.fx 旧网 HUD 富层
//材质：深潜链路仪表——暗钢切角底板（拉丝+顶缘受光+微呼吸）与
//数据流噪音条（流动数据划+档位渐变+前沿热点）；锐利前景仍归 CPU/SVG
//TechPanel=底板 / TechBar=噪音条填充
//全程序化零采样器；直线算术无动态分支；AlphaBlend 预乘输出
//画布契约：Panel quad=整个底板矩形；Bar quad=轨道全长（uFrac 控填充）
// ============================================================================

float uTime;
//底板画布像素尺寸（切角/边线按像素折算）
float2 uPanelSize;
//噪音填充比 0..1
float uFrac;
//档位 0..4（前沿热点提频用）
float uTier;
float uAlpha;

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// ──────────── 底板：暗钢切角基板 ────────────
float4 PSPanel(float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = uv * uPanelSize;
    float t = uTime;

    //切角 SDF：矩形去四角（L1 折叠；half 是 HLSL 保留字，用 hs）
    float2 hs = uPanelSize * 0.5;
    float2 q = abs(px - hs);
    float chamfer = 9.0;
    float inRect = step(q.x, hs.x) * step(q.y, hs.y);
    float corner = step(q.x + q.y, hs.x + hs.y - chamfer);
    float plate = inRect * corner;

    //拉丝暗钢：横向 hash 丝纹 + 低频云斑；呼吸幅度压 0.02
    float brush = hash21(float2(floor(px.y), 3.7)) * 0.06;
    float breathe = 0.02 * sin(t * 1.1);
    float3 steel = float3(0.055, 0.075, 0.085) * (0.9 + brush + breathe);

    //顶缘受光线 + 底缘沉暗
    float topLight = smoothstep(2.2, 0.0, px.y) * 0.5;
    float bottomDark = smoothstep(uPanelSize.y - 2.2, uPanelSize.y, px.y) * 0.5;
    //切角棱线：沿切角边一线微亮
    float edgeGlint = smoothstep(2.0, 0.0, abs(q.x + q.y - (hs.x + hs.y - chamfer))) * 0.18;

    float3 col = steel
        + float3(0.35, 0.55, 0.60) * topLight * 0.25
        + float3(0.35, 0.55, 0.60) * edgeGlint;
    col *= 1.0 - bottomDark * 0.6;

    float alpha = plate * 0.88 * uAlpha;
    col = saturate(col) * plate;
    return float4(col * alpha, alpha);
}

// ──────────── 噪音条：数据流填充 ────────────
float4 PSBar(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float3 coldC = float3(0.55, 0.78, 0.82);
    float3 emberC = float3(0.92, 0.25, 0.17);

    //轨道暗底（未填充区）
    float3 col = float3(0.045, 0.075, 0.085) * 0.8;
    float alpha = 0.75;

    float lit = step(uv.x, uFrac);

    //填充渐变：冷青→黑墙红
    float3 fill = lerp(coldC, emberC, uv.x);

    //数据划：向前沿流动的短划（档位越高流越快）
    float flow = frac(uv.x * 9.0 - t * (0.8 + uTier * 0.35));
    float dash = smoothstep(0.55, 0.2, abs(flow - 0.5)) * 0.5 + 0.6;

    //白芯线（读数的骨）
    float core = smoothstep(0.28, 0.0, abs(uv.y - 0.5)) * 0.30;

    //前沿热点：靠近 uFrac 的白热提亮
    float tip = exp(-abs(uv.x - uFrac) * 26.0) * step(0.003, uFrac);
    float tipPulse = 0.7 + 0.3 * sin(t * 6.0);

    col = lerp(col, fill * dash + float3(1.0, 1.0, 1.0) * core, lit);
    col += (fill + float3(0.6, 0.6, 0.6)) * tip * tipPulse * 0.5;
    alpha = lerp(alpha, 0.95, lit);
    alpha = saturate(alpha + tip * 0.4);

    //上下 1px 边线收口
    float edge = smoothstep(0.1, 0.0, min(uv.y, 1.0 - uv.y)) * 0.25;
    col += float3(0.35, 0.55, 0.60) * edge;

    col = saturate(col) * uAlpha;
    alpha = saturate(alpha) * uAlpha;
    return float4(col * alpha, alpha);
}

technique TechPanel
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSPanel();
    }
}

technique TechBar
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSBar();
    }
}
