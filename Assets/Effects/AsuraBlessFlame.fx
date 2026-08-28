// ============================================================================
//AsuraBlessFlame.fx 魂焰：珠位与引魂灯共用的程序化火苗
//材质=魂焰：上舔摆动 / 明暗颤闪 / 余烬热核冷缘。AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uSeed;        //每盏灯的相位种子
float3 uAccent;     //冷缘色
float3 uEmber;      //热核色
float uLit;         //点燃进度 0..1（熄灭态归零）
float uLean;        //受风倾斜 -1..1，焰尖权重（悬停气流/解锁涌动用，0=无风）

#define PI 3.14159265

float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    return frac(p * (p + p));
}

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

float fbm2(float2 p) {
    float v = 0.0;
    float a = 0.55;
    for (int i = 0; i < 2; i++) {
        v += a * valueNoise(p);
        p = p * 2.13 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //画布坐标 → 焰域：x 对称，y 向上为负；焰根在 y=+0.42
    float2 p = coords * 2.0 - 1.0;
    float t = uTime;
    float seed = uSeed * 13.7;
    float lit = saturate(uLit);
    if (lit <= 0.001) {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    //焰高坐标：根在 p.y=+0.5，尖端随点燃进度伸长；h 0根→1尖
    float height = lerp(0.30, 1.0, lit);
    float h = saturate((0.5 - p.y) / (1.05 * height));

    //上舔摆动：越靠焰尖摆幅越大，fbm 上滚
    float sway = (fbm2(float2(h * 2.6 + t * 2.8, seed)) - 0.5) * 0.9;
    p.x += sway * h * h * 0.42;
    p.x += sin(t * 3.1 + seed) * 0.06 * h;
    //受风倾斜：焰尖权重的整体偏移，根部钉在灯芯上
    p.x += uLean * h * h * 0.5;

    //焰形宽度谱：三成高最宽，向尖端收拢归零，根部微收
    float width = 0.34 * pow(saturate(1.0 - h), 0.85) * smoothstep(-0.06, 0.24, h);
    width *= 0.9 + 0.1 * sin(t * 5.2 + seed * 4.0 + h * 6.0);

    //锐利焰缘 + 热核（核只在下半段）
    float d = abs(p.x) - width;
    float body = 1.0 - smoothstep(0.0, 0.085, d);
    body *= step(p.y, 0.52) * step(h, 0.999);
    float core = 1.0 - smoothstep(0.0, 0.05, abs(p.x) - width * 0.5);
    core *= body * smoothstep(0.95, 0.30, h) * smoothstep(0.02, 0.14, h);

    //明暗颤闪：双频拍动
    float flicker = 0.84 + 0.16 * sin(t * 7.3 + seed * 3.0) * sin(t * 12.9 + seed * 7.1);

    //焰内游丝：细噪声亮纹上升
    float wisp = fbm2(float2(p.x * 5.0 + seed, h * 3.2 + t * 3.4));
    wisp = smoothstep(0.55, 0.9, wisp) * body;

    //根部一点幽蓝反差，压住纯暖色的塑料感
    float rootCool = smoothstep(0.16, 0.0, h) * body;

    float3 col = uEmber * core * 1.55
        + uAccent * saturate(body - core) * 0.9
        + uEmber * wisp * 0.45
        + uAccent * rootCool * 0.5;
    col *= flicker;

    float alpha = saturate(body * (0.5 + 0.5 * core)) * uAlpha * lit;
    return float4(col * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass AsuraBlessFlamePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
