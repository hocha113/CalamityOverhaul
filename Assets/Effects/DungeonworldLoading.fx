// ============================================================================
//DungeonworldLoading.fx 地牢子世界加载屏——「下降即加载」吊笼降井
//全幅石壁+中轴吊笼;井缝只是略暗的砖,不用 Abyss 实底糊中间。运动=世界相对上移(uScrollY 由 C# 积分)
//色板与 DungeonworldLoadTheme 同源,改动必须双改
//直线算术,无动态分支,无 tex2Dlod,无 atan2(极角审计:零 theta 消费),噪声全 hash 手拼
//fbm ≤3 octave(QuestChronicleBg 验证过的 FNA3D 安全水位)
// ============================================================================

float uTime;        //实时秒
float uScrollY;     //累计滚动量(屏高单位,含方向:进入为正增,退出为负增)
float uDepth;       //0..7 深度
float uAspectRatio;
float uIntro;       //入场包络 0..1(0=纯黑保持;兼吊笼滑入驱动)
float uTopLight;    //顶光强度 0..1(CPU 已算入深度衰减与呼吸)
float uCandle;      //烛光 flicker ≈0.68..1.04(CPU 双频+hash)
float3 uBand0;      //七层强调色 I..VII(与 DungeonworldLoadTheme.BandAccents 同源)
float3 uBand1;
float3 uBand2;
float3 uBand3;
float3 uBand4;
float3 uBand5;
float3 uBand6;

//恒定主色板(与 DungeonworldLoadTheme 同源)
#define ABYSS      float3(0.0196, 0.0275, 0.0549)
#define STONE_DEEP float3(0.0549, 0.0745, 0.1137)
#define STONE      float3(0.1216, 0.1529, 0.2078)
#define STONE_LIT  float3(0.2275, 0.2627, 0.3373)
#define CANDLE     float3(0.9137, 0.7255, 0.4000)
#define CANDLE_HI  float3(1.0000, 0.9137, 0.7216)
#define COLDLIGHT  float3(0.7800, 0.8500, 1.0000)

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

    //==================== 吊索(sin 慢摆 3.8s) + 链结 ====================
    float cageSlide = 1.0 - pow(1.0 - uIntro, 3.0);    //easeOutCubic 滑入
    float cageCY = lerp(-0.28, 0.635, cageSlide);
    float ropeX = 0.5 + sin(t * 1.65 + uv.y * 2.3) * 0.004;
    float ropeD = abs(uv.x - ropeX) * aspect;
    float ropeMask = smoothstep(0.0038, 0.0012, ropeD) * step(uv.y, cageCY);
    col = lerp(col, float3(0.008, 0.010, 0.016), ropeMask * 0.9);
    //链结节点固定在索上(与笼同静止,世界相对滚动)
    float knot = smoothstep(0.60, 0.95, sin(uv.y * 34.0));
    col += STONE_LIT * ropeMask * knot * 0.3 * uCandle;

    //==================== 吊笼(平顶棺形剪影 + 四角吊链 + 笼底烛灯) ====================
    float2 cp = float2((uv.x - 0.5) * aspect, uv.y - cageCY);
    float cw = 0.082 + cp.y * 0.10;                    //向下略放宽的棺形
    float cage = max(abs(cp.x) - cw, abs(cp.y) - 0.052);
    float cageM = smoothstep(0.005, -0.004, cage);
    col = lerp(col, float3(0.006, 0.008, 0.013), cageM * 0.96);
    //吊链:自索底收拢到笼顶两肩
    float chainSpan = saturate((cp.y + 0.182) / 0.13);
    float chain = smoothstep(0.0045, 0.0015, abs(abs(cp.x) - 0.074 * chainSpan))
                * step(-0.182, cp.y) * step(cp.y, -0.050);
    col = lerp(col, float3(0.008, 0.010, 0.016), chain * 0.85);
    //烛灯:暖光点+烛芯,flicker;并向近壁漫射一点暖
    float2 lp = cp - float2(0.0, 0.063);
    float lr2 = dot(lp, lp);
    col += CANDLE * exp(-lr2 * 850.0) * 1.05 * uCandle;
    col += CANDLE_HI * exp(-lr2 * 4200.0) * 0.9 * uCandle;
    col += CANDLE * exp(-dot(cp, cp) * 20.0) * 0.05 * uCandle;

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

    //==================== 收尾:轻屏角 + 细尘。入场黑场已由压黑门代劳,禁止再乘 uIntro 整屏压黑 ====================
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
