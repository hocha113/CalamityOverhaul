// ============================================================================
//OniEye.fx 鬼眼：开域/收域主演出 + 翻转负片帧的日月化眼彩蛋
//墨笔眼睑 + 暗血巩膜 + 血红虹膜 + 竖瞳 + 三勾玉旋转环 + 噪声消散
//quad UV 0~1 全域，TechEyeBase=AlphaBlend 本体，TechEyeGlow=Additive 红光/爆闪
//勾玉用旋转矩阵摆放局部 SDF，无 atan2，无极坐标 seam；噪声全笛卡尔
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;
float uIntensity;   //0~1 整体可见度
float uOpen;        //0闭~1全开
float uSpin;        //勾玉环累计旋转（弧度）
float uFlash;       //0~1 虹膜爆闪
float uDissolve;    //0~1 消散进度

static const float3 INK_LID = float3(0.050, 0.020, 0.055);
static const float3 SCLERA_DARK = float3(0.085, 0.020, 0.028);
static const float3 IRIS_CORE = float3(0.880, 0.130, 0.100);
static const float3 IRIS_MID = float3(0.520, 0.045, 0.050);
static const float3 IRIS_RIM = float3(0.220, 0.020, 0.030);
static const float3 PUPIL_BLACK = float3(0.012, 0.004, 0.010);
static const float3 TOMOE_INK = float3(0.030, 0.008, 0.018);
static const float3 GLOW_RED = float3(0.950, 0.180, 0.100);
static const float3 FLASH_HOT = float3(1.000, 0.520, 0.400);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//眼睑口径与墨笔描线共用的几何
struct EyeFrame {
    float aperture;   //眼内区域 0~1
    float stroke;     //眼睑墨线 0~1
    float ex;         //归一横坐标
};

EyeFrame eyeFrame(float2 p, float openAmt) {
    EyeFrame o;
    float ex = p.x / 0.92;
    o.ex = ex;
    float env = saturate(1.0 - ex * ex);
    float openEff = max(openAmt, 0.02);
    float lidUp = openEff * 0.52 * pow(env, 0.72);
    float lidLo = openEff * 0.40 * pow(env, 0.85);

    float aa = 0.020;
    float inside = smoothstep(-aa, aa, lidUp - p.y) * smoothstep(-aa, aa, p.y + lidLo);
    inside *= step(abs(ex), 1.0);
    o.aperture = inside;

    //墨笔描线：上下眼睑曲线，笔宽随噪声起伏出飞白
    float wUp = 0.040 * (0.70 + 0.55 * noiseTex(float2(p.x * 2.7 + 3.1, 0.31)));
    float wLo = 0.032 * (0.70 + 0.55 * noiseTex(float2(p.x * 2.9 + 8.7, 0.77)));
    float sUp = exp(-pow((p.y - lidUp) / wUp, 2.0));
    float sLo = exp(-pow((p.y + lidLo) / wLo, 2.0));
    //上睑笔锋越出眼角略长并上挑，下睑收得更早
    float exAbs = abs(ex);
    float reachUp = 1.0 - smoothstep(1.04, 1.18, exAbs);
    float reachLo = 1.0 - smoothstep(0.98, 1.06, exAbs);
    //眼尾上挑：越出眼角的部分上睑线整体抬升
    float tailLift = smoothstep(0.85, 1.15, exAbs) * 0.06;
    float sTail = exp(-pow((p.y - lidUp - tailLift) / wUp, 2.0)) * step(1.0, exAbs);
    //二重线：上睑上方一道更细的淡墨
    float dLine = exp(-pow((p.y - lidUp - 0.085 * openEff - 0.02) / 0.016, 2.0))
                * smoothstep(0.25, 0.75, openEff) * env * 0.55;

    float stroke = max(sUp * reachUp, sLo * 0.9 * reachLo);
    stroke = max(stroke, sTail * reachUp);
    stroke = max(stroke, dLine);
    o.stroke = saturate(stroke);
    return o;
}

//三勾玉环覆盖度，局部旋转坐标内三珠渐细
float tomoeRing(float2 pc, float spin) {
    float cov = 0.0;
    [unroll]
    for (int i = 0; i < 3; i++) {
        float ang = spin + (float)i * 2.09439510;
        float ca = cos(ang);
        float sa = sin(ang);
        float2 c = float2(ca, sa) * 0.215;
        float2 q = pc - c;
        //局部系：x=径向 y=切向
        float2 qr = float2(q.x * ca + q.y * sa, -q.x * sa + q.y * ca);
        float d0 = length(qr) - 0.058;
        float d1 = length(qr - float2(-0.013, 0.064)) - 0.037;
        float d2 = length(qr - float2(-0.032, 0.114)) - 0.020;
        float dt = min(d0, min(d1, d2));
        cov = max(cov, 1.0 - smoothstep(0.0, 0.022, dt));
    }
    return cov;
}

