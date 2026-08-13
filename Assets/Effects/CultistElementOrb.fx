// ============================================================================
//CultistElementOrb.fx 三元素球体材质
//火=沸腾撕舌 冰=晶面冷芯 雷=等离子丝环；uElement 精确整数按权重乘混合（无分支）
//极角审计：theta 仅进 sin(4θ)/cos(3θ)（整数）；噪声全走刚体旋转笛卡尔
//Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uElement;   //0火 1冰 2雷
float uCharge;    //0~1 充能（尺寸/亮度）
float uFlash;     //0~1 白闪
float uSeed;      //个体相位
float3 uColDeep;
float3 uColMain;
float3 uColBright;

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

//单层值噪声（笛卡尔）
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

//刚体旋转（无缝的"旋转场"手段）
float2 rot(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 OrbPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p) + 1e-5;
    float theta = atan2(p.y, p.x);

    float isIce = step(0.5, uElement) * (1.0 - step(1.5, uElement));
    float isTh = step(1.5, uElement);
    float isFire = 1.0 - isIce - isTh;

    float size = 0.30 + 0.24 * uCharge;

    //---- 火：沸腾轮廓 + 撕裂舌 ----
    float boil = vnoise(rot(p, uTime * 0.9 + uSeed) * 3.2 + uTime * 0.55);
    float fireEdge = size * (1.0 + (boil - 0.5) * 0.55);
    float fireBody = 1.0 - smoothstep(fireEdge * 0.55, fireEdge, r);
    float fireTongue = (1.0 - smoothstep(fireEdge, fireEdge * 1.5, r))
                     * pow(boil, 3.0) * 1.2;
    float fireCore = exp(-r * r / (size * size * 0.24));
    float fireLum = fireBody * 0.55 + fireTongue * 0.6 + fireCore * 0.8;

    //---- 冰：锐利边缘 + 晶面格 + 十字闪芒 ----
    float iceBody = 1.0 - smoothstep(size * 0.88, size, r);
    float facet = hash21(floor(rot(p, uSeed * 2.3) * 5.0));
    float facetLum = lerp(0.45, 1.0, facet) * iceBody;
    float glint = pow(saturate(sin(4.0 * theta + uTime * 1.6 + uSeed)), 24.0)
                * (1.0 - smoothstep(size * 0.6, size * 1.35, r)) * 0.9;
    float iceCore = exp(-r * r / (size * size * 0.30)) * 0.6;
    float iceLum = facetLum * 0.62 + glint + iceCore;

    //---- 雷：等离子核 + 抖动丝环 + 3θ弧段 ----
    float jitter = hash21(float2(floor(uTime * 14.0) + uSeed, floor(uTime * 9.0)));
    float filamentR = size * (0.86 + 0.22 * (jitter - 0.5));
    float filament = exp(-pow((r - filamentR) / (size * 0.07), 2.0))
                   * (0.5 + 0.5 * saturate(sin(3.0 * theta + uTime * 6.0 + uSeed * 7.0)));
    float thCore = exp(-r * r / (size * size * 0.20)) * (0.9 + 0.3 * jitter);
    float thHalo = exp(-r * r / (size * size * 1.4)) * 0.25;
    float thLum = filament * 1.1 + thCore + thHalo;

    //---- 权重乘混合 ----
    float lum = fireLum * isFire + iceLum * isIce + thLum * isTh;
    float coreMask = fireCore * isFire + iceCore * isIce + thCore * isTh;

    float3 col = uColDeep * lum * 0.5
               + uColMain * lum * 0.7
               + uColBright * coreMask * 0.9;

    col += float3(1.0, 1.0, 1.0) * uFlash * lum * 0.8;

    float a = saturate(lum * (0.65 + 0.35 * uCharge));
    //画布边缘保险
    a *= 1.0 - smoothstep(0.90, 1.0, r);

    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

technique OrbTech
{
    pass OrbPass
    {
        PixelShader = compile ps_3_0 OrbPS();
    }
}
