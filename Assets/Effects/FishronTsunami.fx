// ============================================================================
// FishronTsunami.fx 海啸浪墙（2026-08 流体化重写）
// 世界 quad：uv.x 横向，uv.y 纵向 0(顶)~1(底)；uDir 控制前进朝向
// 流体核心：噪声域锚定在【世界坐标】——浪形在跑，水体纹理留在原地，
// 水从浪身里向后流过（真实波浪的物理：波形移动，水近似不动）
// 签名行为：行进浪涌沿冠线滚动 / 浪冠卷唇随涌周期性过卷破碎（破碎拍喷沫）/
// 前脸攀爬流+内部双层视差水体 / 冠下透光薄水层+卷管阴影 /
// 前倾剪切随速度加深 / 起浪几何生长、溃散自冠而下蚀顶
// 顶部预留 30% 画布给抛沫；直线算术无分支；噪声全走绑定贴图；无极角
// ============================================================================

float uTime;
float uIntensity;   // 0~1 残余亮度包络
float uGrowth;      // 0→1 起浪：浪体从地面长起
float uCollapse;    // 0→1 溃散：自浪冠向下蚀掉
float uDir;         // +1 向右 / -1 向左
float uSeed;
float uWorldX;      // 弹幕世界 X（px），流体域锚定用
float uCanvasPx;    // 画布世界宽度（px），uv→世界折算
float uSpeedRatio;  // 当前速度/基准速度，驱动前倾与翻涌烈度
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

