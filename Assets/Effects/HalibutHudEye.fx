// ============================================================================
//HalibutHudEye.fx 比目鱼HUD核心：深渊之眼
//一只完全程序生成的海渊生物之眼：
//流体虹膜纤维 + 竖窄瞳孔(注视/扩张) + 眼睑眨动 + 波动外环刻度 +
//冷却薄翳扫掠 + 死机红化故障抖动 + 复苏躁动充血 + 就绪闪光
//所有状态由CPU侧每帧喂入，着色器只负责合成
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float2 uPupilOffset;   //瞳孔注视偏移（像素）
float uDilate;         //瞳孔扩张 0-1
float uBlink;          //睁眼程度 0闭-1开
float uLayers;         //领域层数 0-1（驱动虹膜色相与外环活性）
float uCrash;          //死机程度 0-1（红化+故障）
float uCooldown;       //技能剩余冷却 0-1（薄翳扫掠）
float uAgitation;      //复苏躁动 0-1（充血+瞳孔不安）
float uReadyFlash;     //冷却结束闪光 0-1

#define PI 3.14159265
#define TAU 6.28318530

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.07 + float2(3.1, 7.4);
        a *= 0.5;
    }
    return v;
}

static const float3 COL_VOID    = float3(0.004, 0.012, 0.022);
static const float3 COL_DEEP    = float3(0.012, 0.038, 0.060);
static const float3 COL_MID     = float3(0.030, 0.110, 0.150);
static const float3 COL_GLOW    = float3(0.300, 0.780, 0.980);
static const float3 COL_CAUSTIC = float3(0.620, 0.940, 1.000);
static const float3 COL_VIOLET  = float3(0.480, 0.360, 1.000);
static const float3 COL_DANGER  = float3(1.000, 0.300, 0.300);
static const float3 COL_LID     = float3(0.020, 0.070, 0.095);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 res = uResolution;
    float2 p = (coords - 0.5) * res;          //像素坐标，原点居中
    float t = uTime;

    //死机故障：按横向条带随机抖动采样坐标
    if (uCrash > 0.01) {
        float band = floor(p.y / 3.0);
        float jitter = hash21(float2(band, floor(t * 9.0)));
        float gate = step(1.0 - uCrash * 0.30, jitter);
        p.x += (jitter - 0.5) * 7.0 * gate;
    }

    float r = length(p);
    float ang = atan2(p.y, p.x + 0.0001);
    float Router = min(res.x, res.y) * 0.5 - 2.0;

    float3 col = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    //----- 区域半径 -----
    float ringR = Router - 3.0;     //外环
    float eyeR = Router - 10.0;     //眼球
    float irisR = eyeR * 0.60;      //虹膜

    //----- 1 外环：波动双环 + 旋转刻度 -----
    float wob = sin(ang * 6.0 + t * 1.4) * 1.1 + sin(ang * 11.0 - t * 0.9) * 0.5;
    float ringDist = abs(r - (ringR + wob));
    float ringPulse = 0.75 + 0.25 * sin(t * 2.2);
    float3 ringCol = lerp(COL_GLOW, COL_VIOLET, uLayers * 0.6);
    ringCol = lerp(ringCol, COL_DANGER, uCrash * 0.65);
    float ringLine = exp(-ringDist * ringDist * 0.55);
    col += ringCol * ringLine * 0.85 * ringPulse;
    //次级内环
    float ring2 = exp(-pow(abs(r - (ringR - 4.5)), 2.0) * 0.8);
    col += ringCol * ring2 * 0.28;
    //旋转刻度梳：层数越高刻度越亮
    float comb = pow(abs(sin(ang * 9.0 - t * 0.6)), 18.0);
    float combMask = smoothstep(2.6, 0.6, abs(r - (ringR + wob)));
    col += COL_CAUSTIC * comb * combMask * (0.25 + uLayers * 0.45);
    //冷却进度弧：环上从顶部顺时针的亮弧（剩余部分暗）
    float angN = frac((ang + PI * 0.5) / TAU);
    if (uCooldown > 0.005) {
        float inCd = step(angN, uCooldown);
        col *= 1.0 - inCd * combMask * 0.0;   //刻度保留
        float cdEdge = exp(-abs(angN - uCooldown) * 60.0);
        col += COL_CAUSTIC * cdEdge * combMask * 0.9;
        col -= ringCol * ringLine * inCd * 0.45;
    }
    alpha = max(alpha, (ringLine * 0.9 + ring2 * 0.3 + comb * combMask * 0.5) * ringPulse);

    //----- 2 眼睑开合遮罩（杏仁形开口）-----
    float xN = clamp(p.x / eyeR, -1.0, 1.0);
    float arch = sqrt(max(1.0 - xN * xN, 0.0));
    float lidOpen = eyeR * arch * (0.10 + 0.90 * uBlink);
    float eyeOpenMask = smoothstep(1.5, -1.5, abs(p.y) - lidOpen) * smoothstep(0.5, -1.5, r - eyeR);

    //----- 3 眼球内部 -----
    if (eyeOpenMask > 0.003) {
        //巩膜：深渊水体 + 流动
        float2 flowUV = p * 0.045 + float2(t * 0.10, -t * 0.06);
        float flow = fbm3(flowUV);
        float3 sclera = lerp(COL_VOID, COL_DEEP, 0.6 + flow * 0.5);
        sclera += COL_MID * (flow - 0.5) * 0.35;
        //边缘暗化
        sclera *= 1.0 - smoothstep(irisR, eyeR, r) * 0.35;

        //虹膜
        float2 ip = p - uPupilOffset * 0.55;   //虹膜轻微跟随注视
        float ir = length(ip);
        float ia = atan2(ip.y, ip.x + 0.0001);
        if (ir < irisR + 2.0) {
            //纤维：角向噪声细丝，缓慢旋转，躁动时微颤
            float fiberA = ia + t * 0.10 + uAgitation * sin(t * 7.0 + ir * 0.3) * 0.05;
            float fiber = valueNoise(float2(fiberA * 6.4, ir * 0.16 - t * 0.05));
            float fiber2 = valueNoise(float2(fiberA * 13.0 + 7.7, ir * 0.30));
            float fibers = fiber * 0.65 + fiber2 * 0.35;
            //双色虹膜：内圈亮青，外圈随层数偏紫
            float3 irisInner = lerp(COL_GLOW, COL_CAUSTIC, 0.35);
            float3 irisOuter = lerp(COL_MID, COL_VIOLET, uLayers);
            float radT = saturate(ir / irisR);
            float3 irisCol = lerp(irisInner, irisOuter, radT);
            irisCol *= 0.55 + fibers * 0.8;
            //外缘灰边（limbal ring）
            irisCol *= 1.0 - smoothstep(irisR * 0.82, irisR, ir) * 0.55;
            //充血：躁动从外缘渗入红
            irisCol = lerp(irisCol, COL_DANGER * (0.5 + fibers * 0.5), uAgitation * radT * 0.45);
            irisCol = lerp(irisCol, COL_DANGER * (0.4 + fibers * 0.6), uCrash * 0.55);

            float irisMask = smoothstep(1.5, -0.5, ir - irisR);
            sclera = lerp(sclera, irisCol, irisMask);

            //瞳孔：竖窄椭圆，注视偏移 + 扩张
            float2 pp = p - uPupilOffset;
            float pw = irisR * (0.20 + uDilate * 0.34);
            float ph = irisR * (0.72 + uDilate * 0.16);
            float pd = length(pp / float2(pw, ph));
            float pupilMask = smoothstep(1.06, 0.92, pd);
            float3 pupilCol = COL_VOID * 0.5;
            //死机瞳底泛红芯
            pupilCol = lerp(pupilCol, COL_DANGER * 0.30, uCrash * step(pd, 0.55));
            sclera = lerp(sclera, pupilCol, pupilMask);
            //瞳缘亮线
            float pupilRim = exp(-abs(pd - 1.0) * 16.0) * irisMask;
            sclera += lerp(COL_GLOW, COL_DANGER, max(uCrash, uAgitation * 0.6)) * pupilRim * 0.5;

            //就绪闪光：整个虹膜短促提亮
            sclera += COL_CAUSTIC * irisMask * uReadyFlash * 0.55;
        }

        //上方高光
        float2 hl = p - float2(-irisR * 0.38, -irisR * 0.46);
        sclera += COL_CAUSTIC * exp(-dot(hl, hl) * 0.012) * 0.50;

        //冷却薄翳：扫掠区域蒙上灰翳（在眼球上）
        if (uCooldown > 0.005) {
            float inCd = step(angN, uCooldown);
            sclera = lerp(sclera, COL_DEEP * 0.7, inCd * 0.42);
        }

        col = lerp(col, sclera, eyeOpenMask);
        alpha = max(alpha, eyeOpenMask * 0.96);
    }

    //----- 4 眼睑本体：开口之外、眼球之内的皮膜 -----
    float lidMask = (1.0 - eyeOpenMask) * smoothstep(1.0, -1.5, r - eyeR);
    if (lidMask > 0.003) {
        float lidFlow = fbm3(p * 0.06 + float2(0.0, t * 0.04));
        float3 lid = COL_LID * (0.75 + lidFlow * 0.5);
        //睑缘亮线
        float lidEdge = exp(-pow(abs(abs(p.y) - lidOpen), 2.0) * 0.30);
        lid += lerp(COL_GLOW, COL_DANGER, uCrash) * lidEdge * 0.45 * smoothstep(eyeR, eyeR * 0.3, r);
        col = lerp(col, lid, lidMask);
        alpha = max(alpha, lidMask * 0.92);
    }

    //----- 5 外侧呼吸辉光 -----
    float halo = exp(-max(r - ringR, 0.0) * 0.22);
    float breathe = 0.5 + 0.5 * sin(t * 1.6);
    col += ringCol * halo * (0.10 + breathe * 0.07 + uReadyFlash * 0.35);
    alpha = max(alpha, halo * (0.16 + uReadyFlash * 0.3));

    //整体边界淡出
    float cut = smoothstep(Router + 2.0, Router - 1.0, r);
    alpha *= cut;
    col *= cut;

    float fa = saturate(alpha) * uAlpha;
    return float4(col * uAlpha, fa) * vertexColor;
}

technique Technique1
{
    pass HalibutHudEyePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
