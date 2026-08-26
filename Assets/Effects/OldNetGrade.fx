// ============================================================================
//OldNetGrade.fx 旧网世界内分级 + 地形数字化提边（CyberspaceField L3 精简变体）
//砍掉圆域裁剪/层权重/takeover 机器/实体标记环，只留三件事：
//  ①场景轻分级：轻度去饱和 + 暗部红移（数字尸骸的色温，强度随带内腐化）
//  ②地形轮廓数字化：4-tap 亮度梯度提边 + 两档量化 + 世界坐标虚线 + 角点标记
//    （"世界本身被重新渲染"的黑墙手法，常驻低强度：墙脚~0.15 → 衰减区~0.45）
//  ③疯域故障脉冲 uGlitch：行片 UV 错位 + 色差分离 + 亮度抖动（事件短促驱动）
//纯 hash 零采样噪声；直线算术无动态分支；整屏重写输出 alpha=1
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uIntensity;       //在场 0~1
float uCorrupt;         //带内腐化 0~1（墙脚0→衰减区1）
float uGlitch;          //疯域故障脉冲 0~1（多写者取 max 合成：疯域/劣化撕裂/外部贡献）
float uStrain;          //链路劣化 0~1（RAM 见底渐层）：屏幕边缘红移+色差，屏心恒不动
float4 uWatch;          //网的注视：x=注视度 0~4 y=跃迁脉冲 0~1 z=T4边缘脉动幅度 w=备用
float uDepth;           //深层剖面 0~1：色阶量化+抖动+暖灰偏（纵向场，地表以上恒为 0）
float2 uTideFront;      //黑墙大潮：x=潮锋世界x（px，无潮=大负值）y=锋后强度 0~1
float2 uScreenSize;     //屏幕像素
float2 screenPosition;  //世界视口原点（px，含缩放换算）
float2 worldViewSize;   //世界视口尺寸（px）

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PSGrade(float2 coords : TEXCOORD0) : COLOR0
{
    float2 pxc = coords * uScreenSize;

    //═══ ② 链路劣化：屏幕边缘环带权重（约 12% 环带，二次衰减，屏心恒为 0——不是全屏滤镜） ═══
    float2 ec = abs(coords - 0.5) * 2.0;
    float border = smoothstep(0.76, 1.0, max(ec.x, ec.y));
    border *= border;

    //═══ 故障切片：行片 UV 错位 + 色差分离（uGlitch=0 时恒等输入） ═══
    //劣化色差折进同组采样：边缘 RGB 错位幅度 uStrain×2px，零新增取样
    float rowId = floor(coords.y * uScreenSize.y / 3.0);
    float rowRand = hash21(float2(rowId, floor(uTime * 24.0)));
    float slice = step(0.86, rowRand) * (rowRand - 0.5) * uGlitch * 0.030;
    float2 suv = coords + float2(slice, 0.0);
    float ca = uGlitch * 0.0022 + uStrain * border * (2.0 / uScreenSize.x);
    float3 col;
    col.r = tex2D(uImage0, suv + float2(ca, 0.0)).r;
    col.g = tex2D(uImage0, suv).g;
    col.b = tex2D(uImage0, suv - float2(ca, 0.0)).b;

    //═══ 场景分级：轻度去饱和 + 暗部红移 ═══
    float3 lumW = float3(0.299, 0.587, 0.114);
    float lum = dot(col, lumW);
    float3 shadowTint = float3(0.16, 0.03, 0.05);
    float3 midTint = float3(0.70, 0.30, 0.26);
    float3 graded = lerp(shadowTint * lum * 2.2, midTint * lum * 1.35,
        smoothstep(0.12, 0.55, lum));
    float gradeAmt = uIntensity * (0.16 + 0.22 * uCorrupt);
    col = lerp(col, graded, gradeAmt);

    //═══ ② 链路劣化红移：视野边缘先坏，"眼睁睁看着链路断掉"的前奏 ═══
    col = lerp(col, col * float3(1.32, 0.70, 0.64), border * uStrain * uIntensity);

    //═══ ⑧ 深层剖面：越深协议越旧，世界以更低保真度渲染自己 ═══
    //色阶量化（64→10 级）+ hash 抖动掩带纹 + 色温向陈旧暖灰偏；
    //saturate(uDepth×6) 混权保证地表以上恒等输入（纵向场不是滤镜）。
    //提边梯度在下方直接采原图（uImage0），量化不污染提边——实施顺序契约
    float postMix = saturate(uDepth * 6.0);
    float levels = lerp(64.0, 10.0, uDepth);
    float dith = (hash21(pxc * 0.53 + floor(uTime * 7.0)) - 0.5) * (0.035 * uDepth);
    float3 colQ = floor((col + dith) * levels) / levels;
    colQ = lerp(colQ, dot(colQ, lumW) * float3(1.08, 0.97, 0.85), 0.12 * uDepth);
    col = lerp(col, colQ, postMix);

    //═══ 地形轮廓数字化（CyberspaceField L3 同款 4-tap，eps=2 世界px） ═══
    float2 epsX = float2(2.0 / worldViewSize.x, 0.0);
    float2 epsY = float2(0.0, 2.0 / worldViewSize.y);
    float lumL = dot(tex2D(uImage0, coords - epsX).rgb, lumW);
    float lumR = dot(tex2D(uImage0, coords + epsX).rgb, lumW);
    float lumU = dot(tex2D(uImage0, coords - epsY).rgb, lumW);
    float lumD = dot(tex2D(uImage0, coords + epsY).rgb, lumW);
    float gx = lumR - lumL;
    float gy = lumD - lumU;
    float gmag = sqrt(gx * gx + gy * gy);
    float edgeRaw = smoothstep(0.13, 0.45, gmag);
    //两档量化（数据感）+ 世界坐标对角虚线 + 双轴同强角点标记
    float edgeQ = smoothstep(0.18, 0.30, edgeRaw) * 0.55
                + smoothstep(0.58, 0.72, edgeRaw) * 0.45;
    float2 worldPos = screenPosition + coords * worldViewSize;
    float dashE = 0.70 + 0.30 * step(0.5, frac((worldPos.x + worldPos.y) * 0.031));
    float cornerMark = smoothstep(0.16, 0.34, min(abs(gx), abs(gy)));
    float edgeAmt = uIntensity * (0.15 + 0.30 * uCorrupt) * (1.0 + uGlitch * 0.8);
    //⑥ 大潮锋后：锋面以西的地形轮廓被红光重描 + 亮度轻抬（世界坐标一次 step 比较）
    float tideMask = step(worldPos.x, uTideFront.x) * uTideFront.y;
    float3 cEdge = lerp(float3(0.85, 0.22, 0.10), float3(1.05, 0.13, 0.06), tideMask);
    col *= 1.0 + tideMask * 0.05;
    col += cEdge * (edgeQ * dashE + cornerMark * 0.6) * edgeAmt * (1.0 + tideMask * 0.6);

    //═══ ③ 网的注视：取景框角标 + 记录点 + T4 边缘脉动（角标只占四角 <2% 面积，明确不是滤镜） ═══
    float2 q = min(pxc, uScreenSize - pxc);            //到最近角的双轴像素距离（四角对称合一）
    float wBand = saturate((uWatch.x - 1.8) / 1.4);    //角标 [1.8,3.2] 淡入（T2 网开始记录你）
    float m = lerp(74.0, 40.0, wBand);                 //随注视收紧内移
    float armL = lerp(24.0, 40.0, wBand);
    float armH = step(abs(q.y - m), 1.1) * step(m, q.x) * step(q.x, m + armL);
    float armV = step(abs(q.x - m), 1.1) * step(m, q.y) * step(q.y, m + armL);
    float bracket = max(armH, armV);
    //阈下压迫：极淡 0.05~0.18，跃迁脉冲闪现一拍再落定
    float bracketA = lerp(0.05, 0.18, wBand) * wBand * (1.0 + uWatch.y * 2.5);
    col += float3(0.85, 0.16, 0.10) * bracket * bracketA * uIntensity;

    //记录点：右上角 2px，0.8s 周期眨动（相位偏移去同相）；[2.8,4] 淡入（T3 红色记录点开始眨）
    float dBand = saturate((uWatch.x - 2.8) / 1.2);
    float2 dp = float2(uScreenSize.x - m - 9.0, m + 9.0);
    float blink = step(frac(uTime * 1.25 + 0.37), 0.62);
    float dotM = step(max(abs(pxc.x - dp.x), abs(pxc.y - dp.y)), 2.0);
    col += float3(0.95, 0.10, 0.07) * dotM * blink * dBand * 0.55 * uIntensity;

    //T4 清剿波：边缘常驻慢脉动红晕（单元素慢正弦，非全局同相场；幅度含被追数加权）
    float slowPulse = 0.5 + 0.5 * sin(uTime * 2.4);
    col += float3(0.40, 0.045, 0.035) * border * uWatch.z
        * (0.30 + 0.45 * slowPulse) * uIntensity;

    //═══ 故障期整体亮度轻抖 ═══
    col *= 1.0 + (hash21(float2(floor(uTime * 30.0), 7.7)) - 0.5) * uGlitch * 0.12;

    return float4(col, 1.0);
}

technique TechGrade
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSGrade();
    }
}
