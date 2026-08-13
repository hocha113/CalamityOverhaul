// ============================================================================
//ShockRing.fx 共享参数化冲击环（Ring01 灰度图的替代承载）
//锐外锋+拖内尾+噪声撕裂缘+不均匀厚度；uSquish 透视椭圆；三色 uniform 调色
//坐标全笛卡尔（无 atan2，无极角接缝）；直线算术+普通 tex2D，FNA3D 安全
//预乘输出，进 Additive 批；消费入口 Common/ShockRingDraw.cs
// ============================================================================

float uTime;
float uRadius;      //环半径（半画布归一）
float uThickness;   //环带基准厚度（同单位）
float uTear;        //撕裂位移幅度（同单位）
float uSquish;      //Y 透视压缩，1=正圆
float uAlpha;       //整体透明度
float uInnerGlow;   //环内残波强度 0~1
float3 uColBright;  //波前亮缘
float3 uColMain;    //环带主体
float3 uColDeep;    //内侧尾波/残波

sampler uNoiseTex : register(s1);

float4 PSRing(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //raw 供画布护栏；centered 供环几何（Y 除以压缩比→贴地椭圆）
    float2 raw = coords * 2.0 - 1.0;
    float2 centered = raw;
    centered.y /= max(uSquish, 0.05);
    float dist = length(centered);

    //撕裂位移：双频笛卡尔噪声外流（贴图采样自带 wrap，无手写 floor）
    float n1 = tex2D(uNoiseTex, centered * 0.55 + float2(uTime * 0.11, -uTime * 0.07)).r;
    float n2 = tex2D(uNoiseTex, centered * 1.70 + float2(-uTime * 0.16, uTime * 0.13)).g;
    float disp = (n1 * 0.62 + n2 * 0.38 - 0.5) * uTear;
    float adjDist = dist + disp;

    //厚度不均匀：中频噪声把环带揉成 0.55~1.45 倍粗细
    float nTh = tex2D(uNoiseTex, centered * 0.85 + float2(uTime * 0.05, uTime * 0.09)).b;
    float th = max(uThickness, 1e-3) * (0.55 + nTh * 0.9);

    float ringDist = adjDist - uRadius;

    //主环带：外沿锐利（激波前锋）、内沿拖出长尾
    float outerFall = 1.0 - smoothstep(0.0, th * 0.45, ringDist);
    float innerFall = 1.0 - smoothstep(0.0, th * 1.6, -ringDist);
    float band = min(outerFall, innerFall);

    //波前亮缘：贴着外锋的细亮线
    float rim = 1.0 - smoothstep(0.0, th * 0.5, abs(ringDist - th * 0.22));
    rim *= rim;

    //内侧尾波：环后方一段噪声撕开的余波碎带
    float wakeZone = smoothstep(0.0, th * 3.2, -ringDist) * (1.0 - smoothstep(th * 2.2, th * 5.5, -ringDist));
    float nWake = tex2D(uNoiseTex, centered * 1.15 + float2(uTime * 0.07, -uTime * 0.18)).r;
    float wake = wakeZone * smoothstep(0.35, 0.75, nWake) * 0.6;

    //环内残波：整个内域的噪声薄雾（绽放类要、预警类给 0）
    float interior = smoothstep(uRadius, uRadius * 0.15, adjDist);
    float inner = interior * (0.55 + 0.45 * n1) * uInnerGlow;

    //合成
    float3 col = uColBright * (rim * 1.5)
               + uColMain * band
               + uColDeep * (wake + inner * 0.8);
    float a = saturate(band * 0.85 + rim * 0.75 + wake * 0.5 + inner * 0.45);

    //画布护栏：raw uv 边缘前归零，防切边
    float guard = smoothstep(1.0, 0.86, max(abs(raw.x), abs(raw.y)));
    a *= uAlpha * guard;

    return float4(col * a, a) * vertexColor;
}

technique Technique1
{
    pass ShockRingPass
    {
        PixelShader = compile ps_3_0 PSRing();
    }
}
