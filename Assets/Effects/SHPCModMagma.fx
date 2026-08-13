// ============================================================================
// SHPCModMagma.fx 熔岩喷口改件 熔融液体三态
// TechJet:  喷涌熔浆柱——游走中轴+粘稠蠕动轮廓+Plateau-Rayleigh颈缩断液滴串+
//           黑壳浮板挂侧缘亮芯居中+根部白热核
// TechGlob: 空中熔岩团——SDF摆动轮廓+表面张力亮缘+尾部颈缩+黑壳浮板漂亮体
// TechPool: 贴地熔岩池——液面慢波+端部弯月挂边+沸泡+面下窄反射带+
//           黑壳结皮渐干+边缘蚀退
// 全部笛卡尔/半径场取样，无极角无动态分支无tex2Dlod；无采样器(纯ALU噪声)；
// 预乘输出，AlphaBlend 批
// ps_3_0
// ============================================================================

float uTime;
float uSeed;
float uRise;       //Jet 喷发强度0~1(脉冲窗)
float uEnv;        //总强度(出生/塌熄包络)
float uLife;       //Pool 0新鲜炽亮→1结皮干涸
float uAspect;     //Jet 高宽比 / Pool 宽高比
float uStretch;    //Glob 速度拉伸 0.75~2.0
float uGlow;       //脉冲加亮0~1
float3 uColorCrust;   //黑壳(暗红黑)
float3 uColorLava;    //熔浆体(橙红)
float3 uColorHot;     //炽芯(黄白)

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
// 熔浆柱：uv.y=1 底端(锚喷口)，uv.y=0 顶端
// 粘稠液体：噪声滚动比尘柱/血柱慢，轮廓幅度小；颈缩深谷天然把顶段切成液滴串
// ----------------------------------------------------------------------------
float4 JetPS(float2 uv : TEXCOORD0) : COLOR0
{
    float xc = (uv.x - 0.5) * 2.0;
    float along = 1.0 - uv.y;           //0根部→1顶端

    //画布护栏：左右与顶部必归零(底边贴口允许实切)
    float guard = smoothstep(1.0, 0.80, abs(xc)) * smoothstep(1.0, 0.86, along);

    //纵向按真实画布比例取样，高柱下噪声不被拉成直线
    float yTex = uv.y * uAspect * 0.5;

    //游走中轴：厚液慢摆，根部钉死喷口
    float lean = (valueNoise(float2(along * 1.4 - uTime * 0.6, uSeed * 29.0)) - 0.5) * 0.55;
    float c = xc - lean * smoothstep(0.04, 0.85, along);

    //轮廓蠕动：低频慢滚(粘稠，不是气体撕裂)
    float edgeN = (fbm2(float2(c * 2.2 + uSeed * 17.0, yTex * 2.0 - uTime * 1.3)) - 0.5) * 0.30;

    //柱形：根部裙摆外扩→上行收窄
    float width = lerp(0.62, 0.28, pow(saturate(along), 0.75));
    width += smoothstep(0.12, 0.0, along) * 0.22;

    //Plateau-Rayleigh颈缩：上半程起波，喷发越猛越深，深谷断成液滴串
    float neckWave = 0.5 + 0.5 * sin(along * 18.0 - uTime * 4.6 + uSeed * 6.0);
    float neck = 1.0 - (0.30 + 0.44 * uRise) * smoothstep(0.45, 1.0, along) * neckWave;

    float halfW = width * neck;
    float rr = abs(c) + edgeN;
    float body = smoothstep(halfW + 0.02, halfW - 0.26, rr);

    //顶端撕裂收头，任何时刻无平顶
    float capTear = (valueNoise(float2(c * 2.6 + uSeed * 7.0, uTime * 1.1)) - 0.5) * 0.20;
    float capLine = 0.94 + capTear;
    body *= smoothstep(capLine + 0.02, capLine - 0.20, along);

    //黑壳浮板：低频暗斑被流场缓慢上带，只挂侧缘，芯部保持炽亮
    float plateN = fbm2(float2(c * 3.0 + uSeed * 41.0, yTex * 2.6 - uTime * 0.8));
    float sideBand = smoothstep(0.18, 0.60, abs(c) / max(halfW, 0.05));
    float crust = smoothstep(0.56, 0.70, plateN) * sideBand;
    //板缝橙裂：紧贴壳板边缘
    float crack = smoothstep(0.10, 0.02, abs(plateN - 0.56)) * sideBand;

    //根部白热核：口部源头，与地面/喷口衔接
    float rootCore = exp(-c * c * 7.0) * smoothstep(0.42, 0.0, along);

    //辐射梯度：内芯亮外缘暗
    float coreness = 1.0 - saturate(rr / max(halfW, 0.05));

    float3 col = lerp(uColorLava, uColorHot, saturate(coreness * 0.62 + rootCore * 0.55));
    col = lerp(col, uColorCrust, crust * 0.85);
    col += uColorHot * crack * 0.85;
    col += uColorHot * rootCore * (0.45 + uGlow * 0.55);

    float alpha = saturate(body * (0.95 - crust * 0.10) + rootCore * 0.5) * guard * uEnv;
    return float4(col * alpha, alpha);
}

