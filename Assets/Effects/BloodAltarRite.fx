//血月祭坛献祭仪式，三个技法共用一套浓血色阶，全部预乘 Alpha
//TechPool:   碗内血面，液面起伏 + 沸腾泡涨破 + 四槽涟漪 + 贴壁 meniscus 挂血；
//            高光只走各向异性窄反射带，禁圆形高光（那是塑料）
//TechGeyser: 血柱，不对称截面（重力先撕一侧）+ 根部近实心 + 中段液舌 +
//            顶端颈缩断裂成液滴串；uDrain 自根部向上啃掉，不做整体淡出
//TechSigil:  地面血纹环，流进石缝的血，不是发光符文；细而不匀 + 噪声侵蚀成断续
//
//约束（VFX.md）：直线算术 + 平 tex2D，无动态分支、无 tex2Dlod
//极角审计：theta 仅出现在 TechSigil，消费点是 sin(6*theta) / sin(12*theta)（整数倍角，跨 ±pi 连续）
//与生长头 smoothstep(uOpen, ..., ang01)；后者的不连续处正是血线首尾相接的位置，uOpen=1 时自然消失。
//所有噪声输入走刚体旋转后的笛卡尔坐标，绝不喂 theta / ang01

float uTime;
float uSeed;
//池面
float uFill;
float uBoil;
float uPulse;
float uFlash;
float4 uRipple0;
float4 uRipple1;
float4 uRipple2;
float4 uRipple3;
//血柱
float uRise;
float uDrain;
float uAspect;
//血纹环
float uOpen;

texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

//浓血三档：焦干 -> 深血 -> 湿血。白只从 uFlash 来，不常驻
static const float3 ColDry = float3(0.165, 0.016, 0.027);
static const float3 ColDeep = float3(0.420, 0.043, 0.071);
static const float3 ColWet = float3(0.658, 0.070, 0.110);

//单槽涟漪：xy=中心(uv)，z=年龄 0~1，w=强度。返回液面位移量
float RippleLift(float4 rip, float2 uv)
{
    float2 d = uv - rip.xy;
    float dist = length(float2(d.x, d.y * 2.2));
    float age = saturate(rip.z);
    //波前外扩 + 距离与年龄双衰减
    float front = dist * 15.0 - age * 9.0;
    float wave = sin(front) * exp2(-dist * 11.0) * (1.0 - age) * (1.0 - age);
    return wave * rip.w;
}

// ============================ 碗内血面 ============================
float4 PSPool(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    //碗形横向收束：越靠壁越窄，避免液面画成一整块方矩形
    float ax = abs(uv.x - 0.5) * 2.0;
    float bowl = smoothstep(1.0, 0.80, ax) * smoothstep(1.02, 0.93, uv.y);

    //液面线（uv.y 向下增长，故 fill 越大 surf 越小）
    float surf = 1.0 - uFill;

    float2 flow = float2(uv.x * 1.35 - uTime * 0.09, uv.y * 0.7 + uTime * 0.05);
    float nLow = tex2D(noiseSamp, flow + uSeed * 0.21).r;
    float nMid = tex2D(noiseSamp, uv * float2(3.1, 2.2) + float2(uTime * 0.21, -uTime * 0.13) + uSeed * 0.53).r;

    //起伏：常态低频呼吸 + 沸腾期加码
    float breathe = sin(uv.x * 7.3 + uTime * 1.15) * 0.006 * (0.5 + 0.5 * uPulse);
    float wob = (nLow - 0.5) * 0.018 + (nMid - 0.5) * 0.020 * uBoil + breathe;
    wob += RippleLift(uRipple0, uv) + RippleLift(uRipple1, uv)
        + RippleLift(uRipple2, uv) + RippleLift(uRipple3, uv);

    float surfLine = surf + wob;
    float below = smoothstep(surfLine - 0.012, surfLine + 0.012, uv.y);
    float body = below * bowl;

    //越深越暗
    float depth = saturate((uv.y - surfLine) / max(uFill, 0.10));

    //沸腾泡：高频噪声阈值切团，只在液面下几分之一处冒
    float bubN = tex2D(noiseSamp, uv * float2(5.4, 9.6) + float2(uTime * 0.15, -uTime * 0.72) + uSeed).r;
    float bubZone = smoothstep(0.42, 0.02, depth);
    float bubble = smoothstep(0.60, 0.80, bubN) * uBoil * bubZone * body;

    //贴壁挂血：更暗更饱和的收边，液体的表面张力靠这条读出来
    float wall = smoothstep(0.66, 1.0, ax) * body;

    //各向异性窄反射带：紧贴液面下方的一条横向亮带，被噪声打断成不匀的段
    float sheenBand = exp2(-pow((uv.y - surfLine) * 52.0 - 1.1, 2.0) * 2.2);
    float sheenBreak = 0.35 + 0.65 * smoothstep(0.30, 0.72, nMid);
    float sheen = sheenBand * sheenBreak * body;

    float3 col = lerp(ColWet, ColDry, saturate(depth * 1.35));
    col = lerp(col, ColDeep, wall * 0.75);
    col += ColWet * bubble * 0.55;
    col += float3(0.86, 0.30, 0.26) * sheen * 0.42;
    //过曝只在结算的两三帧
    col += float3(1.0, 0.86, 0.82) * uFlash * (sheen * 0.9 + bubble * 0.5);

    float alpha = saturate(body * (0.90 + 0.10 * sheen) - wall * 0.06);
    alpha = saturate(alpha + bubble * 0.10);

    return float4(col * alpha, alpha) * vertexColor;
}

