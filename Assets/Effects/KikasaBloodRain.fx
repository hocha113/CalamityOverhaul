// ============================================================================
//KikasaBloodRain.fx 鬼伞血湖形态普攻:血珠 / 血柱 / 血索
//材质=浓血,按血月祭坛血柱确立的三律执行:表面张力挂边(缘最暗最饱和)、
//高光只走各向异性窄反射带(圆高光=塑料)、形体不对称(重力先撕一侧)+颈缩断滴。
//血是暗的:密度走色深不走透明度,没有白芯,"尾暗→白热"是刀与激光的语法,这里禁用。
//TechBead  :血珠。主液团头圆尾锥,沿速度反向拖 2~3 颗颈缩相连的卫星滴,
//           双模态张力抖动(顶点滞空放大),一侧噪声撕胖,一侧窄反射带,
//           uSubmerged 入水转凝血(红进红靠更沉的色读轮廓),uGhost 追击穿透态鬼青缘。
//           头在 quad 上缘(y 负=运动方向):C# 侧 rotation = 速度角 + PiOver2
//TechColumn:血柱。自湖面拔起的上升射流,三稿(2026-09-04):液体感的重点是**离散与解离**,不是圆润——
//           一稿(撕碎毛边)被判"湖里升起个红东西",二稿加球头反而读成"窜出来的红拳头",整体感更重。
//           三稿把柱拆散:柱身=四股各自摆动的液股,离根越远越散开,每股沿高度按噪声颈缩成珠链、
//           越高越碎,到自己的股高断成渐小的滴串;根部诸股并成一段主干;股外散着随流上涌的碎滴;
//           两翼=uFallback 回落帘(向下流的离散细丝);根=离散的溅刺(不是实心丘)+薄沫环;
//           厚(叠合)处沉、单股薄处透亮(薄膜血是亮红)+外沿一线暗轮廓;
//           uCollapse 塌回=根部颈缩断供+整柱下坠+断裂阈值抬升(整束崩解),不淡出。
//           纵坐标以水线为原点按柱宽标定(y=(uRootV-v)/uWScale,正=水上)。
//           飞沫另由 C# 有物理粒子(PRT_KikasaBloodSpray)承担,着色器只画"连续的那几股"
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