//消散侵蚀：返回保留度，几何采样点同步微膨胀由调用方处理
float dissolveKeep(float2 p, float dis) {
    float n = noiseTex(p * 2.6 + 7.7);
    float th = dis * 1.15 - 0.05;
    return smoothstep(th, th + 0.25, n);
}

float4 PSEyeBase(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = (coords - 0.5) * 2.0;
    //消散时整体微涨散
    p /= 1.0 + uDissolve * 0.22;

    EyeFrame f = eyeFrame(p, uOpen);
    if (f.aperture <= 0.002 && f.stroke <= 0.002) {
        return float4(0, 0, 0, 0);
    }

    float2 pc = p - float2(0.0, 0.01);
    float rd = length(pc);

    //巩膜：暗血底，噪声脏斑
    float sn = noiseTex(p * 3.3 + 1.9);
    float3 col = SCLERA_DARK * (0.72 + 0.28 * sn);

    //虹膜三段渐变 + 笛卡尔噪斑
    float irisMask = 1.0 - smoothstep(0.325, 0.350, rd);
    float t = saturate(rd / 0.34);
    float3 iris = lerp(IRIS_CORE, IRIS_MID, smoothstep(0.15, 0.75, t));
    iris = lerp(iris, IRIS_RIM, smoothstep(0.78, 1.0, t));
    float mottle = noiseTex(p * 4.2 + uTime * 0.03);
    iris *= 0.86 + mottle * 0.26;
    //放射状虹膜纤维：单位方向向量喂噪声，笛卡尔连续无 seam
    float2 dirN = pc / max(rd, 1e-4);
    float fiber = noiseTex(dirN * 1.25 + 7.3) * 0.65 + noiseTex(dirN * 2.6 - 4.1) * 0.35;
    iris *= 0.84 + fiber * 0.32 * smoothstep(0.25, 0.85, t);
    //虹膜外缘限制环（墨线勾边）
    iris = lerp(iris, TOMOE_INK, smoothstep(0.90, 1.0, t) * 0.55);
    //勾玉环轨道微亮
    iris += IRIS_CORE * 0.18 * exp(-pow((rd - 0.215) / 0.05, 2.0));
    col = lerp(col, iris, irisMask);

    //三勾玉
    float tomoe = tomoeRing(pc, uSpin) * irisMask;
    col = lerp(col, TOMOE_INK, tomoe);

    //竖瞳，爆闪时瞳孔扩张
    float2 pe = pc / float2(0.052 + uFlash * 0.030, 0.235);
    float slit = 1.0 - smoothstep(0.80, 1.15, dot(pe, pe));
    col = lerp(col, PUPIL_BLACK, slit);

    //爆闪漂白
    col = lerp(col, FLASH_HOT, uFlash * 0.55 * irisMask);

    float a = f.aperture;
    //眼睑墨线覆盖在最上
    col = lerp(col, INK_LID, f.stroke);
    a = saturate(a + f.stroke);

    float keep = dissolveKeep(p, uDissolve);
    a *= keep * uIntensity * vertexColor.a;
    col *= vertexColor.rgb;
    return float4(col * a, a);
}

float4 PSEyeGlow(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = (coords - 0.5) * 2.0;
    p /= 1.0 + uDissolve * 0.22;

    EyeFrame f = eyeFrame(p, uOpen);
    float2 pc = p - float2(0.0, 0.01);
    float rd = length(pc);

    //虹膜红光，只在睁眼后泄出
    float openGate = smoothstep(0.15, 0.7, uOpen);
    float3 col = GLOW_RED * exp(-pow(rd / 0.42, 2.0)) * 0.50 * openGate * f.aperture;

    //湿润高光点：偏左上小亮斑，让眼睛活过来
    float2 dg = pc - float2(-0.085, -0.115);
    float glint = exp(-dot(dg, dg) * 480.0);
    col += float3(1.0, 0.82, 0.72) * glint * 0.85 * openGate * f.aperture;

    //眼睑缝隙红渗
    col += GLOW_RED * f.stroke * 0.22 * openGate;

    //眼角血泪两点
    float2 dc1 = p - float2(0.83, -0.02);
    float2 dc2 = p - float2(-0.83, -0.02);
    float tears = exp(-dot(dc1, dc1) * 90.0) + exp(-dot(dc2, dc2) * 90.0);
    col += GLOW_RED * tears * 0.30 * openGate;

    //爆闪：整眼白热
    col += FLASH_HOT * uFlash * (exp(-pow(rd / 0.55, 2.0)) * 1.6 + f.aperture * 0.5);

    float keep = dissolveKeep(p, uDissolve);
    col *= keep * uIntensity * vertexColor.a;
    col *= vertexColor.rgb;
    return float4(col, 0);
}

technique TechEyeBase {
    pass P0 {
        PixelShader = compile ps_3_0 PSEyeBase();
    }
}

technique TechEyeGlow {
    pass P0 {
        PixelShader = compile ps_3_0 PSEyeGlow();
    }
}
