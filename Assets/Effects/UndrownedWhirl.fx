// ============================================================================
// UndrownedWhirl.fx 锚涡（P3 大招的水面漩涡盘，C# 压扁成透视椭圆贴水面）
// 归一 quad：中心 (0.5,0.5)，r=1 为画布缘
// 签名行为：三臂向心螺旋泡沫（差速旋转） / 中心吸落的暗喉 /
// 锚轨亮环（r=uTrackR，可见环=判定环，gap 视觉同一性） /
// 外域放射拉力纹（沿半径向心滑动） / 全域随涡速提亮
// 极角审计：theta 唯一消费是 sin(3*theta - ...)（3∈ℤ 跨 ±π 连续），
// 泡沫/拉力纹全走刚体旋转笛卡尔坐标与径向距离
// s1=PerlinNoise（值域 0.22~0.776，阈值过 nrm）
// 直线算术无分支；预乘输出进 AlphaBlend 批
// ============================================================================

float uTime;
float uIntensity;   // 0~1 总包络（起涡/收涡由消费端喂）
float uSpin;        // 累计旋转相位（弧度，C# 按涡速积分）
float uTrackR;      // 锚轨半径（归一 0~1）
float uSeed;
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

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float2 c = (uv - 0.5) * 2.0;
    float r = length(c);
    float theta = atan2(c.y, c.x);

    // ------- 基础水面：向心越深越暗（漩涡是一只往下看的喉咙）-------
    float rim = smoothstep(1.0, 0.86, r);                  // 外缘羽化
    float3 col = lerp(uSeaColor, uDeepColor, smoothstep(0.85, 0.10, r));
    float throat = smoothstep(0.30, 0.04, r);              // 中心暗喉
    col = lerp(col, uDeepColor * 0.35, throat);

    // ------- 三臂螺旋泡沫：sin(3θ - logR·k - spin)，整数倍角零接缝 -------
    float logR = log(r * 5.0 + 1.0);
    float arms = sin(3.0 * theta - logR * 7.5 - uSpin * 3.0);
    float armBand = smoothstep(0.35, 0.9, arms) * smoothstep(0.06, 0.22, r) * smoothstep(1.0, 0.55, r);
    // 臂上泡沫肌理：刚体旋转坐标的噪声撕碎臂带
    float foamN = tex2D(noiseSamp, rot(c, -uSpin * 0.8) * 1.6 + uSeed).g;
    float armFoam = armBand * smoothstep(0.30, 0.72, nrm(foamN));
    col += uFoamColor * armFoam * 0.75;

    // ------- 锚轨亮环：可见环=判定环 -------
    float track = smoothstep(0.045, 0.0, abs(r - uTrackR));
    float trackN = tex2D(noiseSamp, rot(c, -uSpin) * 2.2 + uSeed * 2.0).r;
    col += uFoamColor * track * (0.45 + 0.55 * nrm(trackN));

    // ------- 外域放射拉力纹：沿射线取样，径向坐标随时间向心滑动 -------
    float2 dirRot = rot(c / max(r, 1e-4), uSpin * 0.35);
    float pullN = tex2D(noiseSamp, dirRot * 0.9 + (r * 2.4 + uTime * 0.9) * 0.33 + uSeed).b;
    float pull = smoothstep(0.55, 0.85, nrm(pullN)) * smoothstep(0.35, 0.75, r) * smoothstep(1.0, 0.8, r);
    col += uSeaColor * pull * 0.5;

    // ------- 水面碎闪：高分位噪声点，随涡速提密 -------
    float glintN = tex2D(noiseSamp, rot(c, -uSpin * 1.6) * 3.4 + uSeed * 5.0).g;
    float glint = smoothstep(0.78, 0.92, nrm(glintN)) * smoothstep(0.12, 0.4, r) * smoothstep(0.95, 0.6, r);
    col += uFoamColor * glint * 0.55;

    // ------- 合成（预乘）：喉部更实，外缘让水面透出来 -------
    float density = (0.30 + throat * 0.45 + armFoam * 0.55 + track * 0.5 + pull * 0.25 + glint * 0.4)
        * rim * smoothstep(0.0, 0.06, r + 0.06);
    float alpha = saturate(density * uIntensity);

    return float4(col * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass WhirlPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
