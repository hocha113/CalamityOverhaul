// ============================================================================
//CyberspaceField.fx 赛博空间领域
//采样 s0 + s1；全屏世界坐标映射，gridSize 栅格单元(世界像素)
//
//三层几何语汇(tierWeights 加权混合，C#端归一化)：
//  L1 正交栅格·坐标系(静态，无时间项)
//  L2 六边形蜂巢·可寻址单元(per-cell 静态亮度 + 超慢驻留起伏)
//  L3 流场解体·空间被读出(屏幕相对数据流 + 字节块 + 噪声侵蚀的残余蜂巢)
//
//常驻舒适约定：禁止全局同相 sin 呼吸，亮度变化一律空间 hash 或超慢漂移；
//压暗/红染收敛为氛围而非洗屏；加法层合成前过软限幅。
//分层选择只用权重乘，不新增动态分支(直线算术 + 普通 tex2D)。
//极角约束：全部结构走笛卡尔/屏幕相对坐标，实体环的 atan2 只与整数倍角组合。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float radius;           //领域半径(世界像素)
float intensity;        //0~1 效果强度(淡入淡出)
float expandProgress;   //0~1 展开进度
float dimStrength;      //压暗强度 0不压暗 1最大
float motionFade;       //0~1 玩家运动淡化装饰层
float3 tierWeights;     //三层几何权重(方格/蜂巢/流场)，恒和为1
float2 setPoint;        //领域中心(世界坐标)
float2 screenPosition;  //屏幕左上角(世界坐标)
float2 worldViewSize;   //缩放修正后世界可视范围
float gridSize;         //栅格单元边长(世界像素)

