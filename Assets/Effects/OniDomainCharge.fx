// ============================================================================
//OniDomainCharge.fx 鬼斩领域起手蓄力 鬼灭式血色丝带螺旋汇聚
//两 technique：TechBase(AlphaBlend) 写暗血底+圆柱明暗丝带；TechHighlight(Additive) 写鲜血脉冲+反光
//armN 固定整数避免 ±π 处 seam；rotation 由 C# 累积控制节奏
//中心方形面片；ps_3_0；s1=Extra_193 Voronoi 灰度
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //Extra_193 灰度

float uTime;
float uProgress;        //0~1 演绎进度(已 ease，含三阶段)
float uIntensity;       //总强度倍率
float uOpacity;         //最终不透明度
float uSeed;            //每实例偏置
float uPulse;           //外部脉冲注入
float uBeat;            //心跳爆发 0~1
float uRotation;        //C# 累积的螺旋旋转角

float3 uBloodDark;      //(40,2,5)/255 域底
float3 uBloodFlesh;     //(130,8,14)/255 丝带主体
float3 uBloodBright;    //(210,28,32)/255 脉冲/高光
float3 uBloodGleam;     //(255,200,195)/255 反光/核

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

//==========================================================================
//Ribbon SDF：armN 必须是编译期常量整数(否则 wrap*N != TAU 导致 ±π 处 seam)
//cyl 是圆柱体厚度因子：丝带中线=1，边缘=0(球面 sqrt)，用于"顶光"明暗
//==========================================================================
struct Ribbon {
    float mask;     //丝带遮罩 0~1
    float cyl;      //圆柱体厚度因子 0~1
    float local;    //带符号距丝带中心的局部距离
    float phi;      //沿丝带的连续相位
};

Ribbon spiralRibbon6(float r, float theta, float twist, float rotation, float thickness) {
    const float armN = 6.0;
    const float wrap = TAU / armN;
    float logR = log(max(r * 6.0, 0.01) + 1.0);
    float phi = logR * twist - theta - rotation;
    float armId = floor(phi / wrap + 0.5);
    float local = phi - armId * wrap;
    float d = abs(local);
    float mask = exp(-(d * d) / (thickness * thickness));
    float n = saturate(1.0 - d / max(thickness, 1e-4));
    float cyl = sqrt(max(0.0, 1.0 - (1.0 - n) * (1.0 - n)));
    Ribbon o;
    o.mask = mask;
    o.cyl = cyl;
    o.local = local;
    o.phi = phi;
    return o;
}

Ribbon spiralRibbon10(float r, float theta, float twist, float rotation, float thickness) {
    const float armN = 10.0;
    const float wrap = TAU / armN;
    float logR = log(max(r * 6.0, 0.01) + 1.0);
    float phi = logR * twist - theta - rotation;
    float armId = floor(phi / wrap + 0.5);
    float local = phi - armId * wrap;
    float d = abs(local);
    float mask = exp(-(d * d) / (thickness * thickness));
    float n = saturate(1.0 - d / max(thickness, 1e-4));
    float cyl = sqrt(max(0.0, 1.0 - (1.0 - n) * (1.0 - n)));
    Ribbon o;
    o.mask = mask;
    o.cyl = cyl;
    o.local = local;
    o.phi = phi;
    return o;
}

//A 域扭曲血雾场：深血色浸染底色，沿径向缓慢流动；中心更浓
//用归一化角 normAngle 而非 theta 直接做 UV，规避 ±π 处 seam
float bloodField(float r, float theta, float time) {
    float normAngle = (theta + PI) * INV_TAU;
    float2 warpUV = float2(normAngle * 2.0 + time * 0.05, r * 1.4 - time * 0.03);
    float2 w = float2(
        fbm3(warpUV * 2.2),
        fbm3(warpUV * 2.2 + float2(5.2, 1.3))
    ) - 0.5;
    float2 mainUV = float2(normAngle * 3.5 + w.x * 1.5 + time * 0.08,
                           r * 2.0 + w.y * 1.3 - time * 0.05);
    float density = fbm3(mainUV * 1.6);
    density = smoothstep(0.22, 0.85, density);
    float core = smoothstep(1.0, 0.0, r);
    density = saturate(density * (0.35 + core * 0.65) + core * core * 0.45);
    return density;
}

