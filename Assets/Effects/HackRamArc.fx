// ============================================================================
// HackRamArc.fx 骇客时间RAM弧形HUD着色器
// 赛博朋克2077风格的穹顶状RAM资源条
// SDF抗锯齿弧线、能量液面流动、扫描线、色散、低RAM故障效果
// ============================================================================
// 参数说明：
//   uResolution     绘制quad的像素尺寸
//   uArcCenter      弧线圆心在quad内的像素坐标
//   uInnerR/uOuterR 弧线内外半径(像素)
//   uAStart         起始角度(弧度)
//   uCellAngle      单格角度跨度(弧度)
//   uCellGap        格子之间的间隙角度(弧度)
//   uCellCount      总格数
//   uFillValue      当前显示的RAM值(浮点,支持分数)
//   uLowRam         低RAM警告强度(0~1)
//   uLockFill       系统锁定剩余比例(0~1)，用于绘制故障倒计时填充
//   uInfinite       无限模式标志(0或1)
//   uDecoOuterR     外侧装饰环半径
//   uDecoInnerR     内侧装饰环半径
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float2 uArcCenter;
float uInnerR;
float uOuterR;
float uAStart;
float uCellAngle;
float uCellGap;
float uCellCount;
float uFillValue;
float uLowRam;
float uLockFill;
float uInfinite;
float uDecoOuterR;
float uDecoInnerR;

//================== 工具函数 ==================

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//角度环绕到[0,2pi)，支持 SHPC 小弧条超过半圈的跨度
float wrapAngle(float a) {
    a = fmod(a, 6.28318530);
    if (a < 0) a += 6.28318530;
    return a;
}

