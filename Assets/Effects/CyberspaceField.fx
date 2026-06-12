// ============================================================================
// CyberspaceField.fx — 赛博空间领域着色器 v5
// 黑墙AI——深红领域，现实扭曲，数字侵蚀
// v5 舒适度精修：内部通透化（减压暗/减去饱和）、花纹密度向边界区重分配、
// 高频闪烁全面放缓，长时间战斗不疲劳；保留网格/数据流/实体扫描的赛博身份
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float radius;           // 领域半径（世界像素）
float intensity;        // 0-1 效果强度（淡入淡出）
float expandProgress;   // 0-1 展开进度
float dimStrength;      // 压暗强度 (0=不压暗, 1=最大压暗)
float motionFade;       // 0-1 玩家运动淡化：移动越快越大，会模糊淡化装饰层细节，保留网格与背景
float layerTier;        // 1-3 视觉层级（连续插值），高层领域内部氛围略增强
float2 setPoint;        // 领域中心（世界坐标）
float2 screenPosition;  // 屏幕左上角（世界坐标）
float2 worldViewSize;   // 缩放修正后的世界可视范围（非屏幕像素尺寸）
float gridSize;         // 栅格单元边长（世界像素）

// 域内实体扫描圆环
int entityCount;        // 域内实体数量 (最大32)
float4 entities[32];    // (centerX, centerY, ringRadius, seed)

