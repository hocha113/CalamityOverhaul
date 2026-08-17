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
float uGlitch;          //疯域故障脉冲 0~1
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
    //═══ 故障切片：行片 UV 错位 + 色差分离（uGlitch=0 时恒等输入） ═══
    float rowId = floor(coords.y * uScreenSize.y / 3.0);
    float rowRand = hash21(float2(rowId, floor(uTime * 24.0)));
    float slice = step(0.86, rowRand) * (rowRand - 0.5) * uGlitch * 0.030;
    float2 suv = coords + float2(slice, 0.0);
    float ca = uGlitch * 0.0022;
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
    float3 cEdge = float3(0.85, 0.22, 0.10);
    col += cEdge * (edgeQ * dashE + cornerMark * 0.6) * edgeAmt;

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
