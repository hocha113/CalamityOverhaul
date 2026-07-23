// ============================================================================
//OniMeiBlade.fx 改铭台横陈刀身——解剑鉴定的裸刃全览(锋左茎右,刃上栋下):
//素钢(沿轴肌理+镐线+刃文+刃口白线)自切先收窄铺到区(machi),
//铜金 habaki 一箍,右段裸茎(黑锈 patina+鑢目斜纹+目钉孔);
//丁子油光泽带缓移,烛光自下缘暖染栋侧,绯色环境反光同 OniStanceBlade 语言。
//形状吃恒定 uSeed 每帧稳定;AlphaBlend 预乘输出;色板 CPU 传入与主题同源
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;   //quad 像素尺寸
float uSeed;          //形状种子(会话内恒定)
float uTangFrac;      //茎段占比(右侧)
float3 uColInk;       //墨黑
float3 uColPaper;     //纸白(钢底)
float3 uColDeep;      //深红
float3 uColHot;       //白热
float3 uColGold;      //金象嵌亮
float3 uColGoldDeep;  //金象嵌暗
float3 uColCandle;    //烛焰暖

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

//双八度噪声:茎锈用,比三八度省约 30 槽
float fbm2(float2 p) {
    return valueNoise(p) * 0.62 + valueNoise(p * 2.13 + float2(1.7, 9.2)) * 0.38;
}

//预乘 over 合成
void OverLayer(inout float3 C, inout float A, float3 c, float a) {
    C = c * a + C * (1.0 - a);
    A = a + A * (1.0 - a);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float padX = 6.0;
    float x0 = padX;
    float x1 = uResolution.x - padX;
    float u = saturate((px.x - x0) / (x1 - x0));
    float midY = uResolution.y * 0.5;

    //刀轴:轻微反り,刀腹向上拱起一线(鉴刀横陈的弧)
    float axisY = midY - sin(u * PI) * (uResolution.y * 0.016);
    float dy = px.y - axisY;   //<0 刃侧(上),>0 栋侧(下)

    float tangStart = 1.0 - uTangFrac;
    bool isTang = u >= tangStart;

    //====半高:切先收窄出锋,茎瘦一圈,茎尾轻收====
    float bladeHalf = uResolution.y * 0.135;
    float kTip = saturate(u / 0.085);
    bladeHalf *= kTip * (2.0 - kTip);
    bladeHalf = max(bladeHalf, 0.5);
    float tangHalf = uResolution.y * 0.135 * 0.82;
    tangHalf *= 1.0 - smoothstep(0.988, 1.0, u) * 0.35;
    float half_ = isTang ? tangHalf : bladeHalf;

    float bodySDF = abs(dy) - half_;
    if (bodySDF > 14.0) {
        return float4(0, 0, 0, 0);
    }

    float edgeY = axisY - half_;
    float muneY = axisY + half_;

    //====钢(刃区)====
    float grain = valueNoise(float2(u * 130.0, dy * 1.7) + uSeed * 13.0);
    float3 steel = lerp(uColPaper * 0.50, uColPaper * 0.85, grain);
    //上承光下沉影
    steel *= 1.0 - saturate(dy / max(half_, 0.001)) * 0.22;
    steel *= 1.0 + saturate(-dy / max(half_, 0.001)) * 0.08;
    //镐线:一线极淡的分面(偏栋侧)
    steel += uColPaper * exp(-pow((dy - half_ * 0.16) * 1.1, 2.0)) * 0.07;
    //绯色环境反光:这把刀活在血色世界里
    steel += uColDeep * (valueNoise(float2(u * 5.0, uTime * 0.04) + uSeed) - 0.40) * 0.20;
    //刃文:贴刃侧的波带,恒定种子,高频细节走 sin 省噪声
    float hamonOff = 2.0 + valueNoise(float2(u * 22.0, uSeed * 7.0)) * 4.6
        + sin(u * 197.0 + uSeed * 21.0) * 0.7;
    float hamon = exp(-pow((px.y - (edgeY + hamonOff)) * 0.50, 2.0));
    steel += uColHot * hamon * 0.15;
    //刃口白线
    float edgeLine = exp(-pow(px.y - edgeY, 2.0) * 1.3);
    steel += uColHot * edgeLine * 0.48;
    //丁子油光泽带:缓移的高光,擦过时钢面微亮
    float sheenU = frac(uTime * 0.05 + uSeed * 0.31);
    steel += uColPaper * exp(-pow((u - sheenU) * 8.0, 2.0)) * 0.07;
    //烛光自下:栋缘接一线暖
    steel += uColCandle * exp(-pow(px.y - muneY, 2.0) * 0.7) * 0.18;

    //====茎(裸铁)====
    float patina = fbm2(float2(u * 30.0, dy * 0.33) + uSeed * 5.0);
    float3 tang = lerp(uColInk * 1.25, float3(0.30, 0.20, 0.13), patina * 0.85);
    //鑢目:斜向锉痕细纹
    float yasu = sin((px.x + px.y * 1.6) * 1.05 + uSeed * 40.0);
    tang -= smoothstep(0.72, 0.96, yasu) * 0.055;
    //茎下缘接烛光,上缘一线冷灰轮廓
    tang += uColCandle * exp(-pow(px.y - muneY, 2.0) * 0.7) * 0.12;
    tang += uColPaper * exp(-pow(px.y - edgeY, 2.0) * 1.1) * 0.08;
    //目钉孔:一眼穿茎,底缘接一点光
    float2 holeC = float2(lerp(x0, x1, 0.815), axisY);
    float holeD = length(px - holeC);
    float hole = 1.0 - smoothstep(4.2, 6.0, holeD);
    tang = lerp(tang, uColInk * 0.55, hole);
    tang = lerp(tang, uColInk * 0.25, 1.0 - smoothstep(2.4, 3.8, holeD));
    tang += uColCandle * exp(-pow(holeD - 6.0, 2.0) * 0.35) * saturate((px.y - axisY) / 6.0) * 0.22;

    //====habaki 铜金口:区前一箍,盖刃====
    float habL = tangStart - 0.033;
    float habMask = smoothstep(habL - 0.004, habL, u) * (1.0 - smoothstep(tangStart - 0.004, tangStart, u));
    float3 hab = uColGoldDeep * (0.85 + grain * 0.25);
    hab += uColGold * exp(-pow(px.y - edgeY, 2.0) * 0.9) * 0.75;
    hab += uColCandle * exp(-pow(px.y - muneY, 2.0) * 0.8) * 0.20;

    //====区(machi)分界:一线深红落影====
    float machi = exp(-pow((u - tangStart) * (x1 - x0), 2.0) * 0.30);

    //====选色与合成====
    float3 body = isTang ? tang : steel;
    body = lerp(body, hab, habMask);
    body -= uColDeep * machi * 0.12;

    float bodyMask = 1.0 - smoothstep(-0.7, 0.7, bodySDF);
    //外辉:深红微光衬底,黑背景上也读得清
    float outerA = exp(-max(bodySDF, 0.0) * 0.30) * (1.0 - bodyMask) * 0.15;

    float3 C = float3(0.0, 0.0, 0.0);
    float A = 0.0;
    OverLayer(C, A, uColDeep, outerA);
    OverLayer(C, A, body, bodyMask);

    return float4(C, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniMeiBladePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
