// ============================================================================
//OniDomainCharge.fx 鬼斩领域起手蓄力 鬼灭式血色丝带螺旋汇聚
//两 technique：TechBase(AlphaBlend) 写带方向光照的血色丝带本体；TechHighlight(Additive) 写镜面高光+脉冲+反光
//质感核心：每条丝带按圆管解析法线做 Lambert 漫反射 + Blinn-Phong 高光，旋转时高光扫过表面 → 湿润立体
//Seam 规则：法线用解析梯度(rhat/that+local)；flow/pulse/wet 用 logR；底雾用笛卡尔；角向变化用 sin(整数·phi)，禁止 hash(armId)
//中心方形面片；ps_3_0；s1=Extra_193 灰度(未直接采样，保留兼容)
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

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

//固定方向光(屏幕左上偏向观察者)，丝带旋转时高光自然扫过
static const float3 LIGHT_DIR = float3(-0.42, -0.52, 0.74);

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
//  local 跨 ±π 连续；mask/cyl 仅依赖 local 连续
//  armId 是 phi 的不连续商，只在函数内部用来 wrap 出 local，绝不外泄到 struct/hash
//  角向变化经 angVar = sin(整数·phi) 输出(seam-safe)，而非 hash(armId)
//==========================================================================
struct Ribbon {
    float mask;     //丝带遮罩 0~1
    float cyl;      //圆柱体厚度因子 0~1
    float local;    //带符号距丝带中心的局部距离
    float angVar;   //角向变化 0~1，由 sin(phi)/sin(2phi) 合成(整数 k，seam-safe)。禁止改用 hash(armId)：armId 是 phi 不连续商，喂 hash 必 seam
};

Ribbon spiralRibbon6(float r, float theta, float twist, float rotation, float thickness) {
    const float armN = 6.0;
    const float wrap = TAU / armN;
    float logR = log(max(r * 6.0, 0.01) + 1.0);
    float phi = logR * twist - theta - rotation;
    float armId = floor(phi / wrap + 0.5);    //仅内部用于 wrap 出连续 local，绝不外泄/喂 hash
    float local = phi - armId * wrap;
    float d = abs(local);
    float mask = exp(-(d * d) / (thickness * thickness));
    float n = saturate(1.0 - d / max(thickness, 1e-4));
    float cyl = sqrt(max(0.0, 1.0 - (1.0 - n) * (1.0 - n)));
    Ribbon o;
    o.mask = mask;
    o.cyl = cyl;
    o.local = local;
    //角向变化：sin(phi)/sin(2phi) 整数 k → 跨 ±π 连续；归一到 0~1
    o.angVar = saturate(0.5 + 0.35 * sin(phi) + 0.15 * sin(2.0 * phi + 1.7));
    return o;
}

Ribbon spiralRibbon10(float r, float theta, float twist, float rotation, float thickness) {
    const float armN = 10.0;
    const float wrap = TAU / armN;
    float logR = log(max(r * 6.0, 0.01) + 1.0);
    float phi = logR * twist - theta - rotation;
    float armId = floor(phi / wrap + 0.5);    //仅内部用于 wrap 出连续 local，绝不外泄/喂 hash
    float local = phi - armId * wrap;
    float d = abs(local);
    float mask = exp(-(d * d) / (thickness * thickness));
    float n = saturate(1.0 - d / max(thickness, 1e-4));
    float cyl = sqrt(max(0.0, 1.0 - (1.0 - n) * (1.0 - n)));
    Ribbon o;
    o.mask = mask;
    o.cyl = cyl;
    o.local = local;
    o.angVar = saturate(0.5 + 0.35 * sin(phi) + 0.15 * sin(2.0 * phi + 1.7));
    return o;
}

