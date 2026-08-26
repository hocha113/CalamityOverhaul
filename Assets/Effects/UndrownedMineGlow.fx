// ============================================================================
// UndrownedMineGlow.fx 狱水沉雷幽光（水下鬼火般的雷体光场 + 读秒收缩环）
// 归一 quad：中心 (0.5,0.5)，r=1 画布缘；画在雷体贴图之下/之上各一层由消费端定
// 签名行为：幽光不是裸径向渐变——焦散细胞肌理啃出斑驳光壳 /
// 读秒收缩环（引信烧掉多少环就收多少，环触雷体=爆） /
// 呼吸频率随充能爬升 / 末段高频警闪
// 反塑料：光体由焦散细胞结构承载；充能色由沼靛滑向锈橙（警告色语义）
// s1=PerlinNoise（值域 0.22~0.776，阈值过 nrm）
// 直线算术无分支；预乘输出进 AlphaBlend 批
// ============================================================================

float uTime;
float uCharge;      // 0→1 引信进度
float uSeed;
float3 uGlowColor;  // 常态幽光（沼靛）
float3 uWarnColor;  // 警告色（锈橙）
float3 uFoamColor;  // 白闪

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

    // ------- 幽光壳：径向衰减 × 焦散细胞（慢旋噪声阈值），不是裸光球 -------
    float causticN = tex2D(noiseSamp, rot(c, uTime * 0.22) * 1.35 + uSeed).g;
    float caustic2 = tex2D(noiseSamp, rot(c, -uTime * 0.14) * 2.6 + uSeed * 2.0).r;
    float cells = smoothstep(0.30, 0.70, nrm(causticN * 0.6 + caustic2 * 0.4));
    float halo = smoothstep(0.95, 0.15, r) * (0.35 + cells * 0.65);

    // 呼吸：频率随充能从 0.9Hz 爬到 3.5Hz
    float breath = 0.75 + 0.25 * sin(uTime * (5.5 + uCharge * 16.0) + uSeed * 7.0);
    halo *= breath;

    // ------- 读秒收缩环：r 从 0.9 收到 0.22，带噪声毛边 -------
    float ringR = lerp(0.90, 0.22, uCharge);
    float ringN = tex2D(noiseSamp, rot(c, uTime * 0.5) * 1.8 + uSeed * 3.0).b;
    float ring = smoothstep(0.05, 0.0, abs(r - ringR + (ringN - 0.5) * 0.06));

    // ------- 末段警闪：charge>0.82 后逐帧硬闪 -------
    float flickN = tex2D(noiseSamp, float2(uTime * 3.7, uSeed)).r;
    float flick = step(0.5, frac(uTime * 9.0 + flickN)) * smoothstep(0.82, 0.95, uCharge);

    // ------- 上色：沼靛→锈橙随充能，环与警闪推白 -------
    // 衰减只写进 alpha 一次；col 保持满亮度色（col 再乘 halo = 包络平方，
    // 幽光整壳压灭成黑——2026-08 沙盒毙过一版）
    float3 baseCol = lerp(uGlowColor, uWarnColor, uCharge * uCharge);
    float3 col = baseCol * (1.05 + cells * 0.55);
    col = lerp(col, lerp(baseCol, uFoamColor, 0.55), saturate(ring));
    col += uWarnColor * flick * 0.5 * smoothstep(0.7, 0.2, r);

    // ------- 合成（预乘，光层低 alpha：加光为主、轻微遮挡）-------
    float density = halo * 0.50 + ring * 0.60 + flick * 0.3;
    float alpha = saturate(density * smoothstep(1.0, 0.9, r));

    return float4(col * alpha, alpha * 0.55) * vColor.a;
}

technique Technique1
{
    pass MineGlowPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
