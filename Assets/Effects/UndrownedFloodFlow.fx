// ============================================================================
// UndrownedFloodFlow.fx 泄洪流（两技法）
// TechColumn 立管泄洪柱：涨水仪式期立管喷出的竖直水柱
//   uv.y 0(管口)~1(落点水面)。签名行为：管口溢流球根（禁平切） /
//   重力加速签名（sqrt 纵坐标，上密下疏） / 沿程微收窄 / 两缘剥离飞沫 /
//   落点溅丘翻沫（端部有名字的物理答案） / uLife 展开、uDrain 自管口向下排空
// TechDrain 泄洪拽流层：死亡演出铺满房间的向心水流场
//   uFocus=格栅 uv 焦点。签名行为：全域流纹向焦点滑动 / 收缩的同心泡沫圈
//   （圈=cos(dist)，纯径向零极角） / 焦点白水喉 / 近焦三臂涡（sin(3θ) 整数倍角）
// s1=PerlinNoise（值域 0.22~0.776，阈值过 nrm）
// 直线算术无分支；预乘输出进 AlphaBlend 批
// ============================================================================

float uTime;
float uLife;        // 柱：0→1 展开包络 / 拽流：总强度
float uDrain;       // 柱：0→1 自管口排空
float uSeed;
float2 uFocus;      // 拽流焦点（uv）
float uAspect;      // 拽流画布宽高比（w/h，等距化用）
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

sampler noiseSamp : register(s1);

float nrm(float v) { return saturate((v - 0.22) / 0.556); }

float2 rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

// ---------------------------------------------------------------- TechColumn
float4 ColumnPS(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float2 c = float2(uv.x - 0.5, uv.y);

    // 展开：宽度 easeOut 从 0 撑满；排空：可见段自管口向下撤
    float widen = 1.0 - (1.0 - saturate(uLife)) * (1.0 - saturate(uLife));
    float drainFront = uDrain * 1.15;
    float drainN = tex2D(noiseSamp, float2(uv.x * 3.0 + uSeed, uTime * 0.7)).r;
    float drainCut = smoothstep(drainFront - 0.10, drainFront + 0.06, uv.y + (drainN - 0.5) * 0.10);

    // 柱宽剖面：管口球根（0~12% 鼓出）→ 沿程微收窄
    float bulb = 1.0 + exp2(-uv.y * 9.0) * 0.85;
    float taper = lerp(1.0, 0.72, uv.y);
    float halfW = 0.16 * bulb * taper * widen;

    // 中轴低频游走（水柱不是直尺）
    float sway = (tex2D(noiseSamp, float2(uv.y * 0.9 - uTime * 0.5, uSeed)).g - 0.5) * 0.08 * uv.y;
    float dx = abs(c.x - sway);

    // 重力加速签名：纵向采样按 sqrt(v) 展开，上密下疏
    float fallV = sqrt(uv.y) * 2.6 - uTime * 1.9;
    float streak = tex2D(noiseSamp, float2(uv.x * 4.5 + uSeed, fallV)).b;
    float streak2 = tex2D(noiseSamp, float2(uv.x * 8.0 + uSeed * 2.0, fallV * 1.7 + 0.37)).r;

    float core = smoothstep(halfW, halfW * 0.35, dx);
    float edge = smoothstep(halfW * 1.5, halfW * 0.9, dx) - core;

    // 两缘剥离飞沫：柱缘外离散碎白
    float peelN = tex2D(noiseSamp, float2(uv.x * 10.0 + uSeed * 3.0, fallV * 1.3)).g;
    float peel = smoothstep(halfW * 1.0, halfW * 1.7, dx) * smoothstep(halfW * 2.6, halfW * 1.7, dx)
        * smoothstep(0.68, 0.88, nrm(peelN)) * smoothstep(0.1, 0.5, uv.y);

    // 落点溅丘：底部 12% 翻沫堆，宽度加倍
    float mound = smoothstep(0.88, 1.0, uv.y) * smoothstep(halfW * 2.8, halfW * 0.8, dx)
        * (0.5 + 0.5 * nrm(tex2D(noiseSamp, float2(uv.x * 6.0 - uTime * 1.5, 0.9 + uSeed)).r));

    float3 col = lerp(uSeaColor, uDeepColor, 0.35 + uv.y * 0.25);
    col *= 0.72 + streak * 0.5;
    col += uFoamColor * (edge * (0.35 + streak2 * 0.4) + peel * 0.9 + mound * 0.75);
    // 管口溢流亮环：球根上白水翻出
    col += uFoamColor * smoothstep(0.10, 0.0, uv.y) * core * 0.55;

    float guard = smoothstep(0.0, 0.05, uv.x) * smoothstep(1.0, 0.95, uv.x)
        * smoothstep(0.0, 0.01, uv.y) * smoothstep(1.0, 0.985, uv.y);
    float density = (core * (0.6 + streak * 0.3) + edge * 0.4 + peel * 0.7 + mound * 0.6) * drainCut;
    float alpha = saturate(density * guard) * 0.92;
    return float4(col * alpha, alpha) * vColor.a;
}

// ----------------------------------------------------------------- TechDrain
float4 DrainPS(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // 等距化坐标（补偿画布拉伸，圈才是圆的）
    float2 p = float2((uv.x - uFocus.x) * uAspect, uv.y - uFocus.y);
    float dist = length(p);

    // 全域流纹：沿"指向焦点"的方向滑动采样（笛卡尔连续，无极角）
    float2 toFocus = -p / max(dist, 1e-4);
    float flowN = tex2D(noiseSamp, (uv + toFocus * uTime * 0.10) * 2.6 + uSeed).g;
    float flow = smoothstep(0.35, 0.75, nrm(flowN));

    // 收缩同心泡沫圈：cos(dist·k + t)，纯径向
    float ringWave = cos(dist * 22.0 + uTime * 5.0);
    float rings = smoothstep(0.55, 0.95, ringWave) * smoothstep(1.15, 0.2, dist);

    // 近焦三臂涡：theta 唯一消费 sin(3θ - ...)，整数倍角连续
    float theta = atan2(p.y, p.x);
    float swirl = sin(3.0 * theta - log(dist * 6.0 + 1.0) * 6.0 - uTime * 4.0);
    float swirlBand = smoothstep(0.3, 0.9, swirl) * smoothstep(0.5, 0.12, dist) * smoothstep(0.02, 0.08, dist);

    // 焦点白水喉
    float throat = smoothstep(0.10, 0.015, dist);

    float3 col = lerp(uSeaColor, uDeepColor, saturate(dist * 1.3)) * (0.6 + flow * 0.4);
    col += uFoamColor * (rings * 0.45 + swirlBand * 0.7 + throat * 0.85);

    // 焦点更实、远处轻罩；洪流总量随 uLife
    float density = (0.16 + flow * 0.14 + rings * 0.28 + swirlBand * 0.45 + throat * 0.7)
        * smoothstep(1.35, 0.25, dist);
    float guard = smoothstep(0.0, 0.02, uv.x) * smoothstep(1.0, 0.98, uv.x)
        * smoothstep(0.0, 0.02, uv.y) * smoothstep(1.0, 0.98, uv.y);
    float alpha = saturate(density * uLife * guard) * 0.88;
    return float4(col * alpha, alpha) * vColor.a;
}

technique TechColumn
{
    pass ColumnPass
    {
        PixelShader = compile ps_3_0 ColumnPS();
    }
}

technique TechDrain
{
    pass DrainPass
    {
        PixelShader = compile ps_3_0 DrainPS();
    }
}
