// ============================================================================
// CyberFreezeCage.fx 赛博冻结六角能量罩
// 圆形六角囚笼+脉冲辉光；s0 白图；AlphaBlend
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;      // 冻结进度 0(刚冻结)→1(即将解冻)
float formProgress;  // 笼体形成进度 0→1 (前30帧快速完成)
float seed;          // 每个实体独立随机种子

// 工具函数

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// 六角距离函数
float hexDist(float2 p)
{
    p = abs(p);
    return max(dot(p, normalize(float2(1.0, 1.7320508))), p.x);
}

// 六角网格: 返回 float4(edgeDist, cellHash, cellCenter.x, cellCenter.y)
float4 hexGrid(float2 uv)
{
    float sqrt3 = 1.7320508;
    float2 r = float2(1.0, sqrt3);
    float2 h = r * 0.5;

    // 加大偏移避免 fmod 负值问题
    float2 uvOff = uv + float2(1000.0, 1000.0);
    float2 a = fmod(uvOff, r) - h;
    float2 b = fmod(uvOff - h, r) - h;

    float2 gv;
    if (length(a) < length(b))
        gv = a;
    else
        gv = b;

    float edgeDist = hexDist(gv);
    float2 id = uv - gv;

    return float4(edgeDist, hash21(id + float2(500.0, 500.0)), id.x, id.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    // =
    // UV映射: [0,1] → [-1,1] 中心坐标系
    // =
    float2 uv = (coords - 0.5) * 2.0;
    float dist = length(uv);
    float angle = atan2(uv.y, uv.x);

    // =
    // 笼体边界
    // =
    float maxRadius = 0.82;
    float currentRadius = maxRadius * formProgress;

    // 解冻碎裂阶段 (progress > 0.82)
    float thawPhase = smoothstep(0.82, 1.0, progress);

    // 笼体主遮罩 (柔化边缘)
    float edgeSoft = 0.04;
    float cageMask = 1.0 - smoothstep(currentRadius - edgeSoft, currentRadius + edgeSoft * 0.3, dist);

    // 外辉光范围检测: 超出笼体外0.22仍绘制辉光
    if (dist > currentRadius + 0.22 && cageMask < 0.001)
        return float4(0, 0, 0, 0);

    // =
    // 六角网格生成
    // =
    float hexScale = 4.5;
    float2 hexUV = uv * hexScale;
    float4 hex = hexGrid(hexUV);
    float hexEdge = hex.x;
    float hexId = hex.y;
    float2 hexCtr = hex.zw;

    // 六角边缘线（加宽加亮）
    float edgeW = 0.13;
    float hexLine = 1.0 - smoothstep(edgeW - 0.04, edgeW + 0.01, hexEdge);

    // =
    // 色彩调板 (暗红晶/黑墙风格)
    // =
    float3 edgeCrimson  = float3(1.0, 0.12, 0.30);
    float3 edgeMaroon   = float3(0.70, 0.05, 0.20);
    float3 cageBaseDark = float3(0.18, 0.02, 0.07);
    float3 flashBright  = float3(1.0, 0.6, 0.55);
    float3 flowBright   = float3(1.0, 0.45, 0.45);

    // =
    // 格子可见性 —— 形成 / 碎裂动画
    // =
    float cellDistNorm = length(hexCtr) / (hexScale * maxRadius);

    // 形成: 从中心向外逐层展开
    float cellFormDelay = cellDistNorm;
    float cellAppear = smoothstep(cellFormDelay - 0.08, cellFormDelay + 0.05, formProgress);

    // 碎裂: 从外向内溶解 + 随机偏移制造不规则碎裂
    float shatterNoise = hash11(hexId * 29.0 + seed * 5.0);
    float shatterThreshold = thawPhase * (1.2 + shatterNoise * 0.6) - (1.0 - cellDistNorm) * 0.7;
    float cellShatter = 1.0 - saturate(shatterThreshold);

    float cellVisible = cellAppear * cellShatter;

    // =
    // 能量脉冲: 格子呼吸 + 随机闪烁
    // =
    float cellPhase = hash11(hexId * 73.1 + seed * 17.0) * 6.2832;
    float pulse = 0.35 + 0.65 * sin(uTime * 2.5 + cellPhase);
    pulse *= pulse;

    float flickerTime = floor(uTime * 6.0 + seed * 30.0);
    float cellFlicker = step(0.72, hash11(hexId * 41.3 + flickerTime));
    float cellGlow = pulse * 0.5 + cellFlicker * 0.7;

    // =
    // 数据流纹: 沿六角边缘流动的亮线
    // =
    float flowSpeed = uTime * 2.5;
    float flowParam = frac(hexCtr.x * 0.37 + hexCtr.y * 0.53 + flowSpeed + seed);
    float flowPulse = smoothstep(0.0, 0.12, flowParam) * (1.0 - smoothstep(0.12, 0.30, flowParam));
    float dataFlow = hexLine * flowPulse * 2.5;

    // =
    // 合成六角网格颜色
    // =
    float3 gridColor = lerp(edgeMaroon, edgeCrimson, pulse) * hexLine * 1.5;
    float3 fillColor = cageBaseDark * (1.0 - hexLine) * cellGlow * 1.2;
    float3 flowColor = flowBright * dataFlow * 1.3;

    // =
    // 边缘辉光: 笼体边界内侧亮带 + 外散光晕
    // =
    float edgeGlowBand = smoothstep(currentRadius - 0.18, currentRadius - 0.01, dist)
                       * (1.0 - smoothstep(currentRadius - 0.01, currentRadius + 0.03, dist));
    float edgePulse = 0.55 + 0.45 * sin(uTime * 3.0 + angle * 2.5 + seed * 5.0);
    float3 edgeGlowInner = edgeCrimson * edgeGlowBand * edgePulse * 1.4;

    // 外散辉光 (笼体外部指数衰减)
    float outerDist = max(0.0, dist - currentRadius);
    float outerGlow = exp(-outerDist * 8.0) * step(0.001, outerDist) * formProgress;
    float3 outerGlowColor = edgeMaroon * outerGlow * 1.2 * edgePulse;

    // =
    // 格子形成时的闪白
    // =
    float formFlash = smoothstep(cellFormDelay - 0.01, cellFormDelay, formProgress)
                    * (1.0 - smoothstep(cellFormDelay, cellFormDelay + 0.1, formProgress));
    float3 flashColor = flashBright * formFlash * 0.5;

    // =
    // 最终合成
    // =
    float3 innerColor = (gridColor + fillColor + flowColor + edgeGlowInner + flashColor)
                        * cellVisible * cageMask;
    float3 finalColor = innerColor + outerGlowColor;

    float innerAlpha = (hexLine * 1.0 + cellGlow * 0.35 + dataFlow * 0.4
                      + edgeGlowBand * 0.7 + formFlash * 0.5)
                      * cellVisible * cageMask;
    float outerAlpha = outerGlow * 0.65;
    float finalAlpha = saturate(innerAlpha + outerAlpha);

    return float4(saturate(finalColor), finalAlpha);
}

technique Technique1
{
    pass CagePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
