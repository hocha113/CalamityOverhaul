// ============================================================================
//KikasaSunkEffigy.fx 鬼伞湖底沉影：被沉溺生物在画中的呈现（湖底记忆/岸上伞奴共用）。
//贴图只当形状模板，alpha 作轮廓，内部一律填湖水材质，不吐任何贴图细节。
//uSubmerge 在干湖泥痕与水下沉影间过渡；水下折射把轮廓切成缓慢错位的横向水层
//（行量化错位+细浮动，不是整体扭曲）；焦散亮带周期扫过，扫到的行折射暂时找平，
//轮廓短暂清晰，扫过之后重新化开。
//uTamed：可驱使=形凝得住+缘线血沫+一点余烬沿缘缓移；未驯服=轮廓被水啃散、缘线断续。
//uAbsent：鬼奴在外=实体退成负形（比周围更暗的空缺+一圈淡缘），不是消失。
//色板与 KikasaScene.fx 血湖/鬼雨双形态常量同源；预乘输出。
//s0=生物贴图（uUvRect 帧区域钳制防帧表渗色）s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例相位
float uSubmerge;    //0=干湖泥痕 1=水下沉影
float uDepth;       //0~1 距水面深度：折射幅度与向深水色沉
float uTamed;       //0~1 可驱使度
float uAbsent;      //0~1 鬼奴在外（负形）
float uRain;        //0~1 血湖⇄鬼雨浸染
float uStir;        //0~1 水面活性
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸（纹理 uv 空间）
float2 uTexel;      //一像素的 uv 尺寸，轮廓检测用
float uAspect;      //帧宽/帧高，噪声采样防拉伸

//====== 与 KikasaScene.fx 同源的双形态水色 ======
static const float3 WATER_HI_B = float3(0.150, 0.028, 0.038);
static const float3 WATER_LO_B = float3(0.046, 0.008, 0.013);
static const float3 TINT_B     = float3(0.930, 0.300, 0.270);
static const float3 FOAM_B     = float3(0.965, 0.520, 0.440);
static const float3 BED_B      = float3(0.108, 0.052, 0.040);
static const float3 EMBER_B    = float3(0.950, 0.340, 0.140);
static const float3 WATER_HI_R = float3(0.055, 0.072, 0.082);
static const float3 WATER_LO_R = float3(0.020, 0.027, 0.034);
static const float3 TINT_R     = float3(0.300, 0.345, 0.355);
static const float3 FOAM_R     = float3(0.620, 0.670, 0.680);
static const float3 BED_R      = float3(0.060, 0.072, 0.078);
static const float3 EMBER_R    = float3(0.620, 0.670, 0.680);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//绑定噪声实测值域约 0.227~0.776，阈值判断先归一
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

//采样贴图 alpha，钳在帧区域内防串帧
float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

