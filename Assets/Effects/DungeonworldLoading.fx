// ============================================================================
//DungeonworldLoading.fx 地牢子世界加载屏，「下降即加载」吊笼降井
//全幅石壁+中轴吊装组;井缝只是略暗的砖,不用 Abyss 实底糊中间。运动=世界相对上移(uScrollY 由 C# 积分)
//吊装组位姿全部来自 C# 的 DungeonworldCageRig(Verlet 绳物理):主索5段折线、吊环/横担/卸扣、
//双排吊链、倾角笼体、二级摆灯笼;shader 只做几何与光影,不再自摆(旧 sin 摆已删)
//尺寸 #define(CAGE_*/LANT_*)与 DungeonworldLoadTheme 吊笼常量同源,改动必须双改
//色板与 DungeonworldLoadTheme 同源,改动必须双改
//直线算术,无动态分支,无 tex2Dlod,无 atan2(极角审计:零 theta 消费),噪声全 hash 手拼
//fbm ≤3 octave(QuestChronicleBg 验证过的 FNA3D 安全水位)
// ============================================================================

float uTime;        //实时秒
float uScrollY;     //累计滚动量(屏高单位,含方向:进入为正增,退出为负增)
float uDepth;       //0..7 深度
float uAspectRatio;
float uTopLight;    //顶光强度 0..1(CPU 已算入深度衰减与呼吸)
float uCandle;      //烛光 flicker ≈0.68..1.04(CPU 双频+hash)
float3 uBand0;      //七层强调色 I..VII(与 DungeonworldLoadTheme.BandAccents 同源)
float3 uBand1;
float3 uBand2;
float3 uBand3;
float3 uBand4;
float3 uBand5;
float3 uBand6;
float4 uRopeA;      //主索 Verlet 点0-1(纵横比空间:x∈[0,aspect],y 单位=屏高)
float4 uRopeB;      //主索点2-3
float4 uRopeC;      //主索点4-5(zw=吊环原点)
float4 uCagePose;   //xy=笼中心 zw=(sinθ,cosθ)倾角(吊环→笼中心方向)
float4 uLanternPose;//xy=灯笼中心 zw=(sinφ,cosφ)摆角

//恒定主色板(与 DungeonworldLoadTheme 同源)
#define ABYSS      float3(0.0196, 0.0275, 0.0549)
#define STONE_DEEP float3(0.0549, 0.0745, 0.1137)
#define STONE      float3(0.1216, 0.1529, 0.2078)
#define STONE_LIT  float3(0.2275, 0.2627, 0.3373)
#define CANDLE     float3(0.9137, 0.7255, 0.4000)
#define CANDLE_HI  float3(1.0000, 0.9137, 0.7216)
#define COLDLIGHT  float3(0.7800, 0.8500, 1.0000)

//吊装组尺寸(与 DungeonworldLoadTheme 吊笼常量同源,双改)
#define CAGE_DROP   0.17    //吊环→笼中心(=CageDrop)
#define LANT_ATTACH 0.060   //笼中心→笼底挂点(=CageLanternAttach)
#define CAGE_HW     0.078   //笼身顶部半宽(向下略放宽成棺形)
#define CAGE_HH     0.048   //笼身半高

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

float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.07 + float2(3.1, 7.7);
        a *= 0.5;
    }
    return v;
}

//胶囊段距离
float segDist(float2 p, float2 a, float2 b) {
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
    return length(pa - ba * h);
}

//带弧长参数输出的胶囊段距离(链节/珠结相位用)
float segDistT(float2 p, float2 a, float2 b, out float t) {
    float2 pa = p - a;
    float2 ba = b - a;
    t = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
    return length(pa - ba * t);
}

