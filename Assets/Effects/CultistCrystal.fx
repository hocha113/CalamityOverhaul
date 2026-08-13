// ============================================================================
//CultistCrystal.fx 霜牢晶枪材质
//细长六棱冰晶：纵向晶面带+中脊各向异性高光+棱面glint扫掠+霜白硬缘
//uGrow 凝结成形（噪声侵蚀边界从中段向两端生长）；uFlash 锁定/刺出过曝
//局部UV：x沿枪轴（0尾 1尖），y横截；无极角，全笛卡尔
//Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uGrow;      //0~1 凝结进度
float uFlash;     //0~1 过曝闪
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

float4 CrystalPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //x∈[-1,1] 沿轴（+x=尖端），y∈[-1,1] 横截
    float2 p = (coords - 0.5) * 2.0;

    //---- 六棱剪影：中段等宽，尖端长锥，尾端短锥 ----
    float tipTaper = 1.0 - smoothstep(0.30, 0.96, p.x);   //尖端从0.30起收锥到0.96归零
    float tailTaper = smoothstep(-0.98, -0.55, p.x);      //尾端短锥
    float halfW = 0.34 * tipTaper * tailTaper;
    halfW = max(halfW, 0.0);

    float ay = abs(p.y);
    float edgeAA = 0.045;
    float body = 1.0 - smoothstep(halfW - edgeAA, halfW + edgeAA, ay);

    //---- 凝结成形：从中段向两端生长+噪声撕裂前沿 ----
    float growFront = uGrow * 1.25;
    float axialDist = abs(p.x + 0.15);   //生长中心略偏尾（从手中凝出）
    float condense = vnoise(float2(p.x * 4.0 + uSeed * 9.0, p.y * 3.0) + uSeed);
    float grown = 1.0 - smoothstep(growFront - 0.22, growFront + 0.05, axialDist + condense * 0.30);
    body *= grown;

    //---- 纵向晶面带：三条棱面，中脊亮、侧棱暗 ----
    float bandPos = ay / max(halfW, 0.02);        //0中脊 1边缘
    float ridge = 1.0 - smoothstep(0.0, 0.34, bandPos);          //中脊高光带
    float sideFacet = smoothstep(0.28, 0.42, bandPos) * (1.0 - smoothstep(0.72, 0.88, bandPos));
    //棱面沿轴分段折射（晶体内部平面的亮度量化）
    float cell = hash21(float2(floor(p.x * 5.0 + uSeed * 5.0), floor(bandPos * 2.0)));
    float facetLum = sideFacet * lerp(0.32, 0.62, cell);

    //---- 深层折射暗纹：贴近边缘的内沉色 ----
    float depthShade = smoothstep(0.55, 0.95, bandPos);

    //---- glint 扫掠：亮斑沿枪身周期滑过中脊 ----
    float sweep = cos(p.x * 2.6 - uTime * 5.0 + uSeed * 6.0);
    float glint = pow(saturate(sweep), 30.0) * ridge * 1.4;

    //---- 霜白硬缘：剪影边缘一线寒白 ----
    float rim = smoothstep(halfW - 0.10, halfW - 0.02, ay) * body;

    //---- 尖端聚芒：靠近尖的额外亮度（穿刺方向性） ----
    float tipGlow = smoothstep(0.35, 0.9, p.x) * ridge * 0.8;

    //---- 合成 ----
    float3 col = uColDeep * (0.55 * body + depthShade * 0.4 * body)
               + uColMain * (facetLum + ridge * 0.5) * body
               + uColBright * (rim * 0.85 + glint + tipGlow) * body;

    col += float3(1.0, 1.0, 1.0) * uFlash * body * 0.9;

    float a = saturate(body * (0.55 + ridge * 0.30 + rim * 0.35 + glint * 0.5 + uFlash * 0.4));
    //画布边缘保险
    a *= 1.0 - smoothstep(0.92, 1.0, abs(p.x));
    a *= 1.0 - smoothstep(0.92, 1.0, ay);

    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

technique CrystalTech
{
    pass CrystalPass
    {
        PixelShader = compile ps_3_0 CrystalPS();
    }
}
