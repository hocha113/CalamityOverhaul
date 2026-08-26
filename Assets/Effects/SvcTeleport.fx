// ============================================================================
//SvcTeleport.fx 工业玩家服务通用"相位光"套件
//TechPortal: 传送站待机门户环(水平透视椭圆,画在实体层之下)
//TechColumn: 事件光柱(传送出发收束/到达吐出;日晷金色天讯换色板复用)
//
//输出直进 Additive 批(rgb 不预乘,a 携带包络;A=0 时整像素消失,包络必须写进 a)。
//s1=PerlinNoise(LinearWrap);G 通道实测值域 0.227~0.776,阈值一律先过 nrm() 归一。
//极角纪律:环上角量只以 frac 圆距(跨 0/1 连续)与整数倍角 wrap 采样出现;
//柱体全笛卡尔。全程直线算术+朴素 tex2D,无动态分支(FNA3D 法则)。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;      //秒级时间(实例相位由 C# 混入)
float uSeed;      //实例种子
float3 uColBright;//亮芯色
float3 uColMain;  //主体色
float3 uColDeep;  //外鞘深色
//---- TechPortal 专用 ----
float uPower;     //功率档 0~1:亮度+游光速度
float uPulse;     //余辉/事件过亮 0~1
float uSquish;    //椭圆纵向压缩(0.28~0.4)
float uQuadHalf;  //quad 半宽像素,归一化→像素换算
//---- TechColumn 专用 ----
float uProgress;  //生命进度 0~1
float uDir;       //0=出发(向上生长后收束吞没) 1=到达(自上而下吐出后排空)
float uAspect;    //quad 高/宽,竖向噪声等比换算用

//PerlinNoise G 通道实测 0.227~0.776,归一到 0~1
float nrm(float v) { return saturate((v - 0.227) / 0.549); }

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

