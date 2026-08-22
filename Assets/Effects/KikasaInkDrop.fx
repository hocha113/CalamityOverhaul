// ============================================================================
//KikasaInkDrop.fx 墨雨，鬼伞普攻的演出主角
//TechDrop:材质=一笔水墨,不是液珠，细长中脊笔触(头收笔锋、腹在前 28%、长锥收尾)、
//         左右异 seed 蚀边(墨的边不对称)、uBend 随飞行曲率弓身(笔随轨迹弯)、
//         体中段飞白镂空(干笔擦痕)、尾后噪声卷曲的墨丝;血统只留脊线一线暗红
//         头在 quad 上缘(v=0):C# 侧 rotation = 速度角 + PiOver2
//TechPour:倒撑重击的墨瀑柱，材质=倾倒的黑墨水瀑,起止两端皆有收口:
//         源头=碗口溢流球根+球根下颈缩(任何时刻无水平实切),
//         落点=推进期坠落头/触地后溅丘+上卷翻沫+撕裂接触线/无落点时散逸成股,
//         排空=uDrainV 自源撕裂啃掉+尾沿断成滴串;
//         质感=重力加速签名(sqrt 纵坐标上密下疏+柱身沿程收窄)+股束浓淡+双层视差流,
//         中轴血芯一线;uWidthT 宽度全生命周期包络(展开/塌缩由 C# 缓动)
//坐标全笛卡尔（无 atan2）；直线算术+普通 tex2D，FNA3D 安全
//预乘输出，进 AlphaBlend 批，黑要读作黑，加色批画不出黑
//绑定噪声实测值域 0.227~0.776(2026-08 像素探针,三通道同灰度),阈值须过 nrm() 归一
//消费入口 KikasaRains/KikasaRainRender.cs
// ============================================================================

float uTime;
float uSeed;
float uFade;      //出生淡入 0~1
float uStretch;   //速度拉伸 0~1.4，0=表面张力拉圆
float uWobAmp;    //张力抖动幅度（顶点滞空放大）
float uWobPhase;  //抖动相位（CPU 侧 life 驱动，暂停即冻结）
float uBend;      //弓身:飞行转向角速度,笔触随轨迹弯(带符号)
float uChurn;     //墨瀑落点搅浊强度 0~1
float uWScale;    //墨瀑:一个柱宽的 v 跨度(WidthPx/quadH)
float uSrcV;      //墨瀑:碗口面 v(球根中心带)
float uFront;     //墨瀑:推进前锋 v
float uSpanV;     //墨瀑:接触面/最大射程 v
float uGrounded;  //墨瀑:1=触地(落点收口),0=空中散逸
float uDrainV;    //墨瀑:排空前沿 v(<0=未排)
float uWidthT;    //墨瀑:宽度生命周期包络 0~1
float uSway;      //墨瀑:甩尾行波相位(源头钉死,末端摆)
float uFill;      //墨瀑:蓄力档 0~1(沫量/飞沫微调)
float3 uColBody;  //墨体近黑
float3 uColDeep;  //暗血缘
float3 uColCore;  //血芯
float3 uColSheen; //湿反光

sampler uNoiseTex : register(s1);

