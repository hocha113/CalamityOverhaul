// ============================================================================
//KikasaInkDrop.fx 大墨滴——鬼伞普攻的演出主角
//TechDrop:材质=浸饱血水的墨——头圆尾锥的液滴剪影、速度拉伸、噪声蚀缘张力抖动、
//         身后墨须在空气里越散越宽地晕开；墨黑为体、血色为芯、小面积湿反光
//         头在 quad 上缘(v=0)：C# 侧 rotation = 速度角 + PiOver2
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
float uLen;       //墨瀑前锋 0~1
float uDrain;     //墨瀑自源排空 0~1
float uChurn;     //墨瀑落点搅浊强度 0~1
float3 uColBody;  //墨体近黑
float3 uColDeep;  //暗血缘
float3 uColCore;  //血芯
float3 uColSheen; //湿反光

sampler uNoiseTex : register(s1);

//变半径胶囊：head→tail 连线段，半径沿轴锥缩
float DropSdf(float2 q, float2 head, float2 tail, float rHead, float rTail)
{
    float2 axis = tail - head;
    float len2 = max(dot(axis, axis), 1e-5);
    float t = saturate(dot(q - head, axis) / len2);
    float2 onAxis = head + axis * t;
    float r = lerp(rHead, rTail, pow(t, 0.8));
    return length(q - onAxis) - r;
}

float4 PSDrop(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 raw = coords * 2.0 - 1.0; //y 负方向=运动方向（头在上缘）
    float2 q = raw;

    //液滴几何：头圆、尾随拉伸抽长变细
    float wob = sin(uWobPhase) * uWobAmp;
    float rHead = 0.30 * (1.0 + wob);
    float len = 0.26 + uStretch * 0.46;
    float rTail = rHead * (0.62 - uStretch * 0.22) * (1.0 - wob * 0.8);
    float2 head = float2(0.0, -0.36);
    float2 tail = head + float2(0.0, len);

    float d = DropSdf(q, head, tail, rHead, rTail);

    //表面张力蚀缘：低幅笛卡尔噪声揉边，墨的边界不是数学圆
    float nEdge = tex2D(uNoiseTex, q * 0.9 + float2(uSeed, uSeed * 0.7) + float2(uTime * 0.05, -uTime * 0.03)).r;
    d += (nEdge - 0.5) * (0.05 + uWobAmp * 0.16);

    //墨体与晕染光环（墨往空气里洇的那一圈）
    float body = 1.0 - smoothstep(-0.015, 0.045, d);
    float halo = (1.0 - smoothstep(0.0, 0.24, d)) * (1.0 - body);

    //体内色程：缘→暗血缘，芯→血红
    float depth = saturate(-d / max(rHead, 1e-3));
    float rimBand = 1.0 - smoothstep(0.0, 0.14, -d);
    float3 bodyCol = lerp(uColBody, uColDeep, rimBand * 0.75);
    bodyCol = lerp(bodyCol, uColCore, saturate(depth * 1.25 - 0.32) * 0.62);

    //墨须：尾后随距离越散越宽的撕裂细流，只有在动时才拖
    float b = q.y - tail.y;
    float zone = smoothstep(0.02, 0.14, b) * (1.0 - smoothstep(0.30, 0.62, b));
    float nStreak = tex2D(uNoiseTex, float2(q.x * 1.7 + uSeed * 2.3, q.y * 0.4 - uTime * 0.85)).g;
    float lat = exp2(-q.x * q.x * 9.0 / (0.16 + b * 1.6));
    float tendril = zone * smoothstep(0.50, 0.78, nStreak) * lat * saturate(uStretch * 1.6);

    //湿反光玻头：头侧上方一点小高光
    float sheen = 1.0 - smoothstep(0.0, 0.11, length(q - head - float2(-0.09, -0.08)));
    sheen *= body;

    //预乘合成
    float aBody = body * 0.96;
    float aHalo = halo * 0.22;
    float aTendril = tendril * 0.5;
    float3 col = bodyCol * aBody
               + lerp(uColBody, uColDeep, 0.4) * (aHalo + aTendril);
    float a = saturate(aBody + aHalo + aTendril);
    col += uColSheen * sheen * 0.5;

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
