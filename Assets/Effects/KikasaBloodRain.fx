// ============================================================================
//KikasaBloodRain.fx 鬼伞血湖形态普攻:血珠 / 血柱 / 血索
//材质=浓血,按血月祭坛血柱确立的三律执行:表面张力挂边(缘最暗最饱和)、
//高光只走各向异性窄反射带(圆高光=塑料)、形体不对称(重力先撕一侧)+颈缩断滴。
//血是暗的:密度走色深不走透明度,没有白芯,"尾暗→白热"是刀与激光的语法,这里禁用。
//TechBead  :血珠。主液团头圆尾锥,沿速度反向拖 2~3 颗颈缩相连的卫星滴,
//           双模态张力抖动(顶点滞空放大),一侧噪声撕胖,一侧窄反射带,
//           uSubmerged 入水转凝血(红进红靠更沉的色读轮廓),uGhost 追击穿透态鬼青缘。
//           头在 quad 上缘(y 负=运动方向):C# 侧 rotation = 速度角 + PiOver2
//TechColumn:血柱。自湖面拔起的上升射流,液体感四件(2026-09-04 二稿,一稿被判"湖里升起个红东西"):
//           芯股=x 高频 y 低频快速上涌的纵向丝流(边与浓淡都吃它)+上行鼓包泵动,
//           头=推进的球状液团+其上 3~5 根液指、指尖断滴,
//           两翼=uFallback 回落帘(与芯反向、向下流的血丝,液体到顶后往回落),
//           根=被顶起的薄溅裙+搅浊沫环;厚处沉、薄处透亮(薄膜血是亮红)+外沿一线暗轮廓;
//           uCollapse 塌回=根部颈缩断供+整柱下坠+球头先脱离,不淡出。
//           纵坐标以水线为原点按柱宽标定(y=(uRootV-v)/uWScale,正=水上)。
//           飞沫另由 C# 有物理粒子(PRT_KikasaBloodSpray)承担,着色器只画"连续的那一股"
//TechSiphon:血索。倒撑蓄墨期从湖面抽进碗口的细索:足=张力尖锥,身=两股拧绳,
//           顶=喇叭进碗(禁平切);uFill 越满越粗越亮
//坐标全笛卡尔(无 atan2),直线算术+普通 tex2D,FNA3D 安全;预乘输出进 AlphaBlend 批。
//绑定噪声实测值域 0.227~0.776,阈值一律过 nrm() 归一。
//消费入口 KikasaRains/KikasaRainRender.cs(血形态批)
// ============================================================================

float uTime;
float uSeed;
float uFade;      //出生淡入 / 整体强度 0~1

//---- 血珠 ----
float uStretch;   //速度拉伸 0~1.4
float uWobAmp;    //张力抖动幅度(顶点滞空放大)
float uWobPhase;  //抖动相位(CPU life 驱动)
float uBend;      //弓身:转向角速度,尾向轨迹外甩(带符号)
float uGhost;     //追击穿透态 0~1
float uSubmerged; //入水凝血 0~1

//---- 血柱 ----
float uWScale;    //一个柱宽的 v 跨度(WidthPx/quadH)
float uRootV;     //水线 v
float uHeightW;   //当前柱高(柱宽单位,含起柱过冲包络)
float uCollapse;  //塌回进度 0~1
float uKe;        //入水动能 0~1(冠量/沫量)
float uMound;     //根部溅裙强度 0~1
float uFallback;  //两翼回落帘强度 0~1(液体到顶后往回落的那一层,与芯反向流)

//---- 血索 ----
float uLenW;      //索长(宽单位)
float uFill;      //蓄力档 0~1

float3 uColBody;   //血体
float3 uColDeep;   //血缘(挂边)
float3 uColBright; //血亮(体心)
float3 uColSheen;  //湿光(窄带)
float3 uColGhost;  //鬼青缘光

sampler uNoiseTex : register(s1);

//绑定噪声归一:实测值域 0.227~0.776,映到 0~1 后阈值才有效
float nrm(float n) { return saturate((n - 0.23) * 1.82); }

//==================== 血珠 ====================

