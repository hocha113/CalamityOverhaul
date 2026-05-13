// ============================================================================
// CosmicCrescent.fx —— 寰宇灾厄长矛的月牙能量冲击波着色器
// SDF 月牙：外圆减去偏移的内圆，所有色彩、扭曲、脉冲全部在像素级生成
// 视觉对齐：深空紫蓝 + 白心 + 品红流光的"宇宙能量"调性
// ============================================================================

float4x4 transformMatrix;
float uTime;          //归一化时间
float fadeAlpha;      //全局透明度 0~1
float growProgress;   //生长进度 0~1.3
float energyPulse;    //当前能量脉冲值 0~1
float seed;           //每实例独立随机种子
float stage;          //连击阶段 0/1/2，越大越亮

float3 coreColor;     //内核白蓝
float3 midColor;      //过渡紫罗兰
float3 edgeColor;     //外缘深空紫
float3 accentColor;   //粉红能量斑

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

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VS(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += a * vnoise(p);
        p *= 2.07;
        a *= 0.5;
    }
    return v;
}

float4 PS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;

    //=========================================================
    //月牙 SDF：外圆 ∩ ¬内圆
    //外圆中心略偏后侧；内圆中心偏前，让"horns"指向 UV.x = 1（飞行前方）
    //=========================================================
    float2 outerC = float2(0.42, 0.5);
    float2 innerC = float2(0.59, 0.5);
    float outerR = 0.40;
    float innerR = 0.36;

    //轻微的呼吸：随脉冲膨胀，让月牙看起来在"喘息"
    float breathe = 1.0 + energyPulse * 0.04;
    outerR *= breathe;
    innerR *= breathe;

    //边缘扰动：基于极坐标的噪声，让外缘看起来像"能量边界"
    //角度参考来自外圆中心
    float2 dpOut = uv - outerC;
    float ang = atan2(dpOut.y, dpOut.x);
    float distortAngle = ang / 6.2832 + 0.5;
    //双层扰动：长波 + 细波
    float distortA = (tex2D(noiseSamp, float2(distortAngle * 1.4 + uTime * 0.5, seed * 0.13)).r - 0.5) * 0.020;
    float distortB = (tex2D(noiseSamp, float2(distortAngle * 5.6 - uTime * 1.1, seed * 0.31 + 0.7)).r - 0.5) * 0.008;
    float edgeDistort = (distortA + distortB) * (0.75 + stage * 0.25);

    float dOut = length(dpOut) - (outerR + edgeDistort);
    float dIn  = (innerR - edgeDistort * 0.6) - length(uv - innerC);

    //crescentSdf < 0：在月牙内
    float crescentSdf = max(dOut, dIn);
    //柔边：用一段平滑过渡而非硬切
    float edgeWidth = 0.012 + (1.0 - growProgress) * 0.030;
    float mask = smoothstep(edgeWidth, -edgeWidth, crescentSdf);
    if (mask <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    //=========================================================
    //内部深度场：到最近边界的距离，用于色彩分层
    //=========================================================
    float depthFromOuter = outerR - length(dpOut);                 //离外缘的距离（正在内部）
    float depthFromInner = length(uv - innerC) - innerR;           //离内缘的距离（正在月牙内）
    float thickness = min(depthFromOuter, depthFromInner);
    //归一化到 0~1：从月牙边缘（0）到中心脊（1）
    float maxThick = outerR - innerR + 0.04;
    float thicknessN = saturate(thickness / maxThick);

    //=========================================================
    //中央能量脊：沿月牙厚度最厚的那条带，宽度由能量脉冲驱动
    //=========================================================
    float spineCore = pow(thicknessN, 0.55);
    float spineSharp = pow(thicknessN, 2.6);
    //能量脉冲让脊线时强时弱
    float spineGlow = spineCore * (0.55 + energyPulse * 0.55 + stage * 0.1);

    //=========================================================
    //极坐标噪声流：月牙内部"能量沸腾"质感
    //角度沿外圆参考，半径沿外缘距离
    //=========================================================
    float radialT = saturate(length(dpOut) / outerR);
    float2 polarUV = float2(distortAngle * 2.7, radialT * 3.2);
    float swirl1 = fbm(polarUV + float2(uTime * 1.4, -uTime * 0.7) + seed);
    float swirl2 = fbm(polarUV * 1.7 + float2(-uTime * 0.6, uTime * 1.2) + seed * 0.3);
    float plasma = saturate(swirl1 * 0.65 + swirl2 * 0.45);
    plasma = pow(plasma, 1.4);
    //仅在月牙内部显现
    plasma *= smoothstep(0.0, 0.2, thicknessN);

    //=========================================================
    //流动的能量带：在 horns 之间从背缘流向尖端
    //tFlow 沿外圆角度推进，给观众"能量在月牙里旋转"的错觉
    //=========================================================
    float flowPhase = frac(distortAngle - uTime * 0.45 + seed * 0.21);
    float flowBand = smoothstep(0.0, 0.07, flowPhase) * smoothstep(0.35, 0.07, flowPhase);
    flowBand *= smoothstep(0.0, 0.15, thicknessN);
    float flowBand2 = smoothstep(0.0, 0.06, frac(flowPhase + 0.5)) * smoothstep(0.28, 0.06, frac(flowPhase + 0.5));
    flowBand2 *= smoothstep(0.0, 0.18, thicknessN);
    float flow = max(flowBand, flowBand2 * 0.7);

    //=========================================================
    //月牙背缘 (convex outer) 高光：沿外缘 0.08 内的细带
    //=========================================================
    float outerRimDist = -dOut; //月牙内为正
    float outerRim = smoothstep(0.020, 0.0, outerRimDist) * (1.0 - smoothstep(0.0, 0.014, outerRimDist));
    outerRim *= 1.0 + energyPulse * 0.4;

    //=========================================================
    //月牙内缘 (concave inner) 描边：象征"刀刃"的锋利感
    //=========================================================
    float innerRimDist = -dIn;
    float innerRim = smoothstep(0.020, 0.0, innerRimDist) * (1.0 - smoothstep(0.0, 0.014, innerRimDist));
    innerRim *= 1.0 + stage * 0.25;

    //=========================================================
    //horns 高亮：月牙两端（thickness 接近 0 且远离背缘）
    //利用"距外圆中心的角度 ang"识别两侧 horn
    //=========================================================
    float hornAngle = abs(sin(ang));
    //hornAngle 在 horns 附近 (ang ≈ ±π*0.3) 是中等值；
    //但其实 horns 在月牙最尖锐的两端，对应于 thicknessN 接近 0 而 dOut 接近 0 的位置
    float hornMask = smoothstep(0.35, 0.0, thicknessN) * smoothstep(0.085, 0.0, abs(dOut))
                   * smoothstep(0.18, 0.45, hornAngle);
    hornMask = pow(saturate(hornMask), 1.3);

    //=========================================================
    //背景散粒：闪烁的小亮点（模拟"星屑"）
    //=========================================================
    float sparkPhase = floor(uTime * 6.0);
    float sparkBase = hash21(floor(uv * 36.0) + sparkPhase + seed * 7.3);
    float sparkOn = step(0.965, sparkBase) * smoothstep(0.0, 0.25, thicknessN);
    sparkOn *= 0.6 + energyPulse * 0.4;

    //=========================================================
    //径向脉冲波：每隔一段时间从中心向 horns 推出的"圈"
    //=========================================================
    float pulsePhase = frac(uTime * 0.7 + seed * 0.11);
    float pulseRadius = pulsePhase * 0.55;
    float pulseDist = abs(length(dpOut) - (outerR - 0.04 - pulseRadius));
    float pulseWave = smoothstep(0.025, 0.0, pulseDist) * (1.0 - pulsePhase);
    pulseWave *= smoothstep(0.0, 0.15, thicknessN);

    //=========================================================
    //颜色合成
    //=========================================================
    //厚度区间：edge → mid → core，做平滑三段渐变
    float3 baseTint = lerp(edgeColor, midColor, smoothstep(0.0, 0.55, thicknessN));
    baseTint = lerp(baseTint, coreColor, smoothstep(0.55, 1.0, thicknessN));

    float3 color = float3(0, 0, 0);
    //底色：基础色 * 厚度
    color += baseTint * (0.45 + thicknessN * 0.7);
    //plasma 沸腾：让月牙内部不死板
    color += lerp(midColor, accentColor, plasma * 0.7) * (plasma * 0.45);
    //中央脊：饱和到核心色
    color += coreColor * spineSharp * (1.4 + stage * 0.3);
    color += lerp(midColor, coreColor, spineGlow) * spineGlow * 0.55;
    //流动能量带：accent 色为主
    color += lerp(accentColor, coreColor, 0.4) * flow * (1.1 + stage * 0.25);
    //外缘高光：从 mid 过渡到 core
    color += lerp(midColor, coreColor, energyPulse) * outerRim * 1.1;
    //内缘描边：偏冷的 accent（淡品红）
    color += lerp(accentColor, coreColor, 0.5) * innerRim * 1.05;
    //horns：超饱和高光
    color += coreColor * hornMask * 1.5;
    color += accentColor * hornMask * 0.45;
    //径向脉冲波：白心带
    color += coreColor * pulseWave * 1.3;
    //闪烁星屑
    color += lerp(coreColor, accentColor, hash21(uv + sparkPhase)) * sparkOn * 1.4;

    //=========================================================
    //alpha 合成：以厚度为主体，再叠加各种"发光"贡献
    //=========================================================
    float alpha = mask * (
          0.40 + thicknessN * 0.45
        + spineGlow * 0.55
        + flow * 0.50
        + outerRim * 0.45
        + innerRim * 0.45
        + hornMask * 0.65
        + pulseWave * 0.55
        + sparkOn * 0.55
        + plasma * 0.30
    );
    alpha = saturate(alpha);
    alpha *= fadeAlpha;

    //加色混合下预乘 alpha：颜色乘 alpha 再返回，避免暗色被加成造成"灰雾"
    return float4(color * alpha, alpha);
}

technique CosmicCrescentTech
{
    pass CosmicCrescentPass
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}
