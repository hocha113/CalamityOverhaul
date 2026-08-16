//KikasaHourglass.fx 鬼伞大范围重启的背景雨水沙漏
//单张屏幕空间 quad（placeholder2 画布），预乘输出进 AlphaBlend 批。
//局部坐标：x 按画布宽高比展开、y 向下为正、颈口为原点；全笛卡尔 SDF，
//无 atan2、无动态分支、平 tex2D（FNA3D 安全）。
//uForm 噪声阈值成形（毛边撕口，WeaverMaterialize 血统）、
//uFill 沙自下腔逆流入上腔的比例=倒带进度、uPulse 中央细流吃回卷脉冲、
//uDisperse 落定溃散、uFlow 雨丝沿玻璃的累计流动相位（增=向上流）。
//s1=PerlinNoise（实测值域 0.227~0.776，nrm() 归一后再做阈值）

float uTime;     //秒，沙粒微闪
float uSeed;     //本场种子，各端同值
float uForm;     //0-1 成形进度
float uFill;     //0-1 沙移入上腔的比例
float uPulse;    //0-1 当帧回卷速率
float uDisperse; //0-1 落定溃散
float uAlpha;    //总透明度包络
float uFlow;     //累计流动相位
float uAspect;   //quad 宽/高
float3 uColBody;  //墨体近黑（框架）
float3 uColDeep;  //墨缘暗沿
float3 uColCore;  //冷青水体（沙）
float3 uColSheen; //湿反光

sampler uNoise : register(s1);   //PerlinNoise，消费端 LinearWrap

//几何常量（画布单位，与 KikasaResetHourglass.cs 同源）
static const float GLASS_H = 0.36;   //玻璃半高
static const float NECK_W  = 0.022;  //颈口半宽
static const float BULB_W  = 0.205;  //腔体最大半宽
static const float CONE_H  = 0.09;   //下堆锥顶高
static const float CRATER  = 0.08;   //上堆漏斗坑深

//PerlinNoise 实测值域 0.227~0.776 归一
float nrm(float v)
{
    return saturate((v - 0.227) / 0.549);
}

//圆角盒 SDF，负值在内
float rbox(float2 o, float2 halfSize, float r)
{
    float2 d = abs(o) - halfSize + r;
    return length(max(d, 0.0)) - r;
}