// ========================== 门户环 ==========================
float4 PortalPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    //椭圆归一空间:q 空间里门户是正圆
    float2 q = float2(centered.x, centered.y / max(uSquish, 0.05));
    float dist = length(q);
    float normAngle = (atan2(q.y, q.x) + 3.14159265) / 6.28318531;

    float pxU = 1.0 / uQuadHalf;
    float R = 0.68; //环基准半径(归一化)

    //---- 环缘低频扰动:相位光不走死圆 ----
    float n1 = nrm(tex2D(noiseTex, float2(normAngle * 3.0 + uTime * 0.09, 0.31 + uSeed)).g);
    float n2 = nrm(tex2D(noiseTex, float2(normAngle * 5.0 - uTime * 0.06, 0.67 + uSeed)).g);
    float disp = (n1 * 0.6 + n2 * 0.4 - 0.5) * 10.0 * pxU;

    float signedOut = dist + disp - R;

    //---- 主环:细芯+内长外短的不对称晕(内侧是"门里",光往里塌) ----
    float breath = 0.92 + 0.08 * sin(uTime * 1.7 + uSeed * 9.0);
    float coreW = 2.6 * pxU * (1.0 + uPulse * 0.8);
    float core = pow(saturate(1.0 - smoothstep(0.0, coreW * 2.0, abs(signedOut))), 1.5);
    float sideOut = step(0.0, signedOut);
    float haloW = lerp(40.0, 16.0, sideOut) * pxU;
    float halo = exp2(-abs(signedOut) / haloW * 3.0) * 0.6;

    //---- 环上流噪:一段亮一段暗,读作光在环里循环 ----
    float flowN = nrm(tex2D(noiseTex, float2(normAngle * 2.0 - uTime * (0.05 + uPower * 0.10), 0.13 + uSeed)).g);
    float flow = 0.55 + 0.65 * smoothstep(0.35, 0.85, flowN);

    //---- 两粒游光沿环滑行:frac 圆距跨缝连续 ----
    float circU = 6.28318531 * R;
    float rdN = dist - R;
    float spd = 0.05 + uPower * 0.09;
    float ad1 = abs(frac(normAngle - frac(uTime * spd + uSeed) + 0.5) - 0.5) * circU;
    float ad2 = abs(frac(normAngle + frac(uTime * spd * 0.83 + uSeed * 2.0) + 0.5) - 0.5) * circU;
    float g1 = exp2(-length(float2(ad1, rdN)) / (5.0 * pxU) * 3.0);
    float g2 = exp2(-length(float2(ad2, rdN)) / (5.0 * pxU) * 3.0);
    float glintGlow = exp2(-length(float2(ad1, rdN)) / (15.0 * pxU) * 3.0)
                    + exp2(-length(float2(ad2, rdN)) / (15.0 * pxU) * 3.0);

    //---- 环内空间下陷微纹:向心衰减的旋转噪声,中心留空 ----
    float ca = cos(uTime * 0.10 + uSeed);
    float sa = sin(uTime * 0.10 + uSeed);
    float2 rp = float2(q.x * ca - q.y * sa, q.x * sa + q.y * ca);
    float sag = nrm(tex2D(noiseTex, rp * 0.55 + 0.5).g);
    float inner = saturate(-signedOut / (R * 0.9));         //0 环上→1 环心
    float sagZone = saturate(inner * 2.4) * (1.0 - smoothstep(0.45, 0.95, inner));
    float sagLight = sagZone * (0.25 + 0.45 * sag) * 0.5;

    //---- 透视:远半(上)压暗,近半(下)提亮 ----
    float depthShade = lerp(0.68, 1.12, saturate(centered.y * 2.2 + 0.5));

    //---- 合成 ----
    float powerCurve = 0.22 + 0.78 * uPower; //低功率暗但不熄,断电由 C# 不画
    float3 col = uColBright * (core * (1.15 + uPulse * 1.1) * flow);
    col += uColMain * (halo * flow);
    col += uColBright * ((g1 + g2) * (0.85 + uPulse * 0.5));
    col += uColMain * (glintGlow * 0.35);
    col += uColDeep * sagLight;
    col *= depthShade * powerCurve * breath;

    //画布保险:内容在 q 空间 0.9 前自然归零后的兜底
    float guard = 1.0 - smoothstep(0.90, 0.985, dist);
    col *= guard;

    float env = saturate(core * 0.9 + (g1 + g2) * 0.6 + halo * 0.5 + sagLight)
              * guard * powerCurve;
    return float4(col, env) * vertexColor;
}

