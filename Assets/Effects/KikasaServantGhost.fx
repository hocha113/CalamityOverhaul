// ============================================================================
//KikasaServantGhost.fx 鬼奴亡躯材质：以 boss 原帧为形体载体，
//替代 KikasaItemForm 在鬼奴身上的"噪声阈值补丁"用法——
//身份不再靠随机噪点，而靠一组结构化的幽灵语言：
//  1. 血玻璃重调色：保留原贴图明度结构，全身重调入血湖色谱（形体细节不丢）
//  2. 体内血流：纵向各向异性低对比流动，血在玻璃躯壳里下淌
//  3. 透光：暗部更透、亮部更实，背景隐约透过躯体
//  4. 湖面底光：下缘暖血沫轮缘光+下身光晕包裹（光从湖里打上来），上缘沉入雾色
//  5. 下缘液化：中段以下渐次拉丝、折射晃动、底部按噪声撕成滴条（永远没凝完）
//  6. 记忆脉冲（事件驱动）：命中/主人受击时一道带自下而上扫过，
//     原版颜色随带短暂上浮再沉回血色；静止时无脉冲，躯体保持纯血玻璃
//uForm 保留出水凝实语义（1=全液态血躯 0=落定鬼躯），uDissolve 保留溶解遣返，
//uScanMode 同旧（1=纵扫 0=斑驳）。输出预乘。
//绘制方需给帧四周留透明衬边（拉丝/滴落画在衬边里），uUvRect 指真帧区域。
//s0=boss 帧 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例随机相位
float uForm;        //1=全液态血躯 0=落定鬼躯
float uDissolve;    //0=完好 1=蚀尽
float uScanMode;    //1=自上而下凝实扫描 0=噪声斑驳交融
float4 uUvRect;     //真帧区域 xy=偏移 zw=尺寸（纹理 uv 空间）
float2 uTexel;      //一像素的 uv 尺寸
float uAspect;      //帧宽/帧高
float uLiquefy;     //0..1 下缘液化强度（石巨人低、史莱姆高）
float uPulse;       //0..1 记忆脉冲强度（事件驱动，静止时 0）
float uPulsePhase;  //0..1 脉冲带扫过进度（C# 按事件时间轴推进）
float uMemory;      //0..1 原色残留（亮部保留一丝本来的颜色）

//====== 与 KikasaGrade / KikasaItemForm 同源的血湖调色 ======
static const float3 LAKE_TINT = float3(0.930, 0.300, 0.270);  //血红流层
static const float3 LAKE_FOG  = float3(0.170, 0.024, 0.036);  //深部血雾底
static const float3 FOAM_COL  = float3(0.965, 0.520, 0.440);  //血沫微光

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//帧界门：真帧之外（衬边/相邻动画帧）一律视作空像素
float inFrame(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    float2 s = step(lo, uv) * step(uv, hi);
    return s.x * s.y;
}

float frameAlpha(float2 uv) {
    return tex2D(uImage0, uv).a * inFrame(uv);
}