// 噪声固定 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图
sampler noiseSamp : register(s1);

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // 归一坐标：xu=1 是浪的前脸（按 uDir 代数翻转，无分支）
    float flip = step(0.0, uDir);
    float xu = lerp(1.0 - uv.x, uv.x, flip);

    // 前倾剪切：越快浪头越往前压（顶端采样后移=显示前移），重心追不上脚；
    // 1.14 倍横向压缩给前脸/卷唇留出画布悬垂空间，鼻尖不顶画布右缘
    float lean = 0.05 + uSpeedRatio * 0.09;
    float xs = xu * 1.14 - (1.0 - uv.y) * lean + lean * 0.5;

    // 世界锚定横坐标（噪声域），水体不跟浪平移
    float wu = (uWorldX + (uv.x - 0.5) * uCanvasPx) * 0.0022 + uSeed;

    // =========================================================
    // A. 浪面线：后坡缓、前脸陡 + 行进浪涌沿冠线滚动
    // =========================================================
    float rise = smoothstep(0.02, 0.72, xs);
    float crestLine = lerp(0.82, 0.36, rise);
    // 行进涌：两股反向滚动的低频噪声，浪面永远在爬坡塌肩
    float surge1 = tex2D(noiseSamp, float2(wu * 0.55 - uTime * 0.42, 0.23)).r;
    float surge2 = tex2D(noiseSamp, float2(wu * 1.15 + uTime * 0.17, 0.71)).g;
    crestLine += (surge1 - 0.5) * 0.14 + (surge2 - 0.5) * 0.06;
    // 起浪几何生长：浪面从贴地(0.97)抬升到位
    float surfLine = lerp(0.97, crestLine, uGrowth);

    // 前脸截断：浪头前方没有水（噪声撕出参差前缘）
    float frontN = tex2D(noiseSamp, float2(uv.y * 2.2 + uSeed, xs * 3.0 - uTime * 0.5)).g;
    float frontCut = smoothstep(0.90, 0.72, xs + (frontN - 0.5) * 0.16);

    // 浪冠卷唇：卷曲量随行进涌呼吸，涌峰处过卷（破碎拍）
    float curl = 0.06 + 0.07 * smoothstep(0.45, 0.85, surge1);
    float nearCrest = smoothstep(surfLine + 0.16, surfLine, uv.y);
    float lipCut = smoothstep(0.99, 0.81, xs - nearCrest * curl + (frontN - 0.5) * 0.16);
    float edgeCut = max(frontCut, lipCut)
        * smoothstep(1.0, 0.93, xu) * smoothstep(0.0, 0.04, xu);

    // 溃散蚀顶：前沿从冠上向下推进，蚀口带噪声毛边
    float collapseFront = lerp(-0.25, 1.02, uCollapse);
    float collapseN = tex2D(noiseSamp, float2(xs * 2.5 + uSeed * 3.0, uTime * 0.6)).b;
    float collapseCut = smoothstep(collapseFront - 0.14, collapseFront + 0.05,
        uv.y + (collapseN - 0.5) * 0.12);

    float body = smoothstep(surfLine - 0.02, surfLine + 0.09, uv.y) * edgeCut * collapseCut;

    // =========================================================
    // B. 水体：世界锚定双层视差 + 前脸攀爬流 + 冠下透光
    // =========================================================
    // 深层慢流（几乎钉在世界上）与表层快流（被浪带着回卷）
    float depth = smoothstep(surfLine, 1.0, uv.y);
    float deepN = tex2D(noiseSamp, float2(wu * 0.7 + uTime * 0.04, uv.y * 1.6 + uSeed)).b;
    float skinN = tex2D(noiseSamp, float2(wu * 1.8 - uTime * 0.55 * uSpeedRatio, uv.y * 2.6 + 0.37)).r;
    float field = lerp(skinN, deepN, depth * 0.65);

    // 前脸攀爬流：贴前脸的窄带里水被卷着向上爬
    float faceBand = smoothstep(0.55, 0.9, xs) * smoothstep(surfLine + 0.34, surfLine + 0.04, uv.y);
    float climbN = tex2D(noiseSamp, float2(xs * 2.2 + uSeed * 7.0,
        uv.y * 3.0 + uTime * (0.9 + uSpeedRatio * 0.6))).g;
    field = lerp(field, climbN, faceBand * 0.55);

    float3 col = lerp(uSeaColor, uDeepColor, depth * 0.85);
    col *= 0.72 + field * 0.56;

    // 冠下透光：浪最薄处阳光穿透的青绿玻璃层
    float translucent = smoothstep(0.14, 0.0, uv.y - surfLine) * body;
    col += uSeaColor * float3(0.65, 1.25, 1.05) * translucent * 0.5;

    // 卷管阴影：冠唇正下方压暗，体积由暗侧撑起
    float tubeShade = smoothstep(surfLine + 0.03, surfLine + 0.10, uv.y)
        * smoothstep(surfLine + 0.20, surfLine + 0.10, uv.y)
        * smoothstep(0.45, 0.85, xs);
    col *= 1.0 - tubeShade * 0.38;

    // =========================================================
    // C. 浪冠翻沫（破碎拍增沫）+ 冠口断裂抛沫
    // =========================================================
    float crestBand = smoothstep(surfLine + 0.07, surfLine, uv.y);
    float crestN = tex2D(noiseSamp, float2(wu * 2.4 - uTime * 1.6, uv.y * 6.0)).g;
    float crestN2 = tex2D(noiseSamp, float2(wu * 4.6 + uTime * 0.9, uv.y * 4.0 + 0.53)).r;
    // 破碎拍：卷唇过卷时冠沫猛增；阈值收窄让泡沫成块撕裂而不是均匀起绒
    float churn = 0.45 + rise * 0.6 + smoothstep(0.09, 0.13, curl) * 0.7;
    float crest = crestBand * smoothstep(0.46, 0.60, crestN * 0.62 + crestN2 * 0.38)
        * churn * collapseCut;
    col += uFoamColor * crest;

    // 抛沫区：紧贴冠口上方，只被离散噪声点亮，速度越快甩得越密
    float sprayZone = smoothstep(0.55, 0.95, xs)
        * smoothstep(surfLine + 0.02, surfLine - 0.10, uv.y)
        * smoothstep(surfLine - 0.24, surfLine - 0.13, uv.y);
    float sprayN = tex2D(noiseSamp, float2(xs * 6.5 - uTime * (2.4 + uSpeedRatio), uv.y * 5.0 + uSeed)).r;
    float sprayN2 = tex2D(noiseSamp, float2(xs * 11.0 - uTime * 3.1 + uSeed, uv.y * 9.0)).g;
    float sprayGate = lerp(0.84, 0.72, saturate(uSpeedRatio - 0.4));
    float spray = sprayZone * smoothstep(sprayGate - 0.2, sprayGate, sprayN * 0.6 + sprayN2 * 0.4)
        * collapseCut;
    col += uFoamColor * spray * 0.9;

    // =========================================================
    // D. 浪脚翻涌 + 浪尾拖曳水裙 + 面上泡沫脉络
    // =========================================================
    float footChurn = smoothstep(0.86, 1.0, uv.y) * (0.35 + 0.65 * field);
    col += uFoamColor * footChurn * 0.35;

    float dragZone = smoothstep(0.55, 0.05, xs) * smoothstep(0.80, 0.97, uv.y);
    float dragN = tex2D(noiseSamp, float2(wu * 1.9 + uTime * 0.35, uv.y * 7.0)).r;
    float drag = dragZone * smoothstep(0.42, 0.68, dragN);
    col += lerp(uDeepColor, uFoamColor, 0.55) * drag * 0.5;

    float veins = smoothstep(0.68, 0.9, field) * (1.0 - depth * 0.6)
        * (0.45 + 0.75 * smoothstep(0.30, 0.90, xs));
    col += uFoamColor * veins * 0.30 * body;

    // =========================================================
    // 合成（预乘）：几何承担生长/溃散，uIntensity 只作残余亮度
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
