// ============================================================================
// EverdeepRing.fx 永渊水环(圆形水流弹体)
// 材质:深渊高压水环。签名行为:
//   1) 环内水体绕环循环——刚体旋转坐标采样噪声 + 整数倍角流辉,无极角接缝
//   2) 迎水面顶泡沫弓,背水面被撕成离体飞沫(速度各向异性)
//   3) 深水体近实心遮挡,内缘一线深渊生物光脉络,环心包一层深水薄膜
// quad 约定:C# 把 quad 旋到 +x=运动方向,scale.x 携带速度拉伸;
// 可见环带外缘 r≈0.73,飞沫止于 r≈0.89,护栏 0.90 起只作采样保险,
// C# 折算:可见外半径px ≈ quad半宽 × 0.73
// 预乘输出 + AlphaBlend(深水体要能压暗背景);噪声绑 s1
// 极角审计:theta 仅以 sin(3θ)/sin(4θ) 消费,噪声全走刚体旋转笛卡尔坐标
// ============================================================================

float uTime;
float uSeed;
float uSpin;        // C# 积分的循环角(随飞行速度增长),内部再叠慢漂底速
float uFade;        // 0~1 总包络(出生/消亡)
float uCharge;      // 0~1 折返充能:脉络与泡沫增亮,主体透青
float3 uDeepColor;  // 深渊水体
float3 uGlowColor;  // 生物光青辉
float3 uFoamColor;  // 泡沫苍白

// 噪声固定在 s1:SpriteBatch.Draw 会把 s0 覆写成画布贴图;
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

// PerlinNoise 实测值域 ≈0.23..0.78,阈值前归一
float nrm(float n) { return saturate((n - 0.23) / 0.55); }

float2 Rot(float2 p, float a)
{
    float s, c;
    sincos(a, s, c);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;          // -1..1,+x=运动方向
    float r = length(p);
    float2 dir = p / max(r, 1e-4);
    float spin = uSpin + uTime * 0.9;      // 静止时也有底速循环
    float theta = atan2(p.y, p.x);

    // ---- 环几何:连续环芯 + 湍流外层双层结构 ----
    // 环芯厚度恒定不吃噪声,保证水环任何时刻读作一个完整的环;
    // 湍流外层厚度随噪声呼吸,负责有机轮廓,撕裂也只咬这一层
    float thBase = 0.150;
    float d = abs(r - 0.54);
    float bandCore = smoothstep(thBase * 0.80, thBase * 0.24, d);
    float edgeN = nrm(tex2D(noiseSamp, Rot(p, spin * 0.6) * 0.85 + uSeed).g);
    float th = thBase * (0.80 + edgeN * 0.45);
    float bandTex = smoothstep(th, th * 0.30, d);

    // ---- 双层反向循环流 + 四臂流辉:主流顺旋、细流逆旋成剪切湍流 ----
    float flowA = nrm(tex2D(noiseSamp, Rot(p, spin) * 0.62 + uSeed * 3.7).g);
    float flowB = nrm(tex2D(noiseSamp, Rot(p, -spin * 0.55) * 1.45 + uSeed * 8.1).b);
    float arms = 0.5 + 0.5 * sin(theta * 4.0 - spin * 2.6 + uSeed * 6.2831);
    float field = flowA * 0.66 + flowB * 0.40 * (1.0 - flowA * 0.55) + arms * 0.30 + 0.06;

    // ---- 速度各向异性:迎水面/背水面(dir.x=cosθ,跨缝连续) ----
    float head = smoothstep(0.05, 0.95, dir.x);
    float tail = smoothstep(-0.05, -0.95, dir.x);
    // 背侧撕裂只作用于湍流层;环芯尾侧只轻收,环体始终连续
    float tearN = nrm(tex2D(noiseSamp, Rot(p, spin * 0.8) * 2.2
        + float2(uTime * 0.45, uSeed * 5.0)).r);
    bandTex *= 1.0 - tail * (1.0 - smoothstep(0.24, 0.58, tearN)) * 0.9;
    float band = max(bandCore * (1.0 - tail * 0.16), bandTex);

    // ---- 管状受光:截面中脊亮、内外缘沉;窄反射弧只挂左上受光位 ----
    float tube = 1.0 - pow(saturate(d / max(th, 1e-3)), 2.0) * 0.42;
    float spec = smoothstep(0.035, 0.0, abs(r - (0.54 + thBase * 0.22)))
        * pow(saturate(dot(dir, float2(-0.472, -0.882))), 7.0);

    // ---- 外缘泡沫弓 + 尾侧离体飞沫:弓贴稳定外缘成连续月牙,不打碎 ----
    float outerE = r - (0.54 + thBase * 0.55);
    float foamN = nrm(tex2D(noiseSamp, Rot(p, spin * 1.2) * 3.1 + uSeed * 1.7).g);
    float foam = smoothstep(0.085 * (1.0 + head * 0.7), 0.0, abs(outerE))
        * smoothstep(0.34, 0.76, foamN) * (0.55 + head * 1.0 + uCharge * 0.35);
    float fleckZone = smoothstep(0.0, 0.06, outerE) * smoothstep(0.35, 0.10, outerE);
    float fleckN = nrm(tex2D(noiseSamp, Rot(p, spin * 0.9) * 4.2
        + float2(uTime * 0.6, uSeed * 11.0)).b);
    float flecks = fleckZone * smoothstep(0.76, 0.90, fleckN) * (0.25 + tail * 0.75);

    // ---- 内缘生物光脉络:锚在稳定内缘,起伏收浅保持连贯 ----
    float veinD = abs(r - (0.54 - thBase * 0.62));
    float vein = smoothstep(0.046, 0.0, veinD) * (0.40 + band * 0.60)
        * (0.62 + 0.38 * sin(theta * 3.0 + spin * 1.7 + uSeed * 9.0))
        * (0.80 + 0.30 * sin(uTime * 3.2 + uSeed * 7.0) + uCharge * 1.1);

    // ---- 环心深水薄膜:环里包着一窗微光深渊 ----
    float holeR = 0.54 - th;
    float film = 1.0 - smoothstep(holeR * 0.55, holeR, r);
    float filmN = nrm(tex2D(noiseSamp, Rot(p, spin * 0.25) * 1.1 + uSeed * 2.3).r);
    float filmA = film * (0.13 + filmN * 0.09);

    // ---- 预乘合成:深水体承遮挡,泡沫/脉络/反射作光层 ----
    float bodyA = band * (0.74 + field * 0.26) * tube;
    float3 bodyCol = uDeepColor * (0.64 + field * 0.72) * tube;
    bodyCol = lerp(bodyCol, uGlowColor * 0.85, saturate(field - 0.55) * (0.42 + uCharge * 0.35));

    float a = saturate(bodyA + filmA * (1.0 - bodyA));
    float3 rgb = bodyCol * bodyA + uDeepColor * 0.72 * filmA * (1.0 - bodyA);
    rgb += uFoamColor * (foam * 1.0 + flecks * 0.82)
        + uGlowColor * vein * (0.85 + uCharge * 1.0)
        + uFoamColor * spec * 0.62;
    a = saturate(a + foam * 0.55 + flecks * 0.48 + vein * 0.22);

    // 采样保险护栏:内容天然止于 r≈0.89
    float guard = smoothstep(0.985, 0.90, r);
    return float4(rgb, a) * (uFade * guard) * vColor.a;
}

technique Technique1
{
    pass RingPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
