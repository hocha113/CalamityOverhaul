// ============================================================================
//KikasaWispFire.fx 鬼伞血湖鬼火，金色鬼火是这件模块的身份色（用户指定的对比设计）
//TechLakeFire: 湖面鬼火层（世界锚定长条 quad，火体纯程序化、不吃任何灰度形状图）
//  贴水熔金根床（吃行波与水线波动，火随涟漪起伏）+ 双频撕裂火舌场（上升流、幽缓摆动：
//  鬼火不是篝火，慢、冷、无烟）+ 游离鬼火星 + 点燃/收火前沿 + 浸线金晕；
//  端部收口：湖两端阈值抬升撕散渐没、舌尖噪声撕裂不平切、画布顶 guard 保险归零；
//  uQuench 鬼雨压制通道：火冠塌缩、连续火幕撕成孤舌、闪烁变急变弱（残喘）、根床失温
//TechBurnBody: 灼身重绘（NPC 帧后处理），热浪扭曲（采样全数钳进 uUvRect 帧界防串帧）
//  + 斑驳焦痕（噪声阈值暗斑自脚部向上蔓延）+ 轮廓边缘火（邻域 alpha 四采样，下缘加权）
//  + 金色脉动余光
//坐标全笛卡尔（无 atan2）；直线算术+普通 tex2D，FNA3D 安全
//预乘输出，进 AlphaBlend 批
//绑定噪声实测值域 0.227~0.776（三通道同灰度），阈值一律过 nrm() 归一
//消费入口 KikasaWisps/KikasaWispFX.cs（湖面层）与 KikasaWisps/KikasaWispBurnNPC.cs（灼身）
// ============================================================================

sampler uImage0 : register(s0);   //批主纹理：LakeFire=白像素（不采样）；BurnBody=NPC 帧图
sampler uNoiseTex : register(s1); //PerlinNoise，LinearWrap，消费端上 s1

//== 共用 ==
float uTime;        //秒（湖面层=EffectTime，灼身=GlobalTime）
float uRain;        //0~1 观看域鬼雨冷化（只轻推金色，不换板）

//== TechLakeFire ==
float2 uQuadSize;   //quad 世界像素尺寸
float uWaterV;      //水线在 quad 内的 v
float uWorldX0;     //quad 左缘世界 X
float uOriginX;     //点燃原点世界 X
float uSpreadPx;    //燃沿已扫半径（px；燃满后传大数=覆盖整湖）
float uFrontGlow;   //0~1 行进前沿亮度（蔓延中 1、反啃 0.45、静息 0）
float uLakeMinX;    //湖左端世界 X（随施术者移动）
float uLakeMaxX;    //湖右端世界 X
float uBurn;        //0~1 在场包络
float uQuench;      //0~1 鬼雨压制
float uWobblePx;    //水线噪声波动幅度（px），与湖面着色器同源换算
float4 uLineWave[4];//行波（世界像素域）x=源worldX y=寿命01 z=幅度px w=范围乘数；空槽 z=0

//== TechBurnBody ==
float2 uTexelSize;  //1/贴图尺寸
float4 uUvRect;     //帧界（xy=min zw=max，半像素内缩）
float uBurnT;       //0~1 灼烧强度（淡入淡出）
float uCharT;       //0~1 焦痕蔓延度（越烧越花）
float uSeed;        //个体相位

//====== 金色鬼火色板 ======
static const float3 GOLD_CORE = float3(1.000, 0.925, 0.660);  //白金焰芯
static const float3 GOLD_BODY = float3(1.000, 0.730, 0.260);  //金焰体
static const float3 AMBER_TIP = float3(0.850, 0.420, 0.120);  //琥珀舌尖
static const float3 PALE_DIE  = float3(0.770, 0.675, 0.535);  //压制失温的苍金
static const float3 COOL_PUSH = float3(0.740, 0.770, 0.730);  //鬼雨轻推的冷灰金

//绑定噪声实测值域归一（0.227~0.776）
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

//行波（世界像素域）：与 KikasaGrade.lineWaveOne 同一波形常数，只是横坐标换成 worldX
float waveOne(float worldX, float4 src) {
    float dpx = abs(worldX - src.x) / max(src.w, 0.25);
    float gate = saturate((src.y * 620.0 - dpx) * 0.05);
    float ph = dpx * 0.062 - src.y * 16.0;
    return sin(ph) * exp2(-dpx * 0.010) * (1.0 - src.y) * gate * src.z;
}

float waveSum(float worldX) {
    return waveOne(worldX, uLineWave[0]) + waveOne(worldX, uLineWave[1])
         + waveOne(worldX, uLineWave[2]) + waveOne(worldX, uLineWave[3]);
}