// ----------------------------------------------------------------------------
// 熔岩团：quad长轴沿速度(局部+x向前，-x尾部)
// ----------------------------------------------------------------------------
float4 GlobPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;
    float r = length(p);

    //轮廓摆动：厚液慢蠕(比酸液慢、幅度小)
    float wobble = (valueNoise(p * 2.0 + float2(uTime * 0.55, -uTime * 0.42) + uSeed * 17.0) - 0.5) * 0.20;
    //尾部颈缩：速度拉伸大时尾侧起波断成液滴
    float tailNeck = 0.5 + 0.5 * sin(p.x * 6.0 + uTime * 3.0 + uSeed * 9.0);
    wobble += tailNeck * smoothstep(0.2, 1.0, -p.x) * saturate(uStretch - 0.7) * 0.14;

    float edge = r + wobble;
    //主体与半径护栏：摆动幅度下也不触及quad边
    float body = smoothstep(0.97, 0.58, edge) * smoothstep(1.0, 0.90, r);

    //表面张力亮缘
    float rim = exp(-pow((edge - 0.70) * 7.0, 2.0));

    //黑壳浮板：暗斑漂在亮体上，缓慢滑移
    float plateN = fbm2(p * 3.4 + uSeed * 31.0 + float2(0.0, uTime * 0.3));
    float crust = smoothstep(0.58, 0.72, plateN);
    float crack = smoothstep(0.09, 0.02, abs(plateN - 0.58));

    //内芯炽亮
    float coreness = 1.0 - saturate(edge / 0.8);

    float3 col = lerp(uColorLava, uColorHot, coreness * 0.75);
    col = lerp(col, uColorCrust, crust * 0.80);
    col += uColorHot * crack * 0.80;
    col += uColorHot * rim * 0.55;

    float alpha = saturate(body * 0.95 + rim * 0.20 * body) * uEnv;
    return float4(col * alpha, alpha);
}

// ----------------------------------------------------------------------------
// 熔岩池：quad上缘上方为空，surfY为液面；uLife驱动结皮渐干+边缘蚀退
// ----------------------------------------------------------------------------
float4 PoolPS(float2 uv : TEXCOORD0) : COLOR0
{
    float x = uv.x;
    float y = uv.y;
    float ax = x * uAspect;

    //液面高度起伏：厚液慢波
    float surfY = 0.30 + (valueNoise(float2(ax * 1.2 + uTime * 0.35, uSeed * 23.0)) - 0.5) * 0.07;

    //端部弯月收窄(表面张力挂边)
    float endBite = smoothstep(0.0, 0.16, x) * smoothstep(1.0, 0.84, x);

    //液体区域：液面以下，底部渐隐入地
    float below = smoothstep(surfY - 0.02, surfY + 0.09, y);
    float bottomFade = smoothstep(1.0, 0.55, y);
    float bodyMask = below * bottomFade * endBite;

    //黑壳结皮：液面暗板缓漂，越干壳越多
    float plateN = fbm2(float2(ax * 2.6 + uTime * 0.10 + uSeed * 41.0, y * 2.0));
    float crustThr = 0.62 - uLife * 0.34;
    float crust = smoothstep(crustThr, crustThr + 0.12, plateN);
    float crack = smoothstep(0.10, 0.02, abs(plateN - crustThr));

    //沸泡：上浮亮点，新鲜时密(热度门)
    float bub = smoothstep(0.78, 0.92, valueNoise(float2(ax * 3.0 + uSeed * 7.0, y * 2.2 - uTime * 0.9)));

    //液面亮线+面下各向异性窄反射带(被噪声打断成不匀的段)
    float surfLine = exp(-pow((y - surfY) * 22.0, 2.0));
    float sheenBand = exp(-pow((y - surfY - 0.10) * 9.0, 2.0));
    float sheen = sheenBand * (0.35 + 0.65 * valueNoise(float2(ax * 2.4 - uTime * 0.5, uSeed * 3.0)));

    float heat = 1.0 - uLife;

    float3 col = lerp(uColorLava, uColorHot, 0.20 + uGlow * 0.28);
    col = lerp(col, uColorCrust, crust * (0.55 + uLife * 0.40));
    col += uColorHot * crack * heat * 0.85;
    col += uColorHot * sheen * 0.30 * heat;
    col += uColorHot * bub * 0.60 * heat;
    //弯月边缘更暗更饱和(挂边)
    col *= lerp(1.0, 0.62, 1.0 - endBite);

    float alpha = saturate(bodyMask * 0.92 + surfLine * endBite * 0.28);
    col += uColorHot * surfLine * endBite * (0.55 + uGlow * 0.45) * heat;

    //干透后自边缘蚀退(留下黑壳斑驳)
    float dryNoise = fbm2(float2(ax * 2.2, y * 1.7) + uSeed * 13.0);
    float erode = smoothstep(uLife * 1.1 - 0.10, uLife * 1.1 + 0.10, 0.62 + (dryNoise - 0.5) * 0.80);
    alpha *= erode;

    alpha *= uEnv;
    return float4(col * alpha, alpha);
}

technique TechJet
{
    pass P0
    {
        PixelShader = compile ps_3_0 JetPS();
    }
}

technique TechGlob
{
    pass P0
    {
        PixelShader = compile ps_3_0 GlobPS();
    }
}

technique TechPool
{
    pass P0
    {
        PixelShader = compile ps_3_0 PoolPS();
    }
}
