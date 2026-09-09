// ============================================================================
//KikasaInkSplash.fx 鬼伞墨滴命中水花:命中一瞬爆发,十几帧内崩解干净,不留任何滞留层
//二版(2026-09-09):一版是"沿直线摆放的液指 + 起手/断裂/断供三段包络",实机被判
//"像帧图动画,不够液体"。根因:形状是摆出来的,不是流出来的——指是直线、珠链相位钉在空间里
//(只会出现消失不会滑动)、三段包络是离散状态切换。二版改成像素空间的**拉格朗日弹道模型**:
//  · 每指是一根液体射流:流体元自指根以初速 v 沿方向 d 喷出,之后只受重力(uGrav,px/f²),
//    射流此刻的形状 = 历次喷出元此刻所在位置连成的线(streakline),天然是弯下去的抛物线;
//    像素→流体元的反解:沿 d 的位置 f(T)=v(T)·T+½g_d·T² 由二阶初猜+一步牛顿解出该像素的元龄 T
//  · 晚喷出的元更慢(薄片余能衰减,v(T)=v0·(0.65+0.35·T/te)),链从根被"拉"出来、头快尾慢
//  · 颈缩珠链的相位是 T 的函数(拉格朗日):珠贴着流体一起往外滑,不是原地闪;
//    颈缩幅度与断裂阈值随元龄增长(Plateau-Rayleigh 扰动随时间放大):指尖先碎、根部仍连
//  · 半径沿链:根 R0 → 中段拉细 → 头部鼓成液滴头(质量往头聚);头是真正的圆球
//  · 喷出在 te 帧后停止:此后最新元龄 Tmin=t−te>0,链脱根整体继续飞、后端拉细断成珠串
//  · 指根落在铺开薄片的边缘上(边缘半径 rimR(t) 随时间外扩后停),不是从一个点射出:
//    边缘是 3D 圆环的侧视投影,根位 x=c·rimR、c=cos(方位角),两翼指外倾、正前正后的指近直立
//  · 甩尾:横向正弦沿链传播,尾端幅大
//  · 落回表面(uSurfaceClip,y<0)即消失=落地了,重力真的在收尾
//  · 逐指湿光带在朝天一侧(upQ=−重力方向),随元龄褪成哑光;薄韧带半透成飞白、头与叠合处沉
//  · 薄片=撞击点铺开的透镜薄膜,边缘环状略厚+噪声波峰隆起,质量随喷出流失而消失;
//    撞击芯=头几帧的一团墨(带血芯)随铺开缩没
//  没有任何"阶段切换":每一帧都是同一套物理往前推。全部由 uT(帧)驱动,无 uTime:暂停即冻结,多端一致。
//坐标:像素空间,根为原点,+y 沿撞击法线离面,+x 沿贴面切向。
//直线算术+普通 tex2D,零流控(step/smoothstep/lerp 门乘),FNA3D 安全;
//绑定噪声实测值域 0.227~0.776,阈值一律过 nrm() 归一。
//旧版效果框架(fx_2_0)不去重内联函数里的字面常量,7 份 finger() 展开会顶到 224 常量寄存器:
//字面数尽量并入 kA/kB 两个 float4 由主函数传入,函数体内少写数字(编译失败 X4507 即此症)。
//预乘输出进 AlphaBlend 批;消费入口 KikasaRains/KikasaInkSplashFX.cs(指几何与轨迹公式两端同式)
// ============================================================================

float uT;           //寿命帧数 0~uLife(CPU 驻帧驱动)
float uLife;        //总寿命(帧)
float4 uQuad;       //x=quadW px  y=quadH px  z=根 u  w=根 v
float2 uGrav;       //重力 px/f² 在 quad 空间:x=切向 y=法向(正=离面)
float uSurfaceClip; //1=贴面(y<0 的流体已落地消失) 0=无面(沾敌/空中)
float uRimMax;      //薄片最大半宽 px,指根随它外扩
float uKe;          //撞击动能 0~1(薄片厚度/撞击芯)
float uSkew;        //切向偏斜 -1~1(薄片与撞击芯往前带)
float uFingerN;     //液指数 ≤7(整数值,多余槽位门乘归零)
float4 uFinger[7];  //x,y=发射方向(quad 空间单位向量) z=初速 px/f  w=根半宽 px
float4 uFingerB[7]; //x=相位  y=喷出时长 te(帧)  z=甩尾幅 px  w=指根落位系数 c∈[-1,1]
float uSeed;
float3 uColBody;    //墨体
float3 uColDeep;    //墨缘/叠合沉色
float3 uColCore;    //血芯(撞击芯头几帧)
float3 uColSheen;   //湿光