// ========================== 事件光柱 ==========================
float4 ColumnPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float xc = coords.x * 2.0 - 1.0; //横截 [-1,1]
    float v = 1.0 - coords.y;        //0=柱根(台面) 1=柱顶
    float p = saturate(uProgress);

    //---- 生命包络:双向各算一套,uDir lerp,无分支 ----
    //出发:头部自根向上冲(快),0.55 后宽度收束吞没
    float headD = smoothstep(0.0, 0.30, p);
    float wLifeD = 1.0 - smoothstep(0.55, 0.96, p);
    //到达:头部自顶向下泻,0.5 后自根向上排空
    float headA = smoothstep(0.0, 0.26, p);
    float drainA = smoothstep(0.50, 0.92, p);

    //行进前锋位置(0~1,越过即无柱体)
    float head = lerp(headD, headA, uDir);
    float alongToHead = lerp(v, 1.0 - v, uDir);        //沿生长方向的坐标
    float bodyGate = 1.0 - smoothstep(head - 0.06, head + 0.015, alongToHead);
    //前锋亮尖:能量的行进面;行程走满即熄,不许在端点停车发光
    float headAlive = 1.0 - smoothstep(0.90, 1.0, head);
    float headHot = exp2(-abs(alongToHead - head) * 26.0) * headAlive;
    //到达排空:自根啃掉
    float drainGate = smoothstep(drainA - 0.05, drainA + 0.02, v);
    bodyGate *= lerp(1.0, drainGate, uDir);

    //---- 宽度生命周期:根部喇叭脚+沿程收窄,收束期整柱向心捏拢 ----
    float wLife = lerp(wLifeD, 1.0 - smoothstep(0.72, 1.0, p) * 0.4, uDir);
    float flare = 1.0 + 0.30 * exp2(-v * 7.0);      //根部张脚,锚在台面
    //到达柱自天上一点泻下:向顶强收成漏斗,源头不是平切
    float taper = 1.0 - lerp(0.22, 0.58, uDir) * v;
    float halfW = 0.46 * flare * taper * max(wLife, 0.03);
    float xcN = xc / max(halfW, 1e-3);               //截面归一

    //---- 双缘异相噪蚀:左右各自撕,不对称 ----
    float flowDir = lerp(-1.0, 1.0, uDir);           //出发上涌,到达下泻
    float eL = nrm(tex2D(noiseTex, float2(v * uAspect * 0.16 + uTime * flowDir * 0.55, 0.21 + uSeed)).g);
    float eR = nrm(tex2D(noiseTex, float2(v * uAspect * 0.16 - uTime * flowDir * 0.47, 0.73 + uSeed)).g);
    float edgeN = lerp(eL, eR, step(0.0, xc));
    float edge = 1.0 - smoothstep(0.55 + edgeN * 0.28, 1.0, abs(xcN));

    //---- 体内双层流:异速视差,读作能量在柱里跑 ----
    float f1 = nrm(tex2D(noiseTex, float2(xc * 0.8 + uSeed, v * uAspect * 0.10 + uTime * flowDir * 0.85)).g);
    float f2 = nrm(tex2D(noiseTex, float2(xc * 1.7 - uSeed, v * uAspect * 0.05 + uTime * flowDir * 0.42)).g);
    float body = (0.40 + 0.60 * smoothstep(0.30, 0.80, f1 * 0.62 + f2 * 0.38));

    //---- 白热窄芯:随流微摆 ----
    float wob = (nrm(tex2D(noiseTex, float2(0.5 + uSeed, v * 0.9 + uTime * flowDir * 0.30)).g) - 0.5) * 0.34;
    float coreLine = exp2(-abs(xcN - wob) * abs(xcN - wob) * 30.0);

    //---- 顶端散逸成缕(出发打空端答案);到达柱源头在高处收成缕口 ----
    float strand = nrm(tex2D(noiseTex, float2(xc * 2.3 + uSeed * 3.0, 0.41 + uSeed)).g);
    float strandEnd = lerp(0.58 + 0.40 * strand, 0.78 + 0.22 * strand, uDir);
    float fadeSpan = lerp(0.24, 0.12, uDir);
    float topFade = 1.0 - smoothstep(strandEnd - fadeSpan, strandEnd, v);
    //根部光池:出发=吸起的光脚,到达=砸在台面的溅亮
    float rootGlow = exp2(-v * 10.0) * (0.5 + 0.9 * uDir * smoothstep(0.20, 0.34, p));

    //---- 合成 ----
    float shape = edge * bodyGate * topFade;
    float3 col = uColDeep * (shape * 0.55);
    col += uColMain * (shape * body * 0.95);
    col += uColBright * (coreLine * shape * 1.05);
    col += uColBright * (headHot * edge * 1.4);
    col += uColMain * (rootGlow * edge * bodyGate);

    //收束吞没的一瞬过曝(出发 0.55~0.7 窗口)
    float pinchFlash = (1.0 - smoothstep(0.55, 0.72, abs(p - 0.62) * 8.0)) * (1.0 - uDir);
    col *= 1.0 + pinchFlash * 0.5;

    //画布保险
    float guard = (1.0 - smoothstep(0.90, 0.99, abs(xc))) * (1.0 - smoothstep(0.94, 1.0, v));
    col *= guard;

    float env = saturate(shape * (0.35 + body * 0.45) + coreLine * shape * 0.5 + headHot * 0.6) * guard;
    return float4(col, env) * vertexColor;
}

technique TechPortal
{
    pass P0
    {
        PixelShader = compile ps_3_0 PortalPS();
    }
}

technique TechColumn
{
    pass P0
    {
        PixelShader = compile ps_3_0 ColumnPS();
    }
}