float4 PSDungeonworldLoading(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float aspect = uAspectRatio;

    //==================== 全幅石壁 ====================
    //砖纹铺满画面;中轴只留一条较暗的井缝给吊笼,不是 56% 不透明黑柱
    float wy = uv.y + uScrollY;
    float2 bp = float2(uv.x * aspect, wy) / 0.085;
    bp.x += 0.5 * step(1.0, fmod(floor(bp.y), 2.0));   //隔行错缝
    float2 cell = frac(bp);
    float2 id = floor(bp);

    //凹槽打光(QuestChronicleBg 沟槽法):缝底吃暗 + 下唇受烛
    float mortar = min(min(cell.x, 1.0 - cell.x), min(cell.y, 1.0 - cell.y));
    float groove = exp(-mortar * mortar * 240.0);
    float lip = exp(-(mortar - 0.085) * (mortar - 0.085) * 190.0);
    float3 stone = lerp(STONE, STONE_DEEP, groove * 0.85);
    stone += STONE_LIT * lip * 0.5 * uCandle;
    stone *= 0.95 + 0.38 * hash21(id);                 //逐砖明度(抬底,砖纹须可读)
    stone += (fbm3(bp * 1.7) - 0.5) * 0.07;            //侵蚀斑

    //==================== 七层强调色(帐篷权重乘混合,全程可加无分支) ====================
    float d = uDepth;
    float b0 = saturate(1.0 - abs(d - 0.5));
    float b1 = saturate(1.0 - abs(d - 1.5));
    float b2 = saturate(1.0 - abs(d - 2.5));
    float b3 = saturate(1.0 - abs(d - 3.5));
    float b4 = saturate(1.0 - abs(d - 4.5));
    float b5 = saturate(1.0 - abs(d - 5.5));
    float b6 = saturate(1.0 - abs(d - 6.5) * 0.667);   //末层帐篷放宽,深度钉95%时权重不塌
    float3 accent = uBand0 * b0 + uBand1 * b1 + uBand2 * b2 + uBand3 * b3
                  + uBand4 * b4 + uBand5 * b5 + uBand6 * b6;
    //强调色永远是局部:只染砖缝
    stone += accent * groove * 0.09;

    //==================== 窗洞(每 1.5 屏高一件,hash 定侧与错位) ====================
    float wSpace = 1.5;
    float wIdx = floor(wy / wSpace);
    float wHash = hash21(float2(wIdx, 7.31));
    float wSide = step(0.5, wHash);
    float wCy = (wIdx + 0.32 + 0.36 * frac(wHash * 7.77)) * wSpace;
    float wCx = lerp(0.118, 0.882, wSide);
    float2 wp = float2((uv.x - wCx) * aspect, wy - wCy);
    //尖拱 = 两圆交(vesica)
    float cA = length(wp - float2(0.062, 0.0)) - 0.115;
    float cB = length(wp + float2(0.062, 0.0)) - 0.115;
    float winSDF = max(cA, cB);
    float win = smoothstep(0.010, -0.008, winSDF);
    //层签名内衬:竖栏(II/V)/横架(III)/圆窗(VI)/水线(IV),按层权重混合;I/VII 纯光斑
    float bars = step(0.5, frac(wp.x * 26.0));
    float shelf = step(0.55, frac(wp.y * 20.0));
    float ring = 1.0 - smoothstep(0.006, 0.016, abs(length(wp) - 0.052));
    float water = step(0.02 + 0.012 * sin(wp.x * 34.0 + t * 1.7), wp.y);
    float sil = saturate(bars * (b1 + b4 * 0.8) * 0.6
              + shelf * b2 * 0.65
              + ring * b5 * 0.9
              + water * b3 * 0.45);
    //洞内 = 强调色光斑(fbm 微流动) - 剪影
    float3 winGlow = accent * (0.50 + 0.42 * fbm3(wp * 7.0 + float2(t * 0.12, 0.0)));
    winGlow *= 1.0 - sil;
    stone = lerp(stone, winGlow + accent * 0.22, win);

    //中轴井缝:只把砖略压暗,中间砖纹仍清楚,禁止用 Abyss 实底糊一块
    float well = smoothstep(0.40, 0.47, uv.x) * smoothstep(0.60, 0.53, uv.x);
    float3 col = lerp(stone, stone * 0.78, well * 0.50);

    //==================== 吊装组(位姿全部来自 DungeonworldCageRig 的 Verlet 结果) ====================
    float2 q = float2(uv.x * aspect, uv.y);
    float2 r0 = uRopeA.xy;
    float2 r1 = uRopeA.zw;
    float2 r2 = uRopeB.xy;
    float2 r3 = uRopeB.zw;
    float2 r4 = uRopeC.xy;
    float2 r5 = uRopeC.zw;
    float2 cageC = uCagePose.xy;
    float ts = uCagePose.z;
    float tc = uCagePose.w;
    float2 dC = q - cageC;
    //笼局部系:+y=沿吊链向下,吊环在局部 (0,-CAGE_DROP)
    float2 pl = float2(dC.x * tc - dC.y * ts, dC.x * ts + dC.y * tc);

    //---- 投影与接触暗影(先画影,构件盖在自己影子上) ----
    //顶光自上而下,影子落在壁面略下处并随摆动平移;半影随投距变宽(简化剪影=圆角箱+末两段索)
    float2 shOff = float2((cageC.x - 0.5 * aspect) * 0.35, 0.045);
    float2 sq = q - shOff;
    float2 sdC = sq - cageC;
    float2 spl = float2(sdC.x * tc - sdC.y * ts, sdC.x * ts + sdC.y * tc);
    float shBox = max(abs(spl.x) - (CAGE_HW + 0.012), abs(spl.y) - (CAGE_HH + 0.020)) - 0.006;
    float shD = min(shBox, segDist(sq, r3, r5) - 0.006);
    float shadow = smoothstep(0.034, -0.006, shD) * uTopLight * 0.34;
    //接触暗影:壁面在笼后小范围环境吃暗(不吃顶光,烛光下也存在)
    float2 aoP = dC * float2(1.0, 0.72);
    float ao = exp(-dot(aoP, aoP) * 55.0) * 0.24;
    col *= 1.0 - shadow - ao;

    //---- 灯笼壁面暖斑:跟随灯笼实际位置摆动(构件前画,剪影保持吃黑) ----
    float2 lq = q - uLanternPose.xy;
    float lqr2 = dot(lq, lq);
    col += CANDLE * (exp(-lqr2 * 260.0) * 0.16 + exp(-lqr2 * 26.0) * 0.045) * uCandle;

    //---- 后排吊链(伪进深:更暗更细,x 收 0.86,笼体随后盖住其下端) ----
    float2 chA = float2(0.030, -CAGE_DROP + 0.031);    //横担端(局部)
    float2 chB = float2(CAGE_HW * 0.92, -CAGE_HH - 0.004); //笼肩(局部)
    float backCh = min(
        segDist(pl, float2(-chA.x * 0.86, chA.y), float2(-chB.x * 0.86, chB.y)),
        segDist(pl, float2(chA.x * 0.86, chA.y), float2(chB.x * 0.86, chB.y)));
    float backM = smoothstep(0.0034, 0.0012, backCh);
    col = lerp(col, float3(0.012, 0.014, 0.020), backM * 0.55);

    //---- 主索:5 段胶囊过 Verlet 点,上粗下细+绞纹+节点缠箍 ----
    float dRope = segDist(q, r0, r1);
    dRope = min(dRope, segDist(q, r1, r2));
    dRope = min(dRope, segDist(q, r2, r3));
    dRope = min(dRope, segDist(q, r3, r4));
    dRope = min(dRope, segDist(q, r4, r5));
    float along = saturate((q.y - r0.y) / max(r5.y - r0.y, 0.05));  //近竖直,弧长≈纵距
    float ropeR = lerp(0.0042, 0.0033, along);
    float ropeM = smoothstep(ropeR + 0.0014, ropeR - 0.0010, dRope);
    col = lerp(col, float3(0.010, 0.012, 0.017), ropeM * 0.94);
    //绞纹:沿索斜向受烛亮线(相位钉在索上,不随世界滚动)
    float twist = smoothstep(0.55, 0.95, sin((q.y - r0.y) * 260.0 + (q.x - r0.x) * 90.0));
    col += STONE_LIT * ropeM * twist * 0.22 * uCandle;
    //节点缠箍:内节点近旁略粗一环
    float knot = min(min(length(q - r1), length(q - r2)), min(length(q - r3), length(q - r4)));
    float knotM = smoothstep(0.0085, 0.0045, knot);
    col = lerp(col, float3(0.014, 0.016, 0.022), knotM * 0.85);
    col += CANDLE * knotM * 0.10 * uCandle;

    //---- 吊环/横担/卸扣(绳笼衔接五金,与笼同倾角) ----
    float2 yk = float2(pl.x, pl.y + CAGE_DROP);        //吊环局部(原点=主索末端)
    float ringD = abs(length(yk - float2(0.0, 0.010)) - 0.0135) - 0.0042;
    float barD = max(abs(yk.x) - 0.030, abs(yk.y - 0.0305) - 0.0036);
    float pinD = max(abs(yk.x) - 0.0030, abs(yk.y - 0.022) - 0.0075);
    float yokeD = min(ringD, min(barD, pinD));
    float yokeM = smoothstep(0.0022, -0.0012, yokeD);
    col = lerp(col, float3(0.012, 0.014, 0.021), yokeM * 0.96);
    //环上缘受顶光一线冷高光
    float ringRim = smoothstep(0.0022, 0.0, abs(ringD)) * step(yk.y, 0.004);
    col += COLDLIGHT * ringRim * 0.20 * uTopLight;

    //---- 前排吊链:两段折线近似悬垂 + 链节宽度调制 ----
    float2 chM = (chA + chB) * 0.5 + float2(0.0035, 0.0035);   //垂弧折点(外/下)
    float dchR = min(segDist(pl, chA, chM), segDist(pl, chM, chB));
    float2 plm = float2(-pl.x, pl.y);
    float dchL = min(segDist(plm, chA, chM), segDist(plm, chM, chB));
    float linkPhase = sin(pl.y * 480.0);
    float linkW = 0.0020 + 0.0014 * (0.5 + 0.5 * linkPhase);
    float chainM = smoothstep(linkW + 0.0013, linkW - 0.0009, min(dchR, dchL));
    col = lerp(col, float3(0.009, 0.011, 0.017), chainM * 0.92);
    col += STONE_LIT * chainM * (0.5 + 0.5 * sin(pl.y * 480.0 + 1.6)) * 0.14 * uCandle;

    //---- 笼体(局部系):檐顶双盖+四角立柱+板身+双箍带铆钉+底裙+底肋 ----
    float bw = CAGE_HW + pl.y * 0.10;                  //向下略放宽的棺形(沿旧值)
    float bodyD = max(abs(pl.x) - bw, abs(pl.y) - CAGE_HH);
    float roofD = min(
        max(abs(pl.x) - CAGE_HW * 1.16, abs(pl.y + CAGE_HH + 0.0065) - 0.0090),
        max(abs(pl.x) - CAGE_HW * 0.88, abs(pl.y + CAGE_HH + 0.0210) - 0.0068));
    float skirtD = max(abs(pl.x) - (bw + 0.008), abs(pl.y - CAGE_HH - 0.0085) - 0.0075);
    float ribD = max(abs(abs(pl.x) - 0.040) - 0.0058, abs(pl.y - CAGE_HH - 0.020) - 0.0052);
    float postD = max(abs(abs(pl.x) - (bw - 0.009)) - 0.0068, abs(pl.y) - CAGE_HH);
    float strapY = min(abs(pl.y + 0.016), abs(pl.y - 0.014));
    float strapD = max(strapY - 0.0042, abs(pl.x) - bw);

    float bodyM = smoothstep(0.0035, -0.0025, bodyD);
    float roofM = smoothstep(0.0030, -0.0020, roofD);
    float skirtM = smoothstep(0.0030, -0.0020, skirtD);
    float ribM = smoothstep(0.0026, -0.0016, ribD);
    float postM = smoothstep(0.0026, -0.0016, postD);
    float strapM = smoothstep(0.0024, -0.0014, strapD) * bodyM;

    //板身吃黑;内衬近灯处透一丝暖(玩家在笼中,只暗示不描绘)
    col = lerp(col, float3(0.0065, 0.0085, 0.0135), bodyM * 0.96);
    float2 toLant = pl - float2(0.0, LANT_ATTACH);
    float lantNear = dot(toLant, toLant);
    col += CANDLE * exp(-lantNear * 300.0) * bodyM * 0.05 * uCandle;
    //四角立柱最暗,箍带略提亮,交点铆钉受烛
    col = lerp(col, float3(0.0045, 0.0060, 0.0100), postM * 0.90);
    col = lerp(col, float3(0.0160, 0.0190, 0.0270), strapM * 0.85);
    float rivet = smoothstep(0.0042, 0.0016,
        length(float2(abs(pl.x) - (bw - 0.009), strapY)));
    col += CANDLE * rivet * bodyM * 0.14 * uCandle;
    //檐顶双盖/底裙/底肋
    col = lerp(col, float3(0.0080, 0.0100, 0.0160), roofM * 0.97);
    col = lerp(col, float3(0.0075, 0.0095, 0.0150), skirtM * 0.96);
    col = lerp(col, float3(0.0060, 0.0080, 0.0130), ribM * 0.94);

    //---- 灯笼(自摆坐标系):珠结挂链+壳/盖/提钮/座+玻璃暖窗+焰芯 ----
    float2 lantC = uLanternPose.xy;
    float ls = uLanternPose.z;
    float lc = uLanternPose.w;
    //挂点(世界)=笼底中心:C + R·(0,LANT_ATTACH)
    float2 attach = cageC + float2(ts * LANT_ATTACH, tc * LANT_ATTACH);
    float th;
    float hangD = segDistT(q, attach, lantC, th);
    float hangW = 0.0016 + 0.0010 * (0.5 + 0.5 * sin(th * 25.13));  //两粒链珠
    float hangM = smoothstep(hangW + 0.0011, hangW - 0.0008, hangD);
    col = lerp(col, float3(0.010, 0.012, 0.018), hangM * 0.90);
    float2 dL = q - lantC;
    float2 ll = float2(dL.x * lc - dL.y * ls, dL.x * ls + dL.y * lc);
    float lampD = max(abs(ll.x) - 0.0100, abs(ll.y) - 0.0125);                 //壳
    lampD = min(lampD, max(abs(ll.x) - 0.0128, abs(ll.y + 0.0135) - 0.0032));  //盖
    lampD = min(lampD, max(abs(ll.x) - 0.0026, abs(ll.y + 0.0182) - 0.0028));  //提钮
    lampD = min(lampD, max(abs(ll.x) - 0.0118, abs(ll.y - 0.0140) - 0.0026));  //座
    float lampM = smoothstep(0.0026, -0.0016, lampD);
    col = lerp(col, float3(0.009, 0.011, 0.016), lampM * 0.95);
    //玻璃窗:壳内开窗透暖,焰芯双层
    float paneD = max(abs(ll.x) - 0.0066, abs(ll.y) - 0.0085);
    float paneM = smoothstep(0.0015, -0.0012, paneD);
    col += CANDLE * paneM * (0.55 + 0.25 * uCandle) * 0.90;
    float lr2 = dot(ll, ll);
    col += CANDLE * exp(-lr2 * 900.0) * 0.55 * uCandle;
    col += CANDLE_HI * exp(-lr2 * 5200.0) * 0.85 * uCandle;

    //---- 边缘受光:顶光冷 rim 落上缘,灯火暖 rim 落下缘(把组件读成受光实体) ----
    float roofRim = smoothstep(0.0026, 0.0004, abs(roofD)) * step(pl.y, -CAGE_HH - 0.012);
    col += COLDLIGHT * roofRim * 0.24 * uTopLight;
    float skirtRim = smoothstep(0.0026, 0.0004, abs(skirtD)) * step(CAGE_HH + 0.009, pl.y);
    col += CANDLE * skirtRim * 0.16 * uCandle;
    float sideRim = smoothstep(0.0022, 0.0004, abs(bodyD)) * exp(-lantNear * 140.0);
    col += CANDLE * sideRim * 0.12 * uCandle;

    //==================== 顶光柱(教堂天光,冷光;降得越深越微弱) ====================
    float shaft = exp(-abs(uv.x - 0.5) * aspect * 6.0) * exp(-uv.y * 3.0);
    col += COLDLIGHT * shaft * uTopLight * 0.55;
    col += CANDLE_HI * shaft * shaft * uTopLight * 0.18;

    //==================== 前景掠过件(大横梁剪影,3.2 倍视差急速上掠) ====================
    float fy = uv.y + uScrollY * 3.2;
    float bIdx = floor(fy / 3.7);
    float bHash = hash21(float2(bIdx, 3.77));
    float bY = fy - (bIdx + frac(bHash * 13.1)) * 3.7;
    float beam = smoothstep(0.040, 0.024, abs(bY)) * step(0.35, bHash);
    col = lerp(col, ABYSS * 0.45, beam * 0.88);

    //==================== 收尾:轻屏角 + 细尘。入场黑场已由压黑门代劳,禁止再乘入场包络整屏压黑 ====================
    float2 vg = uv * 2.0 - 1.0;
    col *= 1.0 - saturate(dot(vg * float2(0.55, 0.50), vg * float2(0.55, 0.50))) * 0.10;
    float dust = hash21(uv * 1531.7 + floor(t * 11.0) * 17.3);
    col *= 1.0 - dust * 0.03;

    return float4(saturate(col), 1.0);
}

technique DungeonworldLoading
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSDungeonworldLoading();
    }
}