sampler uNoiseTex : register(s1);

//绑定噪声归一:实测值域 0.227~0.776,映到 0~1 后阈值才有效
float nrm(float n) { return saturate((n - 0.23) * 1.82); }

//单指。p=像素(px,根原点),fa=(dx,dy,v0,R0),fb=(相位,te,甩尾幅,c),w=槽位权重,t=当前帧,
//rimR=此刻薄片半宽,eJ=共享蚀边抖动(px,带符号),upQ=朝天方向,
//kA=(0.65,0.35,0.5,0.9) kB=(6.2832/6.4, 1.0/7.0, 1.6, 0.4):常量打包传入,省寄存器。
//返回覆盖;thick=相对厚度(头厚),sheen=湿光
float finger(float2 p, float4 fa, float4 fb, float w, float t, float rimR, float eJ, float2 upQ,
    float4 kA, float4 kB, out float thick, out float sheen)
{
    float2 d = fa.xy;
    float2 n = float2(-d.y, d.x);
    float v0 = fa.z;
    float R0 = fa.w;
    float te = fb.y;
    float2 pl = p - float2(fb.w * rimR, 0.0);
    float s = dot(pl, d);
    float q = dot(pl, n);
    float gd = dot(uGrav, d);
    float gn = dot(uGrav, n);

    //反解元龄 T:先按恒速+重力的二阶初猜,再对含速度衰减的 f(T)=v(T)·T+½g_d·T² 做一步牛顿;f' 钳正
    float T0 = s / v0;
    float T = T0 - kA.z * gd * T0 * T0 / v0;
    float sv = saturate(T / te);
    float vT = v0 * (kA.x + kA.y * sv);
    float dv = v0 * kA.y / te * (1.0 - step(te, T));
    float f = vT * T + kA.z * gd * T * T;
    float fp = max(vT + dv * T + gd * T, kB.w);
    T -= (f - s) / fp;
    T = clamp(T, -1.5, t + 1.5);

    //链存在的元龄区间:[Tmin,t],喷出停止后 Tmin 抬升=链脱根
    float Tmin = max(0.0, t - te);
    float inRange = smoothstep(Tmin - 0.6, Tmin + kB.w, T) * (1.0 - smoothstep(t - 0.3, t + 0.3, T));

    //珠链:相位随元龄走(珠贴着流体外滑),波长≈6.4 倍根半宽;颈缩与断裂随元龄放大,指尖先碎;
    //断开时质量往珠里聚:波峰随颈缩幅度鼓起(Rayleigh 断滴比韧带粗),波谷收零
    float omega = kB.x * v0 / R0;
    float wave = kA.z + kA.z * sin(T * omega + fb.x + eJ * kB.z);
    float age = saturate((T - 1.0) * kB.y);
    float neckAmp = kA.w * age * sqrt(age);
    float brk = lerp(-0.35, 0.62, age);
    float bead = smoothstep(brk - 0.17, brk + 0.17, wave);
    //半径沿链:根 R0 → 中段拉细到 0.65 → 近头回粗(质量往头聚),不做球棒头
    float u = saturate(T / max(t, 0.01));
    float Rprof = R0 * (1.0 - kA.y * smoothstep(0.0, kA.z, u) + kA.y * smoothstep(0.6, 1.0, u));
    float rad = Rprof * (1.0 - neckAmp * (1.0 - wave)) * (1.0 + 0.45 * neckAmp * wave) * bead;

    //甩尾:横向正弦沿链传播(相位含 t),尾端幅大;元的横向位置=重力偏折+甩尾
    float whip = fb.z * sin(T * kA.w - t * kA.y + fb.x + fb.x) * saturate(T * 0.25);
    float cq = kA.z * gn * T * T + whip;
    float dq = abs(q - cq) + eJ;
    float body = (1.0 - smoothstep(rad - kA.w, rad + 0.7, dq)) * inRange;
    //半径归零处不得留发丝线
    body *= smoothstep(0.25, kA.w, rad);

    //液滴头:最早喷出的元,沿自身速度方向拉长的泪滴(运动的液体必须各向异性),略随元龄收一点
    float vH = v0 * (kA.x + kA.y * saturate(t / te));
    float whipH = fb.z * sin(t * 0.55 + fb.x + fb.x) * saturate(t * 0.25);
    float2 rHead = d * (vH * t + kA.z * gd * t * t) + n * (kA.z * gn * t * t + whipH);
    float2 hv = normalize(d * vH + uGrav * t + float2(0.0001, 0.0));
    float2 rel = pl - rHead;
    float along = dot(rel, hv) / 1.45;
    float across = dot(rel, float2(-hv.y, hv.x));
    float Rh = R0 * 1.08 * (1.0 - 0.1 * saturate((t - 8.0) * 0.125));
    float dh = sqrt(along * along + across * across) + eJ;
    float head = 1.0 - smoothstep(Rh - kA.w, Rh + 0.7, dh);

    float m = max(body, head) * w;
    thick = (body * saturate(rad / R0) + head * 1.2) * w;

    //湿光:朝天一侧的窄带,新鲜流体亮、随元龄褪成哑光。头上不点高光:一稿头顶亮点让指读成"长眼睛的触手"
    float sgn = sign(dot(n, upQ) + 0.0001);
    float bandW = 0.3 * max(rad, kA.z) + kA.z;
    float bx = (q - cq - sgn * kA.z * rad) / bandW;
    float fresh = 1.0 - smoothstep(1.0, 9.0, T);
    sheen = exp2(-bx * bx * 1.4) * body * fresh * 0.7 * w;
    return m;
}

