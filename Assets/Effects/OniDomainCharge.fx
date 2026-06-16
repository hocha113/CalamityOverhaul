// ============================================================================
//OniDomainCharge.fx 鬼斩领域起手蓄力 鬼灭式血色丝带螺旋汇聚
//对数螺旋 SDF 丝带 + 沿丝带切向流动 + 血液脉冲波 + 湿润前沿 + 中心血池
//中心方形面片；AlphaBlend 预乘 alpha；ps_3_0；s1=Extra_193 Voronoi 灰度
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //Extra_193 灰度

float uTime;
float uProgress;        //0~1 蓄力进度
float uIntensity;       //总强度倍率
float uOpacity;         //最终不透明度
float uSeed;            //每实例偏置
float uPulse;           //外部脉冲注入

float3 uBloodDark;      //暗血 (40,2,5)/255 域底
float3 uBloodFlesh;     //血肉 (130,8,14)/255 丝带主体
float3 uBloodBright;    //鲜血 (210,28,32)/255 高光/前沿
float3 uBloodGleam;     //反光 (255,200,195)/255 极小反光区

#define PI 3.14159265
#define TAU 6.28318530
#define INV_TAU 0.15915494

float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//3 octave fbm，足以表达血液有机扰动
float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 3; i++) {
        v += a * vNoise(p);
        p = p * 2.07 + 17.0;
        a *= 0.52;
    }
    return v;
}

//A 域扭曲血场：深血色雾气浸染，沿径向缓慢流动；中心更浓
float bloodField(float r, float theta, float time) {
    float norm = (theta + PI) * INV_TAU;

    //大尺度有机扰动场
    float2 warpUV = float2(norm * 2.0 + time * 0.06, r * 1.4 - time * 0.04);
    float2 w = float2(
        fbm3(warpUV * 2.2),
        fbm3(warpUV * 2.2 + float2(5.2, 1.3))
    ) - 0.5;

    //主血浆密度（被 w 扭曲，模拟血液流动）
    float2 mainUV = float2(norm * 3.5 + w.x * 1.5 + time * 0.10,
                           r * 2.0 + w.y * 1.3 - time * 0.06);
    float density = fbm3(mainUV * 1.6);
    density = smoothstep(0.22, 0.85, density);

    //中心更浓密
    float core = smoothstep(1.0, 0.0, r);
    density = saturate(density * (0.35 + core * 0.65) + core * core * 0.45);

    return density;
}

//B 对数螺旋丝带：返回(mask, edge, armId, local, phi)
//phi 为同一丝带上不变的相位，可用于沿丝带的流动/脉冲采样
//local 为该像素距离丝带中心的角向距离
struct Ribbon {
    float mask;     //丝带遮罩 0~1
    float edge;     //边缘强度 0~1
    float armId;    //该丝带的整数 id（可作种子）
    float local;    //角向距离（带符号）
    float phi;      //沿丝带的相位
};

Ribbon spiralRibbon(float r, float theta, float armN, float twist,
                    float rotation, float thickness) {
    //log(r) 防中心退化
    float logR = log(max(r * 6.0, 0.01) + 1.0);

    //对数螺旋相位
    float phi = logR * twist - theta - rotation;

    //多臂 wrap
    float wrap = TAU / armN;
    float armId = floor(phi / wrap + 0.5);
    float local = phi - armId * wrap; // [-wrap/2, wrap/2]

    float dist = abs(local);

    //高斯丝带轮廓
    float mask = exp(-(dist * dist) / (thickness * thickness));

    //边缘锐度：丝带的外缘强度高，内核低（用于高光线）
    float edge = smoothstep(thickness * 0.15, thickness * 0.55, dist) * mask;

    Ribbon rib;
    rib.mask = mask;
    rib.edge = edge;
    rib.armId = armId;
    rib.local = local;
    rib.phi = phi;
    return rib;
}

