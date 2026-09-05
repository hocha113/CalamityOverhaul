// ============================================================================
//CultistPhaseSky.fx 教徒五阶段沉浸天幕(第三版:仪式夜穹+极光帘+风)
//一版五套全屏噪声互叠读作"乱",二版收敛:共享深空底+双层视差星野(全相统一秩序),
//每相只保留一个主宰元素:星旋=顶部风暴云盖 星云=大尺度柔雾 星尘=流星细痕
//日耀=灼烧地平辉光 月明=死寂冷穹;uPhase 相邻线性交叉渐变;uStorm=星旋涌激
//三版(2026-09-05 用户令:各相加灰烬/极光/风暴):全相共用一套双层极光帘(同一几何,只换相色与强度,不添第二种秩序),
//云盖/雾场/极光帘的横向漂移改由 uWind(风位移积分)驱动,与场上风暴粒子同一股风
//uCam=相机视差锚(screenPosition/屏高),星野 3%/6.5%、云雾 5%~9% 层间差速给纵深
//预乘 AlphaBlend;s1=平铺 Perlin(消费端 Textures[1]+LinearWrap,实测值域 0.227~0.776 过 nrm)
//极角审计:全笛卡尔无 atan2;无动态分支,门控全走 step/smoothstep
// ============================================================================

sampler uImage0 : register(s0);   //全屏白像素(不采样)
sampler uNoise : register(s1);

float uTime;
float uIntensity;    //在场强度 0~1,满值近乎盖住原版背景
float uPhase;        //当前阶段(可带小数做换相渐变)
float uStorm;        //风暴涌激 0~1
float uAspect;       //屏宽/屏高
float2 uCam;         //相机位置/屏高,层间视差用
float uWind;         //风位移积分(屏高归一,带符号):云盖/雾场/极光帘随它漂
float uGust;         //阵风包络 0~1:极光帘褶皱抖、云盖提亮

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

//绑定 Perlin 实测值域 0.227~0.776 归一(阈值必须过这里,否则高分位层是死代码)
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

//星尘流星痕:斜置轨道上坠落的细光线;列哈希定横位/速度/长短,
//截面高斯细线(平方代 pow 防负底),头部加宽增亮成泪滴,tip 把 frac 断口收圆
float3 dustLayer(float2 base, float cells, float seedOfs, float gate, float fall, float skew) {
    float x = (base.x + base.y * skew) * cells;
    float id = floor(x);
    float fx = x - id;
    float h1 = nrm(noise(float2(id * 0.0371 + seedOfs, 0.517)));
    float h2 = nrm(noise(float2(id * 0.0593 + seedOfs, 5.113)));
    float sel = step(gate, nrm(noise(float2(id * 0.0731 + seedOfs, 2.713))));
    float dxr = (fx - (0.28 + h1 * 0.44)) * (10.0 + h2 * 5.0);
    float latT = exp(-dxr * dxr);          //尾:细截面
    float latH = exp(-dxr * dxr * 0.30);   //头:约1.8倍宽的柔光
    float ph = frac(base.y * (0.85 + h1 * 0.75) - uTime * fall * (0.6 + h1 * 0.8) + h2 * 9.0);
    float tip = smoothstep(1.0, 0.95, ph);
    float tw = 0.85 + 0.15 * sin(uTime * (2.6 + h1 * 3.5) + h2 * 17.0);
    float tail = pow(ph, 6.0) * tip * latT;
    float head = pow(ph, 24.0) * tip * latH;
    float3 c = float3(0.38, 0.68, 0.95) * tail * 0.55 + float3(0.80, 0.93, 1.05) * head * 1.2;
    return c * sel * (0.55 + 0.45 * h2) * tw;
}

//星野层:哈希格稀疏星点,各自明灭;gate 越高星越稀
float starLayer(float2 base, float cells, float seedOfs, float gate) {
    float2 g = base * cells;
    float2 id = floor(g);
    float2 f = g - id;
    float h1 = nrm(noise(id * 0.0293 + seedOfs));
    float h2 = nrm(noise(id * 0.0517 + seedOfs + 3.71));
    float sel = step(gate, nrm(noise(id * 0.0731 + seedOfs + 8.13)));
    float2 d = f - (float2(h1, h2) * 0.56 + 0.22);
    float dot_ = exp(-dot(d, d) * 150.0);
    float tw = 0.62 + 0.38 * sin(uTime * (0.8 + h1 * 2.6) + h2 * 21.0);
    return dot_ * sel * (0.35 + 0.65 * h2) * tw;
}