// ---- 工具函数 ----

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// 多八度噪声采样（用低开销的双层混合替代循环）
float layeredNoise(float2 uv, float timeOff)
{
    float n1 = tex2D(noiseTex, frac(uv * 0.37 + float2(timeOff * 0.013, timeOff * 0.009))).r;
    float n2 = tex2D(noiseTex, frac(uv * 1.13 + float2(timeOff * -0.017, timeOff * 0.021))).g;
    float n3 = tex2D(noiseTex, frac(uv * 2.71 + float2(timeOff * 0.031, timeOff * -0.011))).b;
    return n1 * 0.5 + n2 * 0.35 + n3 * 0.15;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);

    if (intensity < 0.001 || expandProgress < 0.001)
        return original;

    // ================================================================
    // 世界坐标计算（缩放感知）
    // ================================================================
    float2 worldPos = screenPosition + worldViewSize * coords;
    float2 screenUV = worldViewSize * coords; //屏幕相对坐标（不随摄像机滚动）
    float2 relPos = worldPos - setPoint;
    float worldDist = length(relPos);
    float effectiveRadius = radius * expandProgress;

    // ================================================================
    // 网格基础
    // ================================================================
    float2 cellIdx = floor(relPos / gridSize);
    float2 cellCenter = (cellIdx + 0.5) * gridSize;
    float cellDist = length(cellCenter);
    float cellRand = hash21(cellIdx);

    // 噪声驱动不规则边缘
    float2 noiseUV = frac(cellIdx * 0.07 + float2(uTime * 0.02, uTime * 0.015));
    float edgeNoise = tex2D(noiseTex, noiseUV).r;
    float radiusOffset = (edgeNoise - 0.5) * gridSize * 4.5;

    // 边界区呼吸闪烁
    float breathe = sin(uTime * 0.8 + cellRand * 6.28318) * 0.5 + 0.5;
    float flickerRange = gridSize * 3.0;
    if (abs(cellDist - effectiveRadius) < flickerRange)
        radiusOffset += (breathe - 0.5) * gridSize * 1.5;

    float edgeBound = effectiveRadius + radiusOffset;
    bool inside = cellDist < edgeBound;

    // ================================================================
    // 域外溢出光晕（增强：更宽扩散+UV微扭曲）
    // ================================================================
    if (!inside)
    {
        float overDist = (cellDist - edgeBound) / (gridSize * 6.0);
        float outerGlow = saturate(1.0 - overDist);
        outerGlow *= outerGlow;
        outerGlow *= intensity;

        if (outerGlow < 0.005)
            return original;

        // 域外UV微扭曲（屏幕相对，不随摄像机滚动）
        float2 outerDistUV = frac(screenUV * 0.0004 + float2(uTime * 0.012, uTime * 0.008));
        float2 outerWarp = tex2D(noiseTex, outerDistUV).rg * 2.0 - 1.0;
        float2 outerWarpCoords = coords + outerWarp * 0.0012 * outerGlow;
        float3 warpedOuter = tex2D(uImage0, outerWarpCoords).rgb;
        original.rgb = lerp(original.rgb, warpedOuter, outerGlow * 0.35);

        // 压暗+红色氛围
        original.rgb *= lerp(1.0, 0.78, outerGlow);
        original.rgb += float3(0.16, 0.018, 0.025) * outerGlow * 0.45;

        // 外部栅格微光
        float2 outerCell = frac(relPos / gridSize);
        float ob = min(min(outerCell.x, 1.0 - outerCell.x), min(outerCell.y, 1.0 - outerCell.y));
        float outerGrid = 1.0 - smoothstep(0.0, 0.04, ob);
        original.rgb += float3(0.28, 0.025, 0.035) * outerGrid * outerGlow * 0.32;

        return float4(original.rgb, original.a);
    }

    // ================================================================
    // 内部归一化坐标
    // ================================================================
    float normDist = saturate(cellDist / effectiveRadius);
    float edgeFactor = smoothstep(0.7, 1.0, normDist);
    float centerFactor = 1.0 - normDist;

    // 全域呼吸节奏（更缓更浅，减轻长时间观看的明暗疲劳）
    float domainBreathe = 0.94 + 0.06 * sin(uTime * 0.5);

    // 视觉层级加成：高层领域内部氛围略增强，但保持舒适上限
    float tierBoost = 1.0 + (clamp(layerTier, 1.0, 3.0) - 1.0) * 0.08;

    // ================================================================
    // 运动淡化主系数：移动时整体领域简约化
    // baseMul: 作用于失真/色差/压暗/红染等"大面积影响画面观感"的处理
    // skeletonMul: 作用于网格骨架/节点/边缘呼吸光等保留可读性的元素
    // ================================================================
    float mFade = saturate(motionFade);
    float baseMul = 1.0 - mFade * 0.55;
    float skeletonMul = 1.0 - mFade * 0.38;

    // ================================================================
    // 第一层：现实扭曲（黑墙侵蚀现实——核心新增效果）
    // ================================================================
    // 低频大尺度扭曲：整体空间弯曲（屏幕相对，不随摄像机滚动）
    // 强度收敛：扭曲集中在边缘，内部战斗区几乎不弯折
    float2 distUV1 = frac(screenUV * 0.0005 + float2(uTime * 0.022, uTime * 0.016));
    float2 warpDisp = tex2D(noiseTex, distUV1).rg * 2.0 - 1.0;
    float warpStr = intensity * 0.0026 * (0.3 + edgeFactor * 1.3) * baseMul;
    float2 warpedCoords = coords + warpDisp * warpStr;

    // 高频小尺度扭曲叠加：局部数字毛刺（柔化，屏幕相对）
    float2 distUV2 = frac(screenUV * 0.0012 + float2(uTime * -0.03, uTime * 0.025));
    float2 warpDisp2 = tex2D(noiseTex, distUV2).rg * 2.0 - 1.0;
    warpedCoords += warpDisp2 * warpStr * 0.15;

    original = tex2D(uImage0, warpedCoords);

    // ================================================================
    // 第二层：色差分离（收敛在边缘带，内部不抖色）
    // ================================================================
    float2 edgeDir = normalize(relPos + 0.001);
    float caWorldPx = edgeFactor * 2.6 * intensity * baseMul;
    float2 caOffset = edgeDir * caWorldPx / worldViewSize;
    original.r = tex2D(uImage0, warpedCoords + caOffset).r;
    original.b = tex2D(uImage0, warpedCoords - caOffset * 0.7).b;

    // ================================================================
    // 第三层：三阶色彩映射（深邃丰富的红色光谱）
    // 通透化：压暗下限抬高、去饱和减弱、红染保留更多场景本色，
    // 战斗可读性优先，氛围由边界区与加法层承担
    // ================================================================
    //移动时减少压暗与红染，让画面视野恢复，避免快速移动叠加领域产生的晕眩感
    float targetDim = lerp(0.54, 0.32, centerFactor * 0.3);
    float dimFactor = lerp(1.0, targetDim, intensity * dimStrength * baseMul);
    float3 processed = original.rgb * dimFactor;

    float lum = dot(processed, float3(0.299, 0.587, 0.114));
    float3 gray = float3(lum, lum, lum);
    processed = lerp(processed, gray, 0.36 * intensity * baseMul);

    // 三阶映射：深渊酒红→血红→炽热琥珀
    float3 shadowRed  = float3(0.14, 0.02, 0.05);
    float3 midRed     = float3(0.62, 0.07, 0.06);
    float3 highRed    = float3(1.0,  0.38, 0.18);
    float3 redMap;
    if (lum < 0.3)
        redMap = lerp(shadowRed, midRed, lum / 0.3);
    else
        redMap = lerp(midRed, highRed, saturate((lum - 0.3) / 0.7));
    processed = lerp(processed, redMap * (lum * 0.65 + 0.35), 0.34 * intensity * baseMul);

    // 距离色温偏移：中心偏冷暗红，边缘偏热橙红
    float3 distTint = lerp(float3(0.0, -0.015, 0.01), float3(0.08, 0.03, -0.02), edgeFactor);
    processed += distTint * intensity * lum * baseMul;

    // 暗角（减弱，中心区不再被吸暗）
    float vignette = 1.0 - normDist * normDist * 0.20;
    processed *= lerp(1.0, vignette, intensity * baseMul);

    // ================================================================
    // 第四层：加法赛博特效
    // ================================================================

    // --- A. 深层数字暗流（纵向纤维流动，屏幕相对）---
    // 增大UV尺度避免豹纹，加大频率差拉开层次
    float2 fieldUV = screenUV / (gridSize * 48.0);
    //纵向拉伸UV产生条纹化流动
    fieldUV.y *= 0.25;
    float2 flowOff = float2(uTime * 0.01, uTime * 0.006);
    float fn1 = tex2D(noiseTex, frac(fieldUV + flowOff)).r;
    float fn2 = tex2D(noiseTex, frac(fieldUV * 3.1 - flowOff * 0.4 + 0.37)).g;
    float fieldNoise = fn1 * 0.75 + fn2 * 0.25;
    // 柔化对比度：更宽的smoothstep过渡；整体减淡，内部留白给战斗
    float digitalField = smoothstep(0.25, 0.75, fieldNoise);
    digitalField *= lerp(0.05, 0.02, edgeFactor);
    digitalField *= domainBreathe;

    // --- B. 栅格结构线（带方向性能量流动）---
    float2 cellLocal = frac(relPos / gridSize);
    float bx = min(cellLocal.x, 1.0 - cellLocal.x);
    float by = min(cellLocal.y, 1.0 - cellLocal.y);
    float borderDist = min(bx, by);

    float traceNoiseH = tex2D(noiseTex, frac(float2(cellIdx.x * 0.13, cellIdx.y * 0.09 + uTime * 0.01))).r;
    float traceNoiseV = tex2D(noiseTex, frac(float2(cellIdx.y * 0.11, cellIdx.x * 0.07 + uTime * 0.012))).g;
    float traceBright = (by < bx) ? traceNoiseH : traceNoiseV;
    traceBright = smoothstep(0.15, 0.85, traceBright);

    float gridLine = 1.0 - smoothstep(0.0, 0.05 + edgeFactor * 0.02, borderDist);
    float gridOpacity = lerp(traceBright * 0.3, 1.0, edgeFactor);
    gridLine *= gridOpacity;

    // 能量流动：沿网格线方向的移动高亮光斑（柔化+减密）
    float flowH = frac(cellLocal.x + uTime * 0.10 + cellIdx.y * 0.37);
    float flowV = frac(cellLocal.y - uTime * 0.08 + cellIdx.x * 0.29);
    float flow = (by < bx) ? flowH : flowV;
    float flowPulse = smoothstep(0.0, 0.18, flow) * smoothstep(0.30, 0.18, flow);
    gridLine += flowPulse * gridOpacity * 0.22 * (1.0 - smoothstep(0.0, 0.08, borderDist));

    //呼吸振幅收窄：骨架亮度更稳定，减少满屏明暗跳动
    gridLine *= 0.78 + 0.22 * breathe;

    // --- C. 栅格交叉节点亮点（脉冲放缓减幅）---
    float nodeX = 1.0 - smoothstep(0.0, 0.09, bx);
    float nodeY = 1.0 - smoothstep(0.0, 0.09, by);
    float node = nodeX * nodeY;
    float nodePulse = 0.5 + 0.5 * sin(uTime * 1.4 + cellRand * 6.28);
    node *= lerp(nodePulse * 0.12, 0.62, edgeFactor);

    // --- D. [已移除横向扫描线] ---
    // --- E. [已移除主扫描条] ---

    // --- F. 垂直数据流（屏幕相对，不随摄像机滚动；降密减淡）---
    float dColIdx = floor(screenUV.x / (gridSize * 2.0));
    float colRand = hash21(float2(dColIdx, 7.77));
    float streamActive = step(0.62, colRand);
    float streamSpeed = 0.10 + colRand * 0.18;
    float streamPhase = frac(screenUV.y * 0.003 - uTime * streamSpeed);
    float streamHead = smoothstep(0.0, 0.06, streamPhase) * smoothstep(0.35, 0.1, streamPhase);
    float streamTail = pow(saturate(1.0 - streamPhase / 0.35), 3.0) * 0.45;
    float dataStream = (streamHead + streamTail) * streamActive * (1.0 - edgeFactor) * 0.22;

    // --- G. 径向脉冲环（更慢更柔，弱化为氛围底纹）---
    float pulseDistortion = tex2D(noiseTex, frac(screenUV * 0.0008 + uTime * 0.006)).r * 12.0;
    float basePhaseDist = worldDist + pulseDistortion;
    // 降低频率系数+指数，环更宽更柔
    float pulse1 = pow(saturate(sin((basePhaseDist - uTime * 30.0) * 0.012) * 0.5 + 0.5), 6.0);
    float pulse2 = pow(saturate(sin((basePhaseDist - uTime * 19.0) * 0.009) * 0.5 + 0.5), 8.0);
    float pulse = (pulse1 * 0.6 + pulse2 * 0.4);
    pulse *= saturate(1.0 - normDist * 0.6) * 0.12;

    // --- H. 边缘能量裂纹（收窄到贴近边界的窄带，集中戏剧性）---
    float edgeGlow = smoothstep(0.78, 1.0, normDist);
    // 降低噪声频率，加宽裂纹过渡带
    float2 crackUV = frac(screenUV * 0.0018 + float2(uTime * 0.025, uTime * -0.02));
    float crackNoise = tex2D(noiseTex, crackUV).r;
    float crack = smoothstep(0.38, 0.47, crackNoise) * smoothstep(0.60, 0.50, crackNoise);
    float edgeCrack = crack * edgeGlow * 1.2;
    // 基础辉光（脉动放缓）
    float edgePulse = 0.5 + 0.5 * sin(uTime * 0.9 + cellRand * 6.28318);
    float edgeBase = edgeGlow * edgePulse * 0.50;
    float edgeTotal = edgeBase + edgeCrack;

    // --- I. [已移除水平故障撕裂] ---

    // --- J. 辉光粒子（漂浮余烬，更稀疏更柔）---
    float2 particleUV = frac(screenUV * 0.005 + float2(uTime * 0.025, -uTime * 0.02));
    float particleNoise = tex2D(noiseTex, particleUV).r;
    // 提高阈值：粒子更稀疏但更突出
    float particle = smoothstep(0.93, 0.97, particleNoise);
    float particleSeed = floor(particleNoise * 50.0);
    float particlePulse = 0.5 + 0.5 * sin(uTime * 1.6 + particleSeed * 2.7);
    particle *= particlePulse * (1.0 - edgeFactor * 0.5) * 0.40;

    // --- K. 实体扫描圆环 ---
    float entityRingTotal = 0;
    float entityScanTotal = 0;
    [loop]
    for (int e = 0; e < entityCount; e++)
    {
        float2 eCenter = entities[e].xy;
        float eRadius = entities[e].z;
        float eSeed = entities[e].w;

        float2 toEntity = worldPos - eCenter;
        float eDist = length(toEntity);
        float ringDist = abs(eDist - eRadius);

        float ring = 1.0 - smoothstep(0.0, 2.0, ringDist);
        float eAngle = atan2(toEntity.y, toEntity.x);
        float segFrac = frac(eAngle * 1.9099);
        float segGap = smoothstep(0.03, 0.08, min(segFrac, 1.0 - segFrac));
        ring *= lerp(1.0, segGap, 0.45);

        float halo = 1.0 - smoothstep(0.0, 10.0, ringDist);
        halo *= halo * 0.25;

        float scanAngle = uTime * 1.8 + eSeed * 6.28318;
        float angleDiff = abs(frac((eAngle - scanAngle) / 6.28318 + 0.5) - 0.5) * 2.0;
        float scan = smoothstep(0.17, 0.0, angleDiff);

        float ePulse = 0.65 + 0.35 * sin(uTime * 2.5 + eSeed * 12.56);
        entityRingTotal += (ring * 0.7 + halo) * ePulse;
        entityScanTotal += ring * scan * ePulse;
    }

    // --- 赛博色彩面板（丰富的红色光谱）---
    float3 cAbyssRed  = float3(0.35, 0.02, 0.05);
    float3 cBloodRed  = float3(0.70, 0.06, 0.08);
    float3 cBrightRed = float3(1.0,  0.15, 0.10);
    float3 cHotAmber  = float3(1.0,  0.40, 0.22);
    float3 cWhiteRed  = float3(1.0,  0.60, 0.48);
    float3 cNodeColor = float3(0.95, 0.25, 0.18);
    float3 cCrackGlow = float3(1.0,  0.22, 0.12);

    // ================================================================
    // 玩家运动淡化：移动时模糊掉装饰层细节，整体领域同步简约
    // detailMul:   花纹层(数据流/脉冲环/裂纹/粒子/底层暗流) -> 强淡化
    //              stationary -> 1.0 (全显), full motion -> 0.18 (仅留极弱残影)
    // entityMul:   实体扫描环 -> 弱淡化(保留战斗可读性)
    //              stationary -> 1.0, full motion -> 0.55
    // skeletonMul: 网格走线/节点/边缘呼吸光 -> 中度淡化
    //              stationary -> 1.0, full motion -> 0.45
    // ================================================================
    float detailMul = 1.0 - mFade * 0.62;
    float entityMul = 1.0 - mFade * 0.45;

    // --- 合成加法层 ---
    float3 additive = float3(0, 0, 0);
    additive += cAbyssRed   * digitalField * detailMul;                       // A: 底层暗流(花纹) -> 淡化
    additive += cBloodRed   * gridLine * 0.8 * skeletonMul;                   // B: 栅格走线(骨架) -> 中度淡化
    additive += cNodeColor  * node * skeletonMul;                             // C: 节点亮点(骨架) -> 中度淡化
    additive += cAbyssRed   * dataStream * detailMul;                         // F: 垂直数据流(数据块) -> 淡化
    additive += cBrightRed  * pulse * detailMul;                              // G: 双频脉冲环(花纹) -> 淡化
    //H: 边缘呼吸光按骨架淡化，细碎裂纹按花纹淡化
    additive += cCrackGlow  * (edgeBase * skeletonMul + edgeCrack * detailMul); // H
    additive += cHotAmber   * particle * detailMul;                           // J: 辉光粒子(花纹) -> 淡化
    additive += cBrightRed  * entityRingTotal * entityMul;                    // K: 实体扫描环 -> 弱淡化
    additive += cWhiteRed   * entityScanTotal * 0.55 * entityMul;             // K: 扫描弧高亮 -> 弱淡化

    // ================================================================
    // 最终合成
    // ================================================================
    //整体加法亮度再叠一层运动淡化，让快速移动时的画面整体偏向"轻量描边"观感
    float globalAddMul = lerp(1.0, 0.70, mFade);
    float3 finalColor = processed + additive * intensity * domainBreathe * globalAddMul * tierBoost;

    return float4(finalColor, original.a);
}

technique Technique1
{
    pass CyberspacePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
