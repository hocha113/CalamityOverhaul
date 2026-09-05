// ============================================================================
//ShenyoMenuGhost.fx 鬼湖立影：沈幽立绘的雨水幽灵态（主菜单专用）
//TechGhost：立绘压成湿墨黑水剪影，体内竖向径流常挂（身体由落雨构成），
//          月照湿体缘光（沿uMoonDir光向双尺度边检：亮芯+软晕+体内湿光回卷，
//          径流噪声碎化成流动湿条、头肩吃光下身沉暗——不是等宽描边）
//          + 微幅横向蠕动 + 双目冷青微芒；
//          uForm 复刻黑雨汇聚入场：自上而下灌满，前沿水膜挂亮，
//          未成形区先有穿过轮廓的残雨丝；
//          uWaterV 水线：以下没入湖中——水下段折射压扁、随深衰减入浊水、缘光熄灭，
//          水线处一线湿膜；水下段与倒影都按足下涟漪场的坡度折射扭动（与 Lake 同一套波）
//          uReflect 切倒影态：波纹加剧、随离水线渐深渐散
//色板承 ShenyoRainForm：近黑浊体/湿墨冷青/溺月惨白，禁暖
//s0=立绘（批次主纹理） s1=PerlinNoise
//绑定噪声实测值域 0.227~0.776，高阈值一律先过 nrm 归一
//直线算术无动态分支；预乘输出进 AlphaBlend
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;      //秒
float uForm;      //0-1 汇聚成形进度
float uClarity;   //0-0.35 澄出本色的比例（近影略高）
float uHaze;      //0-1 大气透视：向潮雾色靠拢（远影高）
float uReflect;   //0/1 倒影态
float uAlpha;     //整体不透明度
float uWobble;    //蠕动幅度倍率（近影略大）
float uSeed;      //逐影噪声错相
float2 uTexel;    //立绘纹理texel尺寸
float2 uEyeUv;    //双目中心（立绘uv）
float uEyeSep;    //目距半宽（uv）
float uEyeGlow;   //0-1 目芒强度（C#驱动呼吸与眨灭）
float2 uMoonDir;  //指向溺月的纹理空间单位光向（翻面/倒影由C#换算）
float uBlur;      //距离模糊半径（texel数，C#按屏幕像素÷缩放折算）；远影糊成雾形仍留轮廓
float uWaterV;    //水线立绘 v：以下没入湖中（0=无水，整张可见）；倒影按源矩形只画 0~uWaterV
float4 uSpriteRect; //本精灵在屏幕 uv 中的目标矩形 xy=左上 zw=宽高（涟漪场按屏幕位置取样）
float2 uVMap;     //立绘 v→矩形内纵向占比 t = p.y*x + y：本体 (1,0)，倒影 (-1/uWaterV,1)
float uFlipH;     //0/1 横向翻面
float2 uScreenSize; //像素（求 aspect）
float uHorizon;   //水线 y（uv）——涟漪透视椭圆比率
float4 uFeet[8];  //立影水线接触点（与 ShenyoMenuLake.fx 同一份数据）

//====== 湿墨色板（承 ShenyoRainForm）======
static const float3 MURK = float3(0.055, 0.071, 0.082);   //黑水浊体
static const float3 STREAK = float3(0.533, 0.792, 0.847); //径流湿墨冷青
static const float3 EDGE = float3(0.769, 0.839, 0.855);   //溺月惨白水膜
static const float3 HAZE = float3(0.170, 0.202, 0.212);   //潮雾（大气透视目标色）
static const float3 EYE = float3(0.620, 0.870, 0.920);    //目芒冷青
static const float3 LAKE_MURK = float3(0.030, 0.042, 0.052); //水下浊水（承 Lake 近处深水）

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//绑定噪声实测值域 0.227~0.776
float nrm(float v) {
    return saturate((v - 0.227) / 0.549);
}