//====== TechLakeFire ======
float4 PSLakeFire(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0 {
    float worldX = uWorldX0 + coords.x * uQuadSize.x;
    float flameCanvas = uWaterV * uQuadSize.y;      //水线以上画布高（px）
    float lipCanvas = uQuadSize.y - flameCanvas;    //水线以下画布高（px）

    //水面局部起伏：双频噪声波动 + 落点行波，火贴着真实水面烧
    //（波动形状与湖面着色器的屏幕空间版不逐像素同源，幅度同源；根床带厚度吸收差异）
    float n0 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0009 + uTime * 0.020, uTime * 0.011)).r);
    float n1 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0025 - uTime * 0.016, 0.41 + uTime * 0.027)).r);
    float yOff = ((n0 - 0.5) * 1.4 + (n1 - 0.5) * 0.6) * uWobblePx + waveSum(worldX);
    //h：相对起伏水面的高度（px），正=水上
    float h = (uWaterV - coords.y) * uQuadSize.y - yOff;

    //燃沿蔓延：前沿后方 ~220px 内火由矮长高；端部撕散收口（阈值路），画布顶 guard 保险
    float dist = abs(worldX - uOriginX);
    float reach = saturate((uSpreadPx - dist) / 220.0);
    float front = exp2(-abs(dist - uSpreadPx) * 0.05) * uFrontGlow;
    float endFade = saturate((worldX - uLakeMinX) / 260.0) * saturate((uLakeMaxX - worldX) / 260.0);
    float guard = saturate((flameCanvas * 0.96 - h) / 18.0);

    //火冠高度谱：沿 X 低频起伏 + 前沿拔高；压制把火冠塌下去
    float hn  = nrm(tex2D(uNoiseTex, float2(worldX * 0.00135 + uTime * 0.014, 0.13)).r);
    float hn2 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0051  - uTime * 0.022, 0.71)).r);
    float crown = lerp(0.38, 1.0, hn * 0.62 + hn2 * 0.38) * (1.0 + front * 0.42);
    float grow = uBurn * reach * (1.0 - 0.72 * uQuench);
    float hMax = flameCanvas * 0.58 * crown * grow;
    float q = h / max(hMax, 1.0);
    float envGate = saturate(hMax * 0.25);          //火矮到没有时整场熄灭

    //火舌场：双频上升流噪声，撕裂阈值随高度抬升，根近实、越高越碎；
    //压制抬阈值：连续火幕被雨撕成孤立残舌
    float sway = (n0 - 0.5) * h * 0.004;
    float f1 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0065 + sway, h * 0.0075 - uTime * 0.60)).r);
    float f2 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0148 - sway, h * 0.0165 - uTime * 1.05)).r);
    float flameN = f1 * 0.62 + f2 * 0.38;
    float thr = q * (0.66 + 0.40 * uQuench) + uQuench * 0.34 + (1.0 - endFade) * 0.60;
    float rootGate = saturate((h + 3.0) * 0.5);
    float dens = saturate((flameN - thr) * 3.6) * rootGate * guard * envGate;

    //压制残喘：闪烁变急、幅度变弱
    float gutterN = nrm(tex2D(uNoiseTex, float2(worldX * 0.004, uTime * 1.7)).r);
    float gutter = lerp(1.0, 0.42 + 0.58 * gutterN, uQuench);
    dens *= gutter;

    //色带：根白金 → 金体 → 琥珀尖；压制失温向苍金，鬼雨只轻推
    float3 flameCol = lerp(GOLD_CORE, GOLD_BODY, saturate(q * 2.2));
    flameCol = lerp(flameCol, AMBER_TIP, saturate(q * 1.4 - 0.45));
    flameCol = lerp(flameCol, PALE_DIE, uQuench * 0.65);
    flameCol = lerp(flameCol, COOL_PUSH * flameCol, uRain * 0.22);
    float stria = 0.72 + 0.50 * (f2 - 0.5);        //舌内纵向明暗，火不是平涂

    //贴水熔金根床：随行波起伏的一线炽金，是"火长在水上"的锚
    float bedEnv = uBurn * reach * endFade * (1.0 - 0.85 * uQuench) * envGate;
    float bed = exp2(-abs(h - 1.5) * 0.30) * (0.50 + 0.50 * f1) * bedEnv;

    //浸线金晕：水线下的一小段金光沉入（深层照亮由 KikasaGrade.uWispGlow 接管）
    float lip = exp2(h * 0.08) * saturate((h + lipCanvas * 0.95) / 12.0) * saturate(-h * 4.0) * bedEnv;

    //游离鬼火星：火冠上方缓浮的碎金点
    float sp = nrm(tex2D(uNoiseTex, float2(worldX * 0.011 + uTime * 0.03, h * 0.010 - uTime * 0.16)).r);
    float speck = saturate((sp - 0.87) * 10.0)
        * saturate((h - hMax * 0.55) * 0.04) * guard
        * uBurn * reach * endFade * (1.0 - 0.5 * uQuench) * (0.5 + 0.5 * f2);

    //合成（预乘）：火体 + 根床 + 浸线晕 + 前沿白金锋 + 火星
    float3 col = flameCol * dens * stria;
    col += lerp(GOLD_CORE, PALE_DIE, uQuench * 0.7) * bed * 0.95;
    col += GOLD_BODY * lip * 0.30;
    //前沿光柱自带高度衰减、不吃 envGate：锋线正压在 reach=0 处，光要略洒到未燃侧
    float frontHeight = exp2(-max(h, 0.0) * 0.055);
    col += GOLD_CORE * front * uBurn * endFade * frontHeight * (0.55 + 0.50 * f2) * rootGate * guard;
    col += GOLD_CORE * speck * 0.90;

    float alpha = saturate(dens * 0.30 + bed * 0.10);
    return float4(col, alpha) * vc.a;
}

