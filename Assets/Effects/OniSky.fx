// ============================================================================
//OniSky.fx 鬼域双世界天空，跨0深度切片单次绘制，覆盖所有原版背景层
//表世界：永远的逢魔黄昏，定住的夕阳 + 浮世绘平涂云霞 + 暖褐山野 + 飞鸟
//里世界：淡底浓墨，苍白雾空 + 苍白圆月血红月晕 + 墨色山脊 + 鸟居 + 红灯笼远光
//两世界共用同一套山脊几何（同一片山的表里两面），仅调色板不同
//里世界配色按 OniWorldGrade 量化墨阶落点设计：空0.62→淡 远山0.38→中 近山0.06→焦
//s0=占位白图 s1=PerlinNoise；全部噪声输入为笛卡尔 UV
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;
float uSkyAlpha;        //0~1 天空整体在场（开收域淡入淡出）
float uUraBlend;        //0=表 1=里，翻转期间快速过渡
float2 uScreenSize;     //像素
float uCamX;            //Main.screenPosition.X 像素，视差用
float uCamY;            //Main.screenPosition.Y 像素

//====== 表世界调色板：逢魔黄昏 ======
static const float3 OMO_SKY_TOP = float3(0.335, 0.245, 0.285);
static const float3 OMO_SKY_MID = float3(0.685, 0.475, 0.290);
static const float3 OMO_SKY_LOW = float3(0.940, 0.700, 0.370);
static const float3 OMO_SUN_CORE = float3(1.00, 0.86, 0.58);
static const float3 OMO_SUN_HAZE = float3(0.98, 0.62, 0.30);
static const float3 OMO_CLOUD = float3(0.475, 0.315, 0.290);
static const float3 OMO_CLOUD_EDGE = float3(1.00, 0.82, 0.52);
static const float3 OMO_RIDGE_FAR = float3(0.365, 0.265, 0.225);
static const float3 OMO_RIDGE_MID = float3(0.235, 0.160, 0.140);
static const float3 OMO_RIDGE_NEAR = float3(0.130, 0.085, 0.080);
static const float3 OMO_MIST = float3(0.88, 0.66, 0.42);
static const float3 OMO_BIRD = float3(0.10, 0.06, 0.07);

//====== 里世界调色板：淡底浓墨 ======
static const float3 URA_SKY_TOP = float3(0.470, 0.465, 0.455);
static const float3 URA_SKY_LOW = float3(0.660, 0.645, 0.610);
static const float3 URA_MOON = float3(0.900, 0.880, 0.830);
static const float3 URA_MOON_RIM = float3(0.60, 0.07, 0.08);
static const float3 URA_RIDGE_FAR = float3(0.380, 0.375, 0.385);
static const float3 URA_RIDGE_MID = float3(0.215, 0.210, 0.225);
static const float3 URA_RIDGE_NEAR = float3(0.060, 0.058, 0.070);
static const float3 URA_TORII = float3(0.048, 0.045, 0.058);
static const float3 URA_MIST = float3(0.725, 0.710, 0.680);
static const float3 URA_LANTERN = float3(0.780, 0.105, 0.080);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float fbm2(float2 uv) {
    return noiseTex(uv) * 0.65 + noiseTex(uv * 2.7 + 13.1) * 0.35;
}

float hash1(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
}

//山脊线高度：给定世界视差 U 与层参数，返回脊线 y（uv 空间，向下增大）
float ridgeY(float worldU, float seed, float freq, float amp, float baseY) {
    float h = fbm2(float2(worldU * freq + seed, seed * 0.73));
    //双频叠一点碎脊
    h = h * 0.8 + noiseTex(float2(worldU * freq * 3.1 + seed * 1.7, seed)) * 0.2;
    return baseY - (h - 0.5) * amp;
}

//鸟居剪影：局部空间 x∈[-0.7,0.7] y∈[0,1]（0=脚底 1=笠木顶），返回覆盖度
float toriiShape(float2 p) {
    if (abs(p.x) > 0.72 || p.y < 0.0 || p.y > 1.05) {
        return 0.0;
    }
    float aa = 0.012;
    float cov = 0.0;

    //柱：微内倾，脚底外张
    float lean = (1.0 - p.y) * 0.055;
    float legC = 0.335 + lean;
    float legW = 0.045 + (1.0 - p.y) * 0.012;
    float legs = (1.0 - smoothstep(legW - aa, legW + aa, abs(abs(p.x) - legC)))
               * step(p.y, 0.80);
    cov = max(cov, legs);

    //贯（中横梁）
    float nuki = (1.0 - smoothstep(0.030 - aa, 0.030 + aa, abs(p.y - 0.575)))
               * step(abs(p.x), 0.435);
    cov = max(cov, nuki);

    //岛木（次顶梁，直）
    float shimaki = (1.0 - smoothstep(0.026 - aa, 0.026 + aa, abs(p.y - 0.78)))
                  * step(abs(p.x), 0.50);
    cov = max(cov, shimaki);

    //笠木（顶梁，两端上翘的弧）
    float lift = pow(abs(p.x) / 0.60, 2.0) * 0.075;
    float kasagiY = 0.865 + lift;
    float kasagi = (1.0 - smoothstep(0.034 - aa, 0.034 + aa, abs(p.y - kasagiY)))
                 * step(abs(p.x), 0.60);
    cov = max(cov, kasagi);

    return cov;
}

