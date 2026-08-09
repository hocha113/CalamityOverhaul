// ============================================================================
//CyberspaceField.fx 赛博空间领域
//采样 s0 + s1；全屏世界坐标映射，gridSize 栅格单元(世界像素)
//
//三层语汇：
//  L1 正交栅格·坐标系(静态，圈地)         —— tierWeights.x
//  L2 六边形蜂巢·可寻址单元(更大圈地)      —— tierWeights.y
//  L3 世界建模层·撤墙全世界接管           —— uTakeover 主导(w3 只负责蜂巢淡出)
//    地形轮廓数字化(4-tap 提边+两档量化+虚线+角点) + 两道权限环带(反向慢转)
//    + 扫描前沿推进；负空间：不再铺任何满域底纹
//撤墙：C# 侧把 radius 乘上 WallDeparture 增幅，边界带着溢出光晕飞出屏幕，
//takeover 完成后全屏皆"域内"，inside/outside 之分自然消失。
//
//常驻舒适约定：禁止全局同相 sin 呼吸，亮度变化一律空间 hash 或超慢漂移；
//加法层合成前过带趾软限幅(趾部以下原样通过，只压尖峰)。
//分层选择只用权重乘，不新增动态分支(直线算术 + 普通 tex2D)。
//极角约束：theta 消费者只有整数倍角组合——环带分段 (θ±uBandSpin)·k/2π 中
//k∈{3,4}，uBandSpin 由 C# 按整 2π 回绕(0.75·2π·4/2π=3∈ℤ)；刻度梳 60/96∈ℤ；
//实体环 12 段与扫描差值 frac(Δ/2π)。提边/虚线/蜂巢/方格全走笛卡尔坐标。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float radius;           //领域半径(世界像素，L3 撤墙期由 C# 乘增幅)
float intensity;        //0~1 效果强度(淡入淡出)
float expandProgress;   //0~1 展开进度
float dimStrength;      //压暗强度 0不压暗 1最大
float motionFade;       //0~1 玩家运动淡化装饰层
float3 tierWeights;     //三层几何权重(方格/蜂巢/L3)，恒和为1
float2 setPoint;        //领域中心(世界坐标)
float2 screenPosition;  //屏幕左上角(世界坐标)
float2 worldViewSize;   //缩放修正后世界可视范围
float gridSize;         //栅格单元边长(世界像素)