float4 PSBead(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float2 q = coords * 2.0 - 1.0; //y 负=运动方向(头在上缘)

    //张力双模态抖动:主模横鼓纵缩,二次模错相并带符号翻一侧
    float wob1 = sin(uWobPhase) * uWobAmp;
    float wob2 = sin(uWobPhase * 1.73 + 1.1) * uWobAmp * 0.45;
    float2 qs = q;
    qs.x /= (1.0 + wob1 * 0.9);
    qs.y /= (1.0 - wob1 * 0.6);

    float st = saturate(uStretch / 1.4);

    //主液团:头圆大、尾圆小,两圆间锥体相连;拉伸把尾抽长、头略缩
    float R = 0.26 * (1.0 - st * 0.20);
    float headY = -0.48 + st * 0.08;
    float tailLen = 0.18 + st * 0.50;
    float tailY = headY + tailLen;
    float rTail = R * lerp(0.62, 0.30, st);

    //弓身:尾随转向甩出去,头贴轨迹
    float t = saturate((qs.y - headY) / max(tailLen, 1e-3));
    float spineX = uBend * t * t * 0.30;
    float dx0 = qs.x - spineX;
    float side = step(0.0, dx0) * 2.0 - 1.0;

    //不对称:一侧噪声撕胖,二次模再偏一侧
    float nA = nrm(tex2D(uNoiseTex, float2(uSeed * 1.7 + 0.3, t * 0.7 + uSeed)).r);
    float asym = (nA - 0.5) * 0.10 * t + wob2 * 0.4;
    float rr = lerp(R, rTail, t) * (1.0 + asym * side);
    float2 c = float2(spineX, clamp(qs.y, headY, tailY));
    float dBody = length(qs - c) - rr;
    float body = 1.0 - smoothstep(-0.010, 0.026, dBody);

    //卫星滴:尾后沿脊线 2~3 颗,越快越散;颈缩细丝把第一颗和尾相连,噪声把丝断成珠串
    float satVis = smoothstep(0.15, 0.55, st);
    float g = 0.10 + st * 0.05;
    float spineTail = uBend * 0.30;
    float n1 = nrm(tex2D(uNoiseTex, float2(uSeed * 2.9 + 0.37, uSeed + 1.0)).g) - 0.5;
    float n2 = nrm(tex2D(uNoiseTex, float2(uSeed * 2.9 + 0.74, uSeed + 2.0)).g) - 0.5;
    float n3 = nrm(tex2D(uNoiseTex, float2(uSeed * 2.9 + 1.11, uSeed + 3.0)).g) - 0.5;
    float2 s1 = float2(spineTail + n1 * 0.08, tailY + g);
    float2 s2 = float2(spineTail + n2 * 0.14, tailY + g * 2.9);
    float2 s3 = float2(spineTail + n3 * 0.20, tailY + g * 4.6);
    float pulse = 0.85 + 0.3 * nrm(tex2D(uNoiseTex, float2(uSeed * 3.3, uWobPhase * 0.03)).b);
    float d1 = length(qs - s1) - R * 0.30 * pulse;
    float d2 = length(qs - s2) - R * 0.21 * pulse;
    float d3 = length(qs - s3) - R * 0.14;
    float dSat = min(d1, min(d2, d3));
    float sat = (1.0 - smoothstep(-0.008, 0.020, dSat)) * satVis;
    //颈缩细丝:尾心→第一颗卫星的极细胶囊,沿丝噪声阈值断成珠
    float2 nA0 = float2(spineTail, tailY);
    float2 ab = s1 - nA0;
    float tn = saturate(dot(qs - nA0, ab) / max(dot(ab, ab), 1e-4));
    float dNeck = length(qs - (nA0 + ab * tn)) - 0.030 * (1.0 - st * 0.35) * (1.0 - tn * 0.5);
    float nThread = nrm(tex2D(uNoiseTex, float2(uSeed * 5.3, tn * 3.0 + uWobPhase * 0.05)).r);
    float neck = (1.0 - smoothstep(-0.005, 0.015, dNeck)) * satVis * smoothstep(0.35, 0.55, nThread);

    //体色:缘最暗最饱和(挂边),往心渐亮;体内低频斑驳(血不是均匀色块);入水转凝血
    float dn = saturate(-dBody / max(rr, 1e-3));
    float3 col = lerp(uColDeep, uColBody, smoothstep(0.0, 0.28, dn));
    col = lerp(col, uColBright, smoothstep(0.35, 0.95, dn) * 0.55);
    float nMottle = nrm(tex2D(uNoiseTex, float2(qs.x * 0.9 + uSeed * 4.1, qs.y * 0.7 + uSeed * 1.3)).b);
    col *= 1.0 - (nMottle - 0.5) * 0.22;
    col = lerp(col, uColDeep * 0.85, uSubmerged * 0.7);

    //各向异性窄反射带:偏一侧的竖带,沿带一点缓滚的流光,不是圆高光
    float bx = (dx0 + rr * 0.42) / max(rr * 0.11, 1e-3);
    float band = exp2(-bx * bx * 1.6) * body
        * smoothstep(0.0, 0.18, t) * (1.0 - smoothstep(0.55, 0.80, t));
    band *= 0.75 + 0.25 * sin(t * 9.0 - uWobPhase * 0.9 + uSeed);
    band *= 1.0 - 0.6 * uSubmerged;

    //入水后缘上一线浅沫,红进红时靠它读轮廓
    float rim = (1.0 - smoothstep(0.0, 0.05, abs(dBody + 0.010))) * body;
    float foamRim = rim * uSubmerged;

    //穿透态:体略透,轮廓外沿一线鬼青
    float ghostRim = (1.0 - smoothstep(0.0, 0.05, abs(dBody + 0.012))) * uGhost;

    //预乘合成
    float aBody = body * 0.96 * (1.0 - 0.15 * uGhost);
    float aSat = sat * 0.90;
    float aNeck = neck * 0.75;
    float a = saturate(aBody + aSat + aNeck + ghostRim * 0.35);
    float3 outCol = col * aBody + lerp(uColBody, uColDeep, 0.35) * (aSat + aNeck);
    outCol += uColSheen * band * 0.80;
    outCol += uColSheen * foamRim * 0.30;
    outCol += uColGhost * ghostRim * 0.7;

    float guard = smoothstep(1.0, 0.90, max(abs(q.x), abs(q.y)));
    float k = uFade * guard;
    return float4(outCol * k, a * k) * vc;
}

