// ============================================================================
//KikasaInkSplash.fx 鬼伞墨滴命中水花:命中一瞬爆发,14 帧内崩解干净,不留任何滞留层
//TechSplash:冠状离散液指。撞击点为根,沿撞击法线展开一把不均匀的扇——
//           uFinger[7] 的角/长/根半宽/相位由 C# 决定,飞沫粒子从同一组指尖上断下来;
//           起手 EaseOutBack 过冲(快),之后指长按开方缓增(液体只会继续飞,不会缩回);
//           每指沿长按解析正弦颈缩(Plateau-Rayleigh 波长规则,珠是圆的,噪声只扰相位),
//           波谷切珠,断裂阈值随寿命与沿长抬升(越老越碎、指尖先碎);
//           根部随寿命断供(薄片先排空),断离段随 uGravQ 剪切下坠(指尖多、根少);
//           冠膜=指根之间被顶起的薄片,头三成寿命里按噪声阈值撕成洞再消失(指是从它上面撕出来的);
//           溅裙=命中一瞬被压扁摊开的极薄毛边片,只活前三成寿命;收场=崩解不淡出。
//           厚(叠合)处沉、单指薄处透成飞白,血芯只在根部头几帧,窄湿光只在主指一侧头几帧。
//           全部由 uAge(0~1,CPU life 驱动)驱动,无 uTime:暂停即冻结,多端一致。
//坐标全笛卡尔;直线算术+普通 tex2D,零流控(step/smoothstep/lerp 门乘),FNA3D 安全;
//绑定噪声实测值域 0.227~0.776,阈值一律过 nrm() 归一。
//预乘输出进 AlphaBlend 批;消费入口 KikasaRains/KikasaInkSplashFX.cs
// ============================================================================

float uAge;        //寿命相位 0~1
float uKe;         //撞击动能 0~1(溅裙尺度)
float uSkew;       //切向偏斜 -1~1:斜撞往前甩,溅裙与血芯随之偏
float uFingerN;    //液指数 4~7(整数值,多余槽位门乘归零)
float4 uFinger[7]; //x=角(rad,自法线,+x 侧为正) y=长(H 单位) z=根半宽(H 单位) w=相位
float2 uGravQ;     //世界重力在 quad 空间的单位方向(x=切向,y=法向;y 正=沿法线离面)
float uHScale;     //一个 H 的 v 跨度(H/quadH)
float uRootV;      //根所在 v
float uHalfWH;     //quad 半宽(H 单位)
float uSeed;
float3 uColBody;   //墨体
float3 uColDeep;   //墨缘/叠合沉色
float3 uColCore;   //血芯(根部头几帧)
float3 uColSheen;  //湿光(主指一侧窄带)

sampler uNoiseTex : register(s1);

//绑定噪声归一:实测值域 0.227~0.776,映到 0~1 后阈值才有效
float nrm(float n) { return saturate((n - 0.23) * 1.82); }

//指根沿切向的摊开量(H 单位):指不是从一个点射出,而是从摊开的薄片沿上立起
static const float RootSpread = 0.18;

//单指:p 为像素位置(H 单位,根为原点,y 沿法线),f=(角,长,根半宽,相位),w=该指权重(0/1 门)
//返回覆盖 0~1;thick 回报相对厚度供叠合沉色
float fingerMask(float2 p, float4 f, float w, float rise, float grow, float brk, float neckAmp,
    float rootCut, float fall, float eN, out float thick)
{
    float sa = sin(f.x);
    float ca = cos(f.x);
    float2 dir = float2(sa, ca);
    float2 nrm2 = float2(ca, -sa);
    float2 pl = p - float2(sa * RootSpread, 0.0);
    float len = f.y * (rise + grow);

    //坠落剪切:断离段(靠指尖)坠得多、根几乎不坠;沿长权重用未坠位置估,直线算术
    float sFrac = saturate(dot(pl, dir) / max(len, 1e-3));
    float2 pf = pl - uGravQ * (fall * sFrac);
    float s = dot(pf, dir);
    float t = dot(pf, nrm2);
    float u = saturate(s / max(len, 1e-3));

    //沿长收锥,指尖细
    float R = f.z * (1.0 - 0.72 * u);
    //解析正弦颈缩:波长约 4.4 倍根半宽,噪声只扰相位与幅度让珠链不机械
    float nb = nrm(tex2D(uNoiseTex, float2(f.w * 0.37 + uSeed, s * 0.55 + f.w)).r);
    float k = 6.2832 / max(f.z * 4.4, 0.02);
    float wave = 0.5 + 0.5 * sin(s * k + f.w * 6.0 + (nb - 0.5) * 2.2);
    //断裂阈值:指尖先碎(沿长抬升),波谷切断=一颗颗圆珠
    float brkL = brk + 0.35 * u;
    float bead = smoothstep(brkL - 0.16, brkL + 0.16, wave);
    float rad = R * (1.0 - neckAmp * (1.0 - wave)) * (0.85 + 0.3 * nb) * bead;
    //指尖外渐小滴串,超长三成归零;根部断供:rootCut 之下无物;s<0 无物
    rad *= 1.0 - smoothstep(len, len * 1.3, s);
    rad *= smoothstep(rootCut * len - 0.03, rootCut * len + 0.02, s);
    rad *= step(-0.02, s);

    float d = abs(t) + (eN - 0.5) * 0.02;
    float m = (1.0 - smoothstep(rad - 0.012, rad + 0.006, d)) * w;
    //半径归零处不得留一根发丝线
    m *= smoothstep(0.003, 0.012, rad);
    thick = m * saturate(rad / max(f.z, 1e-3));
    return m;
}

