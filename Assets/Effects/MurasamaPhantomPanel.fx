// ============================================================================
//MurasamaPhantomPanel.fx 鬼妖村正委托面板背景
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float  uTime;
float  uAlpha;
float2 uResolution;
float  uEdgePad;      //面板内缩边距
float  uVariant;      //0条目 1追踪窗口
float  uIntensity;    //0~1 hover/select/Tracked
float  uPulse;        //0~1 慢速脉动
float3 uAccent;       //RGB 状态色

#define PI  3.14159265
#define TAU 6.28318530

//─── 工具函数 ───────────────────────────────────────────────────────────────

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
    float a = 0.55;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.07 + float2(1.7, 9.2);
        a *= 0.55;
    }
    return v;
}

//─── 色板(MGR:R血气板) ───────────────────────────────────────────────────────
static const float3 COL_VOID    = float3(0.005, 0.001, 0.002); //近黑(墨色铁)
static const float3 COL_OBSIDIAN= float3(0.020, 0.008, 0.012); //黑曜深红
static const float3 COL_BLOOD   = float3(0.180, 0.018, 0.028); //暗血红
static const float3 COL_CRIMSON = float3(0.560, 0.052, 0.070); //主猩红
static const float3 COL_BLADE   = float3(0.920, 0.140, 0.180); //刀刃赤
static const float3 COL_FLASH   = float3(1.000, 0.420, 0.380); //高光血雾