// ============================= 血柱 =============================
float4 PSGeyser(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    //quad 顶端是柱尖，故沿柱方向 along = 1 - uv.y
    float along = 1.0 - uv.y;
    float across = (uv.x - 0.5) * 2.0;

    //游走中轴：血不是直管，根粗顶散
    float lean = (tex2D(noiseSamp, float2(along * 1.25 - uTime * 0.52, uSeed * 0.63)).r - 0.5) * 0.62;
    float c = across - lean * smoothstep(0.05, 0.9, along);

    //生长与断裂
    float tip = uRise;
    float grow = smoothstep(tip, tip - 0.14, along);

    //收腰：过半程后按整数倍频起颈缩，末段断成液滴串
    float neckWave = 0.5 + 0.5 * sin(along * 23.0 - uTime * 7.4);
    float neck = 1.0 - 0.44 * smoothstep(0.40, 1.0, along) * neckWave;
    float taper = 1.0 - 0.30 * along;

    //不对称侵蚀：一侧先被撕开（step 是常量选择，不是分支）
    float side = step(0.0, c);
    float eatN = tex2D(noiseSamp, float2(along * 2.4 - uTime * 1.05, c * 0.55 + uSeed * 0.37)).r;
    float bite = (eatN - 0.44) * (0.20 + 0.34 * side);

    float halfW = taper * neck * 0.90 - bite;
    float body = smoothstep(halfW, halfW - 0.34, abs(c)) * grow;

    //自根部向上啃掉，边缘也被噪声撕开
    float drainEdge = uDrain + (eatN - 0.5) * 0.09;
    body *= smoothstep(drainEdge - 0.04, drainEdge + 0.13, along);

    //液舌与丝：高频噪声挑出的亮条
    float tongue = smoothstep(0.58, 0.86, eatN) * body;
    //末段珠链
    float bead = 0.55 + 0.45 * sin(along * 31.0 - uTime * 9.6);
    body *= lerp(1.0, bead, smoothstep(0.60, 1.0, along));

    //根近实心且暗，中段湿，顶端回暗（血不发热）
    float rootMask = smoothstep(0.34, 0.0, along);
    float tipMask = smoothstep(0.55, 1.0, along);
    float3 col = lerp(ColDeep, ColWet, smoothstep(0.02, 0.42, along));
    col = lerp(col, ColDry, tipMask * 0.55);
    col = lerp(col, ColDry, rootMask * 0.35);
    col += ColWet * tongue * 0.42;
    col += float3(0.88, 0.32, 0.28) * uFlash * tongue * 0.55;

    float alpha = saturate(body * (0.92 - 0.16 * tipMask) + tongue * 0.14);
    return float4(col * alpha, alpha) * vertexColor;
}

// =========================== 地面血纹环 ===========================
float4 PSSigil(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    //地面透视：竖向压扁成椭圆
    float2 p = float2((uv.x - 0.5) * 2.0, (uv.y - 0.5) * 2.0 * uAspect);
    float r = length(p);
    float theta = atan2(p.y, p.x);

    //噪声只吃刚体旋转后的笛卡尔坐标，绝不吃 theta
    float ca = cos(uTime * 0.035);
    float sa = sin(uTime * 0.035);
    float2 rp = float2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);
    float n = tex2D(noiseSamp, rp * 1.30 + uSeed * 0.29).r;
    float n2 = tex2D(noiseSamp, rp * 3.60 - uSeed * 0.71).r;

    //生长头：血沿圆周流开；uOpen=1 时首尾闭合，判别式自然消失
    float ang01 = (theta + 3.14159265) * 0.15915494;
    float grown = smoothstep(uOpen, uOpen - 0.035, ang01);

    //主线：粗细按整数倍角起伏，再被噪声啃断
    float thick = 0.020 + 0.014 * (0.5 + 0.5 * sin(6.0 * theta + uSeed * 2.7));
    float mainLine = exp2(-pow((r - 0.66) / thick, 2.0) * 1.6);
    //内侧细线
    float innerLine = exp2(-pow((r - 0.45) / 0.011, 2.0) * 1.8)
        * (0.35 + 0.65 * (0.5 + 0.5 * sin(12.0 * theta)));

    //短径向裂缝：血渗进石缝，越靠外越稀
    float crack = smoothstep(0.62, 0.86, n2) * smoothstep(0.20, 0.52, r) * smoothstep(1.02, 0.72, r);

    float erode = smoothstep(0.28, 0.66, n);
    float lines = (mainLine + innerLine * 0.75) * erode + crack * 0.30;
    lines *= grown;

    float pulse = 0.80 + 0.20 * uPulse;
    float3 col = lerp(ColDry, ColDeep, saturate(lines * 1.4));
    col = lerp(col, ColWet, saturate(mainLine * erode * grown) * 0.35);

    float alpha = saturate(lines * 0.95) * pulse;
    return float4(col * alpha, alpha) * vertexColor;
}

technique TechPool
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSPool();
    }
}

technique TechGeyser
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSGeyser();
    }
}

technique TechSigil
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSSigil();
    }
}
