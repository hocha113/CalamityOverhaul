// ============================================================================
// EowGeyser.fx 世界吞噬者蚀土喷发
// TechColumn: 间歇泉尘柱，底宽顶窄、噪声撕裂边、内部上卷翻滚、酸光碎屑
// TechOmen:   破土预兆盘，向心收缩环(纯半径场)+笛卡尔裂纹透光+中心穹光
// 全部笛卡尔/半径场取样，无极角无分支；预乘输出，AlphaBlend 批
// ps_3_0
// ============================================================================

float uTime;
float uSeed;
float uProgress;   //Column 上升包络0~1 / Omen 充能0~1 / Guide 充能0~1
float uFade;       //Column 消散0~1
float uAspect;     //Column/Guide 长宽比
float uLock;       //Guide 锁向加亮0~1
float3 uDirtColor;
float3 uAcidColor;

//哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//值噪声
float valueNoise(float2 p)
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

//双倍频噪声
float fbm2(float2 p)
{
    float v = valueNoise(p) * 0.62;
    v += valueNoise(p * 2.13 + 17.7) * 0.38;
    return v;
}

// ----------------------------------------------------------------------------
// 尘柱：uv.y=0 顶端, uv.y=1 底端(锚地表)
// 轮廓全部由噪声撕裂+裙摆包络给出；左右/顶三边护栏归零杜绝quad裁切直线，
// 底边即地表，由根部喷发核与C#侧碎土衔接
// ----------------------------------------------------------------------------
float4 ColumnPS(float2 uv : TEXCOORD0) : COLOR0
{
    float xc = (uv.x - 0.5) * 2.0;
    float up = 1.0 - uv.y;              //0底→1顶
    float live = 1.0 - uFade;

    //画布护栏：左右与顶部必归零(底边贴地允许实切)
    float guard = smoothstep(1.0, 0.84, abs(xc)) * smoothstep(1.0, 0.80, up);

    //纵向按真实画布比例取样，高柱下噪声不被拉成直线
    float yTex = uv.y * uAspect * 0.5;

    //轮廓撕裂：双倍频上卷噪声
    float edgeN = (fbm2(float2(xc * 2.7 + uSeed * 29.0, yTex * 2.4 - uTime * 2.9)) - 0.5) * 0.46;

    //柱形：根部裙摆外扩→中段收腰→顶部收窄
    float width = lerp(0.52, 0.26, pow(saturate(up), 0.8));
    width += smoothstep(0.15, 0.0, up) * 0.20;

    float rEdge = abs(xc) + edgeN;
    //实体柱芯
    float core = smoothstep(width + 0.03, width - 0.24, rEdge);
    //外圈尘雾羽化：独立噪声相位，柔化余量
    float fringeN = fbm2(float2(xc * 3.4 - uSeed * 19.0, yTex * 2.0 - uTime * 2.2) + 31.7);
    float fringe = smoothstep(width + 0.30, width - 0.04, rEdge) * (1.0 - core) * fringeN * 0.55;

    //上升头部：噪声撕裂收头，配合顶部护栏渐隐，任何时刻无平顶
    float capTear = (valueNoise(float2(xc * 3.1 + uSeed * 7.0, uTime * 1.8)) - 0.5) * 0.24;
    float capLine = uProgress * 0.98 + capTear;
    float head = smoothstep(capLine + 0.02, capLine - 0.36, up);
    float headRoll = exp(-pow((up - capLine) * 5.0, 2.0)) * core;

    //内部高对比上卷湍流
    float roll = fbm2(float2(xc * 2.1 + uSeed * 13.0, yTex * 1.8 - uTime * 3.8));
    roll = pow(roll, 1.6);

    //酸光碎屑：稀疏高阈值亮点
    float fleck = smoothstep(0.85, 0.95, valueNoise(float2(xc * 5.0 + uSeed * 5.0, yTex * 3.2 - uTime * 4.8)));

    //根部喷发核：地表源头亮芯，与地面衔接
    float rootCore = exp(-xc * xc * 8.0) * smoothstep(0.32, 0.0, up) * uProgress;

    float density = (core * (0.55 + roll * 0.75) + fringe) * head * guard * live;
    density *= lerp(0.8, 1.25, uv.y);   //近地更实

    float3 hue = uDirtColor * (0.8 + roll * 0.55);
    hue = lerp(hue, uDirtColor * 1.6, headRoll * head * 0.55);
    hue += uAcidColor * fleck * 0.9;
    hue = lerp(hue, lerp(uDirtColor, uAcidColor, 0.6) * 1.4, saturate(rootCore * 0.8));

    float alpha = saturate(density + rootCore * 0.6 * guard * live);
    //预乘输出，匹配 AlphaBlend 批(修复旧版未预乘导致的泛白光柱)
    return float4(hue * alpha, alpha);
}

