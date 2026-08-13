// ============================================================================
// FishronTornado.fx 鲨鱼龙卷柱体（水+风复合材质，2026-08 反塑料重写）
// 世界 quad：uv.x 横向 0~1，uv.y 纵向 0(顶)~1(底)；柱体只占 quad 中带，
// 四周留足撕裂余量——轮廓由噪声蚀刻半径决定，绝无干净数学圆柱边。
// 签名行为：双层反向螺旋水带(前亮后暗遮挡差) / 噪声撕裂轮廓+离体飞沫 /
// 底部卷吸裙摆碎浪 / 顶部风切歪斜散逸 / 多频不可通约摆轴
// 直线算术无分支，噪声全走绑定贴图，无极角
// ============================================================================

float uTime;
float uIntensity;   // 0~1 起身/消散包络
float uGrade;       // 0/1 升格层级
float uSeed;        // 实例种子
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // =========================================================
    // A. 摆轴：三条不可通约频率 + 噪声漂移——绝不出现单摆钟摆感
    // =========================================================
    float swayN = tex2D(noiseSamp, float2(uTime * 0.045 + uSeed, uv.y * 0.6)).r - 0.5;
    float sway = (sin(uTime * 1.83 + uSeed * 7.0 + (1.0 - uv.y) * 2.2) * 0.45
        + sin(uTime * 0.97 + uSeed * 3.1 + (1.0 - uv.y) * 4.7) * 0.3
        + swayN * 1.1) * 0.062 * (1.0 - uv.y);
    float axis = 0.5 + sway;
    float dx = uv.x - axis;

    // =========================================================
    // B. 轮廓：上宽下窄 + 底部卷吸裙摆外扩；半径由噪声蚀刻
    // =========================================================
    // 名义半宽压在 0.20 内（升格 ×1.12=0.224）；摆幅上界=(0.45+0.3+0.5*1.1)*0.062≈0.081，
    // 飞沫区外界 1.75×半宽：最大横向延伸 0.5+0.081+0.224*1.75≈0.973 < 护栏起点 0.985——裁切在几何层杜绝
    float skirt = smoothstep(0.78, 1.0, uv.y);          // 底部裙摆区
    float halfW = lerp(0.20, 0.088, uv.y) * (1.0 + uGrade * 0.12)
        + skirt * 0.07;                                  // 裙摆外扩
    float side = dx / max(halfW, 1e-4);                  // -1..1 带符号横位
    float rad = abs(side);                               // 0 轴 → 1 名义边缘

    // 轮廓蚀刻：边缘半径随噪声 ±35% 波动 → 撕裂轮廓
    float edgeN = tex2D(noiseSamp, float2(uv.y * 2.8 - uTime * 0.7 + uSeed,
        side * 0.8 + uSeed * 2.0)).g;
    float cutR = 1.0 + (edgeN - 0.5) * 0.7;              // 0.65~1.35
    float body = smoothstep(cutR + 0.12, cutR - 0.30, rad);

    // 离体飞沫：名义边缘外一圈，被高频噪声打碎成孤立水屑
    float fleckZone = smoothstep(0.7, 1.05, rad) * smoothstep(1.75, 1.2, rad);
    float fleckN = tex2D(noiseSamp, float2(uv.y * 5.5 - uTime * 1.9 + uSeed * 3.0,
        side * 2.2 - uTime * 0.4)).b;
    float flecks = fleckZone * smoothstep(0.68, 0.85, fleckN);

    // =========================================================
    // C. 双层反向螺旋水带：斜向条带(横滚+爬升) → 读作旋转上升
    //    前层快、亮、窄；后层慢、暗、宽——速差+遮挡=假体积
    // =========================================================
    // 前层：向右横滚 + 向上爬升的斜带
    float2 fUV = float2(side * 0.55 - uTime * 1.35 + uSeed,
        uv.y * 2.6 + uTime * 0.85);
    float fN = tex2D(noiseSamp, fUV).g;
    float front = smoothstep(0.46, 0.60, fN);            // 窄阈值→离散水带
    // 后层：反向横滚、更慢、更宽的带
    float2 bUV = float2(side * 0.38 + uTime * 0.72 + uSeed * 2.0,
        uv.y * 1.7 + uTime * 0.5 + 0.37);
    float bN = tex2D(noiseSamp, bUV).b;
    float back = smoothstep(0.40, 0.62, bN);

    // 竖向上升水丝
    float2 upUV = float2(side * 1.8 + uSeed, uv.y * 1.2 - uTime * 1.6);
    float updraft = tex2D(noiseSamp, upUV).r;

    // 遮挡合成：前带压住后带(暗部)，明暗交替出体积
    float backLit = back * 0.42 * (1.0 - front * 0.75);
    float field = front * 0.85 + backLit + updraft * 0.22;

    // 圆柱受光：临边压暗 + 左亮右暗的侧光不对称
    float shade = (1.0 - rad * rad * 0.42) * (1.0 + side * -0.14);

    // =========================================================
    // D. 顶部风切散逸：向侧面歪斜抹开 + 噪声蚀出破絮
    // =========================================================
    float crownZone = smoothstep(0.36, 0.04, uv.y);
    float crownN = tex2D(noiseSamp, float2(side * 1.1 - uTime * 0.55 + uSeed,
        uv.y * 4.2 + uSeed * 5.0)).r;
    // 破絮蚀刻：越靠顶部被吃得越碎；最顶 8% 再叠确定性软零——顶边永无硬切
    float crownFade = 1.0 - crownZone * smoothstep(0.28, 0.55, crownN + crownZone * 0.25);
    crownFade *= smoothstep(0.0, 0.08, uv.y);

    // =========================================================
    // E. 色阶：底部深海沉色 → 中段海青 → 顶部苍白水汽
    // =========================================================
    float3 col = lerp(uSeaColor, uDeepColor, smoothstep(0.35, 1.0, uv.y) * 0.8);
    col = lerp(col, uFoamColor * 0.55, crownZone * 0.4);           // 顶部发白
    col *= (0.55 + field * 0.75) * shade;

    // 前层水带亮缘：窄阈值白沫描边
    float foamEdge = smoothstep(0.60, 0.74, fN) * (1.0 - rad * 0.5);
    col += uFoamColor * foamEdge * (0.4 + uGrade * 0.18);

    // 底部卷吸碎浪：裙摆区高对比翻涌
    float churnN = tex2D(noiseSamp, float2(side * 1.6 - uTime * 2.3 + uSeed,
        uv.y * 6.0)).g;
    float churn = skirt * smoothstep(0.4, 0.72, churnN + field * 0.25);
    col += uFoamColor * churn * (0.5 + uGrade * 0.2);

    // 离体飞沫上色（纯白屑）
    col += uFoamColor * flecks * 0.8;

    // =========================================================
    // 合成（预乘）：本体密度 + 飞沫独立通道；uv 护栏仅防采样溢出，
    // 正常轮廓远在护栏内侧，不再依赖护栏切边
    // =========================================================
    float density = body * (0.40 + field * 0.60) * crownFade * shade;
    density += flecks * 0.55 + churn * skirt * 0.3;
    // 底缘 4% 渐没入地——斜坡地形上不露水平硬线
    float guard = smoothstep(0.0, 0.015, uv.x) * smoothstep(1.0, 0.985, uv.x)
        * smoothstep(0.0, 0.012, uv.y) * smoothstep(1.0, 0.96, uv.y);
    float alpha = saturate(density * uIntensity * guard) * 0.92;

    return float4(col * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass TornadoPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