float4 PSSunk(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    //帧内归一坐标；噪声采样按等比坐标防拉伸
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float2 nuv = float2(luv.x * uAspect, luv.y);

    //====== 双形态色板 ======
    float3 waterHi = lerp(WATER_HI_B, WATER_HI_R, uRain);
    float3 waterLo = lerp(WATER_LO_B, WATER_LO_R, uRain);
    float3 tint = lerp(TINT_B, TINT_R, uRain);
    float3 foam = lerp(FOAM_B, FOAM_R, uRain);
    float3 bed = lerp(BED_B, BED_R, uRain);
    float3 ember = lerp(EMBER_B, EMBER_R, uRain);

    //====== 焦散扫带：周期自上而下掠过，带内折射找平、轮廓短暂清晰 ======
    float sweep = frac(uTime * 0.055 + uSeed * 0.71);
    float caus = exp2(-abs(luv.y - sweep) * 16.0)
        * (0.55 + 0.45 * noiseTex(float2(nuv.x * 2.6 + uTime * 0.05, sweep + uSeed)))
        * uSubmerge;

    //====== 分层折射：行量化错位 + 细浮动，切成缓慢错位的横向水层 ======
    float row = floor(luv.y * 9.0);
    float rowShift = noiseTex(float2(row * 0.173 + uSeed, uTime * 0.045)) - 0.5;
    float wob = noiseTex(float2(luv.y * 3.1 + uSeed * 2.3, uTime * 0.08)) - 0.5;
    float refrAmp = (0.035 + 0.050 * uDepth + 0.025 * uStir) * uSubmerge
        * (1.0 - caus * 0.85);
    float shift = (rowShift * 0.8 + wob * 0.35) * refrAmp;
    float2 suv = uv + float2(shift * uUvRect.z, 0.0);

    float srcA = frameAlpha(suv);

    //====== 侵蚀：未驯服被水啃散；在外时形要留得住，蚀势收半 ======
    float er = nrm(noiseTex(nuv * 1.45 + uSeed * 0.83 + float2(0.0, uTime * 0.012)));
    float thr = lerp(0.42, 0.04, uTamed) * (1.0 - uAbsent * 0.55);
    float keep = smoothstep(thr, thr + 0.10, er);
    float eatRim = exp2(-abs(er - thr - 0.05) * 18.0) * saturate(thr * 5.0);

    //====== 轮廓缘线：4-tap 边检，跟着折射后的采样走 ======
    float aL = frameAlpha(suv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(suv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(suv - float2(0.0, uTexel.y));
    float aD = frameAlpha(suv + float2(0.0, uTexel.y));
    float rimShape = saturate((srcA - min(min(aL, aR), min(aU, aD))) * 2.4);
    //未驯服缘线断续，驯服后连成整线
    float rimKeep = lerp(smoothstep(0.38, 0.55,
        noiseTex(float2(luv.y * 2.6 + uSeed * 3.1, luv.x * 1.7 + uTime * 0.03))),
        1.0, uTamed);

    //====== 身体材质 ======
    float n0 = noiseTex(nuv * 0.9 + float2(uSeed, uTime * 0.10));
    float n1 = noiseTex(nuv * 2.1 + float2(-uTime * 0.04, uTime * 0.22) + uSeed * 1.7);
    //水下沉影：深浅水色垫底 + 血红流层，行错位带出水层明暗
    float3 wet = lerp(waterHi, waterLo, luv.y) * (0.9 + rowShift * 0.5)
        + tint * (0.10 + n0 * 0.22 + n1 * 0.12);
    wet = lerp(wet, waterLo, uDepth * 0.45);
    //干湖泥痕：比湖床略深的湿渍
    float3 dry = bed * (0.42 + 0.28 * n0);
    float3 body = lerp(dry, wet, uSubmerge);
    float bodyA = lerp(0.55, 0.82 - 0.22 * uDepth, uSubmerge);

    //在外=负形：形还在，填成比周围更暗的空缺
    body = lerp(body, waterLo * 0.40, uAbsent);
    bodyA = lerp(bodyA, 0.45, uAbsent);

    //====== 可驱使的活痕：一点余烬沿轮廓缘缓移 ======
    float emberPhase = frac(uTime * 0.09 + uSeed * 0.53);
    float emberGlow = rimShape * exp2(-abs(luv.y - emberPhase) * 24.0)
        * uTamed * (1.0 - uAbsent) * uSubmerge;

    //====== 合成（预乘：本体乘 alpha，亮件走加色项） ======
    float bodyGate = srcA * keep * vc.a;
    float aOut = bodyGate * bodyA;
    float presentGate = bodyGate * (1.0 - uAbsent * 0.75);
    float3 rimCol = lerp(tint * 0.7, foam, uSubmerge);
    float3 glow = rimCol * rimShape * rimKeep * (0.20 + 0.18 * uTamed)
            * lerp(1.0, 0.4, uAbsent) * bodyGate
        + foam * caus * 0.32 * presentGate
        + ember * emberGlow * 0.85 * bodyGate
        + tint * eatRim * 0.45 * presentGate;
    return float4(body * vc.rgb * aOut + glow, aOut);
}

technique TechSunk {
    pass P0 {
        PixelShader = compile ps_3_0 PSSunk();
    }
}