//距离模糊：中心加权9抽头，远影糊成雾形但轮廓仍在
float4 sampleBlur(float2 p) {
    float2 r = uTexel * uBlur;
    float4 c = tex2D(uImage0, p) * 0.28;
    float2 d = r * 0.707;
    c += tex2D(uImage0, p + float2(d.x, d.y)) * 0.13;
    c += tex2D(uImage0, p + float2(-d.x, d.y)) * 0.13;
    c += tex2D(uImage0, p + float2(d.x, -d.y)) * 0.13;
    c += tex2D(uImage0, p + float2(-d.x, -d.y)) * 0.13;
    float2 a = r * 0.55;
    c += tex2D(uImage0, p + float2(a.x, 0.0)) * 0.05;
    c += tex2D(uImage0, p + float2(-a.x, 0.0)) * 0.05;
    c += tex2D(uImage0, p + float2(0.0, a.y)) * 0.05;
    c += tex2D(uImage0, p + float2(0.0, -a.y)) * 0.05;
    return c;
}

//====== 足下涟漪坡度（与 ShenyoMenuLake.fx 的 feetRipple 同源，改动须两处同步；此处只要 ∂h）======
//结构同 Lake：两拍波包并成 float2 向量算、相位种子取运行期值、颤纹只给 uFeet[4..7]（fx_2_0 常量预算）
float ripplePulsesSlope(float dist, float rw, float seed) {
    float2 ph = frac(uTime * 0.30 + seed + float2(0.0, 0.5));
    float2 rk = rw * (0.03 + 0.97 * ph);
    float2 sg = rw * (0.11 + 0.06 * ph);
    float2 u = (dist - rk) / sg;
    float2 env = exp(-u * u) * pow(1.0 - ph, 1.5);
    float2 s = sin(u * 2.6);
    float2 c = cos(u * 2.6);
    return dot((2.6 * c - 2.0 * u * s) * env / sg, 1.0);
}

float rippleTremorSlope(float dist, float rw, float seed) {
    float kk = 6.2831853 / (rw * 0.13);
    float L = rw * 0.24;
    float att = exp(-dist / L);
    float phs = dist * kk - uTime * 7.0 + seed;
    return (kk * cos(phs) * att - sin(phs) * att / L) * 0.07;
}

//单影几何：xy=∇dist（uv 空间） z=dist w=rw
float4 rippleGeo(float2 uv, float aspect, float4 f) {
    float dl = saturate((f.y - uHorizon) / max(1.0 - uHorizon, 0.001));
    float yr = lerp(4.6, 2.6, dl);
    float2 dv = (uv - f.xy) * float2(aspect, yr);
    float dist = max(length(dv), 1e-4);
    return float4(dv * float2(aspect, yr) / dist, dist, max(f.w, 0.001));
}

float2 feetRippleGrad(float2 uv, float aspect) {
    float2 acc = 0.0;
    [unroll]
    for (int i = 0; i < 4; i++) {
        float4 f = uFeet[i];
        float4 g = rippleGeo(uv, aspect, f);
        float dh = ripplePulsesSlope(g.z, g.w, f.y * 37.0 + f.w * 91.0);
        acc += dh * g.xy * g.w * 0.25 * f.z;
    }
    [unroll]
    for (int j = 4; j < 8; j++) {
        float4 f = uFeet[j];
        float4 g = rippleGeo(uv, aspect, f);
        float seed = f.y * 37.0 + f.w * 91.0;
        float dh = ripplePulsesSlope(g.z, g.w, seed) + rippleTremorSlope(g.z, g.w, seed);
        acc += dh * g.xy * g.w * 0.25 * f.z;
    }
    return acc;
}

