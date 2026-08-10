// ============================================================================
// VictorCyberPortal.fx  Victor 出场赛博裂隙门（2026-08 重制）
// 预乘输出 + AlphaBlend：门内是吸光暗体（真正遮挡地形），发光成分走低 alpha
// s0 = quad 画布（内容不采样） s1 = PerlinNoise 512 灰度（LinearWrap）
// 角向采样一律用单位方向向量喂噪声（无 atan2，无极角接缝）
// 直线算术无动态分支；所有成分在画布 ~93% 内解析归零 + guard 边界保险
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float seed;           //本实例随机种子
float openProgress;   //门张开度 0~1，允许过冲 ~1.15，弹簧曲线由 C# 驱动
float slit;           //竖缝亮度 0~1：前兆发丝缝 / 收口余晖共用
float slitLen;        //竖缝半长占门半高比例
float flare;          //浮现增辉 0~1
float collapse;       //收口失稳 0~1：吸入加速 + 边缘失稳 + 数据熄灭
float uPower;         //供能 0~1，断电闪烁压发光成分（暗体不受影响）
float uFlash;         //白闪 0~1，仅撕开/收口瞬间 ≤2 帧
float2 portalSize;    //门椭圆半轴 px
float2 quadSize;      //quad 半轴 px
float facing;         //+1 朝右 -1 朝左，翻转内部流向

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

