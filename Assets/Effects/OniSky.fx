// ============================================================================
//OniSky.fx 里世界天空：墨色天穹 + 苍白圆月 + 鸟居剪影视差列 + 墨雾带
//绘制于天空层（CustomSky 最底），世界调色随后统一量化，颜色只需给到近似墨阶
//s0=占位白图 s1=PerlinNoise；全部噪声输入为笛卡尔 UV
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;
float uUra;             //0~1 里世界强度，同时作总 alpha
float2 uScreenSize;     //像素
float uCamX;            //Main.screenPosition.X 像素，视差用
float uCamY;            //Main.screenPosition.Y 像素

static const float3 SKY_TOP = float3(0.020, 0.019, 0.030);
static const float3 SKY_HORIZON = float3(0.085, 0.075, 0.085);
static const float3 MOON_PALE = float3(0.86, 0.84, 0.78);
static const float3 MOON_RIM_RED = float3(0.55, 0.06, 0.07);
static const float3 MIST_INK = float3(0.13, 0.125, 0.15);
static const float3 TORII_FAR = float3(0.105, 0.10, 0.12);
static const float3 TORII_MID = float3(0.060, 0.056, 0.070);
static const float3 TORII_NEAR = float3(0.028, 0.026, 0.036);

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float fbm2(float2 uv) {
    return noiseTex(uv) * 0.65 + noiseTex(uv * 2.7 + 13.1) * 0.35;
}

float hash1(float n) {
    return frac(sin(n * 127.1) * 43758.5453);
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

//一列鸟居：世界 U 平铺 + 逐座抖动，返回覆盖度
float toriiRow(float2 uv, float parallax, float baseY, float baseScale, float density) {
    float worldU = uv.x + uCamX * parallax / uScreenSize.x;
    float cellF = worldU * density;
    float cellId = floor(cellF);
    float h = hash1(cellId);
    //逐座抖动：三成空位，尺寸与落点微变
    if (h < 0.30) {
        return 0.0;
    }
    float scale = baseScale * (0.85 + h * 0.35);
    float yJit = (hash1(cellId + 57.0) - 0.5) * 0.035;
    float xInCell = (frac(cellF) - 0.5) / density;

    float2 local;
    local.x = xInCell / scale;
    local.y = (baseY + yJit - uv.y) / scale;
    return toriiShape(local);
}

float4 PSSky(float2 coords : TEXCOORD0) : COLOR0 {
    if (uUra < 0.004) {
        return float4(0, 0, 0, 0);
    }
    float2 uv = coords;
    float aspect = uScreenSize.x / uScreenSize.y;

    //墨色天穹：垂直渐变 + 缓慢流动的大块墨晕
    float grad = smoothstep(0.0, 0.95, uv.y);
    float3 sky = lerp(SKY_TOP, SKY_HORIZON, grad);
    float wash = fbm2(uv * float2(1.4 * aspect, 1.4) + float2(uTime * 0.006, uCamY * 0.00001));
    sky *= 0.90 + wash * 0.22;

    //苍白圆月：轻视差，噪声斑驳，外缘一线血红
    float2 moonC = float2(0.70 - uCamX * 0.000012, 0.26);
    float2 dm = (uv - moonC) * float2(aspect, 1.0);
    float mr = length(dm);
    float moonR = 0.155;
    float moon = 1.0 - smoothstep(moonR - 0.004, moonR + 0.006, mr);
    float mottle = fbm2(dm * 6.5 + 3.7) * 0.16;
    float3 moonCol = MOON_PALE * (1.0 - mottle);
    //月晕
    float halo = exp(-pow((mr - moonR) / 0.10, 2.0)) * step(moonR, mr);
    float redRim = exp(-pow((mr - moonR - 0.006) / 0.014, 2.0)) * step(moonR, mr);

    float3 col = sky;
    col += MOON_PALE * halo * 0.10;
    col += MOON_RIM_RED * redRim * 0.55;
    col = lerp(col, moonCol, moon);

    //三列鸟居：远淡近黑，脚底沉入墨雾
    float torFar = toriiRow(uv, 0.045, 0.760, 0.16, 2.6);
    float torMid = toriiRow(uv, 0.100, 0.800, 0.26, 1.5);
    float torNear = toriiRow(uv, 0.190, 0.870, 0.45, 0.8);

    //雾对剪影的吞没：越靠下越浓
    float mistGrad = smoothstep(0.55, 0.95, uv.y);
    float mistN = fbm2(uv * float2(2.2 * aspect, 3.0) + float2(uTime * 0.014, 0.0));
    float mist = mistGrad * (0.55 + mistN * 0.45);

    col = lerp(col, TORII_FAR, torFar * saturate(1.0 - mist * 1.25));
    col = lerp(col, TORII_MID, torMid * saturate(1.0 - mist * 1.05));
    col = lerp(col, TORII_NEAR, torNear * saturate(1.0 - mist * 0.85));

    //墨雾带本体
    col = lerp(col, MIST_INK, mist * 0.55);

    float alpha = uUra * 0.97;
    return float4(col * alpha, alpha);
}

technique TechSky {
    pass P0 {
        PixelShader = compile ps_3_0 PSSky();
    }
}
