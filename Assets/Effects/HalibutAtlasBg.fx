// ============================================================================
// HalibutAtlasBg.fx 深渊图鉴的海域背景
// 一根纵向海水柱：随下潜深度(uDepth)从透光浅海过渡到漆黑渊底
// 视觉要素：深度渐变 + 顶部体积光柱(随深度衰减) + 表层焦散 +
//           海雪视差层(uScroll驱动, 密度随深度增加) + 上浮气泡 +
//           深渊躁动红光(uAgitation, 复苏比例驱动) + 暗角
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uDepth;
float uAgitation;
float uScroll;

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

//深度色带：海面 → 浅海 → 远洋 → 深海 → 渊底
static const float3 COL_SURFACE = float3(0.075, 0.290, 0.330);
static const float3 COL_SHALLOW = float3(0.040, 0.165, 0.215);
static const float3 COL_OCEAN   = float3(0.018, 0.075, 0.120);
static const float3 COL_DEEP    = float3(0.008, 0.026, 0.050);
static const float3 COL_ABYSS   = float3(0.002, 0.006, 0.016);
static const float3 COL_GLOW    = float3(0.300, 0.780, 0.980);
static const float3 COL_CAUSTIC = float3(0.620, 0.940, 1.000);
static const float3 COL_VIOLET  = float3(0.300, 0.180, 0.560);
static const float3 COL_DANGER  = float3(0.950, 0.230, 0.230);

