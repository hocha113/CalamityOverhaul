// ============================================================================
//TzeentchPanel.fx 奸奇(变数与命运之神)风格对话框背景
//主题:不断流变的亚空间魔潮 + 编织命运的金色丝线 + 窥视的占卜微光
//AlphaBlend 预乘 alpha;全程笛卡尔坐标,刻意回避 atan2 极坐标噪声以杜绝接缝
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float uEdgePad;      //面板内缩边距
float uMiasma;       //变数脉动0~1,驱动魔潮翻涌与命运金线强度(hover/选中可上调)

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

//色板:奸奇的靛蓝深渊 + 蓝紫魔火 + 品红变数 + 金色命运
static const float3 COL_VOID    = float3(0.028, 0.016, 0.055);//最深的靛黑亚空间
static const float3 COL_DEEP    = float3(0.075, 0.045, 0.165);//深蓝紫主基调
static const float3 COL_AZURE   = float3(0.060, 0.235, 0.470);//深天蓝魔潮
static const float3 COL_VIOLET  = float3(0.250, 0.120, 0.480);//蓝紫罗兰魔火
static const float3 COL_MAGENTA = float3(0.470, 0.090, 0.390);//品红变数
static const float3 COL_GOLD    = float3(0.960, 0.780, 0.300);//奸奇金:命运丝线
static const float3 COL_HALO    = float3(0.820, 0.900, 1.000);//蓝白魔火高光

