// ============================================================================
//OniStanceBlade.fx 架势鞘刀，刃/鞘段(镡到鞘尾)作架势计:
//钢(沿轴肌理+刃文+刃线)自左(鲤口)向右(锋尖)按 uReveal 淹没黑漆鞘身,
//拔刀线=钢与漆的分界,蓄势时分界微光爬亮;满架势刃线白热呼吸+流光巡刃;
//释放时白热拔刀闪沿刃扫出,读数由 CPU 快速回落。空势=一柄安静的鞘中刀。
//终结乱舞(uLockHeat/uLockPulse):刀钉在出鞘位时钢自刃口向栋侧烧成绯红、
//刃线成白热丝、火星顺刃流向锋尖,每记落刀整刀过曝;归鞘后漆身自鲤口透出余温冷却。
//刃文/肌理吃恒定 uSeed,笔形每帧稳定;AlphaBlend 预乘输出;色板 CPU 传入与主题同源。
//全程直线算术:无 if/早退,体外像素由 step 门乘熄灭(驱动首绘编译兼容律)
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;   //quad 像素尺寸
float uReveal;        //0~1 拔刀进度(钢的右缘)
float uFlow;          //进度变化速度,+蓄/-泄
float uFullGlow;      //0~1 满架势刃口点火
float uReleaseFlash;  //0~1 释放拔刀闪
float uLockHeat;      //0~1 终结乱舞灼热(钢在烧/漆身余温)
float uLockPulse;     //0~1 乱舞拍点闪
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

//三倍频手动展开,字节码不出循环
float fbm3(float2 p) {
    float v = 0.5 * valueNoise(p);
    p = p * 2.13 + float2(1.7, 9.2);
    v += 0.25 * valueNoise(p);
    p = p * 2.13 + float2(1.7, 9.2);
    v += 0.125 * valueNoise(p);
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
    //体外 16px 之外熄灭:门乘替代早退
    float inside = 1.0 - step(16.0, min(bladeSDF, sayaSDF));

    float heat = saturate(uLockHeat);
    float pulse = saturate(uLockPulse);
    //乱舞热噬:沿刃爬行的噪声,钢的烧灼与漆的余温共用
    float heatCrawl = valueNoise(float2(u * 16.0 - uTime * 2.6, dy * 0.6 + uTime * 0.9) + uSeed * 3.1);

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

    //====终结乱舞:钢在烧====
    //热自刃口向栋侧渗(刃侧 1 → 栋侧 0.4),随爬行噪声起伏,呼吸比满势急
    float heatGrad = 1.0 - saturate((dy + bladeHalf) / max(2.0 * bladeHalf, 0.001)) * 0.6;
    float heatBreath = 0.5 + 0.5 * sin(uTime * 7.0 + u * 2.0);
    float heatBody = heat * heatGrad * (0.55 + 0.45 * heatBreath) * (0.65 + 0.7 * heatCrawl);
    float3 heatCol = lerp(uColDeep, uColBright, 0.35 + 0.65 * heatCrawl);
    steel = lerp(steel, steel * 0.55 + heatCol * 1.05, saturate(heatBody));
    //刃线烧成白热的丝,闪烁急促;刃文跟着透亮
    float heatFlicker = 0.55 + 0.45 * sin(uTime * 23.0 + u * 14.0 + heatCrawl * 3.0);
    steel += uColHot * edgeLine * heat * (0.6 + 0.6 * heatFlicker);
    steel += uColHot * hamon * heat * 0.35 * heatFlicker;
    //火星:稀疏白热亮点顺刃流向锋尖(横向拉长成短划),只在刀身带内
    float speckN = valueNoise(float2(u * 44.0 - uTime * 7.0, dy * 1.1 + uSeed * 5.0));
    float speckBand = 1.0 - smoothstep(0.0, 4.0, abs(dy) - bladeHalf);
    steel += uColHot * smoothstep(0.80, 0.94, speckN) * speckBand * heat * 0.9;
    //拍点:整刀过曝一瞬
    steel += (uColHot * 0.45 + uColBright * 0.3) * pulse * (0.4 + edgeLine * 1.2 + hamon * 0.6);

    float steelMask = (1.0 - smoothstep(-0.7, 0.7, bladeSDF)) * steelSide;

    //====鞘:黑漆 + 缓移光泽 + 下绪缠带 + 鞘尾铜口====
    float3 lacq = uColInk * 0.92 + uColDeep * 0.10;
    lacq *= 0.92 + (valueNoise(float2(u * 8.0, dy * 0.6) + uSeed) - 0.5) * 0.10;
    float sheenT = frac(uTime * 0.07 + uSeed * 0.2);
    lacq += uColPaper * exp(-pow((u - sheenT) * 9.0, 2.0)) * 0.05;
    //上缘一线淡纸光,黑漆悬在夜里也有轮廓
    float sayaTopLine = exp(-pow(px.y - (axisY - sayaHalf), 2.0) * 1.1);
    lacq += uColPaper * sayaTopLine * 0.16;
    //下绪缠带:两道深红束带
    float wrap = exp(-pow((u - 0.58) * 60.0, 2.0)) + exp(-pow((u - 0.70) * 60.0, 2.0));
    lacq = lerp(lacq, uColDeep * 0.85, saturate(wrap) * 0.75);
    //鞘尾铜口
    float kojiri = smoothstep(0.975, 0.99, u);
    lacq = lerp(lacq, uColDeep * 1.15, kojiri * 0.8);

    //====归鞘后的余温:热刀刚进鞘,漆身自鲤口向鞘尾透出暗红,鲤口边线与缠带发亮,随冷却熄去====
    float sayaHeat = heat * (1.0 - smoothstep(0.0, 0.85, u)) * (0.6 + 0.4 * heatCrawl);
    lacq = lerp(lacq, lerp(uColDeep, uColBright, 0.45) * 0.9, saturate(sayaHeat) * 0.7);
    lacq += uColBright * sayaTopLine * sayaHeat * 0.45;
    lacq = lerp(lacq, uColBright * 0.9, saturate(wrap) * heat * 0.35);

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

    //====外辉:深红微光衬底,黑漆黑钢在深色洞穴背景上也读得清;灼热与拍点把辉光烘亮====
    float bodySDF = lerp(sayaSDF, bladeSDF, steelSide);
    float bodyMask = 1.0 - smoothstep(-0.7, 0.7, bodySDF);
    float outerA = exp(-max(bodySDF, 0.0) * 0.30) * (1.0 - bodyMask)
        * (0.16 + uFullGlow * 0.10 + uReleaseFlash * 0.25 + heat * 0.28 + pulse * 0.30);
    float3 outerCol = lerp(uColDeep, uColBright, saturate(heat * 0.55 + pulse * 0.3));

    //====预乘 over 合成(后→前)====
    float3 C = float3(0.0, 0.0, 0.0);
    float A = 0.0;
    OverLayer(C, A, outerCol, outerA);
    OverLayer(C, A, lacq, sayaMask);
    OverLayer(C, A, steel, steelMask);
    OverLayer(C, A, boundCol, boundA);
    OverLayer(C, A, uColHot, flashA);

    return float4(C, A) * uAlpha * vertexColor * inside;
}

technique Technique1
{
    pass OniStanceBladePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