//==========================================================================
//丝带光照：把丝带视作沿螺旋走向的圆管，解析求表面法线
//  ∇phi = twist*dLogR*rhat - that/r (解析、seam-free)，给出"跨丝带"屏幕方向
//  圆管横截面法线：沿 crossDir 倾斜 u=local/thickness，朝观察者 sqrt(1-u²)
//返回 x=lambert, y=specular, z=fresnel(边缘)
//==========================================================================
float3 ribbonLight(float2 p, float r, float twist, float local, float thickness, float bump) {
    float rr = max(r, 1e-4);
    float2 rhat = p / rr;
    float2 that = float2(-p.y, p.x) / rr;          //单位切向
    float dLogR = 6.0 / (r * 6.0 + 1.0);
    float2 gradPhi = twist * dLogR * rhat - that / rr;
    float2 crossDir = normalize(gradPhi + float2(1e-5, 1e-5));

    float u = clamp(local / max(thickness, 1e-4), -1.0, 1.0);
    float nCross = clamp(u + bump * 0.7, -1.3, 1.3);
    float nUp = sqrt(saturate(1.0 - saturate(nCross * nCross)));
    float3 N = normalize(float3(crossDir * nCross, nUp + 1e-4));

    float3 H = normalize(LIGHT_DIR + float3(0.0, 0.0, 1.0));
    float diff = saturate(dot(N, LIGHT_DIR));
    float spec = pow(saturate(dot(N, H)), 32.0);
    float fres = pow(1.0 - saturate(N.z), 3.0);    //边缘(法线侧倾)处高
    return float3(diff, spec, fres);
}

//多档血色渐变：深渊黑红 → 暗血 → 血肉 → 鲜血，由光照 shade 驱动
float3 bloodRamp(float shade) {
    float3 c = uBloodDark * 0.32;
    c = lerp(c, uBloodDark, smoothstep(0.0, 0.32, shade));
    c = lerp(c, uBloodFlesh, smoothstep(0.28, 0.70, shade));
    c = lerp(c, uBloodBright, smoothstep(0.72, 1.05, shade) * 0.7);
    return c;
}

//A 域扭曲血雾场 - 笛卡尔 UV + 整体缓慢旋转，无 seam
float bloodField(float2 p, float r, float time) {
    float ang = time * 0.05;
    float ca = cos(ang), sa = sin(ang);
    float2 rotP = float2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);

    float2 warpUV = rotP * 1.6 + time * float2(0.05, -0.03);
    float2 w = float2(
        fbm3(warpUV),
        fbm3(warpUV + float2(5.2, 1.3))
    ) - 0.5;

    float2 mainUV = rotP * 1.9 + w * 1.4 + time * float2(0.08, -0.05);
    float density = fbm3(mainUV);
    density = smoothstep(0.22, 0.85, density);

    float core = smoothstep(1.0, 0.0, r);
    density = saturate(density * (0.35 + core * 0.65) + core * core * 0.45);
    return density;
}

//B 沿丝带切向流动血液纹理(域扭曲，更有机)；参数用 logR 规避 seam
float ribbonFlow(float logR, float local, float time, float seed) {
    float warp = vNoise(float2(logR * 0.9 - time * 0.2, local * 4.0 + seed)) - 0.5;
    float2 fUV = float2(logR * 1.6 + warp * 1.1 + time * 0.34 + seed * 3.13,
                        local * 10.0);
    float flow = fbm3(fUV * 1.4);
    flow = smoothstep(0.26, 0.9, flow);
    return saturate(flow);
}

float ribbonBump(float logR, float local, float time) {
    return vNoise(float2(logR * 2.4 + time * 0.32, local * 16.0)) - 0.5;
}

