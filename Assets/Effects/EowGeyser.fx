// ============================================================================
// EowGeyser.fx 世界吞噬者蚀土喷发
// TechColumn: 间歇泉尘柱——底宽顶窄、噪声撕裂边、内部上卷翻滚、酸光碎屑
// TechOmen:   破土预兆盘——向心收缩环(纯半径场)+笛卡尔裂纹透光+中心穹光
// 全部笛卡尔/半径场取样，无极角无分支；预乘输出，AlphaBlend 批
// ps_3_0
// ============================================================================

float uTime;
float uSeed;
float uProgress;   //Column 上升包络0~1 / Omen 充能0~1
float uFade;       //Column 消散0~1
float uAspect;     //Column 高宽比
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
// ----------------------------------------------------------------------------
float4 ColumnPS(float2 uv : TEXCOORD0) : COLOR0
{
    float xc = (uv.x - 0.5) * 2.0;
    float up = 1.0 - uv.y;              //0底→1顶

    //柱形：底宽顶窄
    float width = lerp(1.0, 0.4, up);

    //撕裂边：上卷噪声扰动轮廓
    float edgeN = (valueNoise(float2(xc * 1.9 + uSeed * 29.0, uv.y * 3.2 - uTime * 2.8)) - 0.5) * 0.55;
    float column = smoothstep(width + 0.05, width - 0.3, abs(xc) + edgeN);

    //上升头部包络：柱头随 uProgress 爬升，头部翻卷更浓
    float capLine = uProgress * 1.12;
    float head = smoothstep(capLine, capLine - 0.32, up);
    float headRoll = exp(-pow((up - capLine) * 5.5, 2.0));

    //内部翻滚密度
    float roll = fbm2(float2(xc * 2.3 + uSeed * 13.0, uv.y * 2.6 - uTime * 3.4));

    //酸光碎屑：稀疏高阈值亮点，升速更快
    float fleck = smoothstep(0.82, 0.94, valueNoise(float2(xc * 4.6 + uSeed * 5.0, uv.y * 5.2 - uTime * 4.8)));

    float density = column * head * (0.42 + roll * 0.8);
    //底部加浓(近地更实)
    density *= lerp(0.7, 1.35, uv.y);
    density *= 1.0 - uFade;

    float3 col = uDirtColor * density;
    //头部翻卷提亮(受光尘缘)
    col += uDirtColor * 1.3 * headRoll * column * 0.45 * (1.0 - uFade);
    //酸光
    col += uAcidColor * fleck * column * head * 0.55 * (1.0 - uFade);

    float alpha = saturate(density * 0.95 + headRoll * column * 0.2 * (1.0 - uFade));
    return float4(col, alpha);
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

technique TechColumn
{
    pass P0
    {
        PixelShader = compile ps_3_0 ColumnPS();
    }
}

technique TechOmen
{
    pass P0
    {
        PixelShader = compile ps_3_0 OmenPS();
    }
}
