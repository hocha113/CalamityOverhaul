// ============================================================================
//OldNetTerminal.fx 旧网锚点三态
//材质：还在握手的上行天线，柱类三位置物理答案齐备：
//源头=基座机壳+聚束球根 / 柱身=上行数据包+沿程收窄 / 顶端=噪声撕散成粒（禁平切）
//TechTerminal=登出终端(薄荷绿,整环脉冲) / TechRelay=中继站(琥珀,离散包) /
//TechGate=封锁闸门柱(暗底+双缘警戒线+通电扫描段)
//全程序化零采样器；直线算术无动态分支；AlphaBlend 预乘输出
//画布契约：柱 quad 48x168（底锚），闸门 quad 16x16 逐格
// ============================================================================

float uTime;
float uSeed;
float uAlpha;
//闸门专用：扫描行相对本格的局部 y（格高单位，可越界）
float uLocalScan;

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//柱体公共骨架：packetMode 0=整环脉冲(终端) 1=离散数据包(中继)
float4 Column(float2 uv, float3 tint, float packetMode, float heightMul)
{
    float t = uTime;
    float x = (uv.x - 0.5) * 2.0;      //[-1,1]
    float h = (1.0 - uv.y) / heightMul; //0=基座 →1=名义顶（中继柱矮一截）

    //宽度生命周期：基座宽 → 顶部窄；根部聚束球根（禁水平实切）
    float w = lerp(0.42, 0.10, pow(saturate(h), 0.8));
    w += 0.30 * exp(-h * 12.0);

    //柱体掩码 + 顶端噪声撕散（无平切收头）
    float m = smoothstep(w, w * 0.45, abs(x));
    float erode = vnoise(float2(x * 3.0 + uSeed * 9.0, h * 5.0 - t * 1.1));
    m *= smoothstep(1.02, 0.62, h + (erode - 0.5) * 0.30);
    m *= step(0.0, h);

    //上行数据包：相位行进；中继离散化（包 id 抽签），终端连续环
    float packetPhase = frac(h * 3.0 - t * 0.55 + uSeed);
    float packet = smoothstep(0.30, 0.02, abs(packetPhase - 0.5));
    float packetId = floor(h * 3.0 - t * 0.55 + uSeed);
    float discrete = step(0.35, hash21(float2(packetId, uSeed * 13.0)));
    packet *= lerp(1.0, discrete, packetMode);
    //包沿柱升越高越淡（信号在衰减）
    packet *= saturate(1.15 - h);

    //白热芯线：只在下半程驻留（能量集中在根部）
    float core = smoothstep(0.09, 0.0, abs(x)) * m * saturate(1.0 - h * 1.2);

    //基座机壳：横杠 + 顶缘受光线
    float baseY = h * 6.0;
    float basePlate = smoothstep(0.0, 0.35, 1.0 - abs(baseY - 0.5) * 2.0)
        * smoothstep(0.72, 0.5, abs(x));
    float baseGlint = smoothstep(0.12, 0.0, abs(baseY - 1.0)) * smoothstep(0.66, 0.4, abs(x));

    float pulse = 0.82 + 0.18 * sin(t * 2.0 + uSeed);

    float3 col = tint * m * 0.30 * pulse
        + tint * packet * m * 0.75
        + float3(1.0, 1.0, 1.0) * core * 0.7
        + tint * basePlate * 0.85
        + float3(1.0, 1.0, 1.0) * baseGlint * 0.4;
    float alpha = saturate(m * 0.35 + packet * m * 0.6 + core * 0.6
        + basePlate * 0.9 + baseGlint * 0.4);

    //画布边保险
    float guard = smoothstep(1.0, 0.94, abs(x)) * smoothstep(0.0, 0.02, uv.y);
    col = saturate(col) * guard * uAlpha;
    alpha = saturate(alpha) * guard * uAlpha;
    return float4(col * alpha, alpha);
}

float4 PSTerminal(float2 uv : TEXCOORD0) : COLOR0
{
    return Column(uv, float3(0.47, 1.0, 0.67), 0.0, 1.0);
}

float4 PSRelay(float2 uv : TEXCOORD0) : COLOR0
{
    //中继柱矮一截："驿站不是家"
    return Column(uv, float3(1.0, 0.71, 0.31), 1.0, 0.72);
}

// ──────────── 封锁闸门：逐格 16px 通电柱 ────────────
float4 PSGate(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float3 warn = float3(0.92, 0.25, 0.17);
    float3 dark = float3(0.065, 0.032, 0.04);

    //暗色柱体
    float3 col = dark;
    float alpha = 1.0;

    //双缘警戒线（微呼吸）
    float pulse = 0.7 + 0.3 * sin(t * 2.2 + uSeed);
    float edgeL = smoothstep(0.10, 0.02, abs(uv.x - 0.09));
    float edgeR = smoothstep(0.10, 0.02, abs(uv.x - 0.91));
    col += warn * (edgeL + edgeR) * 0.4 * pulse;

    //通电扫描段：整根闸柱共相位（uLocalScan 由 C# 按世界行折算）
    float d = abs(uv.y - uLocalScan);
    float scan = exp(-d * d * 26.0);
    float inner = smoothstep(0.72, 0.3, abs(uv.x - 0.5) * 2.0);
    col += warn * scan * inner * 0.85;
    col += float3(1.0, 1.0, 1.0) * scan * inner * 0.35;

    col = saturate(col) * uAlpha;
    alpha = saturate(alpha) * uAlpha;
    return float4(col * alpha, alpha);
}

technique TechTerminal
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSTerminal();
    }
}

technique TechRelay
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSRelay();
    }
}

technique TechGate
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSGate();
    }
}