float4 PSDrop(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 raw = coords * 2.0 - 1.0; //y 负方向=运动方向（头在上缘）
    float2 q = raw;

    //笔触参数轴:s=0 笔锋(头) → s=1 收笔,拉伸把整笔抽长
    float headY = -0.55;
    float strokeLen = 0.72 + uStretch * 0.42;
    float s = (q.y - headY) / strokeLen;
    float sc = saturate(s);

    //中脊:uBend 弓身(头贴轨迹、尾向外甩)+尾段噪声游走
    float nSpine = tex2D(uNoiseTex, float2(uSeed * 1.3, sc * 0.9 + uSeed)).r;
    float spineX = uBend * pow(sc, 1.6) * 0.5
                 + (nSpine - 0.5) * 0.10 * sc;

    //宽度谱:笔锋尖入(快时更尖)、腹在 28% 处、长锥收尾;滞空时张力微鼓
    float wob = sin(uWobPhase) * uWobAmp;
    float headPow = lerp(0.34, 0.62, saturate(uStretch));
    float wMax = 0.22 * (1.0 + wob * 0.6) * (1.0 - uStretch * 0.22);
    float w = wMax * pow(saturate(sc / 0.28), headPow)
                   * pow(saturate((1.0 - sc) / 0.72), 1.35);

    //左右异 seed 蚀边:墨的边不是对称几何,尾段更毛
    float dx = q.x - spineX;
    float nL = tex2D(uNoiseTex, float2(uSeed * 2.1, sc * 1.7 + uSeed * 0.7)).g;
    float nR = tex2D(uNoiseTex, float2(uSeed * 3.7 + 0.5, sc * 1.4 + uSeed * 1.9)).b;
    float eN = lerp(nL, nR, step(0.0, dx));
    float d = abs(dx) - w + (eN - 0.5) * 0.055 * (0.45 + sc * 0.9);
    d += (1.0 - step(0.0, s)) * 1.0 + step(1.0, s) * 1.0; //轴外硬裁,笔就这一划

    float body = 1.0 - smoothstep(-0.008, 0.030, d);

    //飞白:体中段沿长轴的高频条纹镂空,头保实,越快越干
    float fb = tex2D(uNoiseTex, float2(q.x * 5.5 + uSeed * 5.0, sc * 1.1 + uSeed)).r;
    float flyWhite = smoothstep(0.54, 0.74, fb)
        * smoothstep(0.16, 0.42, sc) * (1.0 - smoothstep(0.72, 0.95, sc))
        * (0.30 + 0.45 * saturate(uStretch));
    body *= 1.0 - flyWhite;

    //晕染薄纹:墨往空气里洇的一小圈
    float halo = (1.0 - smoothstep(0.0, 0.16, d)) * (1.0 - body) * step(0.0, s) * (1.0 - step(1.1, s));

    //卷须尾:收笔后 2~3 条噪声卷曲的细丝,越快拖越长
    float b = s - 0.82;
    float wispSpan = 0.28 + saturate(uStretch) * 0.5;
    float zone = smoothstep(0.0, 0.10, b) * (1.0 - smoothstep(wispSpan * 0.55, wispSpan, b));
    float curl = (tex2D(uNoiseTex, float2(uSeed * 7.1, s * 1.2 - uTime * 0.4)).r - 0.5) * 0.34 * max(b, 0.0);
    float wx = q.x - spineX - curl;
    float nW = tex2D(uNoiseTex, float2(wx * 3.2 + uSeed * 4.3, s * 0.8 + uSeed * 2.2)).g;
    float wisp = zone * smoothstep(0.55, 0.78, nW)
        * exp2(-wx * wx * 10.0 / (0.10 + b * 1.1))
        * saturate(uStretch * 1.3 + 0.25);

    //体色:头浓尾淡(墨在笔锋上最饱),缘略沉
    float rimBand = 1.0 - smoothstep(0.0, 0.05, -d);
    float3 bodyCol = lerp(uColBody, uColDeep, sc * 0.35 + rimBand * 0.30);
    //血统:脊线一线暗红,不再是发光核
    float vein = (1.0 - smoothstep(0.006, 0.028, abs(dx)))
        * smoothstep(0.12, 0.3, sc) * (1.0 - smoothstep(0.6, 0.8, sc));
    bodyCol = lerp(bodyCol, uColCore, vein * 0.35);

    //湿反光:腹侧极小一点,不给"珠"的光学证据
    float sheen = 1.0 - smoothstep(0.0, 0.05, length(q - float2(spineX - w * 0.4, headY + strokeLen * 0.26)));
    sheen *= body * 0.5;

    //预乘合成
    float aBody = body * 0.95;
    float aHalo = halo * 0.16;
    float aWisp = wisp * 0.42;
    float3 col = bodyCol * aBody
               + lerp(uColBody, uColDeep, 0.45) * (aHalo + aWisp);
    float a = saturate(aBody + aHalo + aWisp);
    col += uColSheen * sheen * 0.18;

    //画布护栏：uv 边缘前归零防切边
    float guard = smoothstep(1.0, 0.86, max(abs(raw.x), abs(raw.y)));
    float k = uFade * guard;
    return float4(col * k, a * k) * vertexColor;
}

//==================== 墨瀑(倒撑重击) ====================

//绑定噪声归一:实测值域 0.227~0.776,映到 0~1 后阈值才有效(0.80+ 全是死代码)
float nrm(float n) { return saturate((n - 0.23) * 1.82); }