//一列鸟居，立在指定山脊上：每座取所在格中心的脊高作地基
float toriiRow(float2 uv, float parallax, float scale, float density,
               float seed, float freq, float amp, float baseY) {
    float worldU = uv.x + uCamX * parallax / uScreenSize.x;
    float cellF = worldU * density;
    float cellId = floor(cellF);
    float h = hash1(cellId + seed);
    //四成空位
    if (h < 0.40) {
        return 0.0;
    }
    float s = scale * (0.85 + h * 0.30);
    //地基取格中心的脊线高度，鸟居坐进山脊
    float cellCenterU = (cellId + 0.5) / density;
    float footY = ridgeY(cellCenterU, seed, freq, amp, baseY) + 0.012;

    float2 local;
    local.x = (frac(cellF) - 0.5) / density / s;
    local.y = (footY - uv.y) / s;
    return toriiShape(local);
}

//飞鸟：45 秒一群，右往左掠过，缓慢振翅
float birdFlock(float2 uv, float aspect) {
    float cycle = frac(uTime / 45.0);
    if (cycle > 0.34) {
        return 0.0;
    }
    float t = cycle / 0.34;
    float flockX = lerp(1.18, -0.18, t);
    float flockY = 0.30 + sin(t * 6.28) * 0.02;

    float cov = 0.0;
    [unroll]
    for (int i = 0; i < 5; i++) {
        float fi = (float)i;
        float2 bp;
        bp.x = flockX + hash1(fi * 3.7) * 0.10 + fi * 0.028;
        bp.y = flockY + (hash1(fi * 9.1) - 0.5) * 0.05;
        float2 p = (uv - bp) * float2(aspect, 1.0) / 0.011;
        if (abs(p.x) > 1.0 || abs(p.y) > 1.0) {
            continue;
        }
        //展翅 V 形，振翅频率各自微差
        float flap = sin(uTime * 5.5 + fi * 1.9) * 0.55;
        float wing = abs(p.y - flap * (0.55 - abs(p.x)));
        cov = max(cov, (1.0 - smoothstep(0.10, 0.22, wing)) * step(abs(p.x), 0.62));
    }
    return cov;
}

//里世界远景红灯笼光点：慢慢上浮
float2 lanternDots(float2 uv, float aspect) {
    //返回 x=光点强度 y=光晕强度
    float acc = 0.0;
    float haze = 0.0;
    [unroll]
    for (int i = 0; i < 2; i++) {
        float fi = (float)i;
        float parallax = 0.06 + fi * 0.05;
        float worldU = uv.x + uCamX * parallax / uScreenSize.x;
        float density = 3.5 + fi;
        float cellF = worldU * density;
        float cellId = floor(cellF);
        float h = hash1(cellId + fi * 17.3);
        if (h < 0.55) {
            continue;
        }
        //上浮循环
        float rise = frac(uTime * 0.014 + h * 7.0);
        float y = 0.86 - rise * 0.42;
        float x = (frac(cellF) - 0.5) / density;
        float2 d = float2(x * aspect, uv.y - y);
        float r2 = dot(d, d);
        float dot_ = exp(-r2 / 0.000012);
        float glow = exp(-r2 / 0.00030);
        //出生与到顶淡出
        float fade = smoothstep(0.0, 0.12, rise) * (1.0 - smoothstep(0.75, 1.0, rise));
        acc += dot_ * fade;
        haze += glow * fade;
    }
    return float2(saturate(acc), saturate(haze));
}