float4 PSSplash(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float2 p = float2((coords.x - uQuad.z) * uQuad.x, (uQuad.w - coords.y) * uQuad.y);
    float t = uT;
    //薄片边缘外扩:快起后停
    float rimR = uRimMax * (1.0 - exp(-t / 2.2));
    float2 upQ = -normalize(uGrav + float2(0.0, 0.0001));
    float4 kA = float4(0.65, 0.35, 0.5, 0.9);
    float4 kB = float4(6.2832 / 6.4, 1.0 / 7.0, 1.6, 0.4);

    //共享蚀边(中频、略毛,折成 ±0.6px 抖动)与低频撕裂噪声
    float eN = nrm(tex2D(uNoiseTex, p * 0.045 + uSeed * 2.1).g);
    float eJ = (eN - 0.5) * 1.2;
    float nTear = nrm(tex2D(uNoiseTex, p * float2(0.02, 0.05) + uSeed * 1.3 + 0.41).r);
    float nCrest = nrm(tex2D(uNoiseTex, float2(p.x * 0.07 + uSeed * 3.7, 0.31)).b);

    float th0, th1, th2, th3, th4, th5, th6;
    float sh0, sh1, sh2, sh3, sh4, sh5, sh6;
    float m0 = finger(p, uFinger[0], uFingerB[0], step(0.5, uFingerN), t, rimR, eJ, upQ, kA, kB, th0, sh0);
    float m1 = finger(p, uFinger[1], uFingerB[1], step(1.5, uFingerN), t, rimR, eJ, upQ, kA, kB, th1, sh1);
    float m2 = finger(p, uFinger[2], uFingerB[2], step(2.5, uFingerN), t, rimR, eJ, upQ, kA, kB, th2, sh2);
    float m3 = finger(p, uFinger[3], uFingerB[3], step(3.5, uFingerN), t, rimR, eJ, upQ, kA, kB, th3, sh3);
    float m4 = finger(p, uFinger[4], uFingerB[4], step(4.5, uFingerN), t, rimR, eJ, upQ, kA, kB, th4, sh4);
    float m5 = finger(p, uFinger[5], uFingerB[5], step(5.5, uFingerN), t, rimR, eJ, upQ, kA, kB, th5, sh5);
    float m6 = finger(p, uFinger[6], uFingerB[6], step(6.5, uFingerN), t, rimR, eJ, upQ, kA, kB, th6, sh6);
    float body = max(max(max(m0, m1), max(m2, m3)), max(max(m4, m5), m6));
    float thick = saturate(th0 + th1 + th2 + th3 + th4 + th5 + th6);
    float sheen = saturate(sh0 + sh1 + sh2 + sh3 + sh4 + sh5 + sh6);

    //落回表面即消失:贴面时 y<0 的流体已经落地
    float clip = lerp(1.0, smoothstep(-3.0, -0.5, p.y), uSurfaceClip);
    body *= clip;
    thick *= clip;
    sheen *= clip;

    //薄片:撞击点铺开的透镜薄膜,向边缘变薄、边缘环略厚、噪声波峰隆起;质量随喷出流失而消失,晚期被撕
    float h0 = 3.0 + 3.0 * uKe;
    float massLeft = 1.0 - smoothstep(3.0, 10.0, t);
    float hT = h0 * (1.0 - 0.5 * saturate(t * 0.125));
    float tor = (abs(p.x) - rimR) / 3.0;
    float torus = exp2(-tor * tor * 1.4427);
    float hLocal = hT * (1.0 - 0.35 * smoothstep(0.0, max(rimR, 1.0), abs(p.x))) + 1.4 * torus + 1.5 * smoothstep(0.55, 0.85, nCrest);
    float xs = p.x - uSkew * 0.3 * rimR;
    float edgeJ = eJ + eJ;
    float inX = 1.0 - smoothstep(rimR + 0.5 + edgeJ, rimR + 2.5 + edgeJ, abs(xs));
    float sheetY = 1.0 - smoothstep(hLocal - 0.8, hLocal + 0.8, abs(p.y - hLocal * 0.35));
    float tearLvl = lerp(-0.2, 0.8, smoothstep(4.0, 10.0, t));
    float tear = smoothstep(tearLvl - 0.15, tearLvl + 0.15, nTear);
    float sheet = inX * sheetY * massLeft * tear * step(-1.5, p.y) * (1.0 - body);

    //撞击芯:头几帧撞击点上的一团墨,随铺开缩没;带血芯
    float coreT = saturate(t * 0.2);
    float coreR = (5.0 + 4.0 * uKe) * (1.0 - coreT);
    float core = (1.0 - smoothstep(coreR - 1.0, coreR + 0.8, length(p - float2(uSkew + uSkew, 1.5)) + eJ))
        * (1.0 - body) * step(-1.5, p.y);

    //体色:薄韧带透成飞白、体墨、头与叠合处沉;体缘一线暗轮廓
    float3 wash = lerp(uColBody, uColSheen, 0.16);
    float3 col = lerp(wash, uColBody, smoothstep(0.1, 0.5, thick));
    col = lerp(col, uColDeep, smoothstep(0.7, 1.3, thick) * 0.6);
    float outline = smoothstep(0.1, 0.45, body) * (1.0 - smoothstep(0.45, 0.9, body));
    col = lerp(col, uColDeep, outline * 0.6);

    //预乘合成:薄处略透、厚处近实;薄片与撞击芯只补体外
    float aBody = body * (0.78 + 0.22 * saturate(thick));
    float aSheet = sheet * 0.72;
    float aCore = core * 0.9;
    float a = saturate(aBody + aSheet + aCore);
    float3 outCol = col * aBody
                  + lerp(wash, uColBody, 0.6) * aSheet
                  + lerp(uColBody, uColCore, 0.35 * (1.0 - coreT)) * aCore;
    outCol += uColSheen * sheen * 0.35;

    //画布护栏:四缘归零;寿命末三帧交接给粒子后清残
    float guard = smoothstep(0.0, 0.05, coords.x) * smoothstep(1.0, 0.95, coords.x)
        * smoothstep(0.0, 0.04, coords.y) * smoothstep(1.0, 0.96, coords.y);
    float k = guard * (1.0 - smoothstep(uLife - 3.0, uLife, t));
    return float4(outCol * k, a * k) * vc;
}

technique TechSplash
{
    pass SplashPass
    {
        PixelShader = compile ps_3_0 PSSplash();
    }
}