//刚体旋转，笛卡尔连续无接缝
float2 Rot(float2 v, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

//吸入隧道缩放层：phase 控缩放与淡入淡出窗口，特征随时间向中心收缩
float TunnelLayer(float2 pe, float phase, float2 off)
{
    float w = smoothstep(0.0, 0.30, phase) * (1.0 - smoothstep(0.70, 1.0, phase));
    float s = exp2(lerp(-1.2, 1.8, phase));
    return tex2D(noiseSamp, pe * s * 0.42 + off).r * w;
}

//数据竖列：x=字符像素 y=列头，纯 hash 无取样，笛卡尔像素空间
float2 DataColumns(float2 px, float t, float cellW, float cellH, float density, float localSeed)
{
    float colIdx = floor(px.x / cellW);
    float colSeed = hash11(colIdx * 0.713 + localSeed * 3.17);
    float yy = (px.y + t * (26.0 + colSeed * 44.0)) / cellH;
    float rowIdx = floor(yy);
    float on = step(1.0 - density, hash11(colIdx * 1.371 + rowIdx * 0.291 + localSeed));
    float subx = floor(frac(px.x / cellW) * 3.0);
    float suby = floor(frac(yy) * 4.0);
    float clock = floor(t * 5.0 + colSeed * 9.0);
    float pix = step(0.42, hash11(subx * 0.37 + suby * 1.91 + colIdx * 0.11 + rowIdx * 0.17 + clock * 0.313));
    float head = step(0.82, hash11(rowIdx + colSeed * 17.0)) * (1.0 - smoothstep(0.0, 0.30, frac(yy))) * on;
    return float2(on * pix, head);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * 2.0;
    p.y = -p.y;                       //y 向上
    float2 pxPos = p * quadSize;      //像素坐标
    pxPos.x *= facing;                //内部流向随朝向镜像

    float tt = uTime;

    //边界保险：任何成分不许触到画布边
    float rectN = max(abs(p.x), abs(p.y));
    float guard = 1.0 - smoothstep(0.93, 0.995, rectN);

    //————————————————————————————
    // 竖缝：门外独立存在（前兆发丝缝 / 收口余晖）
    //————————————————————————————
    float slitHalf = max(slitLen, 0.02) * portalSize.y;
    float lenWin = 1.0 - smoothstep(slitHalf * 0.55, slitHalf, abs(pxPos.y));
    float stutter = 0.55 + 0.45 * hash11(floor(tt * 21.0) + seed * 7.0);
    float sCore = exp(-pxPos.x * pxPos.x / 6.0);
    float sHalo = exp(-pxPos.x * pxPos.x / 170.0);
    float slitAmt = slit * lenWin * stutter;
    float3 col = (float3(1.0, 0.90, 0.78) * sCore * 1.35 + float3(1.0, 0.36, 0.15) * sHalo * 0.5) * slitAmt;
    float a = (sCore * 0.40 + sHalo * 0.08) * slitAmt;

    //————————————————————————————
    // 门几何
    //————————————————————————————
    float open = max(openProgress, 0.0);
    float doorOn = smoothstep(0.02, 0.12, open);
    float2 axes = portalSize * max(open, 0.03);
    float2 pe = pxPos / axes;
    float ellipR = length(pe);
    float2 dir = pe / max(ellipR, 1e-4);      //单位方向向量：无缝角向载体

    //撕裂缘噪声（方向向量采样，笛卡尔连续）
    float nA = tex2D(noiseSamp, dir * 0.35 + float2(tt * 0.055, -tt * 0.034) + seed * 0.71).r;
    float nB = tex2D(noiseSamp, dir * 0.83 + float2(-tt * 0.047, tt * 0.062) + seed * 1.37).r;
    float rimNoise = nA * 0.65 + nB * 0.35 - 0.5;
    float instability = 0.30 + (1.0 - saturate(open)) * 0.55 + collapse * 0.60;
    float spike = pow(saturate(nA * nB * 2.6), 5.0);
    float rimSdf = ellipR - 1.0 - rimNoise * 0.15 * instability - spike * 0.05 * instability;

    float insideMask = smoothstep(0.015, -0.055, rimSdf) * doorOn;
    float depth = saturate(1.0 - ellipR);

    //供能：断电闪烁只压发光，不动暗体
    float powerMul = lerp(0.22, 1.0, saturate(uPower));

    //————————————————————————————
    // 故障切片：对内部内容做真 UV 错位（不动门形）
    //————————————————————————————
    float rowJit = hash11(floor(tt * 1.9) + seed) * 3.0;
    float sliceRow = floor(uv.y * 9.0 + rowJit);
    float sliceGate = step(0.74, hash11(sliceRow * 3.71 + floor(tt * 4.3) * 1.13 + seed * 5.1));
    float sliceMag = (hash11(sliceRow * 7.77 + floor(tt * 4.3) + seed) - 0.5) * sliceGate;
    float shiftPx = sliceMag * portalSize.x * (0.16 + collapse * 0.35 + (1.0 - saturate(uPower)) * 0.25);
    float2 pxi = pxPos;
    pxi.x += shiftPx;

    //————————————————————————————
    // 门内暗体 + 吸入隧道
    //————————————————————————————
    float bodyA = 0.92 * insideMask;
    col += float3(0.014, 0.004, 0.007) * (0.55 + depth * 0.85) * bodyA;
    a += bodyA;

    float2 pei = pxi / axes;
    float zt = tt * (0.14 + collapse * 0.60);
    float2 peR = Rot(pei, tt * 0.12);
    float tun = TunnelLayer(peR, frac(zt), float2(seed * 0.31, seed * 0.77));
    tun += TunnelLayer(peR, frac(zt + 0.3333), float2(0.43 + seed * 0.59, 0.19));
    tun += TunnelLayer(peR, frac(zt + 0.6667), float2(0.87, 0.55 + seed * 0.23));
    tun *= 1.55;
    float tunnel = tun * tun;

    float annulus = saturate(depth * (1.0 - depth) * 4.0);   //中环亮带，中心留深洞
    float scan = 0.92 + 0.08 * sin(pxPos.y * 0.55 - tt * 2.3);
    float3 interior = float3(0.40, 0.045, 0.028) * tunnel * annulus;
    interior += float3(0.85, 0.16, 0.09) * tunnel * tunnel * annulus * 0.6;

    //————————————————————————————
    // 数据竖列：双景深层（远小暗慢 + 近大亮快），中心被黑暗吞没
    //————————————————————————————
    float colFade = smoothstep(0.02, 0.22, depth) * (1.0 - smoothstep(0.50, 0.92, depth));
    colFade *= 1.0 - collapse * 0.85;
    float2 dFar = DataColumns(pxi * 1.55 + float2(37.0, 11.0), tt, 6.0, 10.0, 0.34, seed);
    float2 dNear = DataColumns(pxi, tt * 1.6, 8.5, 14.0, 0.30, seed + 4.7);
    float cellCyan = step(0.968, hash11(floor(pxi.x / 8.5) * 2.71 + floor(pxi.y / 14.0) * 0.53 + seed));
    float3 charColNear = lerp(float3(0.88, 0.11, 0.06), float3(0.18, 0.80, 0.90), cellCyan);
    interior += float3(0.45, 0.06, 0.04) * dFar.x * 0.5 * colFade;
    interior += float3(0.95, 0.42, 0.22) * dFar.y * 0.35 * colFade;
    interior += charColNear * dNear.x * 0.95 * colFade;
    interior += float3(1.0, 0.55, 0.30) * dNear.y * 0.8 * colFade;

    //切片色偏：错位行上下边缘红/青细线
    float rowFrac = frac(uv.y * 9.0 + rowJit);
    float lineTop = smoothstep(0.0, 0.05, rowFrac) * (1.0 - smoothstep(0.05, 0.13, rowFrac));
    float lineBot = smoothstep(0.87, 0.95, rowFrac) * (1.0 - smoothstep(0.95, 1.0, rowFrac));
    interior += float3(0.90, 0.10, 0.07) * lineTop * sliceGate * 0.5;
    interior += float3(0.16, 0.70, 0.85) * lineBot * sliceGate * 0.28;

    interior *= scan * insideMask;

    //————————————————————————————
    // 裂缘：内侧能量带 + 热芯（暖白不常驻）
    //————————————————————————————
    float pxLen = length(pxPos);
    float sPx = pxLen * (1.0 - 1.0 / max(ellipR, 1e-3));  //近边像素距离，门外为正
    float rimBandPx = max(-sPx, 0.0);
    float innerGlow = exp(-rimBandPx / 9.0) * insideMask;
    float hotCore = exp(-rimBandPx / 2.4) * insideMask;
    float rimFlick = 0.72 + 0.28 * nB;
    float3 rimCol = float3(1.0, 0.40, 0.17) * innerGlow * (0.95 + collapse * 0.7);
    rimCol += float3(1.0, 0.76, 0.52) * hotCore * 0.95;
    rimCol *= rimFlick;

    //————————————————————————————
    // 门外辉光：analytic 收尾 + guard，杜绝画布硬切
    //————————————————————————————
    float availPx = min(quadSize.x - axes.x, quadSize.y - axes.y);
    float glowWin = 1.0 - smoothstep(availPx * 0.45, availPx * 0.88, sPx);
    float og = exp(-max(sPx - spike * 14.0, 0.0) / 15.0) * (1.0 - insideMask) * glowWin * doorOn;
    float3 ogCol = float3(1.0, 0.40, 0.17) * og * (0.55 + 0.30 * rimFlick);
    ogCol += float3(0.90, 0.09, 0.05) * og * og * 0.55;

    //————————————————————————————
    // 浮现增辉：中心泛光 + 双轴衰减短十字（每轴都解析归零）
    //————————————————————————————
    float bloom = exp(-ellipR * ellipR * 2.6) * doorOn;
    float crossV = exp(-pxPos.x * pxPos.x / 90.0) * exp(-pow(pxPos.y / (portalSize.y * 0.60), 2.0));
    float crossH = exp(-pxPos.y * pxPos.y / 60.0) * exp(-pow(pxPos.x / (portalSize.x * 0.55), 2.0));
    float3 flareCol = (float3(1.0, 0.82, 0.60) * bloom * 1.15
        + float3(1.0, 0.52, 0.28) * (crossV * 0.85 + crossH * 0.30) * doorOn) * flare;

    //————————————————————————————
    // 合成（预乘）
    //————————————————————————————
    col += (interior + rimCol + ogCol) * powerMul;
    col += flareCol;
    col += float3(1.0, 0.94, 0.86) * uFlash * (insideMask * 0.85 + og * 0.45 + slitAmt * 0.5);

    a += (innerGlow * 0.20 + hotCore * 0.15) * powerMul;
    a += og * 0.08 + bloom * flare * 0.30;
    a = saturate(a);

    col *= guard;
    a *= guard;

    return float4(col, a) * input.Color;
}

technique Technique1
{
    pass VictorCyberPortalPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