//湿润火花：沿丝带的高频细闪，叠在镜面区 → 血面碎光(seam-safe，无 theta)
float wetSparkle(float logR, float local, float time) {
    float s = vNoise(float2(logR * 9.0 + time * 1.6, local * 42.0));
    s = pow(saturate(s - 0.62) * 2.6, 2.0);
    return s;
}

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
//Base Pass：AlphaBlend 写带方向光照的血色丝带本体
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
    float logR_raw = log(max(r * 6.0, 0.01) + 1.0);

    //=== 域底 ===
    float field = bloodField(p, r, time);

    //=== 主丝带(6 条) ===
    float twist1 = lerp(3.4, 5.5, prog);
    float th1 = lerp(0.21, 0.14, prog);
    Ribbon rib1 = spiralRibbon6(r, theta, twist1, uRotation, th1);
    float av1 = rib1.angVar;                                    //角向变化(seam-safe)
    float flow1 = ribbonFlow(logR_raw * twist1, rib1.local, time, seed + av1 * 4.0);
    float bump1 = ribbonBump(logR_raw * twist1, rib1.local, time);
    float3 lit1 = ribbonLight(p, r, twist1, rib1.local, th1, bump1);

    //=== 副丝带(10 条) ===
    float twist2 = lerp(4.4, 7.0, prog);
    float th2 = lerp(0.07, 0.05, prog);
    Ribbon rib2 = spiralRibbon10(r, theta, twist2, uRotation * 1.22 + 1.7, th2);
    float av2 = rib2.angVar;
    float flow2 = ribbonFlow(logR_raw * twist2 + 11.7, rib2.local, time, seed + 0.4 + av2 * 4.0);
    float3 lit2 = ribbonLight(p, r, twist2, rib2.local, th2, 0.0);

    //=== 演绎 gate / 径向包络 ===
    float ribGateMain = smoothstep(0.05, 0.40, prog);
    float ribGateSub  = smoothstep(0.30, 0.70, prog);
    float shrink = lerp(1.0, 0.72, prog);
    float radialEnv = smoothstep(1.0, 0.55, r / shrink) * smoothstep(0.06, 0.18, r);

    float ribMain = rib1.mask * radialEnv * ribGateMain;
    float ribSub  = rib2.mask * radialEnv * ribGateSub;

    //=== 主丝带本体着色：环境光 + 漫反射，再乘粘性流动条纹与逐丝带亮度 ===
    float ambient = 0.22;
    float shade1 = ambient + lit1.x * (1.0 - ambient);
    shade1 *= 0.78 + flow1 * 0.44;            //粘性流动明暗
    shade1 *= 0.82 + av1 * 0.36;              //逐丝带亮度差
    float3 body1 = bloodRamp(shade1);
    body1 *= 1.0 - lit1.z * 0.42;             //圆管边缘暗化(AO，增强立体)

    //=== 副丝带：更亮更细的高光线 ===
    float shade2 = ambient + lit2.x * (1.0 - ambient);
    shade2 *= 0.85 + av2 * 0.30;
    float3 body2 = bloodRamp(shade2 * 1.05);

    //=== 血池(略带血肉色填充) ===
    float pool = bloodPool(r, prog, beat);

    //=== 颜色合成 ===
    float3 col = lerp(uBloodDark * 0.40, uBloodDark, field);
    //丝带之间的接触暗化：底色在丝带边缘外侧再压暗一点，丝带像"浮"在上面
    col *= 1.0 - saturate(ribMain * 0.25);
    col = lerp(col, uBloodFlesh * 0.82, pool * 0.78);
    col = lerp(col, body1, ribMain);
    col = lerp(col, body2, ribSub * 0.7);

    float a = 0.0;
    a += field * 0.45;
    a += ribMain * 0.95;
    a += ribSub * 0.65;
    a += pool * 0.85;
    a = saturate(a);
    a *= uOpacity * uIntensity * edgeFade * vertexColor.a;
    col *= vertexColor.rgb;
    return float4(col * a, a);
}