// ----------------------------------------------------------------------------
// 预兆盘：压扁椭圆quad，中心即破土点
// ----------------------------------------------------------------------------
float4 OmenPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;
    float r = length(p);
    float charge = saturate(uProgress);

    //向心收缩环：纯半径场，天然无缝
    float rings = 0.5 + 0.5 * sin(r * 15.0 - uTime * 8.5);
    rings = pow(rings, 3.0) * smoothstep(1.0, 0.2, r) * smoothstep(0.03, 0.2, r);

    //地裂纹：笛卡尔噪声等值细线
    float crackField = fbm2(p * 3.2 + uSeed * 11.0);
    float crack = smoothstep(0.1, 0.02, abs(crackField - 0.5)) * smoothstep(1.0, 0.35, r);

    //中心穹光：充能鼓包(³曲线后段猛涨)
    float dome = exp(-r * r * 3.4) * charge * charge;

    //末段收拢：环随充能加速加密
    float lateBoost = 1.0 + charge * 1.6;

    float3 col = uAcidColor * (rings * 0.4 * charge * lateBoost + dome * 0.9);
    col += uDirtColor * crack * 0.4 * charge;
    col += uAcidColor * crack * dome * 1.3;

    float alpha = saturate(rings * 0.32 * charge * lateBoost + crack * 0.3 * charge + dome * 0.75);
    alpha *= smoothstep(1.0, 0.72, r);

    return float4(col * alpha, alpha);
}

// ----------------------------------------------------------------------------
// 冲刺导引线：uv.x=0 源头(口部)→1 远端；quad沿冲刺方向旋转
// 有机撕裂窄带+沿线流动酸屑；两端渐隐、横向护栏归零，无任何平切边
// ----------------------------------------------------------------------------
float4 GuidePS(float2 uv : TEXCOORD0) : COLOR0
{
    float yc = (uv.y - 0.5) * 2.0;
    float charge = saturate(uProgress);

    //纵向包络：源头快速渐显，远端长渐隐
    float lenv = smoothstep(0.0, 0.07, uv.x) * smoothstep(1.0, 0.62, uv.x);

    //横向护栏：quad上下边必归零
    float guard = smoothstep(1.0, 0.78, abs(yc));

    //按真实长宽比取样：撕裂噪声沿长轴滚向远端
    float xTex = uv.x * uAspect * 0.5;
    float wob = (fbm2(float2(xTex * 1.7 - uTime * 2.3, uSeed * 23.0 + yc * 0.8)) - 0.5) * 0.5;

    //芯线随充能收窄绷直(蓄势越满承诺越明确)
    float width = lerp(0.55, 0.30, charge);
    float rr = abs(yc) + wob * (1.0 - charge * 0.4);
    float core = smoothstep(width, width * 0.25, rr);
    float haze = smoothstep(width + 0.42, width * 0.4, rr) * 0.35;

    //流向脉冲：酸屑向远端流动，速度随充能提升
    float flow = 0.5 + 0.5 * sin((uv.x * uAspect * 1.4 - uTime * (3.0 + charge * 4.0)) * 6.2832);
    flow = pow(flow, 3.0);

    float body = (core * (0.5 + flow * 0.6) + haze) * lenv * guard;
    float alpha = body * (0.16 + 0.84 * charge * charge);
    //锁向闪：芯线满亮(末3帧全静=最亮承诺)
    alpha = saturate(alpha + core * lenv * guard * uLock * 0.5);

    float3 hue = lerp(uDirtColor * 1.1, uAcidColor, 0.72 + uLock * 0.28);
    hue += uAcidColor * flow * core * 0.5;

    //预乘输出，AlphaBlend 批
    return float4(hue * alpha, alpha);
}

technique TechColumn
{
    pass P0
    {
        PixelShader = compile ps_3_0 ColumnPS();
    }
}

technique TechGuide
{
    pass P0
    {
        PixelShader = compile ps_3_0 GuidePS();
    }
}

technique TechOmen
{
    pass P0
    {
        PixelShader = compile ps_3_0 OmenPS();
    }
}
