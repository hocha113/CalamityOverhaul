// ============================================================================
//OniGhostShadow.fx 鬼影，域扭曲 fbm 生成的幽影剪影,配鬼火之眼
//铭刻仪式(显形/碎裂)与点鬼簿影绘细节板(隔纸看影/凝视)共用
//形体每帧由噪声生成而非贴图循环:躯干+头部两团 SDF 经域扭曲揉出人形将散未散的轮廓,
//下摆溶入上升的烟缕;内部两层墨流;轮廓一圈鬼火青雾沿
//AlphaBlend 预乘 alpha 输出
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uWrithe;      //0~1 扭动量(封印≈0,躁动=1)
float uBreak;       //0~1 碎裂溶解(仪式刀痕后)
float uEyeOpen;     //0~1 睁眼量
float2 uGlance;     //瞳位偏移(UV 空间,凝视光标用,量级 ±0.03)
float uSeed;        //个体差异种子
float3 uColBody;    //影体墨色
float3 uColRim;     //鬼火暗青(雾沿/眼晕)
float3 uColFire;    //鬼火亮青(眼芯)

#define PI 3.14159265

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

float fbm4(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * valueNoise(p);
        p = p * 2.11 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

//鬼火之眼:横椭圆高斯亮斑,open 压竖径(阖眼时缩成一线)
float eyeMask(float2 uv, float2 c, float open, float squash) {
    float2 dd = (uv - c) * float2(30.0, 30.0 / max(open, 0.05) * squash);
    return exp(-dot(dd, dd));
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    //扭动决定时间流速:封印中的影几乎凝固
    float t = uTime * (0.12 + uWrithe * 0.55);

    //====心跳节律:双峰脉冲(咚-咚……),推动轮廓微胀====
    float ph = frac(uTime * 0.42);
    float beat = exp(-pow((ph - 0.10) * 15.0, 2.0)) + 0.55 * exp(-pow((ph - 0.30) * 15.0, 2.0));
    beat *= uWrithe; //封印不心跳

    //====域扭曲:形体在噪声域里揉====
    float2 p = uv * float2(2.0, 1.5) + float2(uSeed * 7.3, uSeed * 3.1);
    float2 warp = float2(
        fbm4(p * 1.6 + float2(0.0, -t * 0.55)),
        fbm4(p * 1.6 + float2(3.7, -t * 0.47)));
    float2 q = uv + (warp - 0.5) * (0.06 + uWrithe * 0.11);

    //====剪影 SDF:躯干椭圆 + 头部小团,取 min 融合====
    float2 torso = float2(0.5, 0.60);
    float dTorso = length((q - torso) * float2(2.5, 1.35));
    float2 head = float2(0.5, 0.295);
    float dHead = length((q - head) * float2(3.2, 2.9)) * 0.94;
    float d = min(dTorso, dHead);
    float edge0 = 0.44 + beat * 0.025;
    float body = 1.0 - smoothstep(edge0, edge0 + 0.13, d);

    //====下摆溶入上升烟缕,顶部软融====
    float wispN = fbm4(float2(uv.x * 6.0 + uSeed * 11.0, uv.y * 2.6 - t * 1.3));
    body *= smoothstep(1.02, 0.55, uv.y + (wispN - 0.5) * 0.38);
    body *= smoothstep(0.015, 0.13, uv.y);

    //====碎裂:阈值溶解成飞屑 + 整体衰减====
    if (uBreak > 0.001) {
        float breakN = fbm4(uv * 7.0 + float2(uSeed * 5.0, t * 0.9));
        body *= smoothstep(uBreak * 1.15 - 0.18, uBreak * 1.15 + 0.14, breakN);
        body *= 1.0 - uBreak * 0.55;
    }

    //====内部墨流:两层异速噪声相乘,影子里有东西在流====
    float flow = fbm4(q * 3.4 + float2(t * 0.16, -t * 0.85)) * 0.62
               + fbm4(q * 6.6 + float2(-t * 0.11, -t * 0.50)) * 0.38;
    float3 col = uColBody * (0.70 + flow * 0.42);

    //====鬼火雾沿:轮廓外带一圈青,标记"这是鬼不是烟"====
    float bodyCore = 1.0 - smoothstep(edge0 - 0.10, edge0 + 0.02, d);
    float rim = saturate(body - bodyCore);
    float rimPulse = 0.30 + 0.16 * sin(t * 2.1 + uSeed * 9.0);
    col = lerp(col, uColRim, rim * rimPulse);

    float A = body * (0.78 + flow * 0.22);

    //====鬼火之眼:眼晕(暗青宽斑) + 眼芯(亮青窄斑),低频闪====
    if (uEyeOpen > 0.01) {
        float flick = 0.78 + 0.22 * sin(uTime * 6.3 + uSeed * 4.0);
        //眼随头部一起被域扭曲轻微带动(取 warp 的低频分量),凝视偏移额外叠加
        float2 drift = (warp - 0.5) * 0.03 * uWrithe;
        float2 eyeL = float2(0.446, 0.300) + drift + uGlance;
        float2 eyeR = float2(0.554, 0.306) + drift + uGlance;

        float halo = eyeMask(uv, eyeL, uEyeOpen, 0.45) + eyeMask(uv, eyeR, uEyeOpen, 0.45);
        float core = eyeMask(uv, eyeL, uEyeOpen, 1.0) + eyeMask(uv, eyeR, uEyeOpen, 1.0);
        halo = saturate(halo);
        core = saturate(core);

        float eyeA = uEyeOpen * flick * (1.0 - uBreak);
        col = lerp(col, uColRim, halo * 0.55 * eyeA);
        col = lerp(col, uColFire, core * eyeA);
        A = max(A, saturate(halo * 0.42 + core) * eyeA);
    }

    return float4(col * A, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniGhostShadowPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