//==========================================================================
//Highlight Pass：Additive 写镜面高光、湿润火花、脉冲、前沿、中心爆光、血池穹顶光
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
    float seed = uSeed;
    float logR_raw = log(max(r * 6.0, 0.01) + 1.0);

    float twist1 = lerp(3.4, 5.5, prog);
    float th1 = lerp(0.21, 0.14, prog);
    Ribbon rib1 = spiralRibbon6(r, theta, twist1, uRotation, th1);
    float bump1 = ribbonBump(logR_raw * twist1, rib1.local, time);
    float3 lit1 = ribbonLight(p, r, twist1, rib1.local, th1, bump1);

    float twist2 = lerp(4.4, 7.0, prog);
    float th2 = lerp(0.07, 0.05, prog);
    Ribbon rib2 = spiralRibbon10(r, theta, twist2, uRotation * 1.22 + 1.7, th2);
    float3 lit2 = ribbonLight(p, r, twist2, rib2.local, th2, 0.0);

    float shrink = lerp(1.0, 0.72, prog);
    float radialEnv = smoothstep(1.0, 0.55, r / shrink) * smoothstep(0.06, 0.18, r);

    float pulseGate = smoothstep(0.45, 0.85, prog);
    float wetGate   = smoothstep(0.35, 0.80, prog);
    float specGate  = smoothstep(0.18, 0.55, prog);

    //=== 镜面高光(湿润立体核心)：旋转时沿丝带扫过 ===
    float spk1 = wetSparkle(logR_raw * twist1, rib1.local, time);
    float spec1 = lit1.y * rib1.mask * radialEnv * (0.85 + spk1 * 1.2);
    float spec2 = lit2.y * rib2.mask * radialEnv * 0.7;
    float specAll = (spec1 + spec2) * specGate * (0.9 + beat * 0.5);

    //=== 血液脉冲 ===
    float pulse1 = bloodPulse(logR_raw, time, prog, beat) * rib1.mask * radialEnv;
    float pulse2 = bloodPulse(logR_raw + 1.7, time + 0.7, prog, beat) * rib2.mask * radialEnv * 0.55;
    float pulseAll = saturate(pulse1 + pulse2) * pulseGate;

    //=== 湿润前沿 ===
    float wet1 = wetFront(logR_raw, rib1.local, time) * radialEnv;
    float wet2 = wetFront(logR_raw + 0.9, rib2.local, time + 0.5) * radialEnv * 0.6;
    float wetAll = saturate(wet1 + wet2) * wetGate;

    //=== 中心爆光 ===
    float core = bloodCore(r, prog, beat);

    //=== 血池穹顶高光：把血池当半球，固定光在偏上位置形成湿亮斑 ===
    float poolR = lerp(0.10, 0.22, prog);
    float pn = saturate(r / max(poolR, 1e-3));
    float2 dome = p / max(poolR, 1e-3);
    float domeZ = sqrt(saturate(1.0 - dot(dome, dome)));
    float3 domeN = normalize(float3(dome, domeZ + 1e-4));
    float3 Hd = normalize(LIGHT_DIR + float3(0.0, 0.0, 1.0));
    float poolSpec = pow(saturate(dot(domeN, Hd)), 18.0)
                   * (1.0 - smoothstep(0.7, 1.0, pn)) * bloodPool(r, prog, beat);

    //=== Additive 颜色合成 ===
    float3 col = lerp(uBloodBright, uBloodGleam, 0.65) * specAll * 0.9;       //镜面偏亮反光
    col += uBloodBright * pulseAll * (0.70 + beat * 0.55);
    col += lerp(uBloodBright, uBloodGleam, 0.55) * wetAll * 0.85;
    col += uBloodGleam * core * 1.10;
    col += lerp(uBloodBright, uBloodGleam, 0.5) * poolSpec * 0.8;

    if (uPulse > 0.01) {
        col += uBloodBright * uPulse * 0.50 * radialEnv;
    }

    float a = saturate(specAll * 0.8 + pulseAll * 0.65 + wetAll * 0.85
                     + core * 1.1 + poolSpec * 0.7);
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
