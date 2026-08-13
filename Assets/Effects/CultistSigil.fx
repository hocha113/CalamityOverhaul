// ============================================================================
//CultistSigil.fx 拜月教徒程序化法阵
//单位quad中心绘制；三环+符栉+元素辐条+核心，向外展开式描绘
//极角审计：theta 仅进 sin/cos(k*theta) 且 k 恒为整数(12/24/5/N∈{6,8,3})；
//噪声/侵蚀走纯笛卡尔 hash，无极角输入
//Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uProgress;   //0~1 展开进度
float uBreak;      //0~1 碎裂
float uFlash;      //0~1 白闪
float uElement;    //0火 1冰 2雷（精确整数）
float uSpin;       //累计旋转（弧度）
float uAlpha;      //整体透明度
float3 uColDeep;
float3 uColMain;
float3 uColBright;

//笛卡尔逐格hash（无极角，无接缝）
float hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

float ringLine(float r, float R, float w)
{
    return exp(-pow((r - R) / w, 2.0));
}

float4 SigilPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p) + 1e-5;
    float theta = atan2(p.y, p.x);

    //元素辐条数：火6 冰8 雷3（uniform为精确整数，谐波连续）
    float isIce = step(0.5, uElement) * (1.0 - step(1.5, uElement));
    float isTh = step(1.5, uElement);
    float isFire = 1.0 - isIce - isTh;
    float N = 6.0 * isFire + 8.0 * isIce + 3.0 * isTh;

    //分层展开闸门
    float g1 = smoothstep(0.08, 0.30, uProgress);
    float g2 = smoothstep(0.32, 0.55, uProgress);
    float g3 = smoothstep(0.58, 0.80, uProgress);
    float gDetail = smoothstep(0.50, 0.95, uProgress);

    //环随出现从中心扩张
    float R1 = 0.86 * g1;
    float R2 = 0.60 * g2;
    float R3 = 0.30 * g3;

    float ring1 = ringLine(r, R1, 0.016) * g1;
    float ring1Glow = ringLine(r, R1, 0.060) * 0.30 * g1;
    float ring2 = ringLine(r, R2, 0.013) * g2;
    float ring3 = ringLine(r, R3, 0.012) * g3;

    //外环带内的旋转符栉（12/24整数谐波）
    float annulus = smoothstep(R2 + 0.02, R2 + 0.07, r) * (1.0 - smoothstep(R1 - 0.07, R1 - 0.02, r));
    float comb = pow(0.5 + 0.5 * sin(12.0 * theta + uSpin * 2.0), 3.0);
    float comb2 = pow(0.5 + 0.5 * sin(24.0 * theta - uSpin * 3.0), 8.0);
    float glyphs = annulus * (comb * 0.30 + comb2 * 0.45) * gDetail;

    //N辐条：内带放射线
    float spokeBand = smoothstep(R3, R3 + 0.05, r) * (1.0 - smoothstep(R2 - 0.04, R2, r));
    float spokes = pow(saturate(cos(N * theta + uSpin)), 24.0) * spokeBand * 0.9 * gDetail;

    //核心呼吸辉点
    float core = exp(-r * r * 14.0) * (0.34 + 0.22 * sin(uTime * 2.2)) * g3;

    //整体呼吸
    float breathe = 0.92 + 0.08 * sin(uTime * 2.4 + r * 3.0);

    //碎裂：5θ整数谐波裂线变暗 + 逐格侵蚀
    float crackLine = 1.0 - smoothstep(0.0, 0.10, abs(sin(5.0 * theta + 1.7)));
    float crackDark = 1.0 - crackLine * uBreak * 0.9;
    float cell = hash21(floor(p * 6.0 + 31.7));
    float erode = lerp(1.0, step(uBreak * 0.95, cell), step(0.01, uBreak));

    float structural = ring1 + ring2 + ring3 + glyphs + spokes;
    float3 col = uColDeep * (ring1Glow + core * 0.6)
               + uColMain * structural
               + uColBright * (spokes * 0.7 + core);

    //白闪提亮结构
    col += uColBright * uFlash * (structural + core) * 1.4;
    col += float3(1.0, 1.0, 1.0) * uFlash * (ring1 + ring3) * 0.5;

    float a = saturate((structural + ring1Glow + core) * breathe);
    a *= uAlpha * crackDark * erode;

    //画布边缘保险归零
    a *= 1.0 - smoothstep(0.92, 1.0, r);

    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

technique SigilTech
{
    pass SigilPass
    {
        PixelShader = compile ps_3_0 SigilPS();
    }
}
