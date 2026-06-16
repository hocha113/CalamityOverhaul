// ============================================================================
//OniDomainCharge.fx 鬼斩领域起手蓄力 鬼灭式血色丝带螺旋汇聚
//两 technique：TechBase(AlphaBlend) 写暗血底+圆柱明暗丝带；TechHighlight(Additive) 写鲜血脉冲+反光
//Seam 修复关键：所有沿丝带方向动态(flow/bump/pulse/wet)改用 logR(单调连续)；
//              底色雾用旋转后的笛卡尔 UV(完全规避 atan2 在 ±π 处的不连续)
//              ribbon mask 仅依赖 phi mod wrap，armN 整数确保跨 ±π 连续
//中心方形面片；ps_3_0；s1=Extra_193 Voronoi 灰度
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //Extra_193 灰度

float uTime;
float uProgress;        //0~1 演绎进度
float uIntensity;
float uOpacity;
float uSeed;
float uPulse;
float uBeat;            //心跳爆发 0~1
float uRotation;        //C# 累积的螺旋旋转角

float3 uBloodDark;
float3 uBloodFlesh;
float3 uBloodBright;
float3 uBloodGleam;

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
//Ribbon SDF：armN 必须为编译期整数常量
//  局部相位 local 跨 ±π 时连续(phi 跳 2π = armN*wrap)
//  mask 仅依赖 local：跨 ±π 连续
//  注意：phi 自身在跨 ±π 时跳变 2π，sin(k*phi) 或 vNoise(k*phi,...) 中 k 非整数会
//      引入 seam；本 shader 严禁外部代码直接使用 phi 做 sin/noise
//==========================================================================
struct Ribbon {
    float mask;     //丝带遮罩 0~1
    float cyl;      //圆柱体厚度因子 0~1
    float local;    //带符号距丝带中心的局部距离
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
    return o;
}

//A 域扭曲血雾场 - 笛卡尔 UV + 整体缓慢旋转，完全规避 atan2 seam
float bloodField(float2 p, float r, float time) {
    //整体场缓慢旋转(刚体仿射，UV 始终连续，无 seam)
    float ang = time * 0.05;
    float ca = cos(ang), sa = sin(ang);
    float2 rotP = float2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);

    //大尺度扰动场
    float2 warpUV = rotP * 1.6 + time * float2(0.05, -0.03);
    float2 w = float2(
        fbm3(warpUV),
        fbm3(warpUV + float2(5.2, 1.3))
    ) - 0.5;

    //主血浆密度
    float2 mainUV = rotP * 1.9 + w * 1.4 + time * float2(0.08, -0.05);
    float density = fbm3(mainUV);
    density = smoothstep(0.22, 0.85, density);

    float core = smoothstep(1.0, 0.0, r);
    density = saturate(density * (0.35 + core * 0.65) + core * core * 0.45);
    return density;
}

//B 沿丝带切向流动血液纹理
//  参数用 logR(沿丝带方向单调)而非 phi，规避 seam
float ribbonFlow(float logR, float local, float time, float seed) {
    float2 fUV = float2(logR * 1.6 + time * 0.34 + seed * 3.13,
                        local * 10.0);
    float flow = fbm3(fUV * 1.4);
    flow = smoothstep(0.28, 0.88, flow);
    return saturate(flow);
}

//C 丝带表面湿润高频起伏 法线扰动
float ribbonBump(float logR, float local, float time) {
    return vNoise(float2(logR * 2.4 + time * 0.32, local * 16.0)) - 0.5;
}

//D 血液脉冲波(沿 logR 涌向中心，logR 增大方向 = 向心)
//  k 取整数倍可使 sin 在 logR 任意值都连续；用 logR 保证完全无 seam
float bloodPulse(float logR, float time, float prog, float beat) {
    float p1 = sin(logR * 7.0 - time * 2.8);
    float p2 = sin(logR * 11.0 - time * 3.6 + 1.3);
    float p3 = sin(logR * 5.0 - time * 1.8 + 2.7);
    p1 = smoothstep(0.80, 0.99, p1);
    p2 = smoothstep(0.76, 0.96, p2);
    p3 = smoothstep(0.74, 0.94, p3);
    float pulse = saturate(p1 + p2 * 0.7 + p3 * 0.55);
    return pulse * ((0.30 + prog * 0.55) + beat * 0.85);
}