//变数循环:随时间在 天蓝->蓝紫->品红 之间无缝轮转,体现"流变"本质
float3 tzCycle(float ph) {
    ph = frac(ph);
    float seg = ph * 3.0;
    float3 col;
    if (seg < 1.0) {
        col = lerp(COL_AZURE, COL_VIOLET, seg);
    }
    else if (seg < 2.0) {
        col = lerp(COL_VIOLET, COL_MAGENTA, seg - 1.0);
    }
    else {
        col = lerp(COL_MAGENTA, COL_AZURE, seg - 2.0);
    }
    return col;
}

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
    float cornerR = 7.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (panelSDF > uEdgePad + 6.0) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime * 0.5;

    //当前帧的变数主色:缓慢轮转,叠加位置相关偏移让左右两侧色相略有错位
    float cyclePh = t * 0.045;
    float3 hueNow = tzCycle(cyclePh);
    float3 hueAlt = tzCycle(cyclePh + 0.16 + uv.x * 0.12);

    //1 主纵向渐变:顶部为最深亚空间,向下渗出深蓝紫
    float vy = uv.y;
    float3 bg;
    if (vy < 0.5) {
        bg = lerp(COL_VOID, COL_DEEP, vy / 0.5);
    }
    else {
        float tb = (vy - 0.5) / 0.5;
        bg = lerp(COL_DEEP, lerp(COL_DEEP, hueAlt, 0.45), tb);
    }
    //横向边缘略亮,中央做暗带托文字
    float hx = abs(uv.x - 0.5) * 2.0;
    bg *= 0.84 + hx * 0.18;

    //2 翻涌的亚空间魔潮:低频fbm域扭曲,把变数主色揉进背景
    float2 flowUV = float2(uv.x * 2.4 + t * 0.14, uv.y * 1.5 - t * 0.10);
    float flow = fbm4(flowUV);
    float2 flow2UV = float2(uv.x * 1.7 - t * 0.08, uv.y * 2.1 + t * 0.06);
    float flow2 = fbm4(flow2UV);
    float swirlMask = smoothstep(0.05, 0.6, abs(uv.y - 0.5)) * 0.7 + 0.3;
    bg += hueNow * (flow - 0.5) * 0.42 * swirlMask;
    bg += COL_VIOLET * (flow2 - 0.5) * 0.16;

    //3 命运金线:fbm域扭曲后取多道正弦脊线,化作流动的金色织网(奸奇的算计)
    float2 warp = float2(fbm4(uv * 2.6 + t * 0.10), fbm4(uv * 2.6 + 11.0 - t * 0.08));
    float2 wuv = uv + (warp - 0.5) * 0.30;
    float threads = 0.0;
    [unroll]
    for (int k = 0; k < 3; k++) {
        float kf = (float)k;
        float freq = 7.0 + kf * 4.5;
        float ph = t * (0.22 + kf * 0.08) + kf * 2.1;
        float s = sin(wuv.x * freq + wuv.y * (3.0 + kf) + ph);
        float ridge = pow(saturate(1.0 - abs(s)), 16.0);
        threads += ridge * (0.55 - kf * 0.12);
    }
    //远离文字中心处金线更明显,避免压住正文
    float threadFade = smoothstep(0.12, 0.42, abs(uv.y - 0.5)) * 0.75 + 0.25;
    threads *= threadFade * (0.55 + uMiasma * 0.55);
    bg += COL_GOLD * threads * 0.9;
    bg += COL_HALO * threads * 0.12;

    //4 占卜微光:中央上方一团缓缓呼吸的椭圆辉光,似窥视的眼,色随变数轮转
    float2 ec = uv - float2(0.5, 0.40);
    ec.x *= 1.85;
    float ed = length(ec);
    float breath = 0.55 + 0.45 * sin(t * 0.7);
    float eye = exp(-ed * ed * 11.0) * breath;
    //一道竖直瞳缝,极轻,仅作暗示
    float slit = 1.0 - smoothstep(0.0, 0.045, abs(ec.x));
    eye *= 1.0 - slit * 0.35;
    bg += lerp(COL_AZURE, hueNow, 0.5) * eye * 0.22;
    bg += COL_HALO * eye * eye * 0.10;

    //5 上浮的蓝白魔火微粒:网格哈希生成,缓慢上升并轻微侧摆
    float gridSize = 58.0;
    float2 g = floor(pixelPos / gridSize);
    float seedp = hash21(g);
    float life = frac(seedp * 5.31 + t * (0.07 + seedp * 0.05));
    float2 p0 = (g + 0.5) * gridSize + (hash22(g) - 0.5) * (gridSize * 0.7);
    p0.y -= life * (gridSize * 1.5);
    p0.x += sin(life * TAU + seedp * 9.0) * 5.0;
    float dPart = length(pixelPos - p0);
    float partSize = 1.1 + seedp * 1.2;
    float core = (1.0 - smoothstep(0.0, partSize, dPart)) * step(0.72, seedp) * sin(life * PI);
    bg += COL_HALO * core * 0.85;
    float halo = exp(-dPart * 0.33) * step(0.72, seedp) * sin(life * PI);
    bg += lerp(COL_GOLD, COL_HALO, 0.4) * halo * 0.16;

    //6 中央文字保护:压暗与微调色,提升前景对比
    float vCenter = abs(uv.y - 0.5) * 2.0;
    float textMask = 1.0 - smoothstep(0.05, 0.5, vCenter);
    bg *= lerp(1.0, 0.60, textMask);
    bg = lerp(bg, bg * float3(0.90, 0.92, 1.05), textMask * 0.40);

    //7 脉动魔法内边:集中表达主题色,顶底偏金,中段偏变数色
    float innerDist = max(-panelSDF, 0.0);
    float rimSoft = exp(-innerDist * 0.14);
    float rimLine = exp(-innerDist * innerDist * 0.55);
    float rimPulse = 0.76 + sin(t * 1.1) * 0.24;
    float topBias = smoothstep(0.0, 0.32, 1.0 - uv.y);
    float botBias = smoothstep(0.0, 0.32, uv.y);
    bg += lerp(COL_VIOLET, hueNow, 0.5) * rimSoft * 0.50 * rimPulse;
    bg += COL_GOLD * rimLine * 0.40 * rimPulse * (topBias * 0.9 + botBias * 0.7);
    float ripple = sin(uv.x * 8.0 - uv.y * 5.0 + t * 1.0) * 0.5 + 0.5;
    bg += COL_HALO * rimLine * ripple * 0.14;

    //8 浮雕斜面,偏金的魔光高光
    float bevelW = 9.0;
    float bevelMask = saturate(-panelSDF / bevelW);
    bevelMask = 1.0 - bevelMask;
    bevelMask *= bevelMask;
    float2 lightDir = normalize(float2(0.55, -0.78));
    float2 edgeN = normalize(pixelPos - center + 0.0001);
    float bevelLight = dot(edgeN, lightDir) * 0.5 + 0.5;
    bg += lerp(COL_VOID, COL_GOLD * 0.7, bevelLight) * bevelMask * 0.30;
    float glint = bevelMask * pow(saturate(bevelLight), 14.0);
    bg += COL_HALO * glint * 0.30;

    //9 暗角
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.50, 0.62), vig * float2(0.50, 0.62));
    bg *= saturate(1.0 - vigStr) * 0.34 + 0.66;

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
    pass TzeentchPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