float4 PSSplash(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float xc = (coords.x - 0.5) * 2.0;
    float2 p = float2(xc * uHalfWH, (uRootV - coords.y) / max(uHScale, 0.004));

    //起手 EaseOutBack 过冲:前四分之一寿命窜到 1.08 再落回 1;之后开方缓增
    float tr = saturate(uAge / 0.25);
    float e = tr - 1.0;
    float rise = 1.0 + 2.3 * e * e * e + 1.3 * e * e;
    float grow = 0.22 * sqrt(saturate((uAge - 0.25) / 0.75));
    //越老越碎:断裂阈值与颈缩幅度随寿命抬升;根部随寿命断供;断离段随寿命坠落
    float brk = lerp(-0.2, 0.7, smoothstep(0.2, 1.0, uAge));
    float neckAmp = lerp(0.15, 0.7, smoothstep(0.1, 0.9, uAge));
    float rootCut = smoothstep(0.35, 0.8, uAge) * 0.85;
    float ef = max(uAge - 0.4, 0.0);
    float fall = 1.2 * ef * ef;

    //共享蚀边(中频、略毛)
    float eN = nrm(tex2D(uNoiseTex, p * 0.9 + uSeed * 2.1).g);

    float th0, th1, th2, th3, th4, th5, th6;
    float m0 = fingerMask(p, uFinger[0], step(0.5, uFingerN), rise, grow, brk, neckAmp, rootCut, fall, eN, th0);
    float m1 = fingerMask(p, uFinger[1], step(1.5, uFingerN), rise, grow, brk, neckAmp, rootCut, fall, eN, th1);
    float m2 = fingerMask(p, uFinger[2], step(2.5, uFingerN), rise, grow, brk, neckAmp, rootCut, fall, eN, th2);
    float m3 = fingerMask(p, uFinger[3], step(3.5, uFingerN), rise, grow, brk, neckAmp, rootCut, fall, eN, th3);
    float m4 = fingerMask(p, uFinger[4], step(4.5, uFingerN), rise, grow, brk, neckAmp, rootCut, fall, eN, th4);
    float m5 = fingerMask(p, uFinger[5], step(5.5, uFingerN), rise, grow, brk, neckAmp, rootCut, fall, eN, th5);
    float m6 = fingerMask(p, uFinger[6], step(6.5, uFingerN), rise, grow, brk, neckAmp, rootCut, fall, eN, th6);
    float body = max(max(max(m0, m1), max(m2, m3)), max(max(m4, m5), m6));
    float thick = saturate(th0 + th1 + th2 + th3 + th4 + th5 + th6);

    //冠膜:指根之间那层被顶起的薄片,头三成寿命里被撕成指——半径随起手涨,
    //撕裂阈值随寿命抬升:先撕出洞,再只剩指根一圈,最后没有;比指薄(飞白色),不是实心丘
    float sheetR = 0.26 * rise * (0.7 + 0.3 * uKe);
    float2 ps = float2((p.x - uSkew * 0.08) * 0.72, p.y - 0.02);
    float nSh = nrm(tex2D(uNoiseTex, ps * 2.3 + uSeed * 3.3).r);
    float sheetEdge = 1.0 - smoothstep(sheetR * (0.75 + 0.3 * (nSh - 0.5)), sheetR * (1.05 + 0.3 * (nSh - 0.5)), length(ps));
    //噪声高处膜厚活得久:阈值抬过噪声即撕穿,抬到 1.15 时整片没有
    float tearLvl = lerp(-0.2, 1.15, smoothstep(0.04, 0.32, uAge));
    float nTear = nrm(tex2D(uNoiseTex, ps * 3.7 + uSeed * 1.3 + 0.41).g);
    float sheet = sheetEdge * smoothstep(tearLvl - 0.15, tearLvl + 0.15, nTear)
        * step(-0.02, p.y) * (1.0 - body);

    //溅裙:命中一瞬被压扁摊开的极薄透镜片(中厚端薄),EaseOut 向两侧摊、偏向偏斜侧,
    //低频噪声把片撕成几段(不是颗粒噪点),阈值随寿命抬升越撕越碎,只活前三成寿命
    float skT = saturate(uAge / 0.2);
    float skE = 1.0 - (1.0 - skT) * (1.0 - skT);
    float skirtW = lerp(0.25, 0.8, skE) * uHalfWH;
    float skirtH = (0.045 + 0.045 * uKe) * (1.0 - 0.45 * skT);
    float xs = p.x - uSkew * 0.25 * skirtW;
    float xn = saturate(abs(xs) / max(skirtW, 1e-3));
    float lensH = skirtH * (1.0 - xn * xn);
    float nSk = nrm(tex2D(uNoiseTex, float2(p.x * 1.4 + uSeed * 4.3, p.y * 2.6 + uSeed * 1.9)).b);
    float skirtY = 1.0 - smoothstep(lensH * 0.7, lensH * (1.0 + 0.3 * nSk) + 0.004, abs(p.y - skirtH * 0.25));
    float skirtTearLvl = lerp(0.3, 0.75, smoothstep(0.05, 0.3, uAge));
    float skirtTear = smoothstep(skirtTearLvl - 0.12, skirtTearLvl + 0.12, nSk);
    float skirtLife = smoothstep(0.0, 0.03, uAge) * (1.0 - smoothstep(0.15, 0.3, uAge));
    float skirt = skirtY * skirtTear * skirtLife * step(xn, 0.999) * (1.0 - body) * (1.0 - sheet);

    //体色:叠合厚处沉,单指薄处透成飞白;体缘一线暗轮廓;血芯只在根部头几帧且只在厚处
    float3 wash = lerp(uColBody, uColSheen, 0.16);
    float3 col = lerp(uColBody, uColDeep, smoothstep(0.35, 1.0, thick) * 0.7);
    col = lerp(wash, col, smoothstep(0.05, 0.4, thick));
    float outline = smoothstep(0.1, 0.45, body) * (1.0 - smoothstep(0.45, 0.9, body));
    col = lerp(col, uColDeep, outline * 0.6);
    float coreZone = (1.0 - smoothstep(0.04, 0.17, length(p - float2(uSkew * 0.06, 0.05))))
        * (1.0 - smoothstep(0.08, 0.22, uAge));
    col = lerp(col, uColCore, coreZone * 0.25 * smoothstep(0.3, 0.9, thick));

    //主指(槽 0)一侧的窄湿光,头几帧、只在指的下半段
    float sa0 = sin(uFinger[0].x);
    float ca0 = cos(uFinger[0].x);
    float2 p0 = p - float2(sa0 * RootSpread, 0.0);
    float s0 = dot(p0, float2(sa0, ca0));
    float t0 = dot(p0, float2(ca0, -sa0));
    float len0 = max(uFinger[0].y, 1e-3);
    float R0 = uFinger[0].z * (1.0 - 0.72 * saturate(s0 / len0));
    float bx = (t0 - R0 * 0.5) / max(R0 * 0.12, 0.003);
    float sheen = exp2(-bx * bx * 1.5) * m0 * (1.0 - smoothstep(0.2, 0.35, uAge))
        * smoothstep(0.05, 0.3, s0) * (1.0 - smoothstep(len0 * 0.45, len0 * 0.65, s0));

    //预乘合成:指体近实心,冠膜与溅裙只补体外;冠膜薄=飞白色
    float aBody = body * (0.86 + 0.12 * thick);
    float aSheet = sheet * 0.62;
    float aSkirt = skirt * 0.7;
    float a = saturate(aBody + aSheet + aSkirt);
    float3 outCol = col * aBody
                  + lerp(wash, uColBody, 0.55) * aSheet
                  + lerp(uColBody, uColDeep, 0.4) * aSkirt;
    outCol += uColSheen * sheen * 0.22;

    //画布护栏:左右/上下缘归零;寿命末一成清残(内容此前已崩解归零)
    float guard = smoothstep(1.0, 0.88, abs(xc))
        * smoothstep(0.0, 0.04, coords.y) * smoothstep(1.0, 0.96, coords.y);
    float k = guard * (1.0 - smoothstep(0.9, 1.0, uAge));
    return float4(outCol * k, a * k) * vc;
}

technique TechSplash
{
    pass SplashPass
    {
        PixelShader = compile ps_3_0 PSSplash();
    }
}