//B 沿丝带切向的血液流动纹理(粘稠流动质感)
float ribbonFlow(float phi, float local, float r, float time, float seed) {
    float2 fUV = float2(phi * 0.32 + time * 0.32 + seed * 3.13,
                        local * 10.0 + r * 3.0);
    float flow = fbm3(fUV * 1.4);
    flow = smoothstep(0.28, 0.88, flow);
    return saturate(flow);
}

//C 丝带表面湿润高频起伏 法线扰动调制顶光
float ribbonBump(float phi, float local, float time) {
    return vNoise(float2(phi * 3.2 + time * 0.32, local * 16.0)) - 0.5;
}

//D 沿丝带的血液脉冲波(主动态)：三组错相 sin 锐利化
//uBeat 注入时整体加强，制造"心跳推一把"
float bloodPulse(float phi, float time, float prog, float beat) {
    float p1 = sin(phi * 0.7 - time * 1.6);
    float p2 = sin(phi * 0.95 - time * 2.2 + 1.3);
    float p3 = sin(phi * 0.55 - time * 1.2 + 2.7);
    p1 = smoothstep(0.80, 0.99, p1);
    p2 = smoothstep(0.76, 0.96, p2);
    p3 = smoothstep(0.74, 0.94, p3);
    float pulse = saturate(p1 + p2 * 0.7 + p3 * 0.55);
    return pulse * ((0.30 + prog * 0.55) + beat * 0.85);
}

//E 湿润前沿：丝带中线尖锐高光
float wetFront(float phi, float local, float time) {
    float wave = sin(phi * 3.5 - time * 3.0);
    wave = smoothstep(0.72, 0.96, wave);
    float onCenter = exp(-local * local * 70.0);
    return wave * onCenter;
}

float bloodPool(float r, float prog, float beat) {
    float poolR = lerp(0.10, 0.22, prog);
    float pool = smoothstep(poolR * 1.7, poolR * 0.3, r);
    return saturate(pool * (1.0 + prog * 0.45) * (0.82 + beat * 0.32));
}

float bloodCore(float r, float prog, float beat) {
    float coreSize = lerp(0.05, 0.11, prog);
    float core = exp(-r * r / (coreSize * coreSize));
    float gate = smoothstep(0.20, 0.55, prog);
    return core * gate * (0.65 + beat * 0.65);
}

//==========================================================================
//Base Pass：AlphaBlend 写暗血底色与丝带阴影
//输出预乘 alpha，让深血色"覆盖"屏幕变暗(非发光叠加)
//==========================================================================
float4 PSBase(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
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
    float beat = uBeat;

    //=== 域底 ===
    float field = bloodField(r, theta, time);

    //=== 主丝带(6 条)，twist/厚度随 prog 变化但 armN 固定 ===
    float twist1 = lerp(3.4, 5.5, prog);
    float th1 = lerp(0.21, 0.14, prog);
    Ribbon rib1 = spiralRibbon6(r, theta, twist1, uRotation, th1);
    float flow1 = ribbonFlow(rib1.phi, rib1.local, r, time, seed);
    float bump1 = ribbonBump(rib1.phi, rib1.local, time);

    //=== 副丝带(10 条更细) ===
    float twist2 = lerp(4.4, 7.0, prog);
    float th2 = lerp(0.07, 0.05, prog);
    Ribbon rib2 = spiralRibbon10(r, theta, twist2, uRotation * 1.22 + 1.7, th2);
    float flow2 = ribbonFlow(rib2.phi, rib2.local, r, time, seed + 0.4);

    //=== 演绎 gate：丝带分阶段显形 ===
    float ribGateMain = smoothstep(0.05, 0.40, prog);
    float ribGateSub  = smoothstep(0.30, 0.70, prog);

    //径向包络
    float shrink = lerp(1.0, 0.72, prog);
    float radialEnv = smoothstep(1.0, 0.55, r / shrink) * smoothstep(0.06, 0.18, r);

    //=== 丝带最终密度 ===
    float ribMain = rib1.mask * (0.55 + flow1 * 0.60) * radialEnv * ribGateMain;
    float ribSub  = rib2.mask * (0.35 + flow2 * 0.50) * radialEnv * ribGateSub;
    float ribAll  = saturate(ribMain + ribSub * 0.7);

    //=== 圆柱明暗：cyl=丝带顶面，bump=表面凹凸 ===
    //顶面强烈受光(顶光 0.35 + cyl*0.65)，bump 制造细微高低差
    float lit1 = saturate(0.35 + rib1.cyl * 0.65 + bump1 * 0.30);
    float lit2 = saturate(0.30 + rib2.cyl * 0.55);
    //取主导丝带的光照
    float w1 = rib1.mask * ribGateMain;
    float w2 = rib2.mask * ribGateSub;
    float litMix = (lit1 * w1 + lit2 * w2 * 0.6) / max(w1 + w2 * 0.6, 1e-3);

    //=== 血池 ===
    float pool = bloodPool(r, prog, beat);

    //=== 颜色合成：暗血底 → 池血 → 丝带(暗/亮分离) ===
    float3 col = lerp(uBloodDark * 0.42, uBloodDark, field);
    col = lerp(col, uBloodFlesh * 0.85, pool * 0.80);

    //丝带：暗部用 BloodDark 加深、亮部用 BloodFlesh，体现立体
    float3 ribCol = lerp(uBloodDark * 0.55, uBloodFlesh, litMix);
    col = lerp(col, ribCol, ribAll);

    //=== Alpha：丝带强力覆盖屏幕，雾气中等 ===
    float a = 0.0;
    a += field * 0.45;
    a += ribAll * 0.95;
    a += pool * 0.85;
    a = saturate(a);
    a *= uOpacity * uIntensity * edgeFade * vertexColor.a;
    col *= vertexColor.rgb;
    return float4(col * a, a);
}