//单股液股:中心 cx、基准半径 R、股高 hI;沿高度按噪声颈缩成珠链(低于断裂阈值处半径归零=股断成珠),
//超过股高后滴串渐小渐散;brk 越大断得越碎(随高度与塌回增长);thick 回报该股在此像素的相对厚度
float strandMask(float dx, float yb, float cx, float R, float hI, float brk, float neckAmp, float phase, float eN, out float thick)
{
    //颈缩用解析正弦(Plateau-Rayleigh 的波长本就规则,珠是圆的),噪声只扰相位与幅度让珠链不机械;
    //阈值切在波谷=股断成一颗颗圆珠,而不是噪声阈值切出的平顶片;neckAmp 随高度增长,低处仍是连续的股
    float nb = nrm(tex2D(uNoiseTex, float2(phase, yb * 0.12 - uTime * 0.9)).r);
    float wave = 0.5 + 0.5 * sin(yb * 5.2 - uTime * 36.0 + phase * 6.0 + (nb - 0.5) * 2.5);
    float bead = smoothstep(brk - 0.18, brk + 0.18, wave);
    float rad = R * (1.0 - neckAmp * (1.0 - wave)) * (0.85 + 0.3 * nb) * bead;
    rad *= 1.0 - smoothstep(hI, hI + 1.4, yb);
    float d = abs(dx - cx) + (eN - 0.5) * 0.06;
    float m = 1.0 - smoothstep(rad - 0.035, rad + 0.02, d);
    thick = m * 0.5 * saturate(rad / max(R, 1e-3));
    return m;
}

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

    //整束中轴:根钉死,向上放大的游走;各股再各自摆
    float swayEnv = pow(tc, 1.3);
    float nSp = nrm(tex2D(uNoiseTex, float2(uSeed * 3.1, yb * 0.12 - uTime * 0.35)).r);
    float spine = sin(yb * 1.9 + uTime * 3.0 + uSeed) * 0.05 * swayEnv
                + (nSp - 0.5) * 0.14 * swayEnv;
    float dx = xc - spine;

    //共享蚀边(中频、略毛:一稿的毛边比二稿的光滑读得更像液体)
    float eL = nrm(tex2D(uNoiseTex, float2(uSeed * 2.1 + 0.13, yb * 0.6 - uTime * 1.4)).g);
    float eR = nrm(tex2D(uNoiseTex, float2(uSeed * 3.7 + 0.57, yb * 0.7 - uTime * 1.6 + 4.2)).b);
    float eN = lerp(eL, eR, step(0.0, dx));

    //股束几何:四股,离根越远越散开(解离随高度增长),塌回时更散;
    //断裂阈值根部近零(连续)、越高越碎、塌回整束崩解;根部断供收颈
    float spread = (0.30 + 0.70 * smoothstep(0.0, 0.9, tc)) * (1.0 + 0.5 * uCollapse);
    float taper = lerp(0.95, 0.42, tc);
    //断裂阈值与颈缩幅度都随高度增长:下三分之一是连续的股,越高越碎;塌回整束崩解
    float brk = lerp(-0.25, 0.55, smoothstep(0.25, 1.0, tc)) + 0.45 * uCollapse;
    float neckAmp = lerp(0.12, 0.6, smoothstep(0.15, 0.9, tc)) + 0.25 * uCollapse;
    float neck = lerp(1.0, smoothstep(0.0, 1.6, yb) * 0.9 + 0.1, uCollapse);

    //每股各自的横向摆:相位、频率、方向都不同,股间不同步=不是一体
    float sw0 = sin(yb * 1.6 + uTime * 2.4) * 0.06 * swayEnv;
    float sw1 = sin(yb * 1.3 - uTime * 2.9 + 1.9) * 0.05 * swayEnv;
    float sw2 = sin(yb * 1.9 + uTime * 2.1 + 3.7) * 0.06 * swayEnv;
    float sw3 = sin(yb * 1.5 - uTime * 2.6 + 5.1) * 0.05 * swayEnv;

    float th0, th1, th2, th3;
    float s0 = strandMask(dx, yb, -0.42 * spread + sw0, 0.31 * 0.50 * taper * neck, H * 0.78, brk, neckAmp, uSeed * 1.3 + 0.1, eN, th0);
    float s1 = strandMask(dx, yb, -0.12 * spread + sw1, 0.31 * 0.72 * taper * neck, H * 1.00, brk, neckAmp, uSeed * 1.3 + 0.9, eN, th1);
    float s2 = strandMask(dx, yb,  0.15 * spread + sw2, 0.31 * 0.66 * taper * neck, H * 0.90, brk, neckAmp, uSeed * 1.3 + 1.7, eN, th2);
    float s3 = strandMask(dx, yb,  0.40 * spread + sw3, 0.31 * 0.46 * taper * neck, H * 0.68, brk, neckAmp, uSeed * 1.3 + 2.5, eN, th3);
    float strands = max(max(s0, s1), max(s2, s3));
    //叠合厚度:多股重叠处厚(沉),单股薄处透亮
    float thick = saturate(th0 + th1 + th2 + th3);

    //根部主干:诸股在此并成一股(带上行鼓包泵动),自水下起、沿高度收窄,下三分之一处交给分股
    float trunkW = 0.31 * 0.95 * lerp(1.0, 0.7, saturate(yb / (H * 0.4)))
        * (1.0 + 0.10 * sin(yb * 2.4 - uTime * 10.0 + uSeed)) * neck;
    float trunk = 1.0 - smoothstep(trunkW - 0.05, trunkW + 0.02, abs(dx) + (eN - 0.5) * 0.10);
    trunk *= 1.0 - smoothstep(H * 0.22, H * 0.42, yb);
    float body = max(strands, trunk);
    body *= smoothstep(-1.2, -0.2, y);

    //股外碎滴:随流上涌的离散小团,离束越远越稀;塌回时噪声反向=改为下落
    float outer = 0.31 * (taper * 0.7 + 0.45 * spread);
    float dropZone = smoothstep(outer * 0.7, outer * 1.1, abs(dx))
        * (1.0 - smoothstep(outer * 1.6, outer * 2.6, abs(dx)));
    float scroll = lerp(-uTime * 2.4, uTime * 1.6, uCollapse);
    float nSat = nrm(tex2D(uNoiseTex, float2(xc * 2.6 + uSeed * 5.1, yb * 0.55 + scroll)).r);
    float satDrops = dropZone * smoothstep(0.70, 0.80, nSat)
        * smoothstep(0.2, 1.0, yb) * (1.0 - smoothstep(H, H * 1.5, yb));
    satDrops *= 1.0 - body;

    //回落帘:两翼向下流的细丝,高阈值=离散的丝不是幕;液体到顶后开始往回落
    float curtainX = smoothstep(outer * 0.6, outer * 0.9, abs(dx))
        * (1.0 - smoothstep(outer * 1.5, outer * 2.2, abs(dx)));
    float cur = nrm(tex2D(uNoiseTex, float2(xc * 3.8 + uSeed * 2.9, yb * 0.11 + uTime * 1.9)).b);
    float curtain = curtainX * smoothstep(0.64, 0.82, cur) * uFallback
        * (1.0 - smoothstep(H * 0.8, H, yb)) * smoothstep(-0.3, 0.6, yb);
    curtain *= 1.0 - body;

    //根部:几根离散的溅刺(疏、长短不一,不是梳齿也不是实心丘)+ 一圈薄沫环;只补体外
    float nSpk = nrm(tex2D(uNoiseTex, float2(xc * 1.6 + uSeed * 7.1, uTime * 0.7)).g);
    float spikeLen = (0.2 + nSpk * 1.3) * uMound;
    float spike = smoothstep(0.55, 0.75, nSpk) * (1.0 - smoothstep(spikeLen * 0.5, spikeLen, y))
        * step(-0.25, y) * smoothstep(0.75, 0.25, abs(xc)) * uMound;
    float nS = nrm(tex2D(uNoiseTex, float2(xc * 1.3 + uSeed * 7.1, uTime * 0.5)).g);
    float ringR = abs(abs(xc) - 0.78 + (nS - 0.5) * 0.12);
    float foamRing = (1.0 - smoothstep(0.03, 0.12, ringR))
        * (1.0 - smoothstep(0.0, 0.2, abs(y - 0.02))) * uMound;
    spike *= 1.0 - body;
    foamRing *= 1.0 - body;

    //纵向丝流:股内的浓淡起伏与闪
    float fil = nrm(tex2D(uNoiseTex, float2(dx * 4.0 + uSeed * 1.7, yb * 0.08 - uTime * 2.6)).r);

    //体色:厚(叠合)处沉、单股薄处透亮(薄膜血是亮红),体缘一线暗轮廓
    float3 col = lerp(uColBright, uColBody, smoothstep(0.1, 0.55, thick));
    col = lerp(col, uColDeep, smoothstep(0.55, 1.0, thick) * 0.6 + trunk * 0.25);
    col *= 0.9 + 0.2 * fil;
    float outline = smoothstep(0.1, 0.45, body) * (1.0 - smoothstep(0.45, 0.9, body));
    col = lerp(col, uColDeep, outline * 0.7);

    //主股一侧的窄反光 + 丝上闪
    float mainCx = -0.12 * spread + sw1;
    float mainR = 0.31 * 0.72 * taper;
    float bx = (dx - mainCx + mainR * 0.4) / max(mainR * 0.12, 0.01);
    float sheen = exp2(-bx * bx * 1.5) * s1
        * smoothstep(0.1, 0.5, tc) * (1.0 - smoothstep(0.75, 0.95, tc)) * (0.6 + 0.4 * fil);
    float glint = smoothstep(0.86, 0.96, fil) * body * 0.45;

    //水线挂边:入水口一线更暗
    float waterLine = (1.0 - smoothstep(0.0, 0.18, abs(y + 0.05))) * body * 0.5;

    //预乘合成:股体近实心,其余层只补体外
    float aBody = body * (0.84 + 0.14 * thick);
    float aSat = satDrops * 0.85;
    float aCurtain = curtain * 0.7;
    float aSpike = spike * 0.85;
    float aFoam = foamRing * 0.55;
    float a = saturate(aBody + aSat + aCurtain + aSpike + aFoam);
    float3 outCol = col * aBody
                  + lerp(uColBody, uColBright, 0.45) * aSat
                  + lerp(uColBright, uColBody, 0.4) * aCurtain
                  + lerp(uColBody, uColBright, 0.3) * aSpike
                  + uColSheen * aFoam * 0.6;
    outCol = lerp(outCol, uColDeep * a, waterLine);
    outCol += uColSheen * (sheen * 0.55 + glint);

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
