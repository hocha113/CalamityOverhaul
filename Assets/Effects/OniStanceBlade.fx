// ============================================================================
//OniStanceBlade.fx 架势鞘刀——刃/鞘段(镡到鞘尾)作架势计:
//钢(沿轴肌理+刃文+刃线)自左(鲤口)向右(锋尖)按 uReveal 淹没黑漆鞘身,
//拔刀线=钢与漆的分界,蓄势时分界微光爬亮;满架势刃线白热呼吸+流光巡刃;
//释放时白热拔刀闪沿刃扫出,读数由 CPU 快速回落。空势=一柄安静的鞘中刀。
//刃文/肌理吃恒定 uSeed,笔形每帧稳定;AlphaBlend 预乘输出;色板 CPU 传入与主题同源
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;   //quad 像素尺寸
float uReveal;        //0~1 拔刀进度(钢的右缘)
float uFlow;          //进度变化速度,+蓄/-泄
float uFullGlow;      //0~1 满架势刃口点火
float uReleaseFlash;  //0~1 释放拔刀闪
float uSeed;          //形状种子(会话内恒定)
float3 uColInk;       //墨黑(漆)
float3 uColPaper;     //纸白(钢底)
float3 uColDeep;      //深红
float3 uColBright;    //亮绯红
float3 uColHot;       //白热

#define PI 3.14159265

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

float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.13 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