//E 湿润前沿 丝带中线尖锐高光，移动方向同上
float wetFront(float logR, float local, float time) {
    float wave = sin(logR * 14.0 - time * 3.5);
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
//Base Pass：AlphaBlend 写暗血底色+丝带阴影
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
    //沿丝带方向的统一连续参数(每个丝带不同 twist 故 logR 实效不同)
    float logR_raw = log(max(r * 6.0, 0.01) + 1.0);

    //=== 域底(笛卡尔，无 seam) ===
    float field = bloodField(p, r, time);

    //=== 主丝带(6 条) ===
    float twist1 = lerp(3.4, 5.5, prog);
    float th1 = lerp(0.21, 0.14, prog);
    Ribbon rib1 = spiralRibbon6(r, theta, twist1, uRotation, th1);
    float flow1 = ribbonFlow(logR_raw * twist1, rib1.local, time, seed);
    float bump1 = ribbonBump(logR_raw * twist1, rib1.local, time);

    //=== 副丝带(10 条) ===
    float twist2 = lerp(4.4, 7.0, prog);
    float th2 = lerp(0.07, 0.05, prog);
    Ribbon rib2 = spiralRibbon10(r, theta, twist2, uRotation * 1.22 + 1.7, th2);
    float flow2 = ribbonFlow(logR_raw * twist2 + 11.7, rib2.local, time, seed + 0.4);

    //=== 演绎 gate ===
    float ribGateMain = smoothstep(0.05, 0.40, prog);
    float ribGateSub  = smoothstep(0.30, 0.70, prog);

    float shrink = lerp(1.0, 0.72, prog);
    float radialEnv = smoothstep(1.0, 0.55, r / shrink) * smoothstep(0.06, 0.18, r);

    float ribMain = rib1.mask * (0.55 + flow1 * 0.60) * radialEnv * ribGateMain;
    float ribSub  = rib2.mask * (0.35 + flow2 * 0.50) * radialEnv * ribGateSub;
    float ribAll  = saturate(ribMain + ribSub * 0.7);

    //=== 圆柱明暗 ===
    float lit1 = saturate(0.35 + rib1.cyl * 0.65 + bump1 * 0.30);
    float lit2 = saturate(0.30 + rib2.cyl * 0.55);
    float w1 = rib1.mask * ribGateMain;
    float w2 = rib2.mask * ribGateSub;
    float litMix = (lit1 * w1 + lit2 * w2 * 0.6) / max(w1 + w2 * 0.6, 1e-3);

    float pool = bloodPool(r, prog, beat);

    //=== 颜色合成 ===
    float3 col = lerp(uBloodDark * 0.42, uBloodDark, field);
    col = lerp(col, uBloodFlesh * 0.85, pool * 0.80);
    float3 ribCol = lerp(uBloodDark * 0.55, uBloodFlesh, litMix);
    col = lerp(col, ribCol, ribAll);

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
    float beat = uBeat;
    float logR_raw = log(max(r * 6.0, 0.01) + 1.0);

    float twist1 = lerp(3.4, 5.5, prog);
    float th1 = lerp(0.21, 0.14, prog);
    Ribbon rib1 = spiralRibbon6(r, theta, twist1, uRotation, th1);

    float twist2 = lerp(4.4, 7.0, prog);
    float th2 = lerp(0.07, 0.05, prog);
    Ribbon rib2 = spiralRibbon10(r, theta, twist2, uRotation * 1.22 + 1.7, th2);

    float shrink = lerp(1.0, 0.72, prog);
    float radialEnv = smoothstep(1.0, 0.55, r / shrink) * smoothstep(0.06, 0.18, r);

    float pulseGate = smoothstep(0.45, 0.85, prog);
    float wetGate   = smoothstep(0.35, 0.80, prog);

    //血液脉冲：用 logR 沿丝带方向涌动，完全无 seam
    float pulse1 = bloodPulse(logR_raw, time, prog, beat) * rib1.mask * radialEnv;
    float pulse2 = bloodPulse(logR_raw + 1.7, time + 0.7, prog, beat) * rib2.mask * radialEnv * 0.55;
    float pulseAll = saturate(pulse1 + pulse2) * pulseGate;

    //湿润前沿
    float wet1 = wetFront(logR_raw, rib1.local, time) * radialEnv;
    float wet2 = wetFront(logR_raw + 0.9, rib2.local, time + 0.5) * radialEnv * 0.6;
    float wetAll = saturate(wet1 + wet2) * wetGate;

    float core = bloodCore(r, prog, beat);

    float3 col = uBloodBright * pulseAll * (0.70 + beat * 0.55);
    col += lerp(uBloodBright, uBloodGleam, 0.55) * wetAll * 0.85;
    col += uBloodGleam * core * 1.10;

    if (uPulse > 0.01) {
        col += uBloodBright * uPulse * 0.50 * radialEnv;
    }

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
