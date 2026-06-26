// ============================================================================
// AckBackdrop.fx  ED 致谢全屏氛围背景
// 近黑纵向渐变 + 低频星云漂移 + 多层视差光尘 + 缓动光带 + 暗角 + 颗粒
// 全部使用笛卡尔坐标，无 atan2/theta，故无极坐标接缝问题
// AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uProgress;   //阶段情绪 0入场 -> 1谢幕，驱动暖度与辉光
float3 uAccent;    //强调色，与 AckTheme.Accent 同步

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

float fbm(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * valueNoise(p);
        p = p * 2.03 + float2(3.1, 7.4);
        a *= 0.5;
    }
    return v;
}

//色板（与 AckTheme 对应）
static const float3 COL_VOID  = float3(0.016, 0.020, 0.031);
static const float3 COL_BASE  = float3(0.035, 0.039, 0.055);
static const float3 COL_PANEL = float3(0.059, 0.067, 0.090);
static const float3 COL_COOL  = float3(0.180, 0.420, 0.550);

//单层视差光尘：网格散布，缓慢上浮，沿生命周期明灭
float moteLayer(float2 px, float gridSize, float speed, float t) {
    float2 g = floor(px / gridSize);
    float s = hash21(g);
    if (s < 0.64) {
        return 0.0;
    }
    float life = frac(s * 5.31 + t * (speed * (0.6 + s * 0.5)));
    float2 cell = (g + 0.5) * gridSize + (hash22(g) - 0.5) * (gridSize * 0.7);
    cell.y -= life * (gridSize * 1.4);
    cell.x += sin(life * TAU + s * 9.0) * gridSize * 0.12;
    float d = length(px - cell);
    return (1.0 - smoothstep(0.0, 1.0 + s * 1.3, d)) * sin(life * PI);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 px = coords * uResolution;
    float t = uTime;

    //1 纵向渐变：顶近黑，底微抬
    float3 bg = lerp(COL_VOID, COL_BASE, pow(uv.y, 0.75));
    bg = lerp(bg, COL_PANEL, pow(uv.y, 3.0) * 0.6);

    //2 低频星云漂移
    float2 fuv = float2(uv.x * 2.2 + t * 0.012, uv.y * 1.6 - t * 0.02);
    float neb = fbm(fuv);
    float neb2 = fbm(fuv * 1.9 + 5.7);
    float3 nebCol = lerp(COL_COOL, uAccent * 0.5, 0.35 + uProgress * 0.4);
    bg += nebCol * (neb - 0.5) * 0.10;
    bg += uAccent * (neb2 - 0.5) * 0.05 * (0.4 + uProgress * 0.6);

    //3 多层视差光尘
    float motes = 0.0;
    motes += moteLayer(px, 150.0, 0.05, t) * 0.55;
    motes += moteLayer(px + 91.0, 95.0, 0.075, t) * 0.40;
    motes += moteLayer(px + 217.0, 60.0, 0.10, t) * 0.28;
    bg += lerp(float3(0.70, 0.80, 1.00), uAccent, 0.5) * motes;

    //4 缓动光带（水平往复的柔光），偏顶部更亮
    float sweepX = 0.5 + sin(t * 0.18) * 0.42;
    float sweep = exp(-pow((uv.x - sweepX) * 2.4, 2.0));
    float topBias = smoothstep(1.0, 0.1, uv.y);
    bg += uAccent * sweep * (0.05 + uProgress * 0.06) * (0.4 + topBias * 0.8);

    //5 暗角（笛卡尔径向）
    float2 d = uv - 0.5;
    float vig = dot(d * float2(1.05, 1.25), d * float2(1.05, 1.25));
    bg *= saturate(1.0 - vig * 1.35) * 0.72 + 0.28;

    //6 细颗粒
    bg += (hash21(px + frac(t) * 131.0) - 0.5) * 0.015;

    //阶段暖化：谢幕段整体微抬并偏暖
    bg = lerp(bg, bg * float3(1.06, 0.99, 0.92) + uAccent * 0.015, uProgress);

    float fa = uAlpha;
    return float4(max(bg, 0.0) * fa, fa) * vertexColor;
}

technique Technique1
{
    pass AckBackdropPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