int entityCount;        //域内实体数量(最大32)
float4 entities[32];    //centerX centerY ringRadius seed

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//正余数取模(负输入连续)，蜂巢平铺用
float2 hmod(float2 x, float2 y)
{
    return x - y * floor(x / y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);

    if (intensity < 0.001 || expandProgress < 0.001)
        return original;

    //=
    //世界坐标计算（缩放感知）
    //=
    float2 worldPos = screenPosition + worldViewSize * coords;
    float2 screenUV = worldViewSize * coords; //屏幕相对坐标（不随摄像机滚动）
    float2 relPos = worldPos - setPoint;
    float effectiveRadius = radius * expandProgress;

    float w1 = tierWeights.x;
    float w2 = tierWeights.y;
    float w3 = tierWeights.z;

    //=
    //边界基础（方格粒度的边缘不规则，慢噪声漂移，无正弦闪烁）
    //=
    float2 cellIdx = floor(relPos / gridSize);
    float2 cellCenter = (cellIdx + 0.5) * gridSize;
    float cellDist = length(cellCenter);
    float cellRand = hash21(cellIdx);

    float2 noiseUV = frac(cellIdx * 0.07 + float2(uTime * 0.014, uTime * 0.010));
    float edgeNoise = tex2D(noiseTex, noiseUV).r;
    float radiusOffset = (edgeNoise - 0.5) * gridSize * 4.0;

    float edgeBound = effectiveRadius + radiusOffset;
    bool inside = cellDist < edgeBound;

    //=
    //域外溢出光晕（收敛版：轻压暗+微红氛围）
    //=
    if (!inside)
    {
        float overDist = (cellDist - edgeBound) / (gridSize * 6.0);
        float outerGlow = saturate(1.0 - overDist);
        outerGlow *= outerGlow;
        outerGlow *= intensity;

        if (outerGlow < 0.005)
            return original;

        //域外UV微扭曲（屏幕相对，不随摄像机滚动）
        float2 outerDistUV = frac(screenUV * 0.0004 + float2(uTime * 0.012, uTime * 0.008));
        float2 outerWarp = tex2D(noiseTex, outerDistUV).rg * 2.0 - 1.0;
        float2 outerWarpCoords = coords + outerWarp * 0.0010 * outerGlow;
        float3 warpedOuter = tex2D(uImage0, outerWarpCoords).rgb;
        original.rgb = lerp(original.rgb, warpedOuter, outerGlow * 0.30);

        //压暗+红色氛围
        original.rgb *= lerp(1.0, 0.82, outerGlow);
        original.rgb += float3(0.16, 0.018, 0.025) * outerGlow * 0.32;

        //外部栅格微光
        float2 outerCell = frac(relPos / gridSize);
        float ob = min(min(outerCell.x, 1.0 - outerCell.x), min(outerCell.y, 1.0 - outerCell.y));
        float outerGrid = 1.0 - smoothstep(0.0, 0.04, ob);
        original.rgb += float3(0.28, 0.025, 0.035) * outerGrid * outerGlow * 0.22;

        return float4(original.rgb, original.a);
    }

    //=
    //内部归一化坐标
    //=
    float normDist = saturate(cellDist / effectiveRadius);
    float edgeFactor = smoothstep(0.7, 1.0, normDist);
    float centerFactor = 1.0 - normDist;

    //=
    //运动淡化系数
    //baseMul: 失真/色差/压暗/红染等大面积处理
    //skeletonMul: 结构骨架  detailMul: 花纹  entityMul: 实体标记
    //=
    float mFade = saturate(motionFade);
    float baseMul = 1.0 - mFade * 0.55;
    float skeletonMul = 1.0 - mFade * 0.38;
    float detailMul = 1.0 - mFade * 0.62;
    float entityMul = 1.0 - mFade * 0.45;

    //=
    //第一层：现实扭曲（收敛到边界带，内部战斗区几乎不弯）
    //=
    float2 distUV1 = frac(screenUV * 0.0005 + float2(uTime * 0.022, uTime * 0.016));
    float2 warpDisp = tex2D(noiseTex, distUV1).rg * 2.0 - 1.0;
    float warpStr = intensity * 0.0026 * (0.06 + edgeFactor * 1.6) * baseMul;
    float2 warpedCoords = coords + warpDisp * warpStr;

    float2 distUV2 = frac(screenUV * 0.0012 + float2(uTime * -0.03, uTime * 0.025));
    float2 warpDisp2 = tex2D(noiseTex, distUV2).rg * 2.0 - 1.0;
    warpedCoords += warpDisp2 * warpStr * 0.15;

    original = tex2D(uImage0, warpedCoords);

    //=
    //第二层：色差分离（仅边缘带）
    //=
    float2 edgeDir = normalize(relPos + 0.001);
    float caWorldPx = edgeFactor * 1.8 * intensity * baseMul;
    float2 caOffset = edgeDir * caWorldPx / worldViewSize;
    original.r = tex2D(uImage0, warpedCoords + caOffset).r;
    original.b = tex2D(uImage0, warpedCoords - caOffset * 0.7).b;

    //=
    //第三层：调色（黑墙压暗保留，红染降为氛围重点色，高光端压缩防刺眼）
    //=
    float targetDim = lerp(0.62, 0.52, centerFactor * 0.3);
    float dimFactor = lerp(1.0, targetDim, intensity * dimStrength * baseMul);
    float3 processed = original.rgb * dimFactor;

    float lum = dot(processed, float3(0.299, 0.587, 0.114));
    float3 gray = float3(lum, lum, lum);
    processed = lerp(processed, gray, 0.26 * intensity * baseMul);

    //三阶映射：深渊酒红→血红→压缩后的暖橙(不再推到炽亮)
    float3 shadowRed  = float3(0.14, 0.02, 0.05);
    float3 midRed     = float3(0.62, 0.07, 0.06);
    float3 highRed    = float3(0.72, 0.28, 0.15);
    float loT = saturate(lum / 0.3);
    float hiT = saturate((lum - 0.3) / 0.7);
    float hiPick = step(0.3, lum);
    float3 redMap = lerp(lerp(shadowRed, midRed, loT), lerp(midRed, highRed, hiT), hiPick);
    processed = lerp(processed, redMap * (lum * 0.65 + 0.35), 0.20 * intensity * baseMul);

    //距离色温偏移：中心偏冷暗红，边缘偏热橙红（减半）
    float3 distTint = lerp(float3(0.0, -0.010, 0.007), float3(0.05, 0.02, -0.012), edgeFactor);
    processed += distTint * intensity * lum * baseMul;

    //暗角（弱化）
    float vignette = 1.0 - normDist * normDist * 0.16;
    processed *= lerp(1.0, vignette, intensity * baseMul);

    //=
    //结构层 L1：正交栅格（完全静态：行列明暗与节点亮度全为空间 hash）
    //=
    float2 sqLocal = frac(relPos / gridSize);
    float sbx = min(sqLocal.x, 1.0 - sqLocal.x);
    float sby = min(sqLocal.y, 1.0 - sqLocal.y);
    float sqBorder = min(sbx, sby);
    float sqLine = 1.0 - smoothstep(0.0, 0.05, sqBorder);
    float rowB = 0.55 + 0.45 * hash21(float2(cellIdx.y, 3.7));
    float colB = 0.55 + 0.45 * hash21(float2(cellIdx.x, 8.1));
    float sqTrace = lerp(colB, rowB, step(sby, sbx));
    //内部退到底噪，只有边界一圈保持可读
    float sqOpacity = lerp(0.16, 1.0, edgeFactor) * sqTrace;
    float sqNode = (1.0 - smoothstep(0.0, 0.08, sbx)) * (1.0 - smoothstep(0.0, 0.08, sby));
    float sqNodeB = lerp(0.10, 0.55, edgeFactor) * (0.6 + 0.4 * cellRand);

    //=
    //结构层 L2：六边形蜂巢（无分支双 lattice 取近者）
    //=
    float hexScale = gridSize * 1.7;
    float2 hp = relPos / hexScale;
    float2 hexRatio = float2(1.0, 1.7320508);
    float2 hh = hexRatio * 0.5;
    float2 ga = hmod(hp, hexRatio) - hh;
    float2 gb = hmod(hp - hh, hexRatio) - hh;
    float pickA = step(dot(ga, ga), dot(gb, gb));
    float2 gv = lerp(gb, ga, pickA);
    float2 hexId = hp - gv;
    float2 av = abs(gv);
    //0=六边形边缘 0.5=中心
    float hexEdgeDist = 0.5 - max(dot(av, float2(0.5, 0.8660254)), av.x);

    float hexCellRand = hash21(hexId * 3.173 + 11.71);
    float hexLine = 1.0 - smoothstep(0.0, 0.055, hexEdgeDist);
    float hexLineB = lerp(0.35, 1.0, edgeFactor) * (0.70 + 0.30 * hash21(hexId * 5.19));
    //数据驻留单元：每格独立超慢起伏(周期约90s，相位 hash 去同相)
    float resident = 0.5 + 0.5 * sin(hexCellRand * 6.28318 + uTime * 0.07);
    float lit = smoothstep(0.72, 0.95, hexCellRand * 0.65 + resident * 0.35);
    float hexFill = smoothstep(0.03, 0.16, hexEdgeDist) * lit * (0.35 + 0.25 * hexCellRand);

    //=
    //结构层 L3：流场解体（屏幕相对上行数据流 + 字节块 + 残余蜂巢骨架）
    //=
    float colW = gridSize * 1.6;
    float colIdx = floor(screenUV.x / colW);
    float colLocal = frac(screenUV.x / colW);
    float colRand = hash21(float2(colIdx, 7.77));
    float colActive = step(0.30, colRand);
    float colSpeed = 0.05 + colRand * 0.09;
    float colPhase = frac(screenUV.y * (0.0015 + colRand * 0.0009) + uTime * colSpeed + colRand * 9.7);
    float streamHead = smoothstep(0.0, 0.05, colPhase) * smoothstep(0.30, 0.07, colPhase);
    float streamTail = pow(saturate(1.0 - colPhase / 0.55), 2.5) * 0.30;
    float colProfile = smoothstep(0.0, 0.35, colLocal) * smoothstep(1.0, 0.65, colLocal);
    float stream = (streamHead + streamTail) * colActive * colProfile;

    //字节块：列内量化行块，随噪声缓慢迁移
    float rowH = gridSize * 0.9;
    float rowIdx = floor(screenUV.y / rowH);
    float rowLocal = frac(screenUV.y / rowH);
    float byteN = tex2D(noiseTex, frac(float2(colIdx * 0.0313, rowIdx * 0.0217) + float2(0.0, -uTime * 0.012))).g;
    float byteProfile = smoothstep(0.0, 0.15, rowLocal) * smoothstep(1.0, 0.85, rowLocal)
                      * smoothstep(0.0, 0.18, colLocal) * smoothstep(1.0, 0.82, colLocal);
    float byteBlock = smoothstep(0.80, 0.92, byteN) * colActive * byteProfile;

    //残余蜂巢骨架：per-cell 噪声侵蚀，读作单元逐块剥离
    float erode = tex2D(noiseTex, frac(hexId * 0.113 + float2(uTime * 0.006, uTime * 0.004))).b;
    float residual = hexLine * smoothstep(0.30, 0.72, erode) * 0.45;

    //=
    //边界带（基础辉光静态化，裂纹保留慢漂移）
    //=
    float edgeGlow = smoothstep(0.80, 1.0, normDist);
    float2 crackUV = frac(screenUV * 0.0018 + float2(uTime * 0.020, uTime * -0.016));
    float crackNoise = tex2D(noiseTex, crackUV).r;
    float crack = smoothstep(0.38, 0.47, crackNoise) * smoothstep(0.60, 0.50, crackNoise);
    float edgeCrack = crack * edgeGlow * 1.1;
    float edgeBase = edgeGlow * 0.42;
    float edgeTotal = edgeBase * skeletonMul + edgeCrack * detailMul;

    //=
    //实体标记：L1 细环 / L2 起分段+扫描弧 / L3 再加指向域心的数据抽取线
    //个体亮度用 seed 静态错开，无同相脉动
    //=
    float entityRingTotal = 0;
    float entityScanTotal = 0;
    float extractTotal = 0;
    float segWeight = saturate(w2 + w3);
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
        //12 段整数倍角，跨 ±π 连续
        float segFrac = frac(eAngle * 1.9099);
        float segGap = smoothstep(0.03, 0.08, min(segFrac, 1.0 - segFrac));
        ring *= lerp(1.0, segGap, 0.45 * segWeight);

        float halo = 1.0 - smoothstep(0.0, 10.0, ringDist);
        halo *= halo * 0.25;

        float scanAngle = uTime * 1.6 + eSeed * 6.28318;
        float angleDiff = abs(frac((eAngle - scanAngle) / 6.28318 + 0.5) - 0.5) * 2.0;
        float scan = smoothstep(0.17, 0.0, angleDiff) * segWeight;

        float eB = 0.78 + 0.22 * eSeed;
        entityRingTotal += (ring * 0.7 + halo) * eB;
        entityScanTotal += ring * scan * eB;

        //数据抽取线：从实体环缘指向域心，包沿线向心流动
        float2 toCenter = setPoint - eCenter;
        float ctLen = max(length(toCenter), 0.001);
        float2 ctDir = toCenter / ctLen;
        float along = dot(toEntity, ctDir);
        float perp = abs(dot(toEntity, float2(-ctDir.y, ctDir.x)));
        float segLen = min(ctLen, 300.0);
        float alongMask = smoothstep(0.0, max(eRadius, 1.0), along) * smoothstep(segLen, segLen * 0.5, along);
        float lineMask = (1.0 - smoothstep(0.6, 2.4, perp)) * alongMask;
        float dash = smoothstep(0.42, 0.10, abs(frac(along / 30.0 - uTime * 1.1 + eSeed * 3.0) - 0.5));
        extractTotal += lineMask * (0.25 + 0.75 * dash) * eB;
    }
    extractTotal *= w3;

    //=
    //赛博色彩面板（红系身份，层间只做冷→热的克制递进）
    //=
    float3 cGridLine  = float3(0.52, 0.055, 0.06);
    float3 cGridNode  = float3(0.85, 0.16, 0.11);
    float3 cHexLine   = float3(0.72, 0.08, 0.07);
    float3 cHexFill   = float3(0.34, 0.035, 0.045);
    float3 cFlowHead  = float3(1.0, 0.34, 0.16);
    float3 cFlowByte  = float3(0.60, 0.10, 0.07);
    float3 cCrackGlow = float3(1.0, 0.22, 0.12);
    float3 cRing      = float3(1.0, 0.15, 0.10);
    float3 cScan      = float3(1.0, 0.60, 0.48);
    float3 cExtract   = float3(1.0, 0.42, 0.20);

    //=
    //合成加法层（每层三路结构 + 边界 + 实体，过软限幅）
    //=
    float3 additive = float3(0, 0, 0);
    additive += (cGridLine * sqLine * sqOpacity * 0.80 + cGridNode * sqNode * sqNodeB) * w1 * skeletonMul;
    additive += (cHexLine * hexLine * hexLineB * 0.55 + cHexFill * hexFill) * w2 * skeletonMul;
    additive += (cFlowHead * stream * 0.55 + cFlowByte * byteBlock * 0.50) * w3 * detailMul;
    additive += cHexLine * residual * 0.35 * w3 * skeletonMul;
    additive += cCrackGlow * edgeTotal;
    additive += cRing * entityRingTotal * entityMul;
    additive += cScan * entityScanTotal * 0.50 * entityMul;
    additive += cExtract * extractTotal * entityMul;

    //软限幅：常驻画面任何像素不过曝
    float addLum = dot(additive, float3(0.299, 0.587, 0.114));
    additive *= 1.0 / (1.0 + addLum * 0.85);

    float globalAddMul = lerp(1.0, 0.70, mFade);
    float3 finalColor = processed + additive * intensity * globalAddMul;

    return float4(finalColor, original.a);
}

technique Technique1
{
    pass CyberspacePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
