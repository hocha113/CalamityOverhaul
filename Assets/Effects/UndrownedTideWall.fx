// ============================================================================
// UndrownedTideWall.fx 泄洪浪（砸地起浪的低矮涌浪，FishronTsunami 血统的紧凑变体）
// 世界 quad：uv.x 横向，uv.y 纵向 0(顶)~1(底)；uDir 控制前进朝向
// 与海啸浪墙的分野：这是 3 格高的"涌浪"不是 30 格的浪墙——
// 前脸低陡带卷唇、背坡长拖裙、浪体矮壮贴地滚；顶部三成画布只给冠口碎沫
// 签名行为：起浪几何生长 / 卷唇前倾+管内暗侧 / 冠口断沫前甩 /
// 背坡拖曳湿裙 / 贴地暗底 / 溃散自冠蚀下（几何塌，不是整浪变淡）
// s1=PerlinNoise（实测值域 0.22~0.776，阈值过 nrm 归一）
// 直线算术无分支；预乘输出进 AlphaBlend 批
// ============================================================================

float uTime;
float uIntensity;   // 残余亮度包络
float uGrowth;      // 0→1 起浪：浪从水面/地面立起
float uCollapse;    // 0→1 溃散：自冠向下蚀
float uDir;         // +1 向右 / -1 向左
float uSeed;
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

sampler noiseSamp : register(s1);

float nrm(float v) { return saturate((v - 0.22) / 0.556); }

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // xu=1 是浪的前脸
    float flip = step(0.0, uDir);
    float xu = lerp(1.0 - uv.x, uv.x, flip);

    // ------- 浪面线：背坡长缓，前脸 0.62 处隆到最高，唇口略回落 -------
    float hump = smoothstep(0.05, 0.62, xu) * smoothstep(1.02, 0.72, xu);
    float crestLine = 0.92 - hump * 0.50;                 // 完全体浪面（0.42 最高）
    float surfN = tex2D(noiseSamp, float2(xu * 2.3 + uSeed - uTime * 0.5, 0.31)).r;
    crestLine += (surfN - 0.5) * 0.07;
    float surfLine = lerp(0.99, crestLine, uGrowth);      // 起浪：从贴地长起

    // ------- 前缘撕裂 + 卷唇前倾 -------
    float frontN = tex2D(noiseSamp, float2(uv.y * 2.0 + uSeed, xu * 3.4 - uTime * 0.7)).g;
    float frontCut = smoothstep(0.97, 0.80, xu + (frontN - 0.5) * 0.14);
    float nearCrest = smoothstep(surfLine + 0.13, surfLine, uv.y);
    float lipCut = smoothstep(1.02, 0.86, xu - nearCrest * 0.10 + (frontN - 0.5) * 0.14);
    float edgeCut = max(frontCut, lipCut)
        * smoothstep(1.0, 0.95, xu) * smoothstep(0.0, 0.05, xu);

    // ------- 溃散蚀顶 -------
    float colN = tex2D(noiseSamp, float2(xu * 2.8 + uSeed * 3.0, uTime * 0.7)).b;
    float colFront = lerp(-0.22, 1.03, uCollapse);
    float collapseCut = smoothstep(colFront - 0.12, colFront + 0.05, uv.y + (colN - 0.5) * 0.11);

    float body = smoothstep(surfLine - 0.02, surfLine + 0.08, uv.y) * edgeCut * collapseCut;

    // ------- 水体：攀爬流纹 + 唇下透光 + 卷管暗侧 + 贴地暗底 -------
    float flow1 = tex2D(noiseSamp, float2(xu * 2.4 + uSeed, uv.y * 1.8 + uTime * 1.3)).b;
    float flow2 = tex2D(noiseSamp, float2(xu * 5.4 - uTime * 0.55, uv.y * 3.1 + 0.47)).r;
    float field = flow1 * 0.6 + flow2 * 0.4;

    float depth = smoothstep(surfLine, 1.0, uv.y);
    float3 col = lerp(uSeaColor, uDeepColor, depth * 0.9);
    col *= 0.78 + field * 0.5;

    float translucent = smoothstep(0.10, 0.0, uv.y - surfLine) * body;
    col += uSeaColor * float3(0.62, 1.22, 1.02) * translucent * 0.62;

    float tubeShade = smoothstep(surfLine + 0.02, surfLine + 0.09, uv.y)
        * smoothstep(surfLine + 0.19, surfLine + 0.09, uv.y)
        * smoothstep(0.40, 0.78, xu);
    col *= 1.0 - tubeShade * 0.40;

    // ------- 冠沫 + 冠口断沫（前甩碎白，不连片）-------
    float crestBand = smoothstep(surfLine + 0.06, surfLine, uv.y);
    float crestN = tex2D(noiseSamp, float2(xu * 4.4 - uTime * 1.8 + uSeed, uv.y * 5.5)).g;
    float crest = crestBand * smoothstep(0.34, 0.62, nrm(crestN)) * (0.7 + hump * 0.9) * collapseCut;
    col += uFoamColor * crest * 1.25;

    float sprayZone = smoothstep(0.45, 0.85, xu)
        * smoothstep(surfLine + 0.02, surfLine - 0.16, uv.y)
        * smoothstep(surfLine - 0.26, surfLine - 0.13, uv.y);
    float sprayN = tex2D(noiseSamp, float2(xu * 7.0 - uTime * 2.6, uv.y * 5.2 + uSeed)).r;
    float sprayN2 = tex2D(noiseSamp, float2(xu * 12.0 - uTime * 3.4 + uSeed, uv.y * 8.6)).g;
    float spray = sprayZone * smoothstep(0.56, 0.80, nrm(sprayN * 0.6 + sprayN2 * 0.4)) * collapseCut;
    col += uFoamColor * spray * 0.9;

    // ------- 背坡拖曳湿裙 + 浪脚翻涌 -------
    float dragZone = smoothstep(0.55, 0.03, xu) * smoothstep(0.72, 0.96, uv.y);
    float dragN = tex2D(noiseSamp, float2(xu * 3.2 + uTime * 1.0 + uSeed, uv.y * 6.4)).r;
    float drag = dragZone * smoothstep(0.45, 0.72, nrm(dragN));
    col += lerp(uDeepColor, uFoamColor, 0.5) * drag * 0.45;

    float footChurn = smoothstep(0.84, 1.0, uv.y) * (0.3 + 0.7 * field) * body;
    col += uFoamColor * footChurn * 0.30;

    // ------- 合成（预乘）-------
    float density = body * (0.74 + field * 0.30) + spray * 0.65 + drag * 0.30;
    float guard = smoothstep(0.0, 0.02, uv.x) * smoothstep(1.0, 0.98, uv.x)
        * smoothstep(0.0, 0.015, uv.y) * smoothstep(1.0, 0.97, uv.y);
    float alpha = saturate(density * uIntensity * guard) * 0.94;

    return float4(col * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass TideWallPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