//====== TechBurnBody ======
float4 PSBurnBody(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0 {
    //帧内归一坐标：多帧精灵表上图案频率按单帧算
    float2 fl = (coords - uUvRect.xy) / max(uUvRect.zw - uUvRect.xy, 0.00001);

    //热浪扭曲：小幅 UV 位移，采样点钳回帧界（帧表渗色双通道防线之一，另一半在 C# 源矩形内缩）
    float wn0 = tex2D(uNoiseTex, float2(fl.x * 1.7 + uSeed, fl.y * 1.3 - uTime * 0.55)).r;
    float wn1 = tex2D(uNoiseTex, float2(fl.x * 3.6 - uSeed, fl.y * 2.8 - uTime * 0.90)).r;
    float2 wob = float2((wn0 - 0.5) * 2.6 + (wn1 - 0.5) * 1.2, (wn1 - 0.5) * 1.8);
    float2 suv = clamp(coords + wob * uTexelSize * (2.4 * uBurnT), uUvRect.xy, uUvRect.zw);
    float4 base = tex2D(uImage0, suv) * vc;

    //斑驳焦痕：双频噪声阈值暗斑，自脚部先焦、随灼烧时长向上蔓延；亮度承原图明暗
    float c0 = nrm(tex2D(uNoiseTex, fl * float2(2.6, 2.2) + uSeed).r);
    float c1 = nrm(tex2D(uNoiseTex, fl * float2(6.8, 5.6) - uSeed * 1.7).r);
    float charN = c0 * 0.6 + c1 * 0.4;
    float charThr = 1.05 - uCharT * (0.52 + 0.28 * fl.y);
    float charM = smoothstep(charThr, charThr - 0.16, charN) * uBurnT;
    float srcLuma = dot(base.rgb, float3(0.333, 0.333, 0.333));
    float3 charCol = float3(0.130, 0.078, 0.056) * (0.35 + 0.85 * srcLuma);
    base.rgb = lerp(base.rgb, charCol * base.a, charM * 0.80);

    //轮廓边缘火：邻域 alpha 四采样找轮廓缺口，下缘加权，火从脚下舔上来
    float2 t3 = uTexelSize * 3.0;
    float aL = tex2D(uImage0, clamp(suv - float2(t3.x, 0.0), uUvRect.xy, uUvRect.zw)).a;
    float aR = tex2D(uImage0, clamp(suv + float2(t3.x, 0.0), uUvRect.xy, uUvRect.zw)).a;
    float aU = tex2D(uImage0, clamp(suv - float2(0.0, t3.y), uUvRect.xy, uUvRect.zw)).a;
    float aD = tex2D(uImage0, clamp(suv + float2(0.0, t3.y), uUvRect.xy, uUvRect.zw)).a;
    float edge = saturate(base.a * 1.2 - min(min(aL, aR), min(aU, aD)));
    float downBias = 0.45 + saturate(base.a - aD) * 1.25;

    //火沿闪变：帧内噪声上刷，火贴着轮廓烧而不是描亮边
    float lick = nrm(tex2D(uNoiseTex, float2(fl.x * 3.0 + uSeed, fl.y * 2.0 - uTime * 0.80)).r);
    float3 gold = lerp(float3(1.000, 0.720, 0.250), float3(0.800, 0.740, 0.560), uRain * 0.35);
    base.rgb += gold * edge * (0.30 + 0.95 * lick) * downBias * uBurnT * 0.85;

    //体表金脉动：受火映照的呼吸余光
    float pulse = 0.5 + 0.5 * sin(uTime * 5.2 + uSeed * 7.0);
    base.rgb += gold * base.a * uBurnT * (0.045 + 0.095 * pulse * (0.4 + 0.6 * lick));

    return base;
}

technique TechLakeFire {
    pass P0 {
        PixelShader = compile ps_3_0 PSLakeFire();
    }
}

technique TechBurnBody {
    pass P0 {
        PixelShader = compile ps_3_0 PSBurnBody();
    }
}
