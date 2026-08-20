// ============================================================================
//CultistRuneSigil.fx 仪式符印
//双环 + 24θ符文刻带 + 3θ辐条 + 弧形描绘进度(uProgress) + 定形迸发(uCommit)
//+ 仪式充能扇区(uFill)；直线算术 + 普通tex2D（FNA3D法则），整数谐波
//调用方合同：Immediate+Additive quad，s1 显式绑 PerlinNoise，quad 内容半径按 0.82 折算
// ============================================================================

sampler uImage0 : register(s0);   //占位画布（白图），不参与采样逻辑
sampler2D noiseTex : register(s1);

float uTime;
float uAlpha;      //整体透明度 0~1
float3 uTint;      //元素主色
float uProgress;   //0~1 印记按弧序描绘完成度
float uCommit;     //0~1 定形迸发：增亮+白化
float uFill;       //0~1 充能扇区（仪式进度表用，攻击印记传0）

float4 SigilPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p) + 1e-5;

    //护栏：quad 边缘归零（不做早退分支，避免动态流后 tex2D 的未定义行为）
    float guard = smoothstep(0.92, 0.84, r);

    float theta = atan2(p.y, p.x);
    //弧序参数：从正上方顺时针 0~1
    float a = frac((theta + 1.5707963) / 6.2831853);

    //描绘前沿：已描绘区渐显 + 前沿亮点（径向限制在环带内，防止贯穿中心的光针）
    float vis = saturate((uProgress - a) * 14.0 + step(0.9995, uProgress));
    float sparkBand = smoothstep(0.42, 0.55, r) * smoothstep(0.90, 0.84, r);
    float spark = exp(-abs(a - uProgress) * 36.0) * step(uProgress, 0.999) * step(0.001, uProgress) * sparkBand;

    //双环
    float ringO = exp(-pow((r - 0.84) / 0.035, 2.0));
    float ringI = exp(-pow((r - 0.52) / 0.030, 2.0));

    //符文刻带：24 扇区块状符点，噪声决定各扇区图样，整带缓转
    float bandMask = smoothstep(0.56, 0.60, r) * smoothstep(0.80, 0.76, r);
    float sector = floor(frac(a + uTime * 0.008) * 24.0);
    float rowQ = floor(r * 9.0) / 9.0;
    float n = tex2D(noiseTex, float2(sector * 0.09, rowQ * 0.61) + uTime * 0.004).r;
    float rune = step(0.56, n) * bandMask;

    //3θ辐条：细亮，反向慢转（整数谐波）
    float spoke = pow(abs(0.5 + 0.5 * sin(3.0 * theta - uTime * 0.5)), 24.0)
        * smoothstep(0.50, 0.55, r) * smoothstep(0.86, 0.80, r);

    //充能扇区：环带间的进度弧，前沿略亮
    float fillArc = step(a, uFill) * bandMask * 0.5;
    float fillEdge = exp(-abs(a - uFill) * 30.0) * bandMask * step(0.01, uFill) * step(uFill, 0.99);

    //中央核：定形时增压
    float core = exp(-r * 4.5) * (0.22 + uCommit * 0.85) * saturate(uProgress * 2.0 - 0.3);

    //汇总：描绘进度门控环带与符文，核不受弧序门控
    float lum = (ringO * 0.95 + ringI * 0.7 + rune * 0.55 + spoke * 0.65 + fillArc + fillEdge * 0.8) * vis
        + spark * 1.2 + core;

    //噪声呼吸：整体轻微明暗游移，防止数学圆的死板
    float breath = 0.88 + 0.12 * tex2D(noiseTex, p * 0.30 + uTime * 0.012).r;
    lum *= breath;

    //定形迸发：整体增亮并向白推
    float3 col = uTint * lum * (1.0 + uCommit * 0.9);
    col += float3(1.0, 1.0, 1.0) * lum * uCommit * 0.45;

    //XNA Additive 的源因子是 SourceAlpha：亮度折进 alpha（合同同 ShockRing.fx）
    float a2 = saturate(lum) * guard * uAlpha;
    return float4(col * guard * uAlpha, a2);
}

technique SigilTech
{
    pass SigilPass
    {
        PixelShader = compile ps_3_0 SigilPS();
    }
}
