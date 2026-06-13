// ============================================================================
// CyberRiftSlash.fx 赛博空间数据走廊
// 深红/血红色板禁金黄过曝；Trail ps/vs
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;        //总体透明度 0~1
float visibleStart;     //可见区间起点 0~1
float visibleEnd;       //可见区间终点 0~1
float glitchSeed;       //本实例随机种子
float impactPulse;      //命中目标的脉冲强度 0~1
float corridorLength;   //走廊像素长度，用于自适应格子密度

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float cross_ = uv.y;
    float crossDist = abs(cross_ - 0.5) * 2.0;

    //可见区间裁剪
    float headMask = smoothstep(visibleEnd + 0.025, visibleEnd - 0.015, along);
    float tailMask = smoothstep(visibleStart - 0.015, visibleStart + 0.025, along);
    float visMask = headMask * tailMask;
    if (visMask < 0.001)
        return float4(0, 0, 0, 0);

    //=========================================================
    //走廊像素格密度：保持~14px 一格
    //=========================================================
    float colCount = clamp(corridorLength / 14.0, 16.0, 64.0);
    float rowCount = 7.0;

    float2 gridIdx = floor(float2(along * colCount, cross_ * rowCount));
    float2 gridFrac = frac(float2(along * colCount, cross_ * rowCount));

    float midRow = floor(rowCount * 0.5);
    float rowDist = abs(gridIdx.y - midRow);

    //=========================================================
    //数据流脉冲：从起点向终点扫一道亮带（双向呼吸）
    //=========================================================
    float flowPhase = frac(uTime * 0.45 + glitchSeed * 0.31);
    float flowDist = abs(along - flowPhase);
    float flowPulse = exp(-flowDist * flowDist * 18.0);
    float flowPhase2 = frac(uTime * 0.27 + 0.5 + glitchSeed * 0.13);
    float flowDist2 = abs(along - flowPhase2);
    flowPulse += exp(-flowDist2 * flowDist2 * 28.0) * 0.5;

    //=========================================================
    //每格状态：开关 + 在线档（无字形，纯方块栅栏）
    //=========================================================
    float timeBucket = floor(uTime * 8.0);
    float hCell = hash21(gridIdx + float2(timeBucket * 1.7, glitchSeed * 11.3));
    //中心行更高概率"在线"；外圈大量暗块
    float onThreshold = lerp(0.20, 0.78, rowDist / (rowCount * 0.5));
    float cellOn = step(onThreshold, hCell);

    //在线档：少数格子额外亮（"激活节点"）
    float lvl = hash21(gridIdx + float2(timeBucket * 0.7 + 9.0, glitchSeed * 5.1));
    float isOnline = step(0.78, lvl);
    //数据流脉冲扫到时整体提亮
    float pulseBoost = saturate(flowPulse * 1.2);

    //=========================================================
    //单格图案：红边描边方块 + 暗红内填（与 Reform 视觉对齐）
    //=========================================================
    float bx = min(gridFrac.x, 1.0 - gridFrac.x);
    float by = min(gridFrac.y, 1.0 - gridFrac.y);
    float borderDist = min(bx, by);

    //描边
    float borderLine = 1.0 - smoothstep(0.05, 0.13, borderDist);
    //内填
    float interior = smoothstep(0.10, 0.50, borderDist);
    interior = (1.0 - interior) * 0.35 + interior * 0.55;
    float interiorMask = step(0.13, borderDist);
    //角节点
    float node = (1.0 - smoothstep(0.0, 0.10, bx)) * (1.0 - smoothstep(0.0, 0.10, by));

    //=========================================================
    //黑墙裂纹腐蚀：噪声啃食描边
    //=========================================================
    float2 crackUV = frac(gridFrac + gridIdx * 0.137 + glitchSeed * 0.31);
    float crackN = tex2D(noiseSamp, crackUV).r;
    float crackBand = (1.0 - smoothstep(0.04, 0.18, borderDist));
    float cellCrackBias = hash21(gridIdx + float2(glitchSeed * 9.1, 5.7));
    float crackDamage = step(0.45 + cellCrackBias * 0.25, crackN) * crackBand;
    float borderErode = saturate(crackDamage * 0.85);

    //=========================================================
    //内部扫描线（横向流动）
    //=========================================================
    float scanPhase = frac(gridFrac.y - uTime * 0.45 + lvl * 7.7);
    float scan = smoothstep(0.0, 0.04, scanPhase) * smoothstep(0.16, 0.04, scanPhase);
    scan *= interiorMask;

    //=========================================================
    //中央主轴脊线：深血红（不再白热）
    //=========================================================
    float spineCore = 1.0 - smoothstep(0.0, 0.075, crossDist);
    spineCore = pow(saturate(spineCore), 1.6);
    //保留极轻微抖动避免死板
    float spineFlicker = 0.85 + 0.15 * sin(uTime * 26.0 + along * 60.0 + glitchSeed * 7.0);
    spineCore *= spineFlicker;
    //仅极薄一线（0.018 内）允许微弱热光，作为"高压数据线"高光
    float spineHotCore = 1.0 - smoothstep(0.0, 0.018, crossDist);

    //=========================================================
    //边缘碎屑：可见末端散落
    //=========================================================
    float edgeAlong = min(along - visibleStart, visibleEnd - along);
    float edgeFringe = smoothstep(0.06, 0.0, edgeAlong);
    float fringeHash = hash21(gridIdx + float2(timeBucket * 2.7 + 17.0, glitchSeed * 8.1));
    float fringe = step(0.66, fringeHash) * edgeFringe;

    //=========================================================
    //外缘渐隐
    //=========================================================
    float corridorMask = 1.0 - smoothstep(0.62, 0.98, crossDist);

    //=========================================================
    //尖端能量爆发：visibleEnd 处的硬光
    //=========================================================
    float tipDist = abs(along - visibleEnd);
    float tipFlare = 1.0 - smoothstep(0.0, 0.04, tipDist);
    tipFlare *= (1.0 - crossDist * 0.5);
    float tipPulse = 0.5 + 0.5 * sin(uTime * 38.0 + glitchSeed * 9.0);
    tipFlare *= 0.5 + 0.5 * tipPulse;

    //命中冲击全段提亮
    float impactGlow = impactPulse * (1.0 - crossDist * 0.3);

    //=========================================================
    //合成像素强度
    //=========================================================
    float showStrength = cellOn * corridorMask;
    //描边亮度：在线块更亮，被裂纹腐蚀
    float borderAmt = showStrength * borderLine
                    * (1.0 + isOnline * 0.5 + pulseBoost * 0.6 + impactGlow * 0.4)
                    * (1.0 - borderErode);
    //内填亮度
    float interiorAmt = showStrength * interior * interiorMask
                      * (0.50 + isOnline * 0.25 + pulseBoost * 0.35);
    //扫描线
    float scanAmt = showStrength * scan * (0.35 + isOnline * 0.4 + pulseBoost * 0.5);
    //角节点
    float nodeAmt = showStrength * node * (0.45 + isOnline * 0.45 + pulseBoost * 0.45);

    //=========================================================
    //颜色（钉死在领域底色板：暗红 / 血红 / 裂纹光，无白无金）
    //=========================================================
    float3 cInteriorDark = float3(0.10, 0.012, 0.025);  //内填暗红
    float3 cBorder       = float3(0.78, 0.08,  0.07);   //描边血红
    float3 cBorderHot    = float3(1.00, 0.18,  0.10);   //在线块鲜红
    float3 cNode         = float3(1.00, 0.22,  0.12);   //角节点
    float3 cScan         = float3(0.95, 0.15,  0.10);   //扫描线
    float3 cSpine        = float3(1.00, 0.30,  0.18);   //脊线主色（暖红，非白）
    float3 cSpineHot     = float3(1.00, 0.65,  0.40);   //仅最细芯允许的微暖
    float3 cFringe       = float3(0.95, 0.20,  0.10);   //碎屑
    float3 cTip          = float3(1.00, 0.45,  0.25);   //尖端
    float3 cImpact       = float3(1.00, 0.55,  0.30);   //命中冲击

    float3 color = float3(0, 0, 0);
    color += cInteriorDark * interiorAmt * 1.6;
    color += lerp(cBorder, cBorderHot, isOnline * 0.7 + pulseBoost * 0.5) * borderAmt;
    color += cScan * scanAmt;
    color += cNode * nodeAmt * 0.65;
    color += cSpine * spineCore * 0.75;
    color += cSpineHot * spineHotCore * 0.55;
    color += cFringe * fringe * 0.6;
    color += cTip * tipFlare * 0.85;
    color += cImpact * impactGlow * 0.7;

    //alpha
    float alpha = saturate(
          borderAmt * 0.95
        + interiorAmt * 0.55
        + scanAmt * 0.6
        + nodeAmt * 0.65
        + spineCore * 0.55
        + spineHotCore * 0.5
        + fringe * 0.5
        + tipFlare * 0.55
        + impactGlow * 0.5
    );
    alpha *= fadeAlpha * visMask;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass CyberRiftSlashPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