float4 PSSky(float2 coords : TEXCOORD0) : COLOR0 {
    if (uSkyAlpha < 0.004) {
        return float4(0, 0, 0, 0);
    }
    float2 uv = coords;
    float aspect = uScreenSize.x / uScreenSize.y;
    float ura = saturate(uUraBlend);
    float omo = 1.0 - ura;

    //====== 天穹渐变 ======
    float grad = saturate(uv.y * 1.12);
    //表：三段黄昏渐变
    float3 omoSky = lerp(OMO_SKY_TOP, OMO_SKY_MID, smoothstep(0.10, 0.62, grad));
    omoSky = lerp(omoSky, OMO_SKY_LOW, smoothstep(0.55, 0.92, grad));
    //里：反向，雾在地平线处发亮
    float3 uraSky = lerp(URA_SKY_TOP, URA_SKY_LOW, smoothstep(0.15, 0.95, grad));
    float wash = fbm2(uv * float2(1.4 * aspect, 1.4) + float2(uTime * 0.006, uCamY * 0.00001));
    float3 col = lerp(omoSky, uraSky, ura) * (0.93 + wash * 0.14);

    //====== 表：定住的夕阳（永远的逢魔时刻，不随时间移动） ======
    if (omo > 0.003) {
        float2 sunC = float2(0.310 - uCamX * 0.000010, 0.560);
        float2 ds = (uv - sunC) * float2(aspect, 1.0);
        float sr = length(ds);
        float sun = 1.0 - smoothstep(0.105, 0.117, sr);
        float sunHaze = exp(-pow(max(sr - 0.10, 0.0) / 0.22, 1.4));
        col += OMO_SUN_HAZE * sunHaze * 0.55 * omo;
        col = lerp(col, OMO_SUN_CORE, sun * omo);

        //浮世绘平涂云霞：横长条带 + 淡金描边，飘得极慢
        float2 cuv = float2(uv.x + uCamX * 0.018 / uScreenSize.x + uTime * 0.0035, uv.y);
        float cField = fbm2(cuv * float2(1.9, 7.5));
        float yEnv = smoothstep(0.08, 0.22, uv.y) * (1.0 - smoothstep(0.46, 0.62, uv.y));
        float band = smoothstep(0.535, 0.575, cField) * yEnv;
        float edge = band * (1.0 - band) * 4.0;
        col = lerp(col, OMO_CLOUD, band * 0.80 * omo);
        col += OMO_CLOUD_EDGE * edge * 0.38 * omo;

        //飞鸟群
        float birds = birdFlock(uv, aspect);
        col = lerp(col, OMO_BIRD, birds * 0.85 * omo);
    }

    //====== 里：苍白圆月 + 血红月晕 ======
    if (ura > 0.003) {
        float2 moonC = float2(0.700 - uCamX * 0.000012, 0.250);
        float2 dm = (uv - moonC) * float2(aspect, 1.0);
        float mr = length(dm);
        float moonR = 0.150;
        float moon = 1.0 - smoothstep(moonR - 0.004, moonR + 0.006, mr);
        float mottle = fbm2(dm * 6.5 + 3.7) * 0.14;
        float halo = exp(-pow(max(mr - moonR, 0.0) / 0.11, 1.6));
        float redRim = exp(-pow((mr - moonR - 0.007) / 0.015, 2.0)) * step(moonR, mr);

        col += URA_MOON * halo * 0.16 * ura;
        col += URA_MOON_RIM * redRim * 0.60 * ura;
        col = lerp(col, URA_MOON * (1.0 - mottle), moon * ura);
    }

    //====== 山脊：表里共用几何，三层视差 ======
    //far
    float u1 = uv.x + uCamX * 0.045 / uScreenSize.x;
    float y1 = ridgeY(u1, 3.1, 1.10, 0.130, 0.640);
    float m1 = smoothstep(y1 - 0.004, y1 + 0.004, uv.y);
    //mid
    float u2 = uv.x + uCamX * 0.100 / uScreenSize.x;
    float y2 = ridgeY(u2, 7.7, 0.85, 0.170, 0.760);
    float m2 = smoothstep(y2 - 0.004, y2 + 0.004, uv.y);
    //near
    float u3 = uv.x + uCamX * 0.190 / uScreenSize.x;
    float y3 = ridgeY(u3, 12.3, 0.65, 0.200, 0.900);
    float m3 = smoothstep(y3 - 0.005, y3 + 0.005, uv.y);

    float3 ridgeFar = lerp(OMO_RIDGE_FAR, URA_RIDGE_FAR, ura);
    float3 ridgeMid = lerp(OMO_RIDGE_MID, URA_RIDGE_MID, ura);
    float3 ridgeNear = lerp(OMO_RIDGE_NEAR, URA_RIDGE_NEAR, ura);

    col = lerp(col, ridgeFar, m1);

    //====== 里：鸟居立在远/中山脊上（在 far 之后画，坐在 far 脊上） ======
    if (ura > 0.003) {
        float torFar = toriiRow(uv, 0.045, 0.085, 3.2, 3.1, 1.10, 0.130, 0.640);
        col = lerp(col, URA_TORII, torFar * 0.90 * ura);
    }

    col = lerp(col, ridgeMid, m2);

    if (ura > 0.003) {
        float torMid = toriiRow(uv, 0.100, 0.150, 1.8, 7.7, 0.85, 0.170, 0.760);
        col = lerp(col, URA_TORII, torMid * 0.94 * ura);

        //远景红灯笼光点，飘在中景之上
        float2 lan = lanternDots(uv, aspect);
        col += URA_LANTERN * (lan.y * 0.35 + lan.x * 1.2) * ura;
    }

    col = lerp(col, ridgeNear, m3);

    //====== 雾带：脊间横雾，表为暖金霞，里为苍白墨雾 ======
    float mistN = fbm2(uv * float2(2.2 * aspect, 3.0) + float2(uTime * 0.014, 0.0));
    float mistGrad = smoothstep(0.58, 0.98, uv.y);
    float mist = mistGrad * (0.45 + mistN * 0.55);
    float3 mistCol = lerp(OMO_MIST, URA_MIST, ura);
    col = lerp(col, mistCol, mist * lerp(0.28, 0.52, ura));

    float alpha = uSkyAlpha * 0.97;
    return float4(col * alpha, alpha);
}

technique TechSky {
    pass P0 {
        PixelShader = compile ps_3_0 PSSky();
    }
}