float4 PSGhost(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = coords;

    //====== 水线：uWaterV 以下为水下段（0=无水）；倒影整张都在水面之下 ======
    float waterV = lerp(1.0, uWaterV, step(0.01, uWaterV));
    float dvw = p.y - waterV;
    float under = (1.0 - uReflect) * saturate(dvw * 60.0);

    //====== 涟漪折射：按屏幕位置取足下涟漪坡度，倒影与水下段随波扭动 ======
    float aspect = uScreenSize.x / max(uScreenSize.y, 1.0);
    float2 suv = uSpriteRect.xy
        + float2(lerp(p.x, 1.0 - p.x, uFlipH), p.y * uVMap.x + uVMap.y) * uSpriteRect.zw;
    float rectOk = step(1e-4, uSpriteRect.z) * step(1e-4, uSpriteRect.w) * step(0.5, abs(uVMap.x));
    float2 dsp = feetRippleGrad(suv, aspect) * 0.0011 * (uReflect + under * 1.3) * rectOk;
    float2 dp = dsp / max(uSpriteRect.zw, 1e-4);
    dp.x *= 1.0 - 2.0 * uFlipH;
    dp.y /= lerp(1.0, uVMap.x, rectOk);
    p += dp;
    //倒影只映水面以上的身体：扭动不许把水下的靴子采进来
    p.y = lerp(p.y, min(p.y, waterV - 0.002), uReflect);

    //水下段折射压扁：斜视水面时水下的身体显得更短更浅
    p.y = lerp(p.y, waterV + dvw * 1.35, under);

    //====== 横向蠕动：形体是攒起来的雨水，永远不完全安分；倒影与水下段更晃 ======
    float loose = 0.35 + uReflect * 1.15 + under * 1.40;
    float drift = noiseTex(float2(p.y * 0.9 + uSeed, uSeed * 0.7 + uTime * 0.05));
    p.x += sin(p.y * 17.0 + uTime * 2.6 + drift * 6.0) * 0.0055 * uWobble * loose;

    float4 portrait = sampleBlur(p);

    //====== 汇聚前沿：自上而下灌满，前沿噪声撕口 ======
    float sweepT = smoothstep(0.04, 0.96, uForm);
    float sweepY = lerp(-0.16, 1.16, sweepT);
    float jitter = (noiseTex(float2(p.x * 5.2 + uSeed, uSeed * 1.3)) - 0.5) * 0.16 * (1.0 - sweepT);
    float yy = p.y + jitter;
    float formed = 1.0 - smoothstep(sweepY - 0.03, sweepY + 0.06, yy);

    //未成形区的残雨：雨丝先穿过轮廓，预告形体将至
    float preRain = smoothstep(0.60, 0.88, nrm(noiseTex(
        float2(p.x * 7.5 + uSeed * 3.1, p.y * 0.9 - uTime * 0.55))));
    float ghostRain = preRain * (1.0 - formed) * step(0.01, uForm) * 0.45;

    //====== 黑水体：浊色蠕动明暗 + 体内竖向径流常挂 ======
    float na = noiseTex(p * float2(2.4, 3.6) + float2(uTime * 0.05 + uSeed, -uTime * 0.04));
    float rivulet = noiseTex(float2(p.x * 9.0 + uSeed * 7.7, p.y * 0.85 - uTime * 0.42));
    float3 body = MURK * (0.60 + 0.40 * na);
    body += STREAK * smoothstep(0.62, 0.92, nrm(rivulet)) * (0.15 + 0.16 * uReflect);

    //一点本色：近影微微澄出立绘原色
    body = lerp(body, portrait.rgb, uClarity * formed);
    //大气透视：远影整体向潮雾靠拢、对比坍塌
    body = lerp(body, HAZE, uHaze);
    //倒影泛一层月气惨白（随径流明暗起伏），否则黑水上读不出剪影
    body = lerp(body, HAZE * 1.40, (0.34 + 0.34 * nrm(rivulet)) * uReflect);
    //水下段：浊水吞色，越深越只剩湖水本色
    float subVis = exp(-max(dvw, 0.0) * 46.0);
    body = lerp(body, LAKE_MURK, under * (0.55 + 0.45 * (1.0 - subVis)));

    //====== 月照湿体缘光：沿光向双尺度边检——亮芯细线+软晕渐层+体内湿光回卷；
    //径流噪声碎化成顺流湿条（不是等宽白描边），头肩吃光下身沉暗；重模糊的远影不挂锐边 ======
    float blurFade = saturate(uBlur / 14.0);
    float2 litStep = uMoonDir * uTexel;
    float aLit2 = tex2D(uImage0, p + litStep * 2.0).a;
    float aLit6 = tex2D(uImage0, p + litStep * 6.0).a;
    float aLit12 = tex2D(uImage0, p + litStep * 12.0).a;
    //三段带：0-2texel亮芯 / 2-6软晕 / 6-12体内回卷（都只挂在朝月一侧的轮廓）
    float core = saturate(portrait.a - aLit2);
    float halo = saturate(saturate(portrait.a - aLit6) - core);
    float wrap = saturate(saturate(portrait.a - aLit12) - saturate(portrait.a - aLit6));
    //径流碎化：缘光是落在流水上的月光，条纹顺雨下行、忽明忽暗
    float litN = nrm(noiseTex(float2(p.x * 11.0 + uSeed * 5.3, p.y * 1.1 - uTime * 0.50)));
    float breakup = 0.45 + 0.65 * smoothstep(0.22, 0.80, litN);
    //纵向包络：月光自上来，头肩亮、下身沉
    float vEnv = lerp(1.0, 0.30, smoothstep(0.06, 0.85, p.y));
    float rimPulse = 0.78 + 0.22 * sin(uTime * 2.1 + uSeed * 9.0);
    //水下没有月光缘：坡面折射把它揉散了
    float rimAmp = rimPulse * formed * (1.0 - blurFade * 0.85) * vEnv * (1.0 - under);
    float3 rimGlow = EDGE * core * breakup * 1.55 * rimAmp;
    rimGlow += lerp(EDGE, STREAK, 0.45) * halo * (0.26 + 0.46 * litN) * rimAmp;
    //湿光回卷：贴亮缘的体侧顺着径流泛冷光，光"包"上湿身体而非只描边
    rimGlow += STREAK * wrap * smoothstep(0.55, 0.92, nrm(rivulet)) * 0.52 * rimAmp;

    //汇聚前沿水膜：一线惨白挂在灌注线上，定形后蒸干
    float frontGate = 1.0 - smoothstep(0.90, 1.0, uForm);
    float frontBand = exp(-abs(yy - sweepY) * 26.0) * frontGate * step(0.01, uForm);
    rimGlow += EDGE * frontBand * 0.50;

    //水线湿膜：水面贴着身体爬起的一线弯月面，随涟漪明暗起伏；只在本体、只在水线那一行
    float menisN = 0.70 + 0.30 * sin(p.x * 40.0 + uTime * 3.0 + uSeed);
    float menis = exp(-abs(dvw) * 150.0) * (1.0 - uReflect) * step(0.01, uWaterV) * formed * menisN;
    rimGlow += lerp(EDGE, STREAK, 0.35) * menis * 0.62;

    //====== 双目冷青微芒：成形后才睁眼 ======
    float2 pxScale = float2(1.0 / max(uTexel.x, 0.0001), 1.0 / max(uTexel.y, 0.0001));
    float2 e1 = (p - (uEyeUv - float2(uEyeSep, 0.0))) * pxScale / 5.0;
    float2 e2 = (p - (uEyeUv + float2(uEyeSep, 0.0))) * pxScale / 5.0;
    float eyes = exp(-dot(e1, e1)) + exp(-dot(e2, e2));
    //晕圈刻意收小压弱：目芒是两粒冷点，不是糊脸的光团
    float2 h1 = e1 * 0.30;
    float2 h2 = e2 * 0.30;
    float eyeHalo = exp(-dot(h1, h1)) + exp(-dot(h2, h2));
    float eyeOn = uEyeGlow * smoothstep(0.80, 1.0, formed);
    float3 eyeGlow = EYE * (eyes * 0.72 + eyeHalo * 0.10) * eyeOn;

    //====== 倒影态：离水线越深越散越淡（翻面绘制下 v=waterV 在水线处），
    //且被水波切成断续横条——这是"倒影浮在水上"的关键读法 ======
    float reflFade = lerp(1.0, lerp(0.10, 0.92, saturate(p.y / waterV)), uReflect);
    float sliceN = nrm(noiseTex(float2(p.x * 1.2 + uSeed, p.y * 22.0 + uTime * 0.40)));
    float slice = 0.40 + 0.60 * smoothstep(0.30, 0.72, sliceN);
    reflFade *= lerp(1.0, slice, uReflect);

    //====== 预乘合成 ======
    //水下段随深衰减：紧贴水线还看得见半截浊影，几个百分点以下就只剩湖水
    float aBody = portrait.a * formed * lerp(1.0, subVis * 0.62, under);
    float mul = uAlpha * vertexColor.a * reflFade;
    float3 rgb = body * aBody + (rimGlow + eyeGlow) * portrait.a
        + STREAK * ghostRain * (1.0 - under) * portrait.a * 0.5;
    float alpha = saturate(aBody
        + ((core * breakup * 0.26 + halo * 0.10) * (1.0 - under) + frontBand * 0.30
            + menis * 0.35 + ghostRain * 0.45 * (1.0 - under)) * portrait.a);

    return float4(rgb * mul * vertexColor.rgb, alpha * mul);
}

technique TechGhost {
    pass P0 {
        PixelShader = compile ps_3_0 PSGhost();
    }
}
