// ============================================================================
// CyberReform.fx 赛博空间体素重组/分解
// direction +1重组 -1分解；s0+s1 Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float fadeAlpha;        //整体透明度 0~1
float reformProgress;   //演出进度 0~1
float snapPulse;        //"咔嗒"重现/解构闪光强度 0~1
float dissipate;        //后段消散进度 0~1
float seed;             //本实例随机种子
float direction;        //+1重组 -1分解

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 p = input.TexCoords - float2(0.5, 0.5);
    float r = length(p);

    //圆形剪裁
    if (r > 0.55) return float4(0, 0, 0, 0);

    //=========================================================
    //目标网格：18x18，每格归属于一个固定的 cell 索引
    //=========================================================
    const float CELL = 18.0;
    float2 cellIdx = floor(input.TexCoords * CELL);
    float2 cellCenterUV = (cellIdx + 0.5) / CELL;
    float2 cellP = cellCenterUV - float2(0.5, 0.5);

    //轮廓蒙版（人形：高瘦椭圆）
    float2 ellip = cellP / float2(0.30, 0.42);
    float ellipDist = dot(ellip, ellip);
    float silhouette = step(ellipDist, 1.0);

    //外环装饰：稀疏点亮一些环带格子，丰富剪影外的视觉
    float ringR = length(cellP);
    float inRing = step(0.32, ringR) * step(ringR, 0.50);
    float ringHash = hash21(cellIdx + float2(seed * 7.7, 17.3));
    float isRingCell = inRing * step(0.82, ringHash);

    float belongs = saturate(silhouette + isRingCell);
    if (belongs < 0.5) {
        belongs = 0.0;
    }

    //=========================================================
    //每格独立时序：lag 控制先后差
    //=========================================================
    float lag = hash21(cellIdx + float2(seed * 3.7, 1.7)) * 0.40;
    float laggedT = saturate((reformProgress - lag) / max(1.0 - lag, 0.001));
    float ease = laggedT * laggedT * (3.0 - 2.0 * laggedT);

    //=========================================================
    //飞行偏移：每格随机径向 + 切向方向，让动画有"数据飞入/飞散"的方向感
    //=========================================================
    float2 radialDir = (ringR < 0.001) ? float2(1.0, 0.0) : (cellP / ringR);
    float2 tangent = float2(-radialDir.y, radialDir.x);
    float jitter = (hash21(cellIdx + float2(seed * 5.1, 9.7)) - 0.5) * 1.0;
    float2 flyDir = normalize(radialDir + tangent * jitter);
    float flyDistMax = 0.30 + hash21(cellIdx + float2(seed * 11.0, 27.0)) * 0.20;

    //direction = +1 (reform): 起始远离目标，归位时回到目标
    //direction = -1 (decompose): 起始在目标，飞远离散
    float offsetMag = (direction > 0.0) ? (1.0 - ease) * flyDistMax : ease * flyDistMax;
    float2 currentCellPos = cellP + flyDir * offsetMag;

    //=========================================================
    //当前像素在该格"飞到位置"内的局部坐标 [0,1]
    //=========================================================
    float2 toCell = (p - currentCellPos) * CELL + 0.5;
    float inside = step(0.0, toCell.x) * step(toCell.x, 1.0)
                 * step(0.0, toCell.y) * step(toCell.y, 1.0);
    if (inside < 0.5) inside = 0.0;

    //=========================================================
    //每格的"激活态"：是否该格当前可见
    //通过 lag 控制先后；direction=+1 时，进度推进过 lag 才激活；direction=-1 时反之
    //=========================================================
    float activeStrength;
    if (direction > 0.0) {
        //reform: laggedT 推进时格子越来越实
        activeStrength = smoothstep(0.0, 0.18, laggedT) * (1.0 - smoothstep(1.05, 1.25, laggedT));
    }
    else {
        //decompose: 0 时全实，逐渐淡出
        activeStrength = (1.0 - smoothstep(0.55, 1.0, laggedT));
    }

    //=========================================================
    //单格图案：红边描边方块 + 内部暗红渐变
    //完全不使用字形/字模，与领域边缘栅栏视觉对齐
    //=========================================================
    //到格子边缘的距离（局部坐标 [0,1] 内）
    float bx = min(toCell.x, 1.0 - toCell.x);
    float by = min(toCell.y, 1.0 - toCell.y);
    float borderDist = min(bx, by);

    //描边线：靠近边的部分高亮
    //调整边宽 ~12% 让线粗细可见
    float borderLine = 1.0 - smoothstep(0.05, 0.13, borderDist);
    //内填渐变：从中心暗到边缘略亮
    float interior = smoothstep(0.10, 0.50, borderDist);
    interior = (1.0 - interior) * 0.35 + interior * 0.55;
    //内填区域：避开描边的部分
    float interiorMask = step(0.13, borderDist);
    //描边硬边：避免太软
    float borderMask = step(borderDist, 0.13);

    //角节点强调：四角小亮点（与领域栅格节点呼应）
    float node = (1.0 - smoothstep(0.0, 0.10, bx)) * (1.0 - smoothstep(0.0, 0.10, by));

    //=========================================================
    //黑墙细节层 1：每格随机"激活档"——多数为暗块，少数为高亮"在线"块
    //=========================================================
    float lvl = hash21(cellIdx + float2(seed * 1.3, 41.0));
    float isOnline = step(0.82, lvl);
    //在线块的 1Hz 心跳
    float onlinePulse = 0.6 + 0.4 * sin(uTime * 4.5 + lvl * 12.0);

    //=========================================================
    //黑墙细节层 2：噪声驱动的裂纹腐蚀——每格独立的损伤蒙版
    //有的格子描边被"啃"出豁口，模拟黑墙的不规则侵蚀
    //=========================================================
    float2 crackUV = frac(toCell + cellIdx * 0.137 + seed * 0.31);
    float crackN = tex2D(noiseSamp, crackUV).r;
    //裂纹仅出现在描边附近（borderDist < 0.18）
    float crackBand = (1.0 - smoothstep(0.04, 0.18, borderDist));
    //裂纹密度：每格不同强度
    float cellCrackBias = hash21(cellIdx + float2(seed * 9.1, 5.7));
    float crackDamage = step(0.45 + cellCrackBias * 0.25, crackN) * crackBand;
    //损伤会"扣掉"描边亮度，让边缘看起来锈蚀
    float borderErode = saturate(crackDamage * 0.85);

    //=========================================================
    //黑墙细节层 3：内部单条暗扫描线
    //=========================================================
    //每格独立扫描相位
    float scanPhase = frac(toCell.y - uTime * 0.45 + lvl * 7.7);
    float scan = smoothstep(0.0, 0.04, scanPhase) * smoothstep(0.16, 0.04, scanPhase);
    //仅在内填区域显现，避开描边
    scan *= interiorMask;

    //归位 / 解构 边缘 SNAP（reform 临归位/decompose 刚撕开）
    float settleFlash = 0.0;
    if (direction > 0.0) {
        settleFlash = smoothstep(0.85, 1.0, laggedT) * (1.0 - smoothstep(1.0, 1.10, laggedT));
    }
    else {
        settleFlash = (1.0 - smoothstep(0.0, 0.18, laggedT));
    }

    //=========================================================
    //外缘消散（仅 reform 后期外圈先熄灭）
    //=========================================================
    float dissMask = 1.0;
    if (direction > 0.0) {
        dissMask = 1.0 - smoothstep(0.50 - dissipate * 0.50, 0.55 - dissipate * 0.50, ringR);
    }

    //=========================================================
    //中心 SNAP 闪光：玩家在中心实体化（深红基调，避免白热过曝）
    //=========================================================
    float snapRadial = 1.0 - smoothstep(0.0, 0.30, r);
    //倾斜十字撕裂带
    float angle1 = 0.18;
    float angle2 = 1.5708 + 0.10;
    float2 d1 = float2(cos(angle1), sin(angle1));
    float2 d2 = float2(cos(angle2), sin(angle2));
    float band1 = exp(-pow(dot(p, float2(-d1.y, d1.x)) * 28.0, 2.0))
                * smoothstep(0.40, 0.0, abs(dot(p, d1)));
    float band2 = exp(-pow(dot(p, float2(-d2.y, d2.x)) * 32.0, 2.0))
                * smoothstep(0.45, 0.0, abs(dot(p, d2)));
    float snap = (snapRadial * 0.7 + (band1 + band2 * 0.85) * 0.9) * snapPulse;
    //snap 极小芯（仅最中心 0.06 以内）才允许极弱白热点缀
    float snapTinyCore = (1.0 - smoothstep(0.0, 0.04, r)) * snapPulse;

    //=========================================================
    //合成像素强度
    //=========================================================
    float showStrength = belongs * inside * activeStrength * dissMask;
    //描边亮度（主视觉）：在线块更亮，裂纹处被腐蚀扣减
    float borderAmt = showStrength * borderLine
                    * (1.0 + isOnline * 0.5 + settleFlash * 0.9 + isOnline * (onlinePulse - 0.6) * 0.8)
                    * (1.0 - borderErode);
    //内填亮度（暗红填充）
    float interiorAmt = showStrength * interior * interiorMask
                      * (0.50 + isOnline * 0.25 + settleFlash * 0.4);
    //内部扫描线亮度
    float scanAmt = showStrength * scan * (0.35 + isOnline * 0.4);
    //角节点
    float nodeAmt = showStrength * node * (0.50 + isOnline * 0.45 + settleFlash * 0.7);

    //=========================================================
    //颜色（与领域底色统一：暗红/血红/鲜红/裂纹光，无金黄、无过曝白）
    //完全采用 CyberspaceField 的色板，确保黑墙调性一致
    //=========================================================
    float3 cInteriorDark = float3(0.10, 0.012, 0.025); //内填暗红（更深）
    float3 cBorder       = float3(0.78, 0.08,  0.07);  //描边血红
    float3 cBorderHot    = float3(1.00, 0.18,  0.10);  //在线块描边鲜红（无橙黄）
    float3 cNode         = float3(1.00, 0.22,  0.12);  //角节点裂纹光
    float3 cScan         = float3(0.95, 0.15,  0.10);  //扫描线鲜红
    float3 cSnapCore     = float3(1.00, 0.40,  0.25);  //SNAP 主色：深橙红，不再纯白
    float3 cSnapTinyHot  = float3(1.00, 0.85,  0.65);  //仅极中心点的微弱热斑

    float3 col = float3(0, 0, 0);
    col += cInteriorDark * interiorAmt * 1.6;
    col += lerp(cBorder, cBorderHot, isOnline * 0.7 + settleFlash * 0.5) * borderAmt;
    col += cScan * scanAmt;
    col += cNode * nodeAmt * 0.65;
    col += cSnapCore * snap;
    col += cSnapTinyHot * snapTinyCore * 0.6;

    //alpha：描边为主，内填、扫描、节点、SNAP 辅助
    float alpha = saturate(
          borderAmt * 0.95
        + interiorAmt * 0.55
        + scanAmt * 0.6
        + nodeAmt * 0.65
        + snap * 0.85
    );
    alpha *= fadeAlpha;

    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass CyberReformPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