//─── 主片段 ─────────────────────────────────────────────────────────────────
float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //几乎尖角的SDF ： MGR:R的HUD是冷硬的金属切边
    float cornerR = lerp(1.0, 3.0, uVariant);
    float2 dd = abs(pixelPos - center) - halfSize;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;

    if (panelSDF > uEdgePad + 4.0) return float4(0, 0, 0, 0);
    float edgeMask = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    if (edgeMask < 0.005) return float4(0, 0, 0, 0);

    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime * 0.55;
    float intensity = saturate(uIntensity);
    float pulse01 = saturate(uPulse);

    //═══ 1. 主渐变底:左暗右浅,顶部更黑,底部带血 ═══════════════════════════════
    float3 bgTop = COL_VOID;
    float3 bgBot = lerp(COL_OBSIDIAN, COL_BLOOD * 0.55, intensity * 0.7 + 0.30);
    float3 bg = lerp(bgTop, bgBot, pow(uv.y, 1.4));
    //左缘略沉,右缘略亮,模拟刀身反射光
    float xLight = pow(uv.x, 2.2) * 0.18;
    bg += COL_BLOOD * xLight;

    //═══ 2. 对角扫描线 ： 血纹细密斜栅 ════════════════════════════════════════
    float diag = pixelPos.x * 0.85 - pixelPos.y * 0.55;
    float scanPhase = frac(diag * 0.075 - t * 0.45);
    float scan = exp(-scanPhase * 14.0) * 0.16;
    bg += COL_CRIMSON * scan;

    //═══ 3. 利刃斩击扫光 ： 周期性的对角光带划过整个面板 ══════════════════════
    float bladeCycle = 0.16 + intensity * 0.10;
    float bladePhase = frac(t * bladeCycle);
    //带宽涉及0~1.4使得光带能扫出屏外,有进出感
    float bladeBand = bladePhase * 1.6 - 0.3;
    float bladeCoord = uv.x * 0.7 + uv.y * 0.5;
    float bladeDist = abs(bladeCoord - bladeBand);
    //开头/结尾较快淡出,只在中段0.05~0.85显形
    float bladeWindow = smoothstep(0.0, 0.10, bladePhase) * (1.0 - smoothstep(0.85, 1.0, bladePhase));
    float blade = exp(-bladeDist * 26.0) * bladeWindow;
    bg += COL_BLADE * blade * (0.45 + intensity * 0.55);
    //刀刃后的细微余韵线
    float bladeAfter = exp(-bladeDist * 9.0) * bladeWindow;
    bg += COL_BLOOD * bladeAfter * 0.22;

    //═══ 4. 血雾(fbm) ： 大尺度低频暗红雾,集中在底半部 ═══════════════════════
    float2 mistUV = pixelPos * 0.018 + float2(t * 0.5, -t * 0.25);
    float mist = fbm3(mistUV);
    float bottomBand = smoothstep(0.35, 0.95, uv.y);
    float bloodMist = pow(saturate(mist), 1.5) * bottomBand;
    bg += COL_BLOOD * bloodMist * (0.45 + intensity * 0.30);
    bg += COL_CRIMSON * pow(bloodMist, 2.5) * 0.20;

    //═══ 5. 数据腐蚀色块 ： 像素级闪现的方块,只在右上角区域出现 ═══════════════
    float2 corrUV = floor(pixelPos / 4.0);
    float corrSeed = hash21(corrUV);
    float corrBlink = step(0.985 - intensity * 0.012, frac(corrSeed * 7.13 + t * 1.3));
    float corrRegion = smoothstep(0.55, 0.95, uv.x) * smoothstep(0.0, 0.40, 1.0 - uv.y);
    bg += COL_BLADE * corrBlink * corrRegion * 0.40;
    //偶发整行水平横移色伪影
    float lineNoise = step(0.992, frac(uv.y * 200.0 + hash11(floor(t * 3.0)) * 50.0));
    bg += COL_BLADE * lineNoise * 0.10 * intensity;

    //═══ 6. 边缘血光 ： 状态色融入内边RIM ═══════════════════════════════════════
    float innerDist = max(-panelSDF, 0.0);
    float rimSoft = exp(-innerDist * 0.16);
    float rimLine = exp(-innerDist * innerDist * 0.55);
    float rimPulse = 0.55 + sin(uTime * 1.4 + pulse01 * TAU) * 0.30 + intensity * 0.20;
    //accent染色融入猩红
    float3 accentMix = lerp(COL_CRIMSON, uAccent, 0.55);
    bg += accentMix * rimSoft * 0.45 * rimPulse;
    bg += COL_BLADE * rimLine * 0.55 * rimPulse;

    //═══ 7. 顶部金属切边 ： 一条细密亮线,模拟刀刃切割面板 ═══════════════════════
    float topDist = uv.y;
    float topEdge = smoothstep(0.05, 0.0, topDist);
    bg += COL_FLASH * topEdge * (0.30 + pulse01 * 0.30);
    //顶部之下0.02~0.06处有细一点的暗腔(切口阴影)
    float topShadow = smoothstep(0.02, 0.05, topDist) * (1.0 - smoothstep(0.05, 0.10, topDist));
    bg -= float3(0.018, 0.005, 0.008) * topShadow;

    //═══ 8. 左侧战术粗带 ： uv.x<0.022区域用accent色填充 ═══════════════════════
    float leftBlock = (1.0 - smoothstep(0.018, 0.024, uv.x))
                    * smoothstep(0.0, 0.04, uv.y) * smoothstep(0.0, 0.04, 1.0 - uv.y);
    bg += uAccent * leftBlock * (0.55 + rimPulse * 0.35);
    //左带高光细线
    float leftHL = (1.0 - smoothstep(0.005, 0.008, uv.x)) * smoothstep(0.0, 0.04, uv.y) * smoothstep(0.0, 0.04, 1.0 - uv.y);
    bg += COL_FLASH * leftHL * 0.30 * rimPulse;

    //═══ 9. Blade Mode 偶发整面血红脉冲 ： 仅当intensity高时较常出现 ═══════════
    float bladeModeT = sin(t * 2.7) * 0.5 + 0.5;
    float bladeMode = step(0.93 - intensity * 0.10, bladeModeT) * intensity;
    bg += COL_BLADE * bladeMode * 0.14;
    bg = lerp(bg, bg * float3(1.10, 0.95, 0.95), bladeMode * 0.5);

    //═══ 10. 暗角 ═══════════════════════════════════════════════════════════════
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.42, 0.55), vig * float2(0.42, 0.55));
    bg *= saturate(1.0 - vigStr) * 0.40 + 0.60;

    //═══ 11. 细颗粒胶片噪声 ═══════════════════════════════════════════════════
    float dust = hash21(pixelPos + t * 28.0) * 0.05;
    bg *= 1.0 - dust * 0.45;

    //═══ 12. 中央内容区轻微压暗(让文字读得更清楚) ═══════════════════════════════
    float vCenter = abs(uv.y - 0.5) * 2.0;
    float textMask = (1.0 - smoothstep(0.10, 0.55, vCenter)) * (1.0 - uVariant * 0.4);
    bg *= lerp(1.0, 0.78, textMask);

    float a = uAlpha * edgeMask;
    //加入自发光提升,使猩红更通透
    float emitBoost = saturate((max(bg.r, max(bg.g, bg.b)) - 0.45) * 0.6);
    a = saturate(a + emitBoost * edgeMask * 0.18);
    return float4(bg * a, a) * vertexColor;
}

//─── Technique ──────────────────────────────────────────────────────────────
technique Technique1
{
    pass MurasamaPhantomPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
