// ============================================================================
//SignalTowerElectrified.fx 信号塔过电滤镜
//采样 uImage0 作遮罩；Additive 叠加
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float electrifyProgress;  //过电 0~1，0刚劈 1恢复
float seed;               //实例随机种子
float2 texelSize;         //1/宽 1/高
float intensity;          //整体强度

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(uImage0, coords);
    //仅在原图不透明像素上叠加电弧，避免光溢出矩形边界
    if (src.a < 0.02) return float4(0, 0, 0, 0);

    float progress = saturate(electrifyProgress);
    //整体强度随进度先爆发后衰减：0→1用短坡上冲，随后用长坡退出
    float env = 1.0 - progress;
    env *= smoothstep(0.0, 0.08, progress) + 0.2;
    env = saturate(env);

    //---- 流动电弧 ----
    //沿贴图竖向跑 3 条横向波动的电弧线，时间向下推进
    float arcSum = 0.0;
    for (int i = 0; i < 3; i++) {
        float fi = float(i);
        //每条弧的相位，seed+index 产生差异
        float phase = uTime * (0.8 + fi * 0.25) + seed * 2.7 + fi * 1.73;
        float yCenter = frac(phase);
        //用noise让弧有上下抖动
        float offset = (valueNoise(float2(coords.x * 8.0 + fi * 3.1, phase * 5.0)) - 0.5) * 0.08;
        float dy = abs(coords.y - yCenter - offset);
        float arc = 1.0 - smoothstep(0.0, 0.018, dy);
        //水平方向加一点毛刺，避免完全笔直
        float jitter = hash21(float2(floor(coords.x * 180.0), floor(phase * 60.0)));
        arc *= 0.55 + 0.45 * jitter;
        arcSum += pow(saturate(arc), 1.4);
    }

    //---- 高频电火花噪声 ----
    float sparkTime = floor(uTime * 28.0 + seed * 9.0);
    float spark = hash21(coords * 240.0 + sparkTime);
    spark = spark > 0.92 ? (spark - 0.92) / 0.08 : 0.0;

    //---- 垂直扫描线：刚被劈时明显 ----
    float scan = sin((coords.y + uTime * 1.5) * 40.0 + seed * 3.0) * 0.5 + 0.5;
    scan = pow(scan, 6.0) * (1.0 - progress);

    //---- 颜色合成：冷白核心 + 淡红halo（和闪电一脉相承）----
    float arcLight = arcSum * env;
    float3 arcColor = float3(1.7, 1.25, 1.0) * arcLight;
    float3 sparkColor = float3(1.5, 0.45, 0.35) * spark * env * 1.2;
    float3 scanColor = float3(0.9, 0.15, 0.12) * scan * 0.55;

    //整体轻微的红色过电辉光，全身像素都有
    float3 ambientGlow = float3(0.55, 0.07, 0.05) * env * 0.35;

    float3 col = arcColor + sparkColor + scanColor + ambientGlow;
    col *= intensity;

    //用原图alpha裁掉矩形外溢，同时轻微收缩掉极暗边
    float alphaMask = smoothstep(0.05, 0.35, src.a);
    float alpha = saturate(max(max(col.r, col.g), col.b)) * alphaMask;
    return float4(col * alphaMask, alpha);
}

technique Tech
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
