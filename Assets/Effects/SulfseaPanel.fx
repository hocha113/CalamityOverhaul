// ============================================================================
//SulfseaPanel.fx 硫磺海风格对话框背景
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;      //面板内缩边距
float uMiasma;       //毒雾脉动强度

#define PI 3.14159265
#define TAU 6.28318530

//哈希与噪声
float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

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

float fbm4(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * valueNoise(p);
        p = p * 2.07 + float2(3.1, 7.4);
        a *= 0.5;
    }
    return v;
}

//色板:硫磺海的黑绿基底与毒黄高光
static const float3 COL_VOID  = float3(0.020, 0.030, 0.012);//最深处的黑绿淤泥
static const float3 COL_DEEP  = float3(0.050, 0.072, 0.030);//硫磺深水主基调
static const float3 COL_MID   = float3(0.110, 0.150, 0.060);//中层毒橄榄
static const float3 COL_ACID  = float3(0.255, 0.333, 0.120);//近表层的酸蚀绿
static const float3 COL_GLOW  = float3(0.520, 0.680, 0.250);//硫磺生物冷光
static const float3 COL_HALO  = float3(0.850, 0.880, 0.420);//毒气高光/硫黄

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //圆角矩形SDF
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 6.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (panelSDF > uEdgePad + 6.0) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime * 0.5;

    //1 主纵向渐变:顶部最深,底部因硫磺海床的微弱辉光而稍亮
    float vy = uv.y;
    float3 bg;
    if (vy < 0.45) {
        bg = lerp(COL_VOID, COL_DEEP, vy / 0.45);
    }
    else {
        float tb = (vy - 0.45) / 0.55;
        bg = lerp(COL_DEEP, COL_MID, tb);
        bg += COL_ACID * pow(tb, 2.4) * 0.22;
    }

    //横向边缘比中央略亮一点,中央做暗带
    float hx = abs(uv.x - 0.5) * 2.0;
    bg *= 0.86 + hx * 0.16;

    //2 缓慢横向毒雾:用低频fbm偏移模拟毒液密度变化
    float2 flowUV = float2(uv.x * 2.6 + t * 0.16, uv.y * 1.4 - t * 0.09);
    float flow = fbm4(flowUV);
    float flowMask = smoothstep(0.05, 0.55, abs(uv.y - 0.5));
    bg += COL_MID * (flow - 0.5) * 0.20 * flowMask;

    //3 底部上升的硫磺毒气柱:数道带相位差的软柱,自下而上飘散变宽
    float plume = 0.0;
    [unroll]
    for (int b = 0; b < 3; b++) {
        float bf = (float)b;
        float speed = 0.06 + bf * 0.022;
        float baseX = 0.20 + bf * 0.30 + sin(t * speed + bf * 1.9) * 0.07;
        float dx = uv.x - baseX;
        //上升时缓慢侧移,顶部更宽,模拟气体扩散
        dx -= (1.0 - uv.y) * (0.05 + bf * 0.02);
        float width = 0.05 + (1.0 - uv.y) * 0.10;
        float gas = exp(-(dx * dx) / (width * width));
        //底强顶弱
        float rise = smoothstep(0.0, 0.95, uv.y);
        //轻微翻涌
        float roll = 0.72 + 0.28 * sin(t * 1.4 + bf * 4.0 + uv.y * 5.0);
        plume += gas * rise * roll * (0.50 - bf * 0.07);
    }
    //毒气受流场调制,显得不那么规整
    plume *= 0.55 + 0.45 * fbm4(float2(uv.x * 3.0 - t * 0.35, uv.y * 1.2 + t * 0.2));
    bg += COL_GLOW * plume * 0.42;
    bg += COL_HALO * plume * 0.08;

    //4 海床酸蚀焦散:仅在底部柔和叠加,像腐蚀液在硫磺海床上的网状光斑
    float bottomBand = smoothstep(0.55, 1.0, uv.y);
    if (bottomBand > 0.001) {
        float2 cu = float2(uv.x * 3.5, uv.y * 2.0);
        cu.x += sin(t * 0.7 + uv.y * 4.0) * 0.18;
        cu.y -= t * 0.22;
        float n1 = valueNoise(cu * 1.6);
        float n2 = valueNoise(cu * 1.6 + float2(7.3, 2.1));
        float caustic = pow(saturate(1.0 - abs(n1 - n2) * 4.5), 3.0);
        caustic *= bottomBand;
        caustic *= (0.6 + uMiasma * 0.4);
        bg += COL_GLOW * caustic * 0.20;
    }

    //5 稀疏上浮硫磺气泡:小亮点缓慢上升,带轻微水平摇摆
    float gridSize = 56.0;
    float2 g = floor(pixelPos / gridSize);
    float s = hash21(g);
    float life = frac(s * 5.31 + t * (0.08 + s * 0.05));
    float2 p0 = (g + 0.5) * gridSize + (hash22(g) - 0.5) * (gridSize * 0.7);
    p0.y -= life * (gridSize * 1.4);
    p0.x += sin(life * TAU + s * 9.0) * 4.0;

    float dPart = length(pixelPos - p0);
    float partSize = 1.2 + s * 1.1;
    float core = 1.0 - smoothstep(0.0, partSize, dPart);
    core *= step(0.74, s);
    core *= sin(life * PI);
    bg += COL_HALO * core * 0.80;
    float halo = exp(-dPart * 0.35) * step(0.74, s) * sin(life * PI);
    bg += COL_GLOW * halo * 0.18;

    //6 中央文字保护:压暗与去饱和,提升前景对比
    float vCenter = abs(uv.y - 0.5) * 2.0;
    float textMask = 1.0 - smoothstep(0.05, 0.5, vCenter);
    bg *= lerp(1.0, 0.66, textMask);
    bg = lerp(bg, bg * float3(0.92, 1.00, 0.80), textMask * 0.40);

    //7 脉动酸绿内边:集中表达主题色,远离文字区
    float innerDist = max(-panelSDF, 0.0);
    float rimSoft = exp(-innerDist * 0.15);
    float rimLine = exp(-innerDist * innerDist * 0.55);
    float rimPulse = 0.78 + sin(t * 1.0) * 0.22;
    float topBias = smoothstep(0.0, 0.30, 1.0 - uv.y);
    float botBias = smoothstep(0.0, 0.30, uv.y);
    float biasWarm = topBias * 0.65 + botBias * 1.0;

    bg += COL_MID * rimSoft * 0.50 * rimPulse;
    bg += COL_GLOW * rimLine * 0.42 * rimPulse * biasWarm;
    float ripple = sin(uv.x * 7.0 - uv.y * 5.0 + t * 0.9) * 0.5 + 0.5;
    bg += COL_HALO * rimLine * ripple * 0.16 * biasWarm;

    //8 浮雕斜面,偏暖硫黄高光
    float bevelW = 9.0;
    float bevelMask = saturate(-panelSDF / bevelW);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;
    float2 lightDir = normalize(float2(0.55, -0.78));
    float2 edgeN = normalize(pixelPos - center + 0.0001);
    float bevelLight = dot(edgeN, lightDir) * 0.5 + 0.5;
    bg += lerp(COL_VOID, COL_GLOW * 0.7, bevelLight) * bevelMask * 0.30;
    float glint = bevelMask * pow(saturate(bevelLight), 14.0);
    bg += COL_HALO * glint * 0.28;

    //9 暗角
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.50, 0.62), vig * float2(0.50, 0.62));
    bg *= saturate(1.0 - vigStr) * 0.32 + 0.68;

    //10 细颗粒
    float dust = hash21(pixelPos + t * 22.0) * 0.04;
    bg *= 1.0 - dust * 0.5;

    float fa = uAlpha * edgeAlpha;
    float emitBoost = saturate((max(bg.r, max(bg.g, bg.b)) - 0.55) * 0.7);
    fa = saturate(fa + emitBoost * edgeAlpha * 0.18);
    return float4(bg * fa, fa) * vertexColor;
}

technique Technique1
{
    pass SulfseaPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
