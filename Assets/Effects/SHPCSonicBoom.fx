// ============================================================================
// SHPCSonicBoom.fx — 超音速枪管音爆着色器
// 突破音障瞬间的马赫环：沿飞行方向压扁的双层激波环 + 后掠汽化锥 + 径向速度线
// 以四边形渲染，C# 端将四边形旋转到光束飞行方向（+X 为前方）
// 配合 SHPCSonicBoomProj（HypersonicBarrelModule）使用
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float progress;         // 0~1 激波扩张进度
float fadeAlpha;        // 整体透明度 0~1
float3 coreColor;       // 核心白热色
float3 ringColor;       // 激波环主色（金橙）

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;

    // 马赫环各向异性：沿飞行方向（x）压扁，把圆环挤成"音障锥截面"
    float2 squashed = float2(centered.x * 1.9, centered.y);
    float dist = length(squashed);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    // 噪声扰动环边
    float n1 = tex2D(noiseSamp, float2(normAngle * 4.0 + uTime * 1.5, progress * 2.0)).r;
    float disp = (n1 - 0.5) * 0.05;
    float adjDist = dist + disp;

    // ============================================================
    // A. 主激波环 —— 锐利的压缩波前
    // ============================================================
    float thickness = 0.05 + (1.0 - progress) * 0.04;
    float mainRing = 1.0 - smoothstep(0.0, thickness, abs(adjDist - progress));
    // 波前内侧更亮（空气被压缩成白雾）
    float innerBias = smoothstep(progress, progress - thickness * 1.6, adjDist);

    // ============================================================
    // B. 次级环 —— 滞后的稀疏波
    // ============================================================
    float lagPos = progress * 0.7;
    float lagRing = (1.0 - smoothstep(0.0, 0.03, abs(adjDist - lagPos))) * 0.5;
    lagRing *= smoothstep(0.0, 0.25, progress);

    // ============================================================
    // C. 后掠汽化锥 —— 只出现在飞行方向后方的雾锥
    // ============================================================
    float behind = smoothstep(0.1, -0.5, centered.x);       // x<0 为后方
    float coneWidth = abs(centered.y) - (-centered.x) * 0.55;
    float cone = (1.0 - smoothstep(-0.05, 0.12, coneWidth)) * behind;
    float coneNoise = tex2D(noiseSamp, float2(centered.x * 1.2 - uTime * 2.5, centered.y * 2.0)).g;
    cone *= (0.4 + coneNoise * 0.6) * (1.0 - progress) * 0.8;

    // ============================================================
    // D. 径向速度线 —— 破障瞬间向外刺出的细线
    // ============================================================
    float rays = pow(abs(sin(angle * 9.0 + n1 * 2.0)), 32.0);
    float rayZone = smoothstep(progress * 0.5, progress, dist) * (1.0 - smoothstep(progress, progress * 1.5, dist));
    rays *= rayZone * (1.0 - progress) * 0.9;

    // ============================================================
    // E. 中心白闪 —— 突破瞬间
    // ============================================================
    float flash = pow(saturate(1.0 - dist / 0.2), 2.5) * pow(saturate(1.0 - progress / 0.3), 2.0);

    // ============================================================
    // 颜色合成
    // ============================================================
    float3 cWhite = float3(1.0, 0.98, 0.93);
    float3 ringMix = lerp(ringColor, cWhite, innerBias * 0.7);

    float3 color = float3(0.0, 0.0, 0.0);
    color += ringMix * mainRing * (0.8 + innerBias * 0.6);
    color += ringColor * lagRing;
    color += coreColor * cone * 0.55;
    color += cWhite * rays * 0.8;
    color += cWhite * flash;

    float alpha = saturate(mainRing + lagRing * 0.6 + cone * 0.5 + rays * 0.7 + flash);
    alpha *= fadeAlpha;

    return float4(color * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCSonicBoomPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