//极光帘:一条横贯天顶的波状下缘,光自下缘向上衰散成垂直射线(真极光下缘最亮最锐,向上淡成纱)
//x 用等比坐标+风位移+视差,y 用屏幕 uv;返回标量强度,着色在外
float auroraCurtain(float sx, float y, float seed, float baseY, float amp) {
    float x = sx * 0.42 + seed;
    //下缘波形:大摆(慢)+小褶(快,阵风时抖得更厉害)
    float wave = (nrm(noise(float2(x * 0.7 + uTime * 0.012, seed * 2.3))) - 0.5) * amp
               + (nrm(noise(float2(x * 2.4 - uTime * 0.02, seed * 4.1 + 1.7))) - 0.5) * (0.05 + 0.04 * uGust);
    float edgeY = baseY + wave;
    float d = edgeY - y;                                     //>0 在缘上方=帘体
    float curtain = smoothstep(-0.012, 0.006, d) * exp(-max(d, 0.0) * 9.0);
    //垂直射线:只随 x 变、沿 y 恒定=竖条纹;粗细两频
    float rays = 0.55 + 0.45 * nrm(noise(float2(x * 5.0 + uTime * 0.03, seed + 0.3)));
    rays *= 0.70 + 0.30 * nrm(noise(float2(x * 13.0 - uTime * 0.05, seed + 0.9)));
    //亮度沿帘缓变(慢):帘在呼吸,不在闪
    float breathe = 0.70 + 0.30 * nrm(noise(float2(x * 1.1 + uTime * 0.05, seed * 7.7)));
    return curtain * rays * breathe;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 c = uv - 0.5;
    c.x *= uAspect;
    float rc = length(c);
    //等比坐标:星点保持圆形,云雾不被屏幕拉扁
    float2 sq = float2(uv.x * uAspect, uv.y);

    //相邻阶段线性权重
    float w0 = saturate(1.0 - abs(uPhase - 0.0));
    float w1 = saturate(1.0 - abs(uPhase - 1.0));
    float w2 = saturate(1.0 - abs(uPhase - 2.0));
    float w3 = saturate(1.0 - abs(uPhase - 3.0));
    float w4 = saturate(1.0 - abs(uPhase - 4.0));

    //---- 共享深空底:相色纵向渐变+边缘沉暗(把视线交还给场心的星球与弹幕) ----
    float3 tint = float3(0.014, 0.038, 0.075) * w0
                + float3(0.045, 0.012, 0.055) * w1
                + float3(0.010, 0.028, 0.050) * w2
                + float3(0.070, 0.022, 0.008) * w3
                + float3(0.008, 0.016, 0.014) * w4;
    float horizon = pow(saturate(uv.y), 2.0);
    float vig = 1.0 - smoothstep(0.55, 1.05, rc) * 0.45;
    float3 col = tint * (0.7 + horizon * 0.9) * vig;

    //---- 星野双层:远层慢近层快,月明黯淡日耀被昼光洗淡 ----
    float star1 = starLayer(sq * 0.9 + uCam * 0.030 + float2(uTime * 0.0016, 0.0), 22.0, 0.0, 0.55);
    float star2 = starLayer(sq * 1.0 + uCam * 0.065 + float2(uTime * 0.0031, 0.0), 36.0, 4.43, 0.66);
    float starMute = 1.0 - w4 * 0.55 - w3 * 0.45 - w0 * uStorm * 0.5;
    float3 starCol = lerp(float3(0.72, 0.82, 0.95), float3(1.0, 0.95, 0.88), horizon);
    col += starCol * (star1 * 0.80 + star2 * 0.45) * starMute * vig;

    //---- 星旋:风暴云盖只住上方,顶光浮雕,下半天留干净深空;云随风走,阵风时顶光提亮 ----
    float spd = 1.0 + uStorm * 1.5;
    float2 cuv = float2(sq.x * 0.62 - uWind * 0.35, uv.y * 1.55) + float2(uTime * 0.010 * spd, 0.0) + uCam * 0.09;
    float cl = noise(cuv) * 0.62 + noise(cuv * 2.3 + 4.1) * 0.38;
    float clift = cl - noise(cuv + float2(0.0, 0.05));
    float capMask = 1.0 - smoothstep(0.12, 0.55, uv.y);
    float cloudBody = smoothstep(0.30, 0.80, nrm(cl));
    float3 vx = float3(0.055, 0.080, 0.105) * cloudBody * 1.15;
    vx += float3(0.24, 0.34, 0.42) * saturate(clift * 3.6) * (0.38 + 0.22 * uGust);
    vx *= capMask;
    //云盖遮蔽量:极光只从云隙里透出来
    float cloudCover = capMask * cloudBody;

    //---- 星云:两频大尺度柔雾,雾芯微光,无环无圈;雾随风缓漂 ----
    float2 nuv = sq * 0.55 + uCam * 0.05 + float2(uTime * 0.006 - uWind * 0.12, -uTime * 0.004);
    float fog1 = nrm(noise(nuv));
    float fog2 = nrm(noise(nuv * 1.9 + 7.3));
    float neb = smoothstep(0.42, 0.95, fog1 * 0.65 + fog2 * 0.35);
    float3 nb = lerp(float3(0.10, 0.020, 0.115), float3(0.28, 0.065, 0.28), fog2) * neb;
    nb += float3(0.85, 0.45, 0.80) * pow(neb, 3.0) * 0.16;

    //---- 星尘:双层视差流星细痕(远层慢淡近层快亮),垫一层薄冷雾 ----
    float3 sd = dustLayer(sq + uCam * 0.035, 30.0, 0.0, 0.72, 0.09, 0.045) * 0.5
              + dustLayer(sq + uCam * 0.080, 19.0, 6.29, 0.68, 0.17, 0.075);
    sd += float3(0.030, 0.075, 0.110) * fog2 * 0.8;

    //---- 日耀:地平被地面烈焰烤亮+升腾热斑,天顶仍是暗空;热斑随风偏 ----
    float2 huv = float2(sq.x * 0.8 - uWind * 0.20, uv.y * 1.4) + float2(uTime * 0.018, 0.0) + uCam * 0.075;
    float heat = nrm(noise(huv) * 0.6 + noise(huv * 2.1 + 3.3) * 0.4);
    float3 so = float3(0.55, 0.18, 0.03) * pow(saturate(uv.y), 2.6) * (0.7 + 0.6 * heat);
    so += float3(0.95, 0.55, 0.20) * pow(saturate(uv.y), 6.0) * 0.5;
    float rise = pow(nrm(noise(huv * 1.6 + float2(0.0, uTime * 0.05))), 4.0) * (1.0 - uv.y * 0.6);
    so += float3(0.85, 0.40, 0.10) * rise * 0.32;

    //---- 月明:死寂雾场缓漂(随风),缘上蚀青 ----
    float mist = nrm(noise(sq * 0.7 + uCam * 0.04 + uTime * float2(0.003, 0.005) - float2(uWind * 0.15, 0.0)));
    float3 mo = float3(0.05, 0.10, 0.09) * smoothstep(0.45, 0.95, mist) * 0.5;
    mo += float3(0.30, 0.62, 0.52) * pow(saturate(rc * 1.1), 3.5) * 0.10;

    //---- 极光帘(全相共用几何,各相换色换强度):双帘错高错速,下缘相亮色→上缘相暗色 ----
    //帘 x 随风漂(极光被高层风吹动是它的物理),再加轻视差;星旋相从云隙透出,日耀相压低成地平热幕
    float ax = sq.x + uWind * 0.55 + uCam.x * 0.02;
    float aur1 = auroraCurtain(ax, uv.y, 0.0, 0.30, 0.22);
    float aur2 = auroraCurtain(ax * 1.15 + 3.7, uv.y, 5.3, 0.21, 0.16) * 0.7;
    float3 aurLo = float3(0.22, 0.62, 0.66) * w0 + float3(0.72, 0.22, 0.70) * w1 + float3(0.40, 0.82, 0.90) * w2
                 + float3(0.95, 0.42, 0.10) * w3 + float3(0.28, 0.90, 0.64) * w4;
    float3 aurHi = float3(0.12, 0.22, 0.48) * w0 + float3(0.30, 0.06, 0.42) * w1 + float3(0.14, 0.30, 0.60) * w2
                 + float3(0.55, 0.12, 0.10) * w3 + float3(0.08, 0.30, 0.34) * w4;
    float aurAmt = 0.40 * w0 + 0.62 * w1 + 0.58 * w2 + 0.34 * w3 + 0.52 * w4;
    //下缘亮上缘暗:按帘内高度(缘上方距离)由亮色滑向暗色
    float aurD1 = saturate((0.30 - uv.y) * 4.0);
    float aurD2 = saturate((0.21 - uv.y) * 4.0);
    float3 aurora = lerp(aurLo, aurHi, aurD1) * aur1 + lerp(aurLo, aurHi, aurD2) * aur2;
    aurora *= aurAmt * (1.0 - w0 * cloudCover * 0.85);

    col += vx * w0 + nb * w1 + sd * w2 + so * w3 + mo * w4 + aurora;
    //涌激压暗与月明沉场作用在总量:星也跟着沉
    col *= 1.0 - w0 * uStorm * 0.22 - w4 * 0.22;

    //全覆盖:满强度时完全盖住原版背景
    float alpha = saturate(uIntensity * 0.97);
    return float4(col * alpha, alpha) * vertexColor;
}

technique TechPhaseSky
{
    pass PhaseSkyPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