//C 沿丝带切向的血液流动纹理（粘稠流动质感的核心）
//在丝带方向(phi)上移动 + 切向方向(local)上变化
float ribbonFlow(float phi, float local, float r, float time, float seed) {
    //主流动：沿丝带快速移动
    float2 fUV = float2(phi * 0.4 + time * 0.55 + seed * 3.13,
                        local * 9.0 + r * 3.0);
    float flow = fbm3(fUV * 1.4);
    flow = smoothstep(0.28, 0.88, flow);

    //次流动：更慢的大块变形，叠加层次
    float2 fUV2 = float2(phi * 0.18 - time * 0.18, r * 1.5);
    float flow2 = vNoise(fUV2 * 3.0);
    flow2 = smoothstep(0.35, 0.80, flow2);

    return saturate(flow * 0.75 + flow2 * 0.35);
}

//D 血液脉冲波：沿丝带涌向中心的"血液流过"亮线
//用 sin(phi - time * ω) 制造移动相位，再 pow 锐利化
float bloodPulse(float phi, float r, float time, float prog) {
    //三组错相位脉冲，制造连绵涌动
    float p1 = sin(phi * 0.8 - time * 2.8);
    float p2 = sin(phi * 1.1 - time * 3.6 + 1.3);
    float p3 = sin(phi * 0.6 - time * 2.2 + 2.7);

    p1 = smoothstep(0.85, 0.99, p1);
    p2 = smoothstep(0.80, 0.96, p2);
    p3 = smoothstep(0.78, 0.94, p3);

    float pulse = saturate(p1 + p2 * 0.7 + p3 * 0.6);
    //进度越高脉冲越强
    return pulse * (0.35 + prog * 0.85);
}

//E 湿润前沿：沿丝带方向的细高光带，模拟"血液正在流过"的反光
float wetFront(float phi, float local, float time) {
    //细密的高频波动，集中在丝带中线附近
    float wave = sin(phi * 4.0 - time * 5.0);
    wave = smoothstep(0.7, 0.95, wave);
    //仅在丝带中线附近显现
    float onCenter = exp(-local * local * 60.0);
    return wave * onCenter;
}

//F 中心血池：饱满的暗血色填充
float bloodPool(float r, float prog, float time) {
    float poolRadius = lerp(0.10, 0.22, prog);
    float pool = smoothstep(poolRadius * 1.7, poolRadius * 0.3, r);
    //缓慢的"心跳"呼吸
    float beat = 0.85 + 0.15 * sin(time * 2.4);
    return saturate(pool * (1.0 + prog * 0.5) * beat);
}

//G 中心鲜血核：进度后期的高亮血核
float bloodCore(float r, float prog, float time) {
    float coreSize = lerp(0.05, 0.10, prog);
    float core = exp(-r * r / (coreSize * coreSize));
    float gate = smoothstep(0.20, 0.55, prog);
    //快速呼吸
    float beat = 0.8 + 0.2 * sin(time * 5.5);
    return core * gate * beat;
}