//==================== 血柱 ====================

float4 PSColumn(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float ws = max(uWScale, 0.004);
    float xc = (coords.x - 0.5) * 2.0;        //一单位=quad 半宽;满宽半宽 0.31
    float y = (uRootV - coords.y) / ws;       //水线为原点,柱宽为单位,正=水上
    float H = max(uHeightW, 0.05);

    //塌回:根部断供,整柱失去支撑下坠(形体坐标随 t² 下移),不是淡出
    float drop = uCollapse * uCollapse;
    float yb = y + drop * H * 0.9;
    float t = yb / H;                         //0 根 → 1 头
    float tc = saturate(t);

    float rise = 1.0 - uCollapse;

    //中轴:根钉死,向上放大的行波+噪声游走,液体甩头不是刚体摆
    float swayEnv = pow(tc, 1.4);
    float nSp = nrm(tex2D(uNoiseTex, float2(uSeed * 3.1, yb * 0.12 - uTime * 0.35)).r);
    float spine = sin(yb * 1.9 + uTime * 3.0 + uSeed) * 0.05 * swayEnv
                + (nSp - 0.5) * 0.14 * swayEnv;
    float dx = xc - spine;
    float sideSign = step(0.0, dx);

    //纵向丝流:x 高频、y 低频、快速上涌——"液体在流"的最强证据,边与密度都吃它
    float fil = nrm(tex2D(uNoiseTex, float2(dx * 4.0 + uSeed * 1.7, yb * 0.08 - uTime * 2.6)).r);
    float fil2 = nrm(tex2D(uNoiseTex, float2(dx * 2.2 + uSeed * 4.3, yb * 0.14 - uTime * 1.7)).g);

    //剖面:根粗头细(上升射流减速签名)+ 上行鼓包(泵动)+ 根部裙 + 一侧撕
    float prof = lerp(0.95, 0.52, tc);
    prof *= 1.0 + 0.10 * sin(yb * 2.4 - uTime * 10.0 + uSeed) * smoothstep(0.0, 0.8, yb);
    prof += exp2(-yb * yb * 2.2) * 0.75 * uMound;
    float eL = nrm(tex2D(uNoiseTex, float2(uSeed * 2.1 + 0.13, yb * 0.22 - uTime * 0.55)).g);
    float eR = nrm(tex2D(uNoiseTex, float2(uSeed * 3.7 + 0.57, yb * 0.26 - uTime * 0.65 + 4.2)).b);
    float eN = lerp(eL, eR, sideSign);
    float asym = lerp(0.05, -0.09, sideSign) * tc;
    float halfW = 0.31 * (prof + asym);

    //塌回时根部颈缩断供
    float neck = lerp(1.0, smoothstep(0.0, 1.6, yb) * 0.9 + 0.1, uCollapse);
    halfW *= neck;

    //柱身(芯股):块状撕边 + 丝流细撕;自水下根起,到头止(头交给球头与指状冠)
    float rr = abs(dx) + (eN - 0.5) * 0.12 * (0.4 + 0.8 * tc) + (fil - 0.5) * 0.05;
    float stem = 1.0 - smoothstep(halfW - 0.05, halfW + 0.02, rr);
    stem *= smoothstep(-1.2, -0.2, y);
    stem *= 1.0 - smoothstep(H - 0.15, H + 0.05, yb);

    //头:推进的液团(球头),塌回时脱离柱身先落
    float headY = H - drop * H * 0.6;
    float2 hp = float2(dx / (0.31 * 1.05), (yb - headY) / 0.62);
    float nH = nrm(tex2D(uNoiseTex, float2(dx * 2.0 + uSeed * 6.3, uTime * 1.3)).b);
    float head = (1.0 - smoothstep(0.72, 1.02, length(hp) + (nH - 0.5) * 0.25))
        * (1.0 - smoothstep(0.5, 1.0, uCollapse));
    head *= 1.0 - stem;
    //球头一侧的窄湿光
    float headSheen = exp2(-pow((hp.x + 0.45) * 3.0, 2.0) - pow((hp.y + 0.25) * 4.0, 2.0)) * head;

    //指状冠:头上 3~5 根竖向液指,长度随 x 噪声,尖端断成滴
    float above = yb - headY;
    float fx = nrm(tex2D(uNoiseTex, float2(xc * 2.6 + uSeed * 5.1, 0.37 + uTime * 0.05)).r);
    float fLen = 0.2 + fx * 1.3;
    float fingerCore = smoothstep(0.60, 0.76, nrm(tex2D(uNoiseTex, float2(xc * 5.0 + uSeed * 8.9, yb * 0.1 - uTime * 1.2)).g));
    float finger = fingerCore * smoothstep(-0.1, 0.05, above) * (1.0 - smoothstep(fLen - 0.25, fLen, above))
        * (1.0 - smoothstep(0.30, 0.55, abs(dx))) * rise;
    finger *= (1.0 - stem) * (1.0 - head);
    float nDrop = nrm(tex2D(uNoiseTex, float2(xc * 1.4 + uSeed * 5.1, yb * 0.45 - uTime * 1.1)).r);
    float crownZone = smoothstep(fLen - 0.2, fLen + 0.2, above) * (1.0 - smoothstep(fLen + 0.6, fLen + 1.4, above));
    float crown = crownZone * smoothstep(0.58, 0.78, nDrop)
        * (1.0 - smoothstep(0.30, 0.55, abs(dx))) * rise * (0.7 + 0.5 * uKe);
    crown *= (1.0 - stem) * (1.0 - head) * (1.0 - finger);

    //回落帘:芯外两翼与芯反向(向下)流的血丝,自顶端挂下、越靠根越密;液体到顶后开始往回落
    float curtainX = smoothstep(halfW * 0.7, halfW * 1.0, abs(dx)) * (1.0 - smoothstep(halfW * 1.5, halfW * 2.1, abs(dx)));
    float cur = nrm(tex2D(uNoiseTex, float2(xc * 3.6 + uSeed * 2.9, yb * 0.11 + uTime * 1.9)).b);
    float curtain = curtainX * smoothstep(0.58, 0.80, cur) * uFallback
        * (1.0 - smoothstep(headY * 0.85, headY, yb)) * smoothstep(-0.3, 0.6, yb);
    curtain *= 1.0 - stem;

    //根部:被顶起的薄溅裙(宽而薄的片)+ 搅浊的沫环;只补体外
    float2 sp = float2(xc / 0.95, (y - 0.06) / 0.20);
    float nS = nrm(tex2D(uNoiseTex, float2(xc * 1.3 + uSeed * 7.1, uTime * 0.5)).g);
    float skirt = (1.0 - smoothstep(0.55, 1.0, length(sp) + (nS - 0.5) * 0.35)) * uMound * step(-0.3, y);
    float ringR = abs(abs(xc) - 0.78 + (nS - 0.5) * 0.12);
    float foamRing = (1.0 - smoothstep(0.03, 0.12, ringR)) * (1.0 - smoothstep(0.0, 0.25, abs(y - 0.02))) * uMound;
    skirt *= 1.0 - stem;
    foamRing *= 1.0 - stem;

    //浓淡由丝流决定:细丝之间透光,厚处沉
    float density = saturate(0.30 + 0.55 * fil + 0.25 * fil2);
    float edgeT = smoothstep(halfW * 0.45, halfW, rr);

    //体色:厚处沉、薄处透亮(薄膜血是亮红),外沿一线暗轮廓
    float3 col = lerp(uColBody, uColDeep, density * 0.55 * (1.0 - edgeT));
    col = lerp(col, uColBright, edgeT * 0.55 + (1.0 - density) * 0.25);
    float outline = smoothstep(halfW - 0.035, halfW - 0.005, rr) * stem;
    col = lerp(col, uColDeep, outline * 0.8);

    //各向异性窄反射带(偏一侧)+ 丝上闪
    float bx = (dx + halfW * 0.40) / max(halfW * 0.10, 0.01);
    float sheenBand = exp2(-bx * bx * 1.5) * stem
        * smoothstep(0.10, 0.50, tc) * (1.0 - smoothstep(0.75, 0.95, tc));
    sheenBand *= 0.6 + 0.4 * fil;
    float glint = smoothstep(0.86, 0.96, fil) * stem * (1.0 - edgeT) * 0.5;

    //水线挂边:入水口一线更暗
    float waterLine = (1.0 - smoothstep(0.0, 0.18, abs(y + 0.05))) * stem * 0.5;

    //预乘合成:芯股近实心,其余层只补芯外
    float aStem = stem * (0.86 + 0.14 * density);
    float aHead = head * 0.95;
    float aFinger = finger * 0.9;
    float aCrown = crown * 0.85;
    float aCurtain = curtain * 0.7;
    float aSkirt = skirt * 0.9;
    float aFoam = foamRing * 0.6;
    float a = saturate(aStem + aHead + aFinger + aCrown + aCurtain + aSkirt + aFoam);
    float3 headCol = lerp(uColBody, uColBright, 0.25 + 0.3 * nH);
    float3 outCol = col * aStem
                  + headCol * aHead
                  + lerp(uColBody, uColBright, 0.4) * (aFinger + aCrown)
                  + lerp(uColBright, uColBody, 0.4) * aCurtain
                  + lerp(uColBody, uColDeep, 0.4) * aSkirt
                  + uColSheen * aFoam * 0.6;
    outCol = lerp(outCol, uColDeep * a, waterLine);
    outCol += uColSheen * (sheenBand * 0.55 + glint + headSheen * 0.5 + skirt * 0.05);

    //画布护栏:左右/上下缘归零,内容在此之前已自然收零
    float guard = smoothstep(1.0, 0.86, abs(xc))
        * smoothstep(0.0, 0.05, coords.y) * smoothstep(1.0, 0.95, coords.y);
    float k = uFade * guard;
    return float4(outCol * k, a * k) * vc;
}

