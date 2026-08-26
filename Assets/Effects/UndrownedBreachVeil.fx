// ============================================================================
// UndrownedBreachVeil.fx 破水水幕（破水/落水/砸浪共用的一次性水幕事件）
// 竖直 quad：uv.y 0(顶)~1(底)，底边锚在水面线；uLife 0→1 走完一次演出
// 签名行为：水幕整片顶起（几何升，不是淡入） / 幕体撕成逐列垂水条 /
// 条端 Plateau-Rayleigh 颈缩断成滴串坠回 / 幕根泡沫堆 /
// 收场=幕顶整体沉回水面（fall 推 sheetTop 下沉，水回到水里，不是原地蒸发）
// 时间轴（uLife）：0~0.25 顶起 / 0.2~0.6 撕条 / 0.45~1 坠回沉没
// s1=PerlinNoise（值域 0.22~0.776，阈值过 nrm）
// 直线算术无分支；预乘输出进 AlphaBlend 批
// ============================================================================

float uTime;
float uLife;        // 0→1 事件寿命
float uSeed;
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

sampler noiseSamp : register(s1);

float nrm(float v) { return saturate((v - 0.22) / 0.556); }

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // ------- 幕顶：升到 0.12 后随坠回下沉；顶缘弧形（中间高两侧低）-------
    float rise = smoothstep(0.0, 0.25, uLife);
    rise = 1.0 - (1.0 - rise) * (1.0 - rise);              // easeOut 顶起
    float fall = smoothstep(0.45, 1.0, uLife);
    float arc = 1.0 - (uv.x - 0.5) * (uv.x - 0.5) * 2.6;   // 中高侧低
    float topN = tex2D(noiseSamp, float2(uv.x * 2.8 + uSeed, 0.4)).r;
    float sheetTop = lerp(1.0, 0.12 + (1.0 - arc) * 0.4, rise) + fall * fall * 0.9;
    sheetTop += (topN - 0.5) * 0.12;

    // ------- 逐列撕条：列噪声定各列的"存活阈值"，撕裂度随寿命抬升 -------
    // 撕裂封顶 0.75：沉没期仍留两三成粗水条一路坠回，幕不会凭空清场
    float colKey = tex2D(noiseSamp, float2(uv.x * 3.6 + uSeed * 2.0, 0.11)).g;
    float tear = smoothstep(0.20, 0.60, uLife) * 0.75;
    float columnAlive = smoothstep(tear - 0.18, tear + 0.12, nrm(colKey) * 0.9 + 0.1);

    // 列内垂流：向下滚动的水条肌理
    float streakN = tex2D(noiseSamp, float2(uv.x * 5.0 + uSeed, uv.y * 1.6 - uTime * 1.6)).b;

    // ------- 条端颈缩断滴：纵向拉长的滴串（高 x 频 × 低 y 频，圆点噪声读不成滴串）-------
    float dropN = tex2D(noiseSamp, float2(uv.x * 9.0 + uSeed * 4.0, uv.y * 1.3 - uTime * 2.4)).g;
    float belowTop = smoothstep(sheetTop, sheetTop + 0.10, uv.y);
    float body = belowTop * columnAlive * smoothstep(1.0, 0.96, uv.y);
    float dropZone = smoothstep(sheetTop + 0.02, sheetTop - 0.30, uv.y) * (1.0 - columnAlive);
    float drops = dropZone * smoothstep(0.52, 0.78, nrm(dropN)) * smoothstep(0.05, 0.25, uLife);

    // ------- 上色：幕体透光水，边缘白沫 -------
    float3 col = lerp(uSeaColor, uDeepColor, uv.y * 0.55);
    col *= 0.60 + streakN * 0.72;
    // 顶缘白沫冠：幕顶被撕开的白边（幕的"水"身份靠它）。
    // 独立峰带，不乘 belowTop 渐入——两个反向 ramp 相乘只剩 0.3 级小鼓包，
    // 冠沫读不出来（2026-08 沙盒毙过一版）
    float crown = smoothstep(sheetTop + 0.12, sheetTop + 0.045, uv.y)
        * smoothstep(sheetTop - 0.01, sheetTop + 0.045, uv.y) * columnAlive;
    col += uFoamColor * crown * 1.6;
    // 根部泡沫堆：底边一坨翻涌
    float rootFoam = smoothstep(0.86, 1.0, uv.y) * columnAlive
        * smoothstep(0.35, 0.7, nrm(tex2D(noiseSamp, float2(uv.x * 6.0 - uTime * 1.2, 0.8 + uSeed)).r))
        * smoothstep(0.55, 0.15, uLife);
    col += uFoamColor * rootFoam * 0.8;

    // ------- 合成（预乘）-------
    float guard = smoothstep(0.0, 0.03, uv.x) * smoothstep(1.0, 0.97, uv.x)
        * smoothstep(0.0, 0.02, uv.y) * smoothstep(1.0, 0.985, uv.y);
    float density = body * (0.72 + streakN * 0.30) + drops * 0.9 + rootFoam * 0.45;
    float alpha = saturate(density * guard) * 0.92;

    return float4(col * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass VeilPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