//==========================================================================
//Highlight Pass：Additive 写鲜血脉冲、湿润前沿、中心爆光
//仅在亮处加色，暗处保持 Base 的厚重感
//==========================================================================
float4 PSHigh(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
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
    float beat = uBeat;

    float twist1 = lerp(3.4, 5.5, prog);
    float th1 = lerp(0.21, 0.14, prog);
    Ribbon rib1 = spiralRibbon6(r, theta, twist1, uRotation, th1);

    float twist2 = lerp(4.4, 7.0, prog);
    float th2 = lerp(0.07, 0.05, prog);
    Ribbon rib2 = spiralRibbon10(r, theta, twist2, uRotation * 1.22 + 1.7, th2);

    float shrink = lerp(1.0, 0.72, prog);
    float radialEnv = smoothstep(1.0, 0.55, r / shrink) * smoothstep(0.06, 0.18, r);

    //演绎 gate：脉冲与高光仅在中后段显现
    float pulseGate = smoothstep(0.45, 0.85, prog);
    float wetGate   = smoothstep(0.35, 0.80, prog);

    //=== 血液脉冲(沿丝带涌动) ===
    float pulse1 = bloodPulse(rib1.phi, time, prog, beat) * rib1.mask * radialEnv;
    float pulse2 = bloodPulse(rib2.phi, time + 0.7, prog, beat) * rib2.mask * radialEnv * 0.55;
    float pulseAll = saturate(pulse1 + pulse2) * pulseGate;

    //=== 湿润前沿(丝带中线尖锐反光) ===
    float wet1 = wetFront(rib1.phi, rib1.local, time) * radialEnv;
    float wet2 = wetFront(rib2.phi, rib2.local, time + 0.5) * radialEnv * 0.6;
    float wetAll = saturate(wet1 + wet2) * wetGate;

    //=== 中心爆光 ===
    float core = bloodCore(r, prog, beat);

    //=== Additive 颜色：脉冲偏鲜血，前沿偏反光，核偏白 ===
    float3 col = uBloodBright * pulseAll * (0.70 + beat * 0.55);
    col += lerp(uBloodBright, uBloodGleam, 0.55) * wetAll * 0.85;
    col += uBloodGleam * core * 1.10;

    //外部脉冲注入
    if (uPulse > 0.01) {
        col += uBloodBright * uPulse * 0.50 * radialEnv;
    }

    //Additive 中 alpha 仅作总亮度调制
    float a = saturate(pulseAll * 0.7 + wetAll * 0.9 + core * 1.1);
    a *= uOpacity * uIntensity * edgeFade * vertexColor.a;

    col *= vertexColor.rgb;
    return float4(col * a, a);
}

technique TechBase {
    pass P0 {
        PixelShader = compile ps_3_0 PSBase();
    }
}

technique TechHighlight {
    pass P0 {
        PixelShader = compile ps_3_0 PSHigh();
    }
}
