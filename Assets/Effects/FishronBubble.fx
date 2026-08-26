// ============================================================================
// FishronBubble.fx 爆裂水膜泡（2026-08 泡泡重绘）
// 归一化圆盘 quad：盘径=画布半径 0.42，C# 折算 quadPx = 可见半径 / 0.42 * 2
// 材质：海水吹成的薄膜泡——不是玻璃珠也不是光球
// 签名行为：方向噪声推挤的呼吸变形（泡永远在轻轻晃）/ Fresnel 膜缘增亮 /
// 薄膜虹彩沿膜缓慢流转 / 顶部各向异性水光高光 / 内腔两层缓旋水纹 /
// 暗缘线真遮挡（亮背景下仍有剪影）/ uArm 待发绷紧+电青灼光 /
// uBurst 破膜：噪声阈值蚀膜、碎片径向散射、一瞬白闪
// 预乘输出配 AlphaBlend；直线算术无分支；无极角（方向噪声走单位向量域）
// ============================================================================

float uTime;
float uSeed;
float uWobble;     // 0~1 变形幅度（随速度/驻停状态）
float uArm;        // 0~1 环阵待发绷紧
float uBurst;      // 0~1 破膜进度
float uFade;       // 0~1 总体包络（渐显/生命）
float3 uTint;      // 膜体基色
float3 uDeepColor; // 内腔沉色

// 噪声固定 s1：C# 侧 Textures[1]=PerlinNoise + LinearWrap，Apply 前绑定
sampler noiseSamp : register(s1);

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float2 p = (uv - 0.5) / 0.42;
    float r = length(p);
    float2 dir = p / max(r, 1e-4);

    // 方向噪声：单位向量直接进噪声域，跨整圈天然连续
    float angN = tex2D(noiseSamp, dir * 0.32 + uSeed + uTime * 0.05).r;
    float angN2 = tex2D(noiseSamp, dir * 0.61 - uTime * 0.037 + uSeed * 2.7).g;

    // 水膜呼吸：两股方向噪声推挤半径
    float wob = (angN - 0.5) * 0.10 + (angN2 - 0.5) * 0.06;
    float rw = r * (1.0 + wob * uWobble);
    // 待发绷紧：膜收 6%，晃动被张力压平
    rw *= 1.0 + 0.06 * uArm;
    rw = lerp(rw, r, uArm * 0.55);

    // 破膜散射：碎片沿噪声一半外抛一半内塌
    float burstN = tex2D(noiseSamp, dir * 0.9 + uSeed * 5.1).b;
    rw += uBurst * 0.24 * (burstN - 0.5) * 2.0;
    // 蚀膜保留量：阈值扫过噪声，膜成块消失而不是整体变淡
    float keep = smoothstep(0.0, 0.09, burstN - (uBurst * 1.15 - 0.05));

    // 膜带与 Fresnel：靠边缘越亮，正对处几乎透明
    float film = smoothstep(0.17, 0.03, abs(rw - 1.0));
    float fresnel = smoothstep(0.30, 1.0, rw);
    fresnel *= fresnel;

    // 薄膜虹彩：相位随半径/噪声/时间缓慢流转；
    // 通道相位压缩在青绿-电青带内，不出系列色板（禁紫红糖果彩虹）
    float phase = rw * 2.2 + angN * 1.6 + uTime * 0.35 + uSeed;
    float3 irid = 0.5 + 0.5 * cos(6.2832 * (phase + float3(0.42, 0.28, 0.13)));
    irid = lerp(irid, irid.ggb, 0.35);
    float3 filmCol = lerp(uTint, irid, 0.38);

    // 内腔水纹：两层反向缓旋
    float2 iuv = p * 0.4;
    float in1 = tex2D(noiseSamp, iuv + float2(uTime * 0.05, -uTime * 0.03) + uSeed).r;
    float in2 = tex2D(noiseSamp, iuv * 1.9 - float2(uTime * 0.02, uTime * 0.06) + uSeed * 3.3).g;
    float inner = smoothstep(0.52, 0.85, in1 * 0.6 + in2 * 0.4);
    float innerMask = smoothstep(1.02, 0.82, rw);
    // 泡底积水弯月：水往下坠的一小汪，膜里真的有水
    float pool = smoothstep(0.35, 0.95, p.y + (angN - 0.5) * 0.3) * innerMask
        * smoothstep(0.55, 0.95, rw);

    // 顶部主高光（斜长条水光）+ 右下弱副光
    float2 gpos = p - float2(-0.42, -0.46);
    float2 gs = gpos * float2(3.4, 6.5);
    float glint = exp2(-dot(gs, gs) * 1.443);
    float2 gpos2 = p - float2(0.35, 0.42);
    float2 gs2 = gpos2 * float2(7.0, 5.0);
    float glint2 = exp2(-dot(gs2, gs2) * 1.443) * 0.35;

    // 待发灼光：膜内侧升起电青，快闪节拍
    float armGlow = uArm * smoothstep(0.5, 0.95, rw) * (0.62 + 0.38 * sin(uTime * 21.0 + uSeed * 9.0));
    // 破膜白闪：只活在破裂的头两帧
    float burstFlash = uBurst * (1.0 - uBurst) * 4.0;

    // 出膜护栏
    float edgeGuard = smoothstep(1.30, 1.06, rw);

    // 暗缘线：真遮挡成分，亮背景下的剪影
    float rimDark = smoothstep(0.055, 0.015, abs(rw - 1.0)) * 0.55;

    float aFilm = film * fresnel * 0.62;
    float aInner = innerMask * (0.07 + inner * 0.13) + pool * 0.16;
    float alpha = saturate((aFilm + aInner + rimDark) * edgeGuard * keep * uFade);

    float3 col = filmCol * aFilm + uDeepColor * aInner + uTint * pool * 0.10 + uTint * 0.22 * rimDark;
    // 纯加光成分：高光/灼光/破闪——只加 rgb 不加 alpha，预乘批里读作光
    float3 light = float3(0.85, 1.0, 1.0) * ((glint + glint2) * innerMask * 0.85 + armGlow * 0.5)
        + float3(0.9, 1.0, 1.0) * burstFlash * film * 0.8;
    col += light * edgeGuard * keep * uFade;

    return float4(col, alpha) * vColor.a;
}

technique Technique1
{
    pass BubblePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
