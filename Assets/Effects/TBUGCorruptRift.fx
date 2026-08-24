// ============================================================================
// TBUGCorruptRift.fx  TBUG 出场裂缝（黑墙 / 代码错误）
// 材质："世界渲染漏掉的一块"，不是发光的门，是缺失
// 配色：黑底 + 终端蓝，品红只作报错，禁绿（TBUG 裂缝世界观自有的色族，与 UI 无关）
// 预乘输出 + AlphaBlend：黑墙是吸光暗体，真正遮挡地形；发光成分走低 alpha
// s0 = quad 画布（内容不采样） s1 = PerlinNoise 512 灰度（LinearWrap）
// 三个签名行为：纯黑内里+坏显存色块 / 报错栈卡顿滚动 / RGB 错位量化撕裂边
// 直线算术无动态分支；成分解析归零 + guard 边界保险；轮廓量化禁圆润椭圆
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float seed;          //本实例随机种子
float openProgress;  //开口度 0~1.4：撕开/呼吸/吸气急收/吐出过冲，曲线由 C# 驱动
float spitPulse;     //吐出脉冲 0~1，边缘增辉 + 内容白闪
float collapse;      //坍塌 0~1（C# 已量化 8 档），切片按随机顺序逐条掉帧消失
float2 riftSize;     //裂缝半轴 px（halfW, halfH）
float2 quadSize;     //quad 半轴 px
float facing;        //+1 朝右 -1 朝左，镜像内部内容

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