//根据归一化的总深度取色
float3 DepthColor(float d) {
    d = saturate(d);
    float3 c;
    if (d < 0.25) {
        c = lerp(COL_SURFACE, COL_SHALLOW, d / 0.25);
    }
    else if (d < 0.5) {
        c = lerp(COL_SHALLOW, COL_OCEAN, (d - 0.25) / 0.25);
    }
    else if (d < 0.75) {
        c = lerp(COL_OCEAN, COL_DEEP, (d - 0.5) / 0.25);
    }
    else {
        c = lerp(COL_DEEP, COL_ABYSS, (d - 0.75) / 0.25);
    }
    return c;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 uv = coords;
    float t = uTime * 0.5;

    //当前像素的"绝对深度"：屏幕内位置 + 下潜进度
    float localDepth = saturate(uDepth + (uv.y - 0.5) * 0.22);

    //1 深度基调渐变
    float3 bg = DepthColor(localDepth);

    //2 低频水团流动
    float2 flowUV = float2(uv.x * 2.2 + t * 0.12, uv.y * 1.6 + uScroll * 0.0008 - t * 0.07);
    float flow = fbm3(flowUV);
    bg += DepthColor(saturate(localDepth - 0.15)) * (flow - 0.5) * 0.22;

    //3 顶部下沉体积光柱，随深度急剧衰减
    float lightFall = saturate(1.0 - uDepth * 1.35);
    if (lightFall > 0.002) {
        float beams = 0.0;
        [unroll]
        for (int b = 0; b < 3; b++) {
            float bf = (float)b;
            float speed = 0.06 + bf * 0.02;
            float baseX = 0.16 + bf * 0.34 + sin(t * speed + bf * 1.7) * 0.09;
            float dx = uv.x - baseX - uv.y * (0.05 + bf * 0.02);
            float width = 0.05 + 0.018 * sin(t * 0.6 + bf * 2.3);
            float beam = exp(-(dx * dx) / (width * width));
            float fall = 1.0 - smoothstep(0.0, 0.95, uv.y);
            float flick = 0.78 + 0.22 * sin(t * 1.5 + bf * 4.1 + uv.y * 6.0);
            beams += beam * fall * flick * (0.5 - bf * 0.08);
        }
        beams *= 0.55 + 0.45 * fbm3(float2(uv.x * 3.0 + t * 0.35, uv.y * 1.2));
        bg += COL_CAUSTIC * beams * 0.30 * lightFall;
        bg += COL_GLOW * beams * 0.10 * lightFall;
    }

    //4 表层焦散，只在接近海面时出现
    float causticBand = saturate(1.0 - uDepth * 2.6) * smoothstep(0.55, 0.0, uv.y);
    if (causticBand > 0.002) {
        float2 cu = float2(uv.x * 4.0, uv.y * 2.4 + uScroll * 0.001);
        cu.x += sin(t * 0.8 + uv.y * 5.0) * 0.2;
        cu.y -= t * 0.3;
        float n1 = valueNoise(cu * 1.7);
        float n2 = valueNoise(cu * 1.7 + float2(7.3, 2.1));
        float caustic = pow(saturate(1.0 - abs(n1 - n2) * 4.2), 3.0);
        bg += COL_CAUSTIC * caustic * causticBand * 0.20;
    }

    //5 海雪：两层视差，密度随深度上升
    float snowDensity = 0.55 + uDepth * 0.45;
    [unroll]
    for (int layer = 0; layer < 2; layer++) {
        float lf = (float)layer;
        float gridSize = 40.0 + lf * 26.0;
        float parallax = 0.55 + lf * 0.35;
        float2 sp = pixelPos + float2(0.0, uScroll * parallax);
        float2 g = floor(sp / gridSize);
        float s = hash21(g + lf * 17.7);
        float life = frac(s * 5.31 + t * (0.05 + s * 0.04));
        float2 p0 = (g + 0.5) * gridSize + (hash22(g + lf * 31.3) - 0.5) * (gridSize * 0.8);
        p0.y += life * (gridSize * 0.9);
        p0.x += sin(life * TAU + s * 9.0) * 3.0;
        float dPart = length(sp - p0);
        float size = 0.9 + s * 1.1;
        float core = (1.0 - smoothstep(0.0, size, dPart)) * sin(life * PI);
        core *= step(1.0 - 0.22 * snowDensity, s);
        bg += COL_CAUSTIC * core * (0.35 + lf * 0.1);
    }

    //6 上浮气泡，浅层更多
    float bubbleDensity = saturate(1.0 - uDepth * 0.8);
    if (bubbleDensity > 0.02) {
        float gridB = 64.0;
        float2 bp = pixelPos + float2(0.0, uScroll * 0.8);
        float2 gb = floor(bp / gridB);
        float sB = hash21(gb + 53.1);
        float lifeB = frac(sB * 7.13 + t * (0.10 + sB * 0.06));
        float2 pb = (gb + 0.5) * gridB + (hash22(gb + 91.7) - 0.5) * (gridB * 0.6);
        pb.y -= lifeB * (gridB * 1.6);
        pb.x += sin(lifeB * TAU * 2.0 + sB * 6.0) * 5.0;
        float dB = length(bp - pb);
        float rB = 1.4 + sB * 2.2;
        float ring = saturate(1.0 - abs(dB - rB) * 1.4);
        ring *= step(0.82, sB) * sin(lifeB * PI) * bubbleDensity;
        bg += COL_GLOW * ring * 0.30;
    }

    //7 渊底紫光与躁动红光
    float fromBottom = smoothstep(0.45, 1.0, uv.y);
    bg += COL_VIOLET * fromBottom * uDepth * 0.10;
    if (uAgitation > 0.01) {
        float unrest = uAgitation * (0.70 + 0.30 * sin(t * (1.6 + uAgitation * 6.0)));
        bg += COL_DANGER * fromBottom * unrest * 0.20;
        bg = lerp(bg, bg * float3(1.12, 0.86, 0.86), uAgitation * 0.35);
    }

    //8 暗角
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.62, 0.55), vig * float2(0.62, 0.55));
    bg *= saturate(1.0 - vigStr * 0.85) * 0.42 + 0.58;

    //9 细颗粒
    float dust = hash21(pixelPos + t * 22.0) * 0.04;
    bg *= 1.0 - dust * 0.5;

    float fa = uAlpha;
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass HalibutAtlasBgPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