float4 PSHourglass(float2 coords : TEXCOORD0) : COLOR0
{
    float2 p = float2((coords.x - 0.5) * uAspect, coords.y - 0.5);
    float ax = abs(p.x);
    float ay = abs(p.y);

    //====== 玻璃剖面：颈口窄、腔肩宽、近座微收 ======
    float q = saturate(ay / GLASS_H);
    float shoulder = smoothstep(0.0, 0.60, q) * (1.0 - 0.25 * smoothstep(0.60, 1.0, q));
    float W = lerp(NECK_W, BULB_W, shoulder);
    float inGlassY = 1.0 - smoothstep(GLASS_H - 0.006, GLASS_H + 0.006, ay);
    float edgeD = ax - W;
    float inside = (1.0 - smoothstep(-0.005, 0.005, edgeD)) * inGlassY;
    float shell = exp2(-abs(edgeD) * 340.0) * inGlassY;

    //====== 框架：上下座 + 双侧柱，湿墨暗体 ======
    float dCap = rbox(float2(p.x, ay - 0.395), float2(0.275, 0.032), 0.012);
    float dPil = rbox(float2(ax - 0.250, p.y), float2(0.014, 0.415), 0.010);
    float dFrame = min(dCap, dPil);
    float frameM = 1.0 - smoothstep(-0.0035, 0.0035, dFrame);
    float frameEdge = exp2(-abs(dFrame) * 260.0);

    //====== 沙面：下堆锥顶（缘水位 yBEdge）、上堆漏斗坑（深 yT） ======
    //倒放的沙堆签名：下堆锥顶持续存在着降低，上堆坑自中心被细流填起
    float wob = (nrm(tex2D(uNoise, float2(p.x * 4.1 + uSeed * 0.7, 0.37)).r) - 0.5) * 0.013;
    float coneP = max(0.0, 1.0 - ax / 0.185);
    float yBEdge = lerp(0.115, GLASS_H + CONE_H + 0.03, uFill);
    float surfB = yBEdge - CONE_H * coneP + wob;
    float yT = uFill * 0.27;
    float surfT = -yT + CRATER * coneP + wob;

    float sandB = smoothstep(-0.004, 0.004, p.y - surfB) * inside;
    float sandT = smoothstep(-0.004, 0.004, p.y - surfT)
        * (1.0 - smoothstep(-0.003, 0.003, p.y)) * inside
        * smoothstep(0.01, 0.05, uFill);

    //====== 沙体质感：双频颗粒 + 零星湿闪 ======
    float grainA = nrm(tex2D(uNoise, p * 7.0 + uSeed * 0.13).r);
    float grainB = nrm(tex2D(uNoise, p * 15.0 + float2(uSeed * 0.29, -uTime * 0.05)).r);
    float sparkle = smoothstep(0.80, 0.94, grainB);

    //沙面亮线：水面张力的一线湿光，堆存在才亮
    float pileBAlive = 1.0 - smoothstep(0.90, 1.0, uFill);
    float pileTAlive = smoothstep(0.03, 0.10, uFill);
    float lineB = exp2(-abs(p.y - surfB) * 300.0) * inside * pileBAlive;
    float lineT = exp2(-abs(p.y - surfT) * 300.0) * inside * pileTAlive
        * (1.0 - smoothstep(-0.003, 0.003, p.y));

    //====== 中央逆流细流：珠串上升、并入上腔坑心 ======
    float streamTop = -yT + CRATER;
    float streamBot = yBEdge - CONE_H;
    float sIn = smoothstep(streamTop - 0.015, streamTop + 0.015, p.y)
        * (1.0 - smoothstep(streamBot - 0.015, streamBot + 0.015, p.y));
    float sway = (nrm(tex2D(uNoise, float2(uSeed * 0.37, p.y * 2.6 + uFlow * 0.5)).r) - 0.5) * 0.012;
    float wS = 0.006 + 0.011 * uPulse;
    float sd = abs(p.x - sway);
    float sCore = exp2(-(sd * sd) / (wS * wS) * 2.2);
    float bead = smoothstep(0.55, 0.85,
        nrm(tex2D(uNoise, float2(uSeed * 0.91, p.y * 9.0 + uFlow * 1.6)).r));
    float streamGate = smoothstep(0.005, 0.03, uFill)
        * (1.0 - smoothstep(0.93, 1.0, uFill)) * inside;
    float stream = sCore * sIn * streamGate * (0.30 + 0.70 * uPulse);

    //====== 玻璃壳雨丝：沿轮廓流动的水线（uFlow 增=向上抽回） ======
    float streakN = nrm(tex2D(uNoise, float2(p.x * 3.0 + uSeed * 0.31, p.y * 2.2 + uFlow)).r);
    float shellStreak = shell * (0.30 + 0.70 * smoothstep(0.35, 0.80, streakN));

    //腔内水汽薄雾
    float mist = inside * (0.045 + 0.05 * nrm(tex2D(uNoise, p * 2.0 + float2(0.0, uFlow * 0.15)).r));

    //====== 成形/溃散：噪声阈值毛边撕口，沙体滞后于骨架显形 ======
    float formN = nrm(tex2D(uNoise, p * 2.4 + float2(uSeed * 0.113, uSeed * 0.271)).r);
    float reveal = saturate((uForm * 1.18 - formN) / 0.14);
    float revealSand = saturate((uForm * 1.34 - 0.34 - formN) / 0.14);
    float erode = 1.0 - saturate((uDisperse * 1.25 - formN) / 0.11);

    //====== 预乘合成 ======
    float3 col = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    //框架暗体 + 湿缘光
    float3 frameCol = lerp(uColBody, uColDeep, 0.30 + 0.40 * grainA);
    float fA = frameM * 0.78 * reveal;
    col += frameCol * fA;
    alpha += fA;
    float feA = frameEdge * 0.16 * reveal;
    col += uColSheen * feA;
    alpha += feA * 0.55;

    //玻璃壳雨丝 + 腔内水汽
    float shA = shellStreak * 0.52 * reveal;
    col += lerp(uColCore, uColSheen, streakN) * shA;
    alpha += shA;
    float mA = mist * reveal;
    col += uColDeep * mA;
    alpha += mA;

    //沙体（颗粒水沙）+ 湿闪 + 沙面亮线
    float sandM = max(sandB, sandT);
    float sA = sandM * (0.44 + 0.18 * grainA) * revealSand;
    col += lerp(uColCore, uColSheen, 0.22 + 0.30 * grainA) * sA;
    alpha += sA;
    col += uColSheen * sparkle * sandM * 0.28 * revealSand;
    float lA = (lineB + lineT) * 0.30 * revealSand;
    col += uColSheen * lA;
    alpha += lA * 0.5;

    //中央逆流细流：亮珠串偏加色
    float stA = stream * revealSand;
    col += (uColSheen * (0.55 + 0.45 * bead) + uColCore * 0.35) * stA;
    alpha += stA * 0.75;

    //画布护栏 + 总量/溃散包络
    float guard = (1.0 - smoothstep(0.47, 0.50, abs(coords.x - 0.5)))
        * (1.0 - smoothstep(0.47, 0.50, abs(coords.y - 0.5)));
    float gain = guard * uAlpha * erode;
    return float4(col * gain, saturate(alpha) * gain);
}

technique TechHourglass
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSHourglass();
    }
}