//y 处撕裂半宽系数：两端收窄，5 档台阶量化出方块化轮廓
float TearTaper(float yn)
{
    float taper = sqrt(saturate(1.0 - yn * yn));
    float q = ceil(taper * 5.0) / 5.0;
    //端点保险归零
    return q * smoothstep(1.0, 0.955, abs(yn));
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * 2.0;
    p.y = -p.y;                        //y 向上
    float2 pxPos = p * quadSize;       //像素坐标
    pxPos.x *= facing;                 //内容随朝向镜像

    float tt = uTime;

    //边界保险：任何成分不许触到画布边
    float rectN = max(abs(p.x), abs(p.y));
    float guard = 1.0 - smoothstep(0.93, 0.995, rectN);

    //============================
    // 裂缝几何：竖直细缝，宽先窄后开，高很快到位
    //============================
    float open = max(openProgress, 0.0);
    float doorOn = smoothstep(0.02, 0.10, open);
    float axesX = riftSize.x * max(open, 0.03);
    float axesY = riftSize.y * max(saturate(open * 1.15), 0.05);
    float yn = pxPos.y / axesY;

    //失稳度：撕开初期 / 吸气收缩 / 吐出 / 坍塌时错位更狠
    float unstable = 0.5 + (1.0 - saturate(open)) * 0.8 + collapse * 1.2 + spitPulse * 0.8;

    //横向切片错位：布局每 ~0.11s 换一次，撕裂但不癫痫
    float rowH = 9.0;
    float sliceIdx = floor(pxPos.y / rowH);
    float tStep = floor(tt * 9.0);
    float sliceJit = hash11(sliceIdx * 7.31 + tStep * 0.917 + seed * 31.0) - 0.5;
    float sliceGate = step(0.55, hash11(sliceIdx * 3.17 + tStep * 1.71 + seed));
    float shift = sliceJit * sliceGate * riftSize.x * 0.35 * unstable;
    float xLoc = pxPos.x + shift;

    //坍塌：每条切片各有随机死亡阈值，一条一条掉帧消失
    float rowAlive = step(hash11(sliceIdx * 1.93 + seed * 3.0), 1.0 - collapse * 0.999);

    float wPx = TearTaper(yn) * axesX;
    float edgePx = wPx - abs(xLoc);            //>0 在缝内
    float insideMask = smoothstep(-1.0, 1.0, edgePx) * doorOn * rowAlive;

    //============================
    // 黑墙主体：吸光暗体，只带最微弱的蓝灰底
    //============================
    float3 col = float3(0.004, 0.010, 0.020) * insideMask;
    float a = 0.94 * insideMask;

    //============================
    // 报错栈：一行行往上顶、卡顿推进、行号列、整行品红反白、偶发复读
    //============================
    float lineH = 7.0;
    //卡顿滚动：按块随机跳行数，推进节奏不均匀
    float scrollT = tt * 2.2 + seed * 9.0;
    float scrollLines = floor(scrollT + 1.8 * hash11(floor(scrollT * 0.53) + seed * 5.0));
    float yy = (pxPos.y + scrollLines * lineH) / lineH;
    float rowIdx = floor(yy);
    //复读：部分行沿用上一行的内容（卡住重复）
    float rowKey = rowIdx - step(0.82, hash11(rowIdx * 0.317 + seed));
    float rowSeed = hash11(rowKey * 1.271 + seed * 13.0);

    //列坐标归一到缝的满宽（不随 taper 摆动）
    float colNorm = saturate((xLoc / max(axesX, 1.0)) * 0.5 + 0.5);
    float colIdx = floor(colNorm * 24.0);

    //字符子像素 3x4，时钟让字换帧
    float subx = floor(frac(colNorm * 24.0) * 3.0);
    float suby = floor(frac(yy) * 4.0);
    float blink = floor(tt * 2.0 + rowSeed * 4.0);
    float chPix = step(0.42, hash11(subx * 0.37 + suby * 1.91 + colIdx * 0.11 + rowKey * 0.17 + blink * 0.313));

    //行号列 / 文本区：行长随行随机
    float gutter = step(colNorm, 0.13);
    float lineLen = 0.22 + 0.62 * rowSeed;
    float textZone = step(0.16, colNorm) * step(colNorm, 0.16 + lineLen);
    //行内密度，别排满
    float charOn = step(0.28, hash11(colIdx * 1.371 + rowKey * 0.291 + seed));

    //报错行：整行品红反白
    float errGate = step(0.90, hash11(rowKey * 3.7 + seed * 7.0));

    //文本只在竖向中段 88% 出现
    float textWin = insideMask * (1.0 - smoothstep(0.78, 0.95, abs(yn)));

    //调色：终端蓝主体，行号暗蓝，报错品红
    float3 termBlue = float3(0.28, 0.62, 1.00);
    float3 dimBlue = float3(0.09, 0.24, 0.44);
    float3 errMagenta = float3(1.00, 0.24, 0.46);

    //行号（暗蓝稀疏）+ 正文（终端蓝）
    float gutterPix = chPix * gutter * charOn * textWin;
    float bodyPix = chPix * textZone * charOn * textWin;
    col += dimBlue * gutterPix * 0.55 * (1.0 - errGate);
    col += termBlue * bodyPix * 0.85 * (1.0 - errGate);
    //反白行：品红底条压满行宽，字反色成近黑（挖掉底条）
    float errBand = errGate * textWin * step(colNorm, 0.16 + lineLen + 0.02);
    col += errMagenta * errBand * 0.55;
    col -= errMagenta * bodyPix * errGate * 0.45;
    a += errBand * 0.10;

    //============================
    // 坏显存色块：品红/亮绿硬边方块，闪一两帧就消失
    //============================
    float2 cellB = floor(pxPos / 14.0);
    float bGate = step(0.992, hash11(cellB.x * 1.37 + cellB.y * 7.13 + floor(tt * 13.0) * 0.71 + seed * 3.0));
    float3 bCol = lerp(float3(1.00, 0.24, 0.46), float3(0.35, 0.70, 1.00),
        step(0.5, hash11(cellB.x + cellB.y * 3.0 + seed * 17.0)));
    col += bCol * bGate * insideMask * 0.85;
    a += bGate * insideMask * 0.08;

    //============================
    // 撕裂边缘：RGB 三通道错位细沿 + 扫描线断裂，禁柔和光环
    //============================
    float split = (1.5 + unstable * 2.0);
    float edgeR = wPx - abs(xLoc - split);
    float edgeB = wPx - abs(xLoc + split);
    float rimMain = exp(-edgePx * edgePx / 4.5);
    float rimR = exp(-edgeR * edgeR / 3.2);
    float rimB = exp(-edgeB * edgeB / 3.2);
    //部分切片的沿是断的
    float rimBreak = 0.45 + 0.55 * step(0.30, hash11(sliceIdx * 5.1 + tStep * 0.77 + seed * 2.0));
    float rimAmp = doorOn * rowAlive * rimBreak * (0.9 + spitPulse * 2.2);
    col += float3(0.28, 0.62, 1.00) * rimMain * rimAmp * 0.85;
    col += float3(1.00, 0.20, 0.42) * rimR * rimAmp * 0.30;
    col += float3(0.55, 0.86, 1.00) * rimB * rimAmp * 0.22;
    a += rimMain * rimAmp * 0.12;

    //============================
    // 边外吸入痕：短横向绿划线向缝内收，读作背景像素被拖进去
    //============================
    float outPx = max(-edgePx, 0.0);
    float availPx = max(min(quadSize.x - axesX, quadSize.y - axesY), 8.0);
    float glowWin = 1.0 - smoothstep(availPx * 0.40, availPx * 0.85, outPx);
    float streakRow = floor(pxPos.y / 3.0);
    float streakGate = step(0.84, hash11(streakRow * 2.71 + floor(tt * 6.0) * 1.31 + seed * 11.0));
    float streakLen = 12.0 + 26.0 * hash11(streakRow + seed * 4.0);
    float streak = streakGate * saturate(1.0 - outPx / streakLen) * step(0.001, outPx);
    col += float3(0.16, 0.44, 0.82) * streak * glowWin * doorOn * 0.30 * (1.0 - collapse);

    //============================
    // 吐出白闪：只在 spitPulse 峰值几帧，内容整体过曝
    //============================
    col += float3(0.86, 0.95, 1.00) * spitPulse * spitPulse * insideMask * 0.55;

    a = saturate(a);
    col *= guard;
    a *= guard;

    return float4(col, a) * input.Color;
}

technique Technique1
{
    pass TBUGCorruptRiftPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