float4 PSServantGhost(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    //帧内归一坐标（衬边处越界，梯度函数自然外延）；噪声用等比坐标防拉伸
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float2 nuv = float2(luv.x * uAspect, luv.y);

    //====== 下缘液化梯度：起始线随列噪声起伏，不许出现整齐的水平过渡带 ======
    float colN = noiseTex(float2(luv.x * uAspect * 3.2 + uSeed * 7.0, uTime * 0.07));
    float liqStart = 0.40 + (colN - 0.5) * 0.22;
    float liqGrad = smoothstep(liqStart, 1.12, luv.y) * uLiquefy;

    //====== 折射晃动：头稳，越往下振幅越大（隔水看物） ======
    float wob = sin(luv.y * 10.0 + uTime * 2.4 + uSeed * 6.2832)
              + 0.5 * sin(luv.y * 23.0 - uTime * 3.7 + uSeed * 2.0);
    float2 suv = uv;
    suv.x += wob * uTexel.x * (0.35 + liqGrad * 2.4);

    //====== 下缘拉丝：少数列被大幅上拉采样，淌出长短分明的血丝而非绒毛 ======
    float stretch = liqGrad * liqGrad * (0.02 + pow(colN, 2.2) * 0.24);
    suv.y -= stretch * uUvRect.w;

    float inF = inFrame(suv);
    float4 src = tex2D(uImage0, suv) * inF;
    float srcA = src.a;

    //====== 血玻璃重调色：伽马压中段拉开值域，暗部沉入血雾、只有真高光浮上血沫 ======
    float luma = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float lumaG = pow(luma, 1.55);
    float3 ghost = lerp(LAKE_FOG * 0.75, LAKE_TINT, smoothstep(0.03, 0.44, lumaG));
    ghost = lerp(ghost, FOAM_COL, smoothstep(0.44, 0.86, lumaG) * 0.92);
    ghost = lerp(ghost, src.rgb, uMemory);

    //====== 体内血流：纵向下淌的低对比调制 + 湿亮闪点（血玻璃的活性） ======
    float2 fuv = float2(nuv.x * 2.2, luv.y * 0.85 - uTime * 0.14);
    float flow = noiseTex(fuv + uSeed);
    float flow2 = noiseTex(fuv * 2.4 + float2(uSeed * 1.7, -uTime * 0.05));
    ghost *= 0.82 + flow * 0.24 + flow2 * 0.10;
    float glintN = noiseTex(nuv * 1.9 + float2(-uTime * 0.05, uTime * 0.34) + uSeed * 1.7);
    ghost += FOAM_COL * pow(saturate(glintN * 1.15), 6.0) * 0.30 * lumaG;

    //====== 体积感：内部自上而下由暗转亮（湖光从下面来），上缘没入夜色 ======
    ghost *= 1.0 - smoothstep(0.45, 0.0, luv.y) * 0.22;

    //====== 透光：暗部更透、亮部更实；上缘参差渐隐；整体缓慢呼吸 ======
    float aBody = srcA * (0.52 + lumaG * 0.42);
    aBody *= 1.0 - smoothstep(0.32, -0.05, luv.y + (flow - 0.5) * 0.18) * 0.26;
    aBody *= 0.97 + 0.04 * sin(uTime * 1.2 + uSeed * 2.0);

    //====== 下缘滴落撕碎：列相干的长血丝（低横频+长纵相干+缓慢下滑） ======
    float dripN = noiseTex(float2(nuv.x * 4.5 + uSeed * 3.1, luv.y * 0.5 - uTime * 0.16));
    aBody *= saturate(1.0 - liqGrad * liqGrad * (1.0 - dripN) * 1.75);

    //====== 湖面底光：下缘轮缘光 + 下身光晕包裹，上缘沉入雾色 ======
    float aU = frameAlpha(suv - float2(0.0, uTexel.y * 1.5));
    float aD = frameAlpha(suv + float2(0.0, uTexel.y * 1.5));
    float aL = frameAlpha(suv - float2(uTexel.x * 1.5, 0.0));
    float aR = frameAlpha(suv + float2(uTexel.x * 1.5, 0.0));
    float rimDown = saturate((srcA - aD) * 2.6);
    float rimUp = saturate((srcA - aU) * 2.6);
    float rimSide = saturate((srcA - min(aL, aR)) * 2.2);
    //光包裹：不止边线，下身整体染一层暖光才读作"被湖照亮"而非"描了底边"
    float underWrap = smoothstep(0.50, 1.05, luv.y);
    ghost += FOAM_COL * underWrap * 0.12 * srcA;
    ghost = lerp(ghost, LAKE_FOG, rimUp * 0.45);
    //液化段的底光只落在部分血丝上（背光挑丝），不许糊成均匀亮边
    float rimPick = lerp(1.0, 0.3 + dripN * 1.1, liqGrad);
    float3 rimGlow = FOAM_COL * (rimDown * (0.42 + underWrap * 0.35) * rimPick
        + rimSide * 0.18 * (0.3 + liqGrad) * rimPick);

    //====== 记忆脉冲：事件驱动，带自下而上扫过，原色短暂上浮 ======
    //uPulsePhase 由 C# 按事件推进 0→1（命中/主人受击），静止时 uPulse=0 零开销
    float bandC = 1.30 - uPulsePhase * 1.60;
    float dBand = luv.y - bandC;
    float memBand = exp(-dBand * dBand / 0.018) * uPulse;
    ghost = lerp(ghost, src.rgb, memBand * 0.55);
    rimGlow += FOAM_COL * memBand * 0.10 * srcA;

    //====== 液态血躯（uForm=1 端）：沿用 KikasaItemForm 的血水材质 ======
    float n0 = noiseTex(nuv * 0.85 + float2(uSeed, uTime * 0.16 + uSeed));
    float3 blood = LAKE_FOG * 1.25 + LAKE_TINT * (0.22 + n0 * 0.40 + glintN * 0.24);
    blood += FOAM_COL * pow(saturate(glintN * 1.15), 6.0) * 0.50;
    //液态水膜轮廓（各向同性描边只属于液态）
    float minN = min(min(aL, aR), min(aU, aD));
    float rimShape = saturate((srcA - minN) * 2.4);
    blood += FOAM_COL * rimShape * 0.42;

    //====== 凝实遮罩：同旧的两模式 ======
    float jn = noiseTex(nuv * 1.3 + uSeed * 0.7);
    float scan = (1.0 - uForm) * 1.34 - 0.17 + (jn - 0.5) * 0.20;
    float maskScan = 1.0 - smoothstep(scan - 0.06, scan + 0.06, luv.y);
    float maskBlend = saturate((jn - uForm) * 3.0 + 0.5);
    float trueMask = lerp(maskBlend, maskScan, uScanMode);

    float formGate = saturate(uForm * (1.0 - uForm) * 12.0);
    float band = exp(-(luv.y - scan) * (luv.y - scan) / 0.0045) * uScanMode;
    float patchEdge = exp(-abs(jn - uForm) * 16.0) * (1.0 - uScanMode);

    //====== 溶解侵蚀 ======
    float dn = noiseTex(nuv * 1.55 + float2(uSeed * 1.9, uSeed * 0.6));
    float thr = uDissolve * 1.12 - 0.06;
    float keep = smoothstep(thr, thr + 0.09, dn);
    float eatRim = exp(-abs(dn - thr - 0.045) * 20.0) * saturate(uDissolve * 8.0);

    //====== 合成（预乘输出） ======
    float3 body = lerp(blood, ghost, trueMask);
    body = lerp(body, blood, saturate(uDissolve * 1.35) * 0.62);
    //液态端 alpha 走 srcA 全值，鬼躯端走透光 alpha
    float aMix = lerp(srcA, aBody, trueMask);
    float aOut = saturate(aMix * keep) * vc.a;
    float3 glow = (rimGlow * trueMask * keep
        + FOAM_COL * ((band + patchEdge) * formGate * 0.85 + eatRim * 0.90))
        * srcA * vc.a;
    return float4(body * vc.rgb * aOut + glow, aOut);
}

technique TechServantGhost {
    pass P0 {
        PixelShader = compile ps_3_0 PSServantGhost();
    }
}