//================== 主像素着色 ==================

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vcol : COLOR0) : COLOR0
{
    float2 p = uv * uResolution;
    float2 d = p - uArcCenter;
    float r = length(d);
    float ang = atan2(d.y, d.x);

    float cellStride = uCellAngle + uCellGap;
    float totalSweep = cellStride * uCellCount - uCellGap;

    //环绕到起始角,得到相对角度
    float aRel = wrapAngle(ang - uAStart);

    //是否在弧段角度范围内
    float inAngleRange = step(-0.001, aRel) * step(aRel, totalSweep + 0.001);

    //径向归一化位置(0=内径,1=外径)
    float radT = (r - uInnerR) / max(uOuterR - uInnerR, 0.001);

    float3 outCol = float3(0, 0, 0);
    float outA = 0;

    //==================================================
    //整体弧区(含些许边缘软化)
    //==================================================
    float radialAA = 1.0;
    {
        //环SDF: 到环中线距离减去半厚度
        float midR = (uInnerR + uOuterR) * 0.5;
        float halfThick = (uOuterR - uInnerR) * 0.5;
        float ringSDF = abs(r - midR) - halfThick;
        //AA
        radialAA = 1.0 - smoothstep(0.0, 1.5, ringSDF);
    }

    if (radialAA > 0.002 && inAngleRange > 0.5) {
        //==========================
        //格子索引与本地坐标
        //==========================
        float cellIdxF = aRel / cellStride;
        float cellIdx = floor(cellIdxF);
        float cellLocalRaw = cellIdxF - cellIdx;
        //0~1在cell内,>1在gap中
        float cellLocal = cellLocalRaw * cellStride / uCellAngle;
        float inCell = step(cellLocal, 1.0);

        //当前格的填充量
        float fillAmt = saturate(uFillValue - cellIdx);
        float filledHere = step(cellLocal, fillAmt) * inCell;

        //==========================
        //背景(深色槽位)
        //==========================
        float3 bgCol = float3(0.035, 0.07, 0.10);
        //向内径方向的阴影梯度(模拟槽位内凹)
        float innerShadow = smoothstep(0.0, 0.35, radT);
        bgCol *= 0.55 + innerShadow * 0.85;
        //像素扫描网纹
        float grid = step(0.5, frac(p.y * 0.55 + uTime * 0.02)) * 0.04;
        bgCol += float3(0.02, 0.05, 0.06) * grid;

        outCol = bgCol;
        outA = 0.88;

        //==========================
        //能量填充
        //==========================
        if (filledHere > 0.5) {
            float3 baseCyan = float3(0.0, 0.78, 0.90);
            float3 hotCyan  = float3(0.35, 1.00, 1.00);

            //纵向(径向)液面光泽: 内侧较暗,中段最亮,外侧有高光
            float lightV = 1.0 - abs(radT - 0.55) * 1.8;
            lightV = saturate(lightV);
            float3 fillCol = baseCyan * (0.55 + lightV * 0.7);

            //外缘高光带
            float outerHL = smoothstep(0.72, 0.96, radT);
            fillCol += float3(0.45, 0.85, 0.95) * outerHL * 0.55;

            //内缘柔和冷光
            float innerHL = smoothstep(0.25, 0.00, radT);
            fillCol += float3(0.05, 0.35, 0.50) * innerHL * 0.6;

            //沿弧方向的能量脉冲流(每个cell独立相位)
            float flowU = cellLocal * 6.283 - uTime * 3.2 + cellIdx * 1.17;
            float flowWave = sin(flowU) * 0.5 + 0.5;
            fillCol = lerp(fillCol, hotCyan, flowWave * 0.22);

            //快速纵向扫描光带(1~2条细线在cell内滑动)
            float scanT = frac(uTime * 0.6 + cellIdx * 0.19);
            float scanDist = abs(cellLocal - scanT);
            float scanLine = exp(-scanDist * 22.0);
            fillCol += hotCyan * scanLine * 0.4;

            //细密扫描线(水平像素线)
            float hScan = step(0.52, frac(p.y * 1.2 + uTime * 0.4));
            fillCol *= 1.0 - hScan * 0.08;

            //表面颗粒噪声
            float grain = valueNoise(p * 0.9 + float2(0, uTime * 0.8));
            fillCol += float3(0.05, 0.10, 0.12) * (grain - 0.5) * 0.25;

            //满格: 更亮+微呼吸
            float fullBonus = step(0.999, fillAmt);
            float pulse = sin(uTime * 2.2 + cellIdx * 0.7) * 0.08 + 0.92;
            fillCol *= 1.0 + fullBonus * (pulse - 0.92) * 3.0;
            fillCol += fullBonus * float3(0.1, 0.25, 0.30) * pulse * 0.3;

            outCol = fillCol;
            outA = 0.92;

            //==========================
            //填充前沿边缘光(仅部分填充的cell)
            //==========================
            float partial = 1.0 - fullBonus;
            float edgeDist = abs(cellLocal - fillAmt);
            float edgePx = edgeDist * uCellAngle * r;
            //锐利前沿线
            float frontLine = smoothstep(2.0, 0.2, edgePx) * partial;
            outCol += hotCyan * frontLine * 1.5;
            //色散: 偏内侧偏青,偏外侧偏白/粉
            float caOffset = (radT - 0.5) * 2.0;
            outCol.r += frontLine * partial * saturate(caOffset) * 0.6;
            outCol.b += frontLine * partial * saturate(-caOffset) * 0.4;
        }

        //==========================
        //系统锁定倒计时填充：满弧=刚锁定，逐步退空=即将恢复
        //==========================
        float lockActive = saturate(uLockFill) * (1.0 - uInfinite);
        float lockFillAmt = saturate(lockActive * uCellCount - cellIdx);
        float lockFilledHere = step(cellLocal, lockFillAmt) * inCell;
        if (lockActive > 0.001) {
            if (lockFilledHere > 0.5) {
                float pulseLock = sin(uTime * 8.0) * 0.5 + 0.5;
                float3 lockRed = lerp(float3(0.85, 0.06, 0.05), float3(1.0, 0.36, 0.12), pulseLock * 0.35);
                float lockSheen = smoothstep(0.15, 0.85, radT) * (1.0 - smoothstep(0.86, 1.0, radT));
                outCol = lerp(outCol, lockRed * (0.65 + lockSheen * 0.55), 0.72);
                outA = max(outA, 0.9);

                float lockPartial = 1.0 - step(0.999, lockFillAmt);
                float lockEdgeDist = abs(cellLocal - lockFillAmt);
                float lockEdgePx = lockEdgeDist * uCellAngle * r;
                float lockFrontLine = smoothstep(2.4, 0.25, lockEdgePx) * lockPartial;
                outCol += float3(1.0, 0.75, 0.45) * lockFrontLine * 1.15;
            }
        }

        //==========================
        //cell边界: 径向封口+内外环细线
        //==========================
        {
            //左右径向线的像素距离(用弧长近似)
            float sideAngDist = min(cellLocalRaw, 1.0 - cellLocalRaw) * cellStride;
            float sidePx = sideAngDist * r;
            float sideLine = smoothstep(1.2, 0.3, sidePx) * inCell;
            float3 borderCol = lerp(float3(0.14, 0.32, 0.38), float3(0.35, 0.82, 0.92),
                                    step(0.999, fillAmt));
            outCol = lerp(outCol, borderCol, sideLine * 0.75);

            //内外径边界
            float rEdge = min(r - uInnerR, uOuterR - r);
            float edgeLine = smoothstep(1.2, 0.3, rEdge);
            outCol = lerp(outCol, borderCol * 0.85, edgeLine * 0.55);
        }

        //==========================
        //间隙区域: 微弱深色填充
        //==========================
        float inGap = 1.0 - inCell;
        outCol = lerp(outCol, float3(0.01, 0.015, 0.02), inGap * 0.9);
        outA *= 1.0 - inGap * 0.25;

        //==========================
        //低RAM故障叠加：作用于整条弧段，包含空槽与填充格
        //空槽改为深红底色，填充格叠加猩红脉冲，整体加扫描线与高频抖动
        //==========================
        float warnActive = uLowRam * (1.0 - uInfinite);
        if (warnActive > 0.01) {
            float pulseWarn = sin(uTime * 8.0) * 0.5 + 0.5;
            float fastFlash = step(0.5, frac(uTime * 14.0));
            //空槽底色：从冷色背景平滑染向暗红
            float3 emptyRed = float3(0.18, 0.015, 0.02);
            float3 hotRed = float3(0.95, 0.08, 0.10);
            //空槽染色强度：随警告强度饱和
            float emptyMix = warnActive * (0.55 + pulseWarn * 0.35) * inCell;
            outCol = lerp(outCol, emptyRed, emptyMix * (1.0 - max(filledHere, lockFilledHere)));
            //填充格上叠加猩红脉冲
            outCol = lerp(outCol, hotRed,
                          warnActive * (0.45 + pulseWarn * 0.4) * filledHere);
            //横向扫描线：警告强度高时整条弧出现错位条带
            float bandY = frac(p.y * 0.18 - uTime * 1.6);
            float band = smoothstep(0.46, 0.5, bandY) * (1.0 - smoothstep(0.5, 0.54, bandY));
            outCol += hotRed * band * warnActive * 0.55;
            //高频闪烁亮度抖动
            outCol *= 1.0 + warnActive * fastFlash * 0.25;
            //保证空槽也有可见 alpha
            outA = max(outA, warnActive * 0.85 * inCell);
        }

        outA *= radialAA;
    }

    //==================================================
    //外侧装饰环
    //==================================================
    {
        float ringDist = abs(r - uDecoOuterR);
        //主细弧
        float ringLine = smoothstep(1.2, 0.3, ringDist);
        //扩展tick角度范围,允许超过主弧一点点
        float tickInRange = step(-0.02, aRel) * step(aRel, totalSweep + 0.02);
        outCol = lerp(outCol, float3(0.18, 0.45, 0.52), ringLine * tickInRange * 0.55);
        outA = max(outA, ringLine * tickInRange * 0.5);

        //刻度线: 每格4个细分刻度
        float tickDensity = uCellCount * 4.0;
        float tickPhase = aRel / totalSweep * tickDensity;
        float tickMark = frac(tickPhase);
        float isTick = step(0.88, tickMark) + step(tickMark, 0.12);
        //主刻度(整格)更粗
        float majorTick = step(0.95, 1.0 - abs(frac(tickPhase / 4.0) - 0.5) * 2.0);
        float tickLen = lerp(4.0, 9.0, majorTick);
        float tickInner = uDecoOuterR;
        float tickOuter = uDecoOuterR + tickLen;
        float inTickBand = step(tickInner, r) * step(r, tickOuter);
        //刻度线的角宽度
        float tickAngThick = lerp(0.001, 0.002, majorTick);
        float nearTickAng = abs(frac(tickPhase) - round(frac(tickPhase))) * totalSweep / tickDensity;
        float tickWidth = smoothstep(tickAngThick * 1.2, tickAngThick * 0.2, nearTickAng);
        float tickVis = inTickBand * tickWidth * tickInRange;
        float3 tickCol = lerp(float3(0.14, 0.32, 0.38), float3(0.32, 0.72, 0.82), majorTick);
        outCol = lerp(outCol, tickCol, tickVis * 0.7);
        outA = max(outA, tickVis * 0.55);
    }

    //==================================================
    //内侧装饰环 + 扫描脉冲
    //==================================================
    if (r < uInnerR) {
        float ringDist = abs(r - uDecoInnerR);
        float ringLine = smoothstep(1.4, 0.3, ringDist);
        float tickInRange = step(-0.02, aRel) * step(aRel, totalSweep + 0.02);

        //基础细环
        outCol = lerp(outCol, float3(0.12, 0.30, 0.36), ringLine * tickInRange * 0.45);
        outA = max(outA, ringLine * tickInRange * 0.4);

        //扫描亮带: 周期性从左到右扫过
        float scanT = frac(uTime * 0.28);
        float scanAng = scanT * totalSweep;
        float scanDelta = abs(aRel - scanAng);
        //高斯衰减
        float scanFalloff = exp(-pow(scanDelta / 0.08, 2.0));
        //扫描带也扩展一点径向范围
        float scanRadFall = exp(-pow((r - uDecoInnerR) / 3.0, 2.0));
        float scanGlow = scanFalloff * scanRadFall * tickInRange;
        outCol += float3(0.25, 0.85, 0.95) * scanGlow * 0.9;
        outA = max(outA, scanGlow * 0.75);

        //沿弧的数据粒子(小亮点)
        for (int i = 0; i < 4; i++) {
            float particleT = frac(uTime * 0.22 + i * 0.25);
            float particleAng = particleT * totalSweep;
            float particleDelta = abs(aRel - particleAng);
            float particleRad = uDecoInnerR + sin(uTime * 3.0 + i * 2.1) * 1.2;
            float particleRDelta = abs(r - particleRad);
            float particleFall = exp(-pow(particleDelta / 0.008, 2.0)) * exp(-pow(particleRDelta / 1.2, 2.0));
            outCol += float3(0.4, 1.0, 1.0) * particleFall * tickInRange * 0.8;
            outA = max(outA, particleFall * tickInRange * 0.7);
        }
    }

    //==================================================
    //外侧弧线柔和辉光(填充格的"漏光")
    //==================================================
    if (r > uOuterR && r < uOuterR + 4.0 && inAngleRange > 0.5) {
        float cellIdxF = aRel / cellStride;
        float cellIdx = floor(cellIdxF);
        float cellLocalRaw = cellIdxF - cellIdx;
        float cellLocal = cellLocalRaw * cellStride / uCellAngle;
        float fillAmt = saturate(uFillValue - cellIdx);
        float filled = step(cellLocal, fillAmt) * step(cellLocal, 1.0);
        float glowFall = exp(-(r - uOuterR) / 2.0);
        outCol += float3(0.25, 0.85, 0.95) * filled * glowFall * 0.35;
        outA = max(outA, filled * glowFall * 0.4);
    }

    //==================================================
    //低RAM整体故障染色：装饰环、刻度、辉光一并染红，确保 RAM=0 锁定状态下也清晰可见
    //==================================================
    if (uLowRam > 0.01 && uInfinite < 0.5) {
        float pulseG = sin(uTime * 5.0) * 0.5 + 0.5;
        float strength = saturate(uLowRam * (0.55 + pulseG * 0.45));
        //通道交换 + 提红降蓝绿
        float3 tinted = float3(
            outCol.r * 1.6 + max(outCol.g, outCol.b) * 0.65 + 0.08,
            outCol.g * 0.18,
            outCol.b * 0.18);
        outCol = lerp(outCol, tinted, strength);
        //周期性高频暗闪，模拟显示器故障
        float glitch = step(0.8, frac(uTime * 11.0));
        outCol *= 1.0 - strength * glitch * 0.35;
    }

    //==================================================
    //输出（按 outA 预乘以匹配 AlphaBlend，避免空区域漏出底色）
    //==================================================
    float finalA = outA * uAlpha;
    return float4(outCol * finalA, finalA);
}

technique Technique1
{
    pass HackRamArcPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