float uTakeover;        //0~1 L3 全世界接管进度(smoothstep 缓动)
float uSpread;          //0~1 入场扫描前沿进度
float2 uSpreadOrigin;   //扫描原点(世界坐标)
float uFlash;           //0~1 入场闪变包络
float uBandSpin;        //权限环带累计相位(弧度，已按 2π 回绕)

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
    //域外溢出光晕（撤墙飞行期跟着边界一起退场）
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
        original.rgb *= lerp(1.0, 0.78, outerGlow);
        original.rgb += float3(0.16, 0.018, 0.025) * outerGlow * 0.40;

        //外部栅格微光
        float2 outerCell = frac(relPos / gridSize);
        float ob = min(min(outerCell.x, 1.0 - outerCell.x), min(outerCell.y, 1.0 - outerCell.y));
        float outerGrid = 1.0 - smoothstep(0.0, 0.04, ob);
        original.rgb += float3(0.28, 0.025, 0.035) * outerGrid * outerGlow * 0.28;

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
    //第三层：调色（黑墙实体感，高光端压缩防刺眼）
    //=
    float targetDim = lerp(0.55, 0.42, centerFactor * 0.3);
    float dimFactor = lerp(1.0, targetDim, intensity * dimStrength * baseMul);
    float3 processed = original.rgb * dimFactor;

    float lum = dot(processed, float3(0.299, 0.587, 0.114));
    float3 gray = float3(lum, lum, lum);
    processed = lerp(processed, gray, 0.33 * intensity * baseMul);

    //三阶映射：深渊酒红→血红→压缩暖橙
    float3 shadowRed  = float3(0.14, 0.02, 0.05);
    float3 midRed     = float3(0.62, 0.07, 0.06);
    float3 highRed    = float3(0.82, 0.30, 0.16);
    float loT = saturate(lum / 0.3);
    float hiT = saturate((lum - 0.3) / 0.7);
    float hiPick = step(0.3, lum);
    float3 redMap = lerp(lerp(shadowRed, midRed, loT), lerp(midRed, highRed, hiT), hiPick);
    processed = lerp(processed, redMap * (lum * 0.65 + 0.35), 0.30 * intensity * baseMul);

    //距离色温偏移：中心偏冷暗红，边缘偏热橙红
    float3 distTint = lerp(float3(0.0, -0.010, 0.007), float3(0.05, 0.02, -0.012), edgeFactor);
    processed += distTint * intensity * lum * baseMul;

    //暗角（弱化；takeover 后 normDist≈0 自然消失）
    float vignette = 1.0 - normDist * normDist * 0.16;
    processed *= lerp(1.0, vignette, intensity * baseMul);

    //入场闪变：短促负片挤压（数字系统重构的一瞬）
    processed = lerp(processed, float3(1.0, 1.0, 1.0) - processed, uFlash * 0.20);

    //=
    //结构层 L1：正交栅格（完全静态）
    //=
    float2 sqLocal = frac(relPos / gridSize);
    float sbx = min(sqLocal.x, 1.0 - sqLocal.x);
    float sby = min(sqLocal.y, 1.0 - sqLocal.y);
    float sqBorder = min(sbx, sby);
    float sqLine = 1.0 - smoothstep(0.0, 0.05, sqBorder);
    float rowB = 0.55 + 0.45 * hash21(float2(cellIdx.y, 3.7));
    float colB = 0.55 + 0.45 * hash21(float2(cellIdx.x, 8.1));
    float sqTrace = lerp(colB, rowB, step(sby, sbx));
    float sqOpacity = lerp(0.30, 1.0, edgeFactor) * sqTrace;
    float sqNode = (1.0 - smoothstep(0.0, 0.08, sbx)) * (1.0 - smoothstep(0.0, 0.08, sby));
    float sqNodeB = lerp(0.18, 0.75, edgeFactor) * (0.6 + 0.4 * cellRand);

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
    float hexEdgeDist = 0.5 - max(dot(av, float2(0.5, 0.8660254)), av.x);

    float hexCellRand = hash21(hexId * 3.173 + 11.71);
    float hexLine = 1.0 - smoothstep(0.0, 0.055, hexEdgeDist);
    float hexLineB = lerp(0.55, 1.0, edgeFactor) * (0.70 + 0.30 * hash21(hexId * 5.19));
    float resident = 0.5 + 0.5 * sin(hexCellRand * 6.28318 + uTime * 0.07);
    float lit = smoothstep(0.72, 0.95, hexCellRand * 0.65 + resident * 0.35);
    float hexFill = smoothstep(0.03, 0.16, hexEdgeDist) * lit * (0.50 + 0.30 * hexCellRand);

    //=
    //结构层 L3：世界建模层（takeover 主导）
    //=
    float l3Amt = saturate(uTakeover) * intensity;

    //扫描空间蒙版：前沿从原点向外推进，完成后锁全屏（防玩家远离原点后蒙版脱落）
    float dOrigin = length(worldPos - uSpreadOrigin);
    float spreadRadius = uSpread * 3200.0;
    float spreadLock = step(0.995, uSpread);
    float spreadMask = max(smoothstep(spreadRadius, spreadRadius - 260.0, dOrigin), spreadLock);
    float l3Vis = l3Amt * spreadMask;

    //扫描前沿光带：仅推进期间存在
    float frontGlow = (1.0 - smoothstep(0.0, 240.0, abs(dOrigin - spreadRadius)))
        * step(0.02, uSpread) * (1.0 - spreadLock) * l3Amt;

    //--- 地形轮廓数字化：世界自身的几何被点亮 ---
    float2 epsX = float2(2.0 / worldViewSize.x, 0.0);
    float2 epsY = float2(0.0, 2.0 / worldViewSize.y);
    float3 lumW = float3(0.299, 0.587, 0.114);
    float lumL = dot(tex2D(uImage0, coords - epsX).rgb, lumW);
    float lumR = dot(tex2D(uImage0, coords + epsX).rgb, lumW);
    float lumU = dot(tex2D(uImage0, coords - epsY).rgb, lumW);
    float lumD = dot(tex2D(uImage0, coords + epsY).rgb, lumW);
    float gx = lumR - lumL;
    float gy = lumD - lumU;
    float gmag = sqrt(gx * gx + gy * gy);
    float edgeRaw = smoothstep(0.13, 0.45, gmag);
    //两档量化(数据感) + 世界坐标对角虚线 + 双轴同强的角点注册标记
    float edgeQ = smoothstep(0.18, 0.30, edgeRaw) * 0.55
                + smoothstep(0.58, 0.72, edgeRaw) * 0.45;
    float dashE = 0.70 + 0.30 * step(0.5, frac((worldPos.x + worldPos.y) * 0.031));
    float cornerMark = smoothstep(0.16, 0.34, min(abs(gx), abs(gy)));
    float edgeGlowL3 = (edgeQ * dashE + cornerMark * 0.6) * l3Vis;

    //--- 两道权限环带：绕持有者反向慢转的大构件 ---
    float pDist = length(relPos);
    float theta = atan2(relPos.y, relPos.x);

    //带1：r=420 厚34，3 段长弧(顺旋) + 60 静态刻度梳 + 双缘细圈
    float b1d = abs(pDist - 420.0);
    float band1Env = 1.0 - smoothstep(10.0, 34.0, b1d);
    float band1Rim = 1.0 - smoothstep(0.0, 5.0, abs(b1d - 30.0));
    float seg1Phase = frac((theta + uBandSpin) * 0.4774648);
    float seg1 = smoothstep(0.055, 0.115, seg1Phase) * smoothstep(0.945, 0.885, seg1Phase);
    float comb1 = step(0.62, frac(theta * 9.5492966));
    float band1 = band1Env * seg1 * (0.45 + 0.55 * comb1) + band1Rim * seg1 * 0.5;

    //带2：r=900 厚42，4 段长弧(逆旋 0.75 倍速) + 96 静态刻度梳
    float b2d = abs(pDist - 900.0);
    float band2Env = 1.0 - smoothstep(12.0, 42.0, b2d);
    float seg2Phase = frac((theta - uBandSpin * 0.75) * 0.6366198);
    float seg2 = smoothstep(0.05, 0.10, seg2Phase) * smoothstep(0.95, 0.90, seg2Phase);
    float comb2 = step(0.55, frac(theta * 15.2788745));
    float band2 = band2Env * seg2 * (0.40 + 0.60 * comb2);

    float bands = (band1 + band2) * l3Vis;

    //=
    //边界带（takeover 后 normDist≈0 自然归零）
    //=
    float edgeGlow = smoothstep(0.80, 1.0, normDist);
    float2 crackUV = frac(screenUV * 0.0018 + float2(uTime * 0.020, uTime * -0.016));
    float crackNoise = tex2D(noiseTex, crackUV).r;
    float crack = smoothstep(0.38, 0.47, crackNoise) * smoothstep(0.60, 0.50, crackNoise);
    float edgeCrack = crack * edgeGlow * 1.25;
    float edgeBase = edgeGlow * 0.55;
    float edgeTotal = edgeBase * skeletonMul + edgeCrack * detailMul;

    //=
    //实体标记：L1 细环 / L2 起分段+扫描弧 / L3 再加指向域心的数据抽取线
    //=
    float entityRingTotal = 0;
    float entityScanTotal = 0;
    float extractTotal = 0;
    float segWeight = saturate(w2 + uTakeover + tierWeights.z);
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
    extractTotal *= saturate(max(tierWeights.z, uTakeover));

    //=
    //赛博色彩面板
    //=
    float3 cGridLine  = float3(0.52, 0.055, 0.06);
    float3 cGridNode  = float3(0.85, 0.16, 0.11);
    float3 cHexLine   = float3(0.72, 0.08, 0.07);
    float3 cHexFill   = float3(0.42, 0.045, 0.05);
    float3 cEdgeL3    = float3(1.0, 0.30, 0.14);
    float3 cBand      = float3(0.95, 0.25, 0.12);
    float3 cFront     = float3(1.0, 0.55, 0.28);
    float3 cCrackGlow = float3(1.0, 0.22, 0.12);
    float3 cRing      = float3(1.0, 0.15, 0.10);
    float3 cScan      = float3(1.0, 0.60, 0.48);
    float3 cExtract   = float3(1.0, 0.42, 0.20);

    //=
    //合成加法层
    //=
    float3 additive = float3(0, 0, 0);
    additive += (cGridLine * sqLine * sqOpacity * 1.10 + cGridNode * sqNode * sqNodeB) * w1 * skeletonMul;
    additive += (cHexLine * hexLine * hexLineB * 0.85 + cHexFill * hexFill) * w2 * skeletonMul;
    additive += cEdgeL3 * edgeGlowL3 * 0.85 * skeletonMul;
    additive += cBand * bands * 0.80 * skeletonMul;
    additive += cFront * frontGlow * 0.90;
    additive += cCrackGlow * edgeTotal;
    additive += cRing * entityRingTotal * entityMul;
    additive += cScan * entityScanTotal * 0.50 * entityMul;
    additive += cExtract * extractTotal * entityMul;

    //带趾软限幅：常规结构亮度原样通过，只压高亮尖峰
    float addLum = dot(additive, float3(0.299, 0.587, 0.114));
    additive *= 1.0 / (1.0 + max(addLum - 0.28, 0.0) * 0.75);

    float globalAddMul = lerp(1.0, 0.70, mFade);
    float3 finalColor = processed + additive * intensity * globalAddMul;

    //入场闪变余辉
    finalColor += float3(0.85, 0.30, 0.18) * (uFlash * uFlash) * 0.30;

    return float4(finalColor, original.a);
}

technique Technique1
{
    pass CyberspacePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
