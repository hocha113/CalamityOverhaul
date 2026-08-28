// ============================================================================
//AsuraBlessWheel.fx 往生轮盘底：黑暗虚空 + 缓旋墨轮 + 余烬微尘
//AlphaBlend 预乘 alpha；极坐标纹样只用整数倍 sin/cos，规避 ±π 接缝
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float2 uCenter;     //轮心（画布像素）
float uRadius;      //主环半径（画布像素）
float3 uAccent;     //主 accent（修罗紫红 / 毁灭苍银）
float3 uEmber;      //余烬色（描金 / 冷白）
float uBurning;     //燃焰占比 0..1，驱动微尘密度与轮的活性

#define PI 3.14159265
#define TAU 6.28318530

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
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
        p = p * 2.07 + float2(3.1, 7.4);
        a *= 0.5;
    }
    return v;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 rel = pixelPos - uCenter;
    float r = length(rel);
    float theta = atan2(rel.y, rel.x);
    float t = uTime;
    float breath = 0.88 + 0.12 * sin(t * 0.6);

    //1 虚空底：近黑 + 低频墨雾漂移（accent 极低浓度染色）
    float3 bg = float3(0.012, 0.009, 0.014);
    float ink = fbm3(coords * float2(2.6, 2.0) + float2(t * 0.03, -t * 0.017));
    bg += uAccent * (ink * ink) * 0.05;

    //2 轮外暗幕：轮心之外渐次沉入黑，聚焦轮面
    float focus = 1.0 - smoothstep(uRadius * 1.35, uRadius * 2.4, r);

    //3 主环：软辉带 + 沿环流动的墨纹（整数倍角频，无接缝）
    float ringW = uRadius * 0.085;
    float band = exp(-((r - uRadius) * (r - uRadius)) / (ringW * ringW));
    float flow = 0.62
        + 0.24 * sin(theta * 6.0 + t * 0.34)
        + 0.14 * sin(theta * 11.0 - t * 0.21 + 2.1);
    bg += uAccent * band * flow * 0.42 * breath;
    bg += uEmber * band * band * 0.10 * breath;

    //4 内外细环：刻度感的两圈窄线
    float thinW = 2.2;
    float rin = uRadius * 0.62;
    float rout = uRadius * 1.24;
    float innerRing = exp(-((r - rin) * (r - rin)) / (thinW * thinW));
    float outerRing = exp(-((r - rout) * (r - rout)) / (thinW * thinW));
    bg += uAccent * (innerRing * 0.30 + outerRing * 0.22);

    //5 辐条：内环与主环之间十二道缓旋幽辐
    float spokeZone = smoothstep(rin, rin + 8.0, r) * (1.0 - smoothstep(uRadius - ringW, uRadius, r));
    float spoke = pow(abs(sin(theta * 6.0 + t * 0.05)), 22.0);
    bg += uAccent * spoke * spokeZone * 0.16;

    //6 余烬微尘：两层视差上升，密度随燃焰占比
    float density = 0.35 + 0.65 * saturate(uBurning);
    for (int layer = 0; layer < 2; layer++) {
        float lf = (float)layer;
        float grid = 52.0 + lf * 34.0;
        float2 sp = pixelPos + float2(sin(t * (0.11 + lf * 0.05)) * 8.0, t * (14.0 + lf * 9.0));
        float2 g = floor(sp / grid);
        float s = hash21(g + lf * 19.3);
        float life = frac(s * 6.13 + t * (0.06 + s * 0.05));
        float2 p0 = (g + 0.5) * grid + (hash22(g + lf * 41.7) - 0.5) * grid * 0.7;
        float dPart = length(sp - p0);
        float size = 1.0 + s * 1.3;
        float core = (1.0 - smoothstep(0.0, size, dPart)) * sin(life * PI);
        core *= step(1.0 - 0.20 * density, s);
        bg += uEmber * core * (0.34 + lf * 0.12);
    }

    //7 轮心幽光：黯核外一圈呼吸辉
    float centerGlow = exp(-(r * r) / (uRadius * 0.42 * uRadius * 0.42));
    float darkCore = exp(-(r * r) / (uRadius * 0.16 * uRadius * 0.16));
    bg += uAccent * (centerGlow - darkCore * 0.9) * 0.12 * breath;

    //8 聚焦与细颗粒
    bg *= 0.30 + 0.70 * focus;
    float grain = hash21(pixelPos + t * 17.0) * 0.05;
    bg *= 1.0 - grain;

    float fa = uAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass AsuraBlessWheelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