//预乘 over 合成
void OverLayer(inout float3 C, inout float A, float3 c, float a) {
    C = c * a + C * (1.0 - a);
    A = a + A * (1.0 - a);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float padX = 3.0;
    float x0 = padX;
    float x1 = uResolution.x - padX;
    float u = saturate((px.x - x0) / (x1 - x0));
    float midY = uResolution.y * 0.5;

    //刀轴:向锋尖微起的反(sori);u=0 处贴 CPU 侧镡的高度
    float axisY = midY - sin(u * 1.5707) * 2.0;
    float dy = px.y - axisY;   //<0 刃侧(上),>0 栋侧(下)

    //刀身半高:近匀,切先收窄出锋
    float bladeHalf = uResolution.y * 0.14;
    bladeHalf *= 1.0 - smoothstep(0.90, 0.995, u) * 0.92;
    bladeHalf = max(bladeHalf, 0.5);
    //鞘身半高:比刀肥一圈,鞘尾圆收
    float sayaHalf = uResolution.y * 0.20;
    sayaHalf *= 1.0 - smoothstep(0.965, 1.0, u) * 0.55;

    float bladeSDF = abs(dy) - bladeHalf;
    float sayaSDF = abs(dy) - sayaHalf;
    if (min(bladeSDF, sayaSDF) > 16.0) {
        return float4(0, 0, 0, 0);
    }

    //====钢/漆分界:利落笃定,拔刀线就是读数====
    float reveal = saturate(uReveal);
    float revealX = lerp(x0, x1, reveal);
    float steelSide = 1.0 - smoothstep(-0.8, 0.8, px.x - revealX);
    float sayaSide = 1.0 - steelSide;

    //====钢:沿轴肌理 + 绯色环境反光 + 刃文 + 刃线====
    float grain = valueNoise(float2(u * 110.0, dy * 1.8) + uSeed * 13.0);
    float3 steel = lerp(uColPaper * 0.55, uColPaper * 0.88, grain);
    //栋侧沉影,立体
    steel *= 1.0 - saturate(dy / max(bladeHalf, 0.001)) * 0.18;
    //绯色环境反光:这把刀活在血色世界里
    steel += uColDeep * (fbm3(float2(u * 5.0, uTime * 0.05) + uSeed) - 0.40) * 0.22;

    //刃文:贴刃侧(上缘)的波带,恒定种子
    float edgeTopY = axisY - bladeHalf;
    float hamonOff = 1.6 + valueNoise(float2(u * 26.0, uSeed * 7.0)) * 2.6;
    float hamon = exp(-pow((px.y - (edgeTopY + hamonOff)) * 0.55, 2.0));
    steel += uColHot * hamon * 0.13;

    //刃线:上缘一线;满架势白热呼吸 + 流光巡刃
    float edgeLine = exp(-pow(px.y - edgeTopY, 2.0) * 1.4);
    float edgeBreath = 0.5 + 0.5 * sin(uTime * 2.6);
    float edgeStr = 0.30 + uFullGlow * (0.40 + 0.45 * edgeBreath);
    steel += uColHot * edgeLine * edgeStr;
    float runX = frac(uTime * 0.20);
    float lightRun = exp(-pow((u - runX) * 16.0, 2.0)) * uFullGlow;
    steel += uColHot * lightRun * (edgeLine * 1.6 + hamon * 0.8) * 0.85;

    float steelMask = (1.0 - smoothstep(-0.7, 0.7, bladeSDF)) * steelSide;

    //====鞘:黑漆 + 缓移光泽 + 下绪缠带 + 鞘尾铜口====
    float3 lacq = uColInk * 0.92 + uColDeep * 0.10;
    lacq *= 0.92 + (valueNoise(float2(u * 8.0, dy * 0.6) + uSeed) - 0.5) * 0.10;
    float sheenT = frac(uTime * 0.07 + uSeed * 0.2);
    lacq += uColPaper * exp(-pow((u - sheenT) * 9.0, 2.0)) * 0.05;
    //上缘一线淡纸光,黑漆悬在夜里也有轮廓
    lacq += uColPaper * exp(-pow(px.y - (axisY - sayaHalf), 2.0) * 1.1) * 0.16;
    //下绪缠带:两道深红束带
    float wrap = exp(-pow((u - 0.58) * 60.0, 2.0)) + exp(-pow((u - 0.70) * 60.0, 2.0));
    lacq = lerp(lacq, uColDeep * 0.85, saturate(wrap) * 0.75);
    //鞘尾铜口
    float kojiri = smoothstep(0.975, 0.99, u);
    lacq = lerp(lacq, uColDeep * 1.15, kojiri * 0.8);

    float sayaMask = (1.0 - smoothstep(-0.7, 0.7, sayaSDF)) * sayaSide;

    //====拔刀线(鲤口位):分界上的绯亮竖光,蓄势时更亮;空/满两端隐去====
    float bd = px.x - revealX;
    float boundGlow = exp(-bd * bd * 0.06);
    float boundCore = exp(-bd * bd * 0.5);
    float boundVis = smoothstep(0.008, 0.03, reveal) * (1.0 - smoothstep(0.955, 0.995, reveal));
    float boundBody = 1.0 - smoothstep(0.0, 6.0, sayaSDF);
    float boundA = (boundGlow * 0.35 + boundCore * 0.75) * boundVis * boundBody
        * (0.45 + saturate(uFlow) * 0.60 + uFullGlow * 0.20 + uReleaseFlash * 0.5);
    float3 boundCol = lerp(uColBright, uColHot, saturate(boundCore));

    //====释放拔刀闪:白热流光沿刃扫出 + 钢面短暂过曝====
    float bladeBand = 1.0 - smoothstep(0.0, 3.0, abs(dy) - uResolution.y * 0.14);
    float streakU = (1.0 - uReleaseFlash) * 1.5 - 0.25;
    float streak = exp(-pow((u - streakU) * 5.0, 2.0));
    float flashA = (steelMask * 0.45 + streak * bladeBand * 0.85) * uReleaseFlash;

    //====外辉:深红微光衬底,黑漆黑钢在深色洞穴背景上也读得清====
    float bodySDF = lerp(sayaSDF, bladeSDF, steelSide);
    float bodyMask = 1.0 - smoothstep(-0.7, 0.7, bodySDF);
    float outerA = exp(-max(bodySDF, 0.0) * 0.30) * (1.0 - bodyMask)
        * (0.16 + uFullGlow * 0.10 + uReleaseFlash * 0.25);

    //====预乘 over 合成(后→前)====
    float3 C = float3(0.0, 0.0, 0.0);
    float A = 0.0;
    OverLayer(C, A, uColDeep, outerA);
    OverLayer(C, A, lacq, sayaMask);
    OverLayer(C, A, steel, steelMask);
    OverLayer(C, A, boundCol, boundA);
    OverLayer(C, A, uColHot, flashA);

    return float4(C, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniStanceBladePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