//==================== 血索 ====================

float4 PSSiphon(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float ws = max(uWScale, 0.004);
    float xc = (coords.x - 0.5) * 2.0;
    float yf = (1.0 - coords.y) / ws;          //足(水面)为原点,正=向上
    float L = max(uLenW, 0.5);

    //足:水面张力尖锥,头两个宽内从 1.5 收到 0.35
    float cone = lerp(1.5, 0.35, smoothstep(0.0, 2.0, yf));
    float nF = nrm(tex2D(uNoiseTex, float2(xc * 1.9 + uSeed * 6.1, uTime * 0.8)).g);
    float dCone = abs(xc) - 0.31 * cone + (nF - 0.5) * 0.08;
    float coneBody = (1.0 - smoothstep(-0.03, 0.04, dCone)) * (1.0 - smoothstep(1.2, 2.4, yf));

    //身:一股主流蛇行上抽,一股细流贴着它时分时合(液索不是螺旋模型);
    //蛇行速率与摆幅吃低频噪声,股径低频起伏,边缘不许出锯齿
    float nT = nrm(tex2D(uNoiseTex, float2(uSeed * 1.9, yf * 0.11 - uTime * 0.4)).g);
    float nAmp = nrm(tex2D(uNoiseTex, float2(uSeed * 4.7 + 0.5, yf * 0.16 - uTime * 0.6)).b);
    float twist = yf * 1.3 + (nT - 0.5) * 2.0 - uTime * 4.2 + uSeed;
    float amp = 0.16 * (0.55 + 0.9 * nAmp);
    float sA = sin(twist) * amp;
    //细流:相位滞后半拍、摆幅略小,交叉处与主流并成一股
    float sB = sin(twist - 1.3) * amp * 0.75;
    float nW = nrm(tex2D(uNoiseTex, float2(uSeed * 2.2, yf * 0.12 - uTime * 0.7)).r);
    float rS = 0.15 * (0.85 + 0.3 * nW) * (0.6 + 0.4 * uFill);
    float dA = abs(xc - sA) - rS;
    float dB = abs(xc - sB) - rS * 0.6;
    float strandA = 1.0 - smoothstep(-0.02, 0.03, dA);
    float strandB = 1.0 - smoothstep(-0.02, 0.03, dB);
    float strands = max(strandA, strandB);

    //顶:最后 1.4 宽内喇叭进碗,禁平切
    float mouthFlare = smoothstep(L - 1.4, L, yf) * 0.5;
    float dMouth = abs(xc) - 0.31 * (0.45 + mouthFlare);
    float mouthBody = (1.0 - smoothstep(-0.03, 0.04, dMouth)) * smoothstep(L - 1.4, L - 0.2, yf);

    float body = saturate(strands + coneBody + mouthBody);
    body *= 1.0 - smoothstep(L, L + 0.15, yf);

    //侧甩小滴:绳身外侧随噪声阈值剥离的碎珠带
    float outBand = smoothstep(0.31, 0.40, abs(xc)) * (1.0 - smoothstep(0.55, 0.75, abs(xc)));
    float nSpray = nrm(tex2D(uNoiseTex, float2(xc * 4.2 + uSeed * 6.3, yf * 1.8 - uTime * 3.2)).r);
    float spray = outBand * smoothstep(0.66, 0.86, nSpray)
        * smoothstep(1.0, 3.0, yf) * (1.0 - smoothstep(L - 1.5, L, yf)) * uFill;

    //体色:股心稍亮、缘挂边;一股上一线窄反光
    float3 col = lerp(uColDeep, uColBody, 0.55 + 0.45 * nW);
    col = lerp(col, uColBright, strandA * 0.25 * uFill);
    float bx = (xc - sA + rS * 0.4) / max(rS * 0.22, 0.01);
    float sheen = exp2(-bx * bx * 1.5) * strandA * smoothstep(0.5, 2.5, yf)
        * (1.0 - smoothstep(L - 1.6, L - 0.4, yf));

    float aBody = body * (0.72 + 0.26 * uFill);
    float aSpray = spray * 0.5;
    float a = saturate(aBody + aSpray);
    float3 outCol = col * aBody + lerp(uColDeep, uColSheen, 0.3) * aSpray;
    outCol += uColSheen * sheen * 0.4;

    float guard = smoothstep(1.0, 0.86, abs(xc))
        * smoothstep(0.0, 0.04, coords.y) * smoothstep(1.0, 0.96, coords.y);
    float k = uFade * guard;
    return float4(outCol * k, a * k) * vc;
}

technique TechBead
{
    pass BeadPass
    {
        PixelShader = compile ps_3_0 PSBead();
    }
}

technique TechColumn
{
    pass ColumnPass
    {
        PixelShader = compile ps_3_0 PSColumn();
    }
}

technique TechSiphon
{
    pass SiphonPass
    {
        PixelShader = compile ps_3_0 PSSiphon();
    }
}