//H 表面湿润高频细节：模拟血液表面的反光起伏
float wetSurface(float r, float theta, float time) {
    float n1 = vNoise(float2(theta * 7.0 + r * 11.0 - time * 0.3,
                             r * 16.0 + theta * 5.0) * 1.3);
    float n2 = vNoise(float2(theta * 13.0 - r * 8.0 + time * 0.45,
                             r * 22.0) * 1.5);
    float wet = pow(saturate(n1 * 0.7 + n2 * 0.5), 1.6);
    return wet;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = coords * 2.0 - 1.0;
    float r = length(p);

    float edgeFade = 1.0 - smoothstep(0.86, 1.02, r);
    if (edgeFade <= 0.001) {
        return float4(0, 0, 0, 0);
    }

    float theta = atan2(p.y, p.x);
    float time = uTime;
    float prog = saturate(uProgress);
    float seed = uSeed;

    //=== 域底色 ===
    float field = bloodField(r, theta, time);

    //=== 螺旋丝带：主+次 ===
    float armN1 = lerp(5.0, 6.0, prog);
    float twist1 = lerp(3.4, 5.8, prog);
    float th1 = lerp(0.20, 0.13, prog);
    float rot1 = time * (0.36 + prog * 0.55) + seed * TAU;
    Ribbon rib1 = spiralRibbon(r, theta, armN1, twist1, rot1, th1);
    float flow1 = ribbonFlow(rib1.phi, rib1.local, r, time, seed);

    float armN2 = lerp(7.0, 9.0, prog);
    float twist2 = lerp(4.6, 7.4, prog);
    float th2 = lerp(0.09, 0.06, prog);
    float rot2 = time * (0.46 + prog * 0.72) - seed * TAU * 0.6 + 1.7;
    Ribbon rib2 = spiralRibbon(r, theta, armN2, twist2, rot2, th2);
    float flow2 = ribbonFlow(rib2.phi, rib2.local, r, time, seed + 0.3);

    //=== 径向包络：丝带在 [0.18, shrink] 显现 ===
    float shrink = lerp(1.0, 0.72, prog);
    float radialEnv = smoothstep(1.0, 0.55, r / shrink) * smoothstep(0.06, 0.18, r);

    //丝带最终密度
    float ribbonMain = rib1.mask * (0.65 + flow1 * 0.55) * radialEnv;
    float ribbonSub  = rib2.mask * (0.40 + flow2 * 0.45) * radialEnv;
    float ribbonAll  = saturate(ribbonMain + ribbonSub * 0.8);

    //=== 沿丝带的血液脉冲 ===
    float pulseMain = bloodPulse(rib1.phi, r, time, prog) * rib1.mask * radialEnv;
    float pulseSub  = bloodPulse(rib2.phi, r, time + 0.7, prog) * rib2.mask * radialEnv * 0.6;
    float pulseAll  = saturate(pulseMain + pulseSub);

    //=== 湿润前沿（丝带中线高光） ===
    float wetMain = wetFront(rib1.phi, rib1.local, time) * radialEnv;
    float wetSub  = wetFront(rib2.phi, rib2.local, time + 0.5) * radialEnv * 0.6;
    float wetLine = saturate(wetMain + wetSub) * (0.3 + prog * 0.85);

    //=== 丝带边缘高光（沿外缘的鲜红反光） ===
    float edgeGlow = (rib1.edge * 1.1 + rib2.edge * 0.55) * radialEnv;

    //=== 中心血池 + 鲜血核 ===
    float pool = bloodPool(r, prog, time);
    float core = bloodCore(r, prog, time);

    //=== 高频湿润表面 ===
    float wet = wetSurface(r, theta, time);

    //==========================================
    //颜色合成（AlphaBlend 预乘 alpha 暗血底）
    //==========================================
    //基底：暗血+域底密度
    float3 col = lerp(uBloodDark * 0.5, uBloodDark, field);

    //血池叠加：浓密血肉色
    col = lerp(col, uBloodFlesh, pool * 0.85);

    //丝带主体：血肉色 + 湿润起伏微调
    col = lerp(col, uBloodFlesh * (1.0 + wet * 0.2), ribbonAll * (0.85 + wet * 0.25));

    //脉冲波：丝带上涌动的鲜血高亮
    col = lerp(col, uBloodBright, pulseAll * 0.65);

    //湿润前沿：丝带中线的尖锐高光（最锐利的"血液反光"）
    col = lerp(col, lerp(uBloodBright, uBloodGleam, 0.45), wetLine * 0.8);

    //丝带边缘：鲜血高光带
    col = lerp(col, uBloodBright, edgeGlow * 0.7);

    //中心鲜血核：白热反光
    col = lerp(col, uBloodGleam, core * 0.85);

    //外部脉冲注入
    if (uPulse > 0.001) {
        col = lerp(col, uBloodBright, uPulse * 0.5);
    }

    //==========================================
    //Alpha 合成（决定"血色覆盖屏幕"的强度）
    //==========================================
    float a = 0.0;
    a += field * 0.55;
    a += ribbonAll * 0.95;
    a += pool * 1.0;
    a += pulseAll * 0.55;
    a += wetLine * 0.55;
    a += edgeGlow * 0.55;
    a += core * 1.1;
    a = saturate(a);

    //总强度调制
    a *= uOpacity * uIntensity * edgeFade;
    a *= vertexColor.a;

    col *= vertexColor.rgb;

    //预乘 alpha 输出
    return float4(col * a, a);
}

technique Technique1 {
    pass OniDomainChargePass {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