float4 PSPour(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float v = coords.y;              //0=碗口上沿(沉口) → uSpanV=接触面/最大射程
    float xc = (coords.x - 0.5) * 2.0;
    float ws = max(uWScale, 0.004);  //一个柱宽的 v 跨度,宽度相关纵向特征全用它标定

    //--- 中轴:源头钉死碗口,行波+噪声游走沿程放大，流体甩尾,不是刚体摆 ---
    float swayT = saturate((v - uSrcV) / max(uSpanV - uSrcV, 0.05));
    float swayEnv = pow(swayT, 1.5);
    float nSpine = nrm(tex2D(uNoiseTex, float2(uSeed * 3.1, v * 1.1 - uTime * 0.6)).r);
    float spine = sin(v * 5.6 - uSway) * 0.075 * swayEnv
                + (nSpine - 0.5) * 0.10 * swayEnv;
    float dx = xc - spine;

    //--- 重力加速签名:sqrt 纵坐标，上密下疏,同一滚速在下段跑得更快 ---
    float fall = sqrt(max(v - uSrcV, 0.0) + 0.015);

    //--- 蚀边噪声(左右异 seed,随流下涌),前锋/接触面/排空的撕裂共用 ---
    float eL = nrm(tex2D(uNoiseTex, float2(uSeed * 2.1 + 0.13, fall * 2.1 - uTime * 1.4)).g);
    float eR = nrm(tex2D(uNoiseTex, float2(uSeed * 3.7 + 0.57, fall * 2.4 - uTime * 1.6 + 4.2)).b);
    float eN = lerp(eL, eR, step(0.0, dx));

    //--- 坠落头:推进中的前锋鼓包(定长后交给接触面,不再熄灭成平切) ---
    float advancing = 1.0 - smoothstep(uSpanV - ws * 0.3, uSpanV, uFront);
    float headD = (v - uFront) / (ws * 0.9);
    float headBulge = exp2(-headD * headD * 2.2) * advancing;

    //--- 柱宽剖面:球根下颈缩 → 加速收窄;溅裙外扩;宽度包络吃 uWidthT ---
    float prof = lerp(1.0, 0.78, swayT);
    float neckDip = exp2(-pow((v - uSrcV - ws * 1.05) / (ws * 0.55), 2.0) * 1.44);
    prof *= 1.0 - 0.26 * neckDip;
    float flare = uGrounded * uChurn * smoothstep(uSpanV - ws * 1.3, uSpanV, v) * 0.6;
    prof *= 1.0 + flare + headBulge * 0.35;
    float halfW = 0.33 * prof * uWidthT;

    float rr = abs(dx) + (eN - 0.5) * 0.13;
    float body = 1.0 - smoothstep(halfW - 0.10, halfW + 0.03, rr);

    //柱身自颈下起笔(球根之上无柱),源头轮廓交给球根
    body *= smoothstep(uSrcV + ws * 0.25, uSrcV + ws * 0.9, v);

    //--- 前锋撕裂截断 ---
    float frontEdge = uFront + (eN - 0.5) * ws * 0.7;
    float frontMask = 1.0 - smoothstep(frontEdge - ws * 0.15, frontEdge + ws * 0.2, v);
    body *= frontMask;

    //--- 落点:触地=噪声撕裂的接触线;空中=末端散逸成股(不硬切) ---
    float planeCut = uSpanV + (eN - 0.5) * ws * 0.4;
    float groundMask = 1.0 - smoothstep(planeCut, planeCut + ws * 0.35, v);
    body *= lerp(1.0, groundMask, uGrounded);
    float airEnd = (1.0 - uGrounded) * smoothstep(uSpanV - ws * 2.4, uSpanV + ws * 0.5, v);

    //--- 排空:自源撕裂啃掉 ---
    float drainF = uDrainV + (eN - 0.5) * ws * 0.9;
    float drainMask = smoothstep(drainF - ws * 0.1, drainF + ws * 0.45, v);
    body *= drainMask;

    //尾沿滴串:排空前沿身后的中轴断珠(Plateau-Rayleigh 收尾)
    float behind = (drainF - v) / (ws * 1.8);
    float dripZone = smoothstep(0.02, 0.2, behind) * (1.0 - smoothstep(0.55, 1.0, behind))
        * step(0.02, uDrainV);
    float nDrip = nrm(tex2D(uNoiseTex, float2(uSeed * 5.3, v * 16.0 - uTime * 2.8)).r);
    float drips = dripZone * smoothstep(0.55, 0.8, nDrip)
        * (1.0 - smoothstep(0.10, 0.16, abs(dx))) * uWidthT;

    //--- 双层视差密度流+股束:慢滚外鞘/快滚内芯/2~3 条纵向浓带拧着冲 ---
    float nSheath = nrm(tex2D(uNoiseTex, float2(dx * 1.1 + uSeed, fall * 2.6 - uTime * 1.5)).r);
    float nCore = nrm(tex2D(uNoiseTex, float2(dx * 2.1 + uSeed * 2.3, fall * 4.2 - uTime * 3.6)).g);
    float coreBand = exp2(-pow(dx / max(halfW, 0.02) * 1.4, 2.0));
    float nStrand = nrm(tex2D(uNoiseTex, float2((xc - spine * 0.55) * 2.8 + uSeed * 5.7, fall * 1.4 - uTime * 1.1)).b);
    float strand = smoothstep(0.34, 0.72, nStrand);
    float density = saturate(0.42 + 0.26 * nSheath + 0.30 * nCore * coreBand + 0.28 * strand);
    //空中末端:股间先散,只剩股芯拖成分离的墨条
    density *= 1.0 - airEnd * (1.0 - strand) * 0.9;
    density *= 1.0 - airEnd * 0.35;

    //--- 落点溅丘+上卷翻沫(触地且供墨中) ---
    float2 mp = float2(xc / max(0.88 * uWidthT, 0.12), (v - uSpanV) / (ws * 0.8));
    float nMound = nrm(tex2D(uNoiseTex, float2(xc * 2.3 + uSeed * 7.1, uTime * 0.9)).g);
    float mound = (1.0 - smoothstep(0.5, 1.0, length(mp) + (nMound - 0.5) * 0.3))
        * smoothstep(ws * 0.7, ws * 0.05, v - uSpanV)
        * uGrounded * uChurn;
    //翻沫:接触区反向上卷的高频簇(+uTime=逆主流上滚)
    float nFoam = nrm(tex2D(uNoiseTex, float2(xc * 3.1 + uSeed * 9.7, v * 10.0 + uTime * 3.2)).b);
    float churnZone = smoothstep(uSpanV - ws * 1.4, uSpanV, v)
        * (1.0 - smoothstep(0.0, ws * 0.8, v - uSpanV));
    float foam = smoothstep(0.52, 0.82, nFoam) * churnZone * uChurn * uGrounded
        * (0.75 + 0.45 * uFill);

    //--- 缘外剥离飞沫:挣脱柱缘的坠珠带 ---
    float outBand = smoothstep(halfW + 0.01, halfW + 0.10, rr)
        * (1.0 - smoothstep(halfW + 0.16, halfW + 0.34, rr));
    float nSpray = nrm(tex2D(uNoiseTex, float2(dx * 4.2 + uSeed * 6.3, fall * 5.0 - uTime * 3.8)).r);
    float spray = outBand * smoothstep(0.62, 0.85, nSpray)
        * smoothstep(uSrcV + ws * 0.8, uSrcV + ws * 2.4, v)
        * frontMask * drainMask * lerp(1.0, groundMask, uGrounded)
        * (0.35 + 0.65 * saturate(swayT * 1.4)) * uWidthT * (0.7 + 0.3 * uFill);

    //--- 源头收口:碗口溢流球根(横鼓底圆,顶端沉进碗口,禁平切) ---
    float2 bulbP = float2(xc / max(0.46 * uWidthT + 0.06, 0.1), (v - uSrcV - ws * 0.42) / (ws * 0.46));
    float nBulb = nrm(tex2D(uNoiseTex, float2(xc * 1.7 + uSeed * 11.0, uTime * 0.5)).r);
    float bulb = (1.0 - smoothstep(0.60, 1.02, length(bulbP) + (nBulb - 0.5) * 0.22))
        * drainMask;

    //--- 体色:墨浓处更黑,缘向暗血;中轴血芯一线;股芯压深 ---
    float edgeT = smoothstep(halfW * 0.4, halfW, rr);
    float3 col = lerp(uColBody, uColDeep, edgeT * 0.75 + (1.0 - density) * 0.15);
    col *= 1.0 - strand * 0.18;
    float vein = exp2(-pow(dx / (0.05 + halfW * 0.22), 2.0))
        * smoothstep(uSrcV + ws * 0.6, uSrcV + ws * 2.0, v) * (1.0 - airEnd);
    col = lerp(col, uColCore, vein * 0.30);
    //内芯偶发窄湿光(阈值在归一域内,不再是死代码)
    float glint = smoothstep(0.80, 0.94, nCore) * coreBand * body;

    //--- 合成(预乘):墨是近实心体,密度走色深,透明度只留窄余量 ---
    float alive = smoothstep(0.01, 0.07, uWidthT);
    float aBody = body * (0.70 + 0.28 * density) * 0.97;
    float aBulb = bulb * 0.92 * uWidthT;
    float aMound = mound * 0.9;
    float aDrip = drips * 0.8;
    float aSpray = spray * 0.5;
    float a = saturate(aBody + aBulb + aMound + aDrip + aSpray + foam * 0.1) * alive;

    float3 outCol = col * aBody
                  + lerp(uColBody, uColDeep, 0.4) * (aBulb + aMound + aDrip)
                  + lerp(uColDeep, uColSheen, 0.35) * aSpray;
    outCol += uColSheen * (foam * 0.18 + glint * 0.10 + headBulge * body * 0.08)
            + uColCore * foam * 0.06;
    outCol *= alive;

    //画布护栏:左右/底缘归零,顶缘由球根自持(留极窄保险)
    float guard = smoothstep(1.0, 0.85, abs(xc)) * smoothstep(1.0, 0.93, v)
        * smoothstep(0.0, ws * 0.1, v);
    float k = uFade * guard;
    return float4(outCol * k, a * k) * vertexColor;
}

technique TechDrop
{
    pass DropPass
    {
        PixelShader = compile ps_3_0 PSDrop();
    }
}

technique TechPour
{
    pass PourPass
    {
        PixelShader = compile ps_3_0 PSPour();
    }
}
