// ============================================================================
//KikasaInkDrop.fx 墨雨——鬼伞普攻的演出主角
//TechDrop:材质=一笔水墨,不是液珠——细长中脊笔触(头收笔锋、腹在前 28%、长锥收尾)、
//         左右异 seed 蚀边(墨的边不对称)、uBend 随飞行曲率弓身(笔随轨迹弯)、
//         体中段飞白镂空(干笔擦痕)、尾后噪声卷曲的墨丝;血统只留脊线一线暗红
//         头在 quad 上缘(v=0):C# 侧 rotation = 速度角 + PiOver2
//TechPour:倒撑重击的墨瀑柱——噪声撕边的流沿、快速下涌的密度流、中轴血芯、
//         uLen 前锋推进/uDrain 自源头排空/uChurn 落点搅浊;v=0 为源头
//坐标全笛卡尔（无 atan2）；直线算术+普通 tex2D，FNA3D 安全
//预乘输出，进 AlphaBlend 批——黑要读作黑，加色批画不出黑
//消费入口 KikasaRains/KikasaRainRender.cs
// ============================================================================

float uTime;
float uSeed;
float uFade;      //出生淡入 0~1
float uStretch;   //速度拉伸 0~1.4，0=表面张力拉圆
float uWobAmp;    //张力抖动幅度（顶点滞空放大）
float uWobPhase;  //抖动相位（CPU 侧 life 驱动，暂停即冻结）
float uBend;      //弓身:飞行转向角速度,笔触随轨迹弯(带符号)
float uLen;       //墨瀑前锋 0~1
float uDrain;     //墨瀑自源排空 0~1
float uChurn;     //墨瀑落点搅浊强度 0~1
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

float4 PSPour(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float u = coords.x;
    float v = coords.y; //0=源头(碗口) 1=落点

    //噪声撕边的流沿:半宽沿程起伏,瀑布两侧不是直线
    float nSide = tex2D(uNoiseTex, float2(v * 1.1 - uTime * 1.7, uSeed * 3.1)).r;
    float nSide2 = tex2D(uNoiseTex, float2(v * 2.6 - uTime * 2.9, uSeed * 1.3 + 0.5)).g;
    float halfW = 0.33 + (nSide - 0.5) * 0.14 + (nSide2 - 0.5) * 0.07;
    float xd = abs(u - 0.5);
    float body = 1.0 - smoothstep(halfW - 0.09, halfW + 0.03, xd);

    //前锋与排空:头端推进,源头随排空啃掉
    float mask = 1.0 - smoothstep(uLen - 0.04, uLen + 0.01, v);
    mask *= smoothstep(uDrain - 0.02, uDrain + 0.07, v);
    mask *= smoothstep(0.0, 0.04, v);

    //下涌密度流:快速向下滚动的浓淡
    float nFlow = tex2D(uNoiseTex, float2(u * 2.4 + uSeed, v * 1.3 - uTime * 3.1)).b;
    float density = 0.72 + 0.28 * nFlow;

    //前锋鼓头:推进中的坠落头略亮略鼓
    float headBand = exp2(-(v - uLen) * (v - uLen) * 480.0) * (1.0 - smoothstep(0.94, 1.0, uLen));

    //落点搅浊:触地端翻涌
    float churn = uChurn * smoothstep(uLen - 0.14, uLen, v) * (0.5 + 0.5 * nFlow);

    //体色:缘向暗血,中轴透血芯
    float edgeT = smoothstep(halfW * 0.45, halfW, xd);
    float core = exp2(-(u - 0.5) * (u - 0.5) * 40.0);
    float3 col = lerp(uColBody, uColDeep, edgeT * 0.8);
    col = lerp(col, uColCore, core * 0.30);

    //流沿高频亮丝:窄湿反光
    float sheen = smoothstep(0.80, 0.92, nFlow) * body * 0.22;

    float a = saturate(body * mask * density * 0.95 + churn * 0.35);
    float3 outCol = col * a
                  + uColSheen * (sheen + headBand * 0.4 + churn * 0.18) * mask;

    float k = uFade;
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
