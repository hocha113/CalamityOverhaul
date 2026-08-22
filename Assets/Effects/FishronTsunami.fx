// ============================================================================
// FishronTsunami.fx 海啸浪墙（2026-08 反塑料重写）
// 世界 quad：uv.x 横向，uv.y 纵向 0(顶)~1(底)；uDir 控制前进朝向
// 签名行为：起浪几何生长(浪从地里立起来，不是淡入) / 浪冠前倾卷曲+管内阴影 /
// 冠下透光薄水层 / 冠口断裂抛沫(孤立水屑) / 浪尾底部拖曳水裙 /
// 溃散自冠而下(几何蚀顶，不是整墙变淡)
// 顶部预留 30% 画布给抛沫，裁切在布局层杜绝
// 直线算术无分支，噪声全走绑定贴图，无极角
// ============================================================================

float uTime;
float uIntensity;   // 0~1 残余亮度包络（几何生长/溃散另走 uGrowth/uCollapse）
float uGrowth;      // 0→1 起浪：浪体从地面长起
float uCollapse;    // 0→1 溃散：自浪冠向下蚀掉
float uDir;         // +1 向右 / -1 向左
float uSeed;
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

// 噪声固定在 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
// sampler_state 块在 FNA 下会被分配到 s0 导致噪声读到画布渐变
// 三审实机"浪顶灰度图"= 画布贴图自身辉光从抛沫通道漏出；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // 归一坐标：xu=1 是浪的前脸（按 uDir 代数翻转，无分支）
    float flip = step(0.0, uDir);
    float xu = lerp(1.0 - uv.x, uv.x, flip);

    // =========================================================
    // A. 浪面线：后坡缓、前脸陡；浪体随 uGrowth 从地里长出
    // =========================================================
    float rise = smoothstep(0.02, 0.72, xu);
    // 冠顶下限 0.36-0.045=0.315：抛沫带(冠上 0.30)最高到 0.015，恰好收在顶护栏处
    float crestLine = lerp(0.82, 0.36, rise);            // 完全体浪面
    float surfN = tex2D(noiseSamp, float2(xu * 1.8 + uSeed - uTime * 0.35, 0.27)).r;
    crestLine += (surfN - 0.5) * 0.09;
    // 起浪几何生长：浪面从贴地(0.97)抬升到位，浪是"立起来"的
    float surfLine = lerp(0.97, crestLine, uGrowth);

    // 前脸截断：浪头前方没有水（噪声撕出参差前缘）
    float frontN = tex2D(noiseSamp, float2(uv.y * 2.2 + uSeed, xu * 3.0 - uTime * 0.5)).g;
    float frontCut = smoothstep(0.90, 0.72, xu + (frontN - 0.5) * 0.16);

    // 浪冠卷曲：临近浪面的水向前探出，前缘随高度前倾
    float nearCrest = smoothstep(surfLine + 0.16, surfLine, uv.y);
    float lipLean = nearCrest * 0.09;
    float lipCut = smoothstep(0.99, 0.81, xu - lipLean + (frontN - 0.5) * 0.16);
    // 确定性外沿软零：噪声/前倾无论怎么推，前后缘都在画布内羽化到零
    float edgeCut = max(frontCut, lipCut)
        * smoothstep(1.0, 0.93, xu) * smoothstep(0.0, 0.04, xu);

    // 溃散蚀顶：溃散前沿从冠上向下推进，蚀口带噪声毛边
    float collapseFront = lerp(-0.25, 1.02, uCollapse);
    float collapseN = tex2D(noiseSamp, float2(xu * 2.5 + uSeed * 3.0, uTime * 0.6)).b;
    float collapseCut = smoothstep(collapseFront - 0.14, collapseFront + 0.05,
        uv.y + (collapseN - 0.5) * 0.12);

    // 体掩码
    float body = smoothstep(surfLine - 0.02, surfLine + 0.09, uv.y) * edgeCut * collapseCut;

    // =========================================================
    // B. 水体：攀爬前脸的流纹 + 冠下透光薄水层 + 底部沉深
    // =========================================================
    float2 flowUV = float2(xu * 2.6 + uSeed, uv.y * 2.0 + uTime * 1.15);
    float flow1 = tex2D(noiseSamp, flowUV).b;
    float2 flowUV2 = float2(xu * 5.2 - uTime * 0.4, uv.y * 3.4 + 0.53);
    float flow2 = tex2D(noiseSamp, flowUV2).r;
    float field = flow1 * 0.6 + flow2 * 0.4;

    float depth = smoothstep(surfLine, 1.0, uv.y);       // 0 浪面 → 1 浪底
    float3 col = lerp(uSeaColor, uDeepColor, depth * 0.85);
    col *= 0.8 + field * 0.45;

    // 冠下透光：浪最薄处阳光穿透，亮出一条青绿色的"玻璃层"
    float translucent = smoothstep(0.14, 0.0, uv.y - surfLine) * body;
    col += uSeaColor * float3(0.65, 1.25, 1.05) * translucent * 0.5;

    // 卷管阴影：冠唇正下方一道压暗，卷曲的体积由这道暗侧撑起
    float tubeShade = smoothstep(surfLine + 0.03, surfLine + 0.10, uv.y)
        * smoothstep(surfLine + 0.20, surfLine + 0.10, uv.y)
        * smoothstep(0.45, 0.85, xu);
    col *= 1.0 - tubeShade * 0.38;

    // =========================================================
    // C. 浪冠翻沫 + 冠口断裂抛沫（孤立水屑，前甩+上扬）
    // =========================================================
    float crestBand = smoothstep(surfLine + 0.07, surfLine, uv.y);
    float crestN = tex2D(noiseSamp, float2(xu * 4.0 - uTime * 1.5 + uSeed, uv.y * 6.0)).g;
    float crest = crestBand * smoothstep(0.30, 0.66, crestN) * (0.45 + rise * 0.8) * collapseCut;
    col += uFoamColor * crest;

    // 抛沫区：冠上方一大片画布，只被离散噪声点亮，断裂的碎白不是连片辉光
    float sprayZone = smoothstep(0.55, 0.95, xu)
        * smoothstep(surfLine + 0.02, surfLine - 0.22, uv.y)
        * smoothstep(surfLine - 0.30, surfLine - 0.18, uv.y);
    float sprayN = tex2D(noiseSamp, float2(xu * 6.5 - uTime * 2.4, uv.y * 5.0 + uSeed)).r;
    float sprayN2 = tex2D(noiseSamp, float2(xu * 11.0 - uTime * 3.1 + uSeed, uv.y * 9.0)).g;
    float spray = sprayZone * smoothstep(0.60, 0.82, sprayN * 0.6 + sprayN2 * 0.4) * collapseCut;
    col += uFoamColor * spray * 0.9;

    // =========================================================
    // D. 浪脚翻涌 + 浪尾拖曳水裙 + 面上泡沫脉络
    // =========================================================
    float footChurn = smoothstep(0.86, 1.0, uv.y) * (0.35 + 0.65 * field);
    col += uFoamColor * footChurn * 0.35;

    // 拖曳裙：浪尾贴地一层被拖出的横纹湿沫，越靠后越薄
    float dragZone = smoothstep(0.55, 0.05, xu) * smoothstep(0.80, 0.97, uv.y);
    float dragN = tex2D(noiseSamp, float2(xu * 3.4 + uTime * 0.9 + uSeed, uv.y * 7.0)).r;
    float drag = dragZone * smoothstep(0.42, 0.68, dragN);
    col += lerp(uDeepColor, uFoamColor, 0.55) * drag * 0.5;

    // 面上泡沫脉络：密度沿前进方向递增，浪尾平静、前脸翻涌的梯度
    float veins = smoothstep(0.68, 0.9, field) * (1.0 - depth * 0.6)
        * (0.45 + 0.75 * smoothstep(0.30, 0.90, xu));
    col += uFoamColor * veins * 0.30 * body;

    // =========================================================
    // 合成（预乘）：溃散/生长由几何承担，uIntensity 只作残余亮度；
    // 护栏仅防采样溢出，顶部 30% 画布本就留白，不靠护栏切形
    // =========================================================
    float density = body * (0.58 + field * 0.42) + spray * 0.6 + drag * 0.35;
    float guard = smoothstep(0.0, 0.02, uv.x) * smoothstep(1.0, 0.98, uv.x)
        * smoothstep(0.0, 0.015, uv.y) * smoothstep(1.0, 0.965, uv.y);
    float alpha = saturate(density * uIntensity * guard) * 0.92;

    return float4(col * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass TsunamiPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
