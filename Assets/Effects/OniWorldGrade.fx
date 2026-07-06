// ============================================================================
//OniWorldGrade.fx 鬼域全屏调色，两个 technique 对应两个渲染时机
//TechGrade（NPC 层之前，只吃环境：天空/墙/物块）：
//  表世界：轻胶片质感（微暖+颗粒+轻暗角），氛围主体在 OniSky
//  里世界：淡底浓墨（亮度量化墨阶上提+Sobel墨线+黑白红三色纪律+纸纹）
//  开/收域：墨水从裂口浸染/退潮，噪声毛边墨须前沿
//TechUnify（EndCapture，吃整帧含 NPC/弹幕/玩家）：
//  轻统一色罩（去饱和+冷/暖染，红色保真）+ 呼吸 + 错位帧 + 负片闪
//  实体只经过这层轻罩，墨阶量化/墨线不上身，战斗可读性优先
//呼吸与错位帧属全帧位移，必须在 TechUnify：拆在环境层会让实体相对地形滑动
//全部噪声输入为屏幕空间笛卡尔 UV，无极坐标
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;
float2 uScreenSize;     //像素
float uWorldBlend;      //0=表 1=里，捕获帧瞬切（纸层覆盖期间不可见）
float uSpreadMode;      //0=全覆盖 1=开合浸染
float uSpreadProgress;  //0~1 墨水覆盖
float2 uSpreadOrigin;   //扩散原点屏幕像素坐标（鬼眼位置）
float uAnomalyPulse;    //0~1 错位帧
float uNegativeFlash;   //0~1 负片闪
float uStillness;       //0~1 死寂加深
float uFrontEmber;      //0~1 扩散前沿红烬强度（爆域时最烈）

#define LUMA_W float3(0.299, 0.587, 0.114)

static const float3 WASHI_TINT = float3(1.030, 1.000, 0.935);
static const float3 INK_BLACK = float3(0.085, 0.082, 0.098);
static const float3 INK_DARK  = float3(0.185, 0.182, 0.215);
static const float3 INK_MID   = float3(0.420, 0.420, 0.470);
static const float3 INK_PALE  = float3(0.840, 0.820, 0.780);
static const float3 ONI_RED   = float3(0.820, 0.075, 0.095);

//亮度量化为 4 档墨阶，档间软过渡
float bandify(float l) {
    float b = saturate(l) * 3.0;
    float i = floor(b);
    float f = b - i;
    float soft = smoothstep(0.40, 0.60, f);
    return (i + soft) / 3.0;
}

float3 inkRamp(float q) {
    float3 c = INK_BLACK;
    c = lerp(c, INK_DARK, smoothstep(0.08, 0.36, q));
    c = lerp(c, INK_MID, smoothstep(0.36, 0.66, q));
    c = lerp(c, INK_PALE, smoothstep(0.66, 0.95, q));
    return c;
}

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//墨水浸染遮罩：毛边墨须为双频笛卡尔噪声扰动前沿
//返回 x=覆盖遮罩 y=前沿淤积带，两个 technique 共用同一前沿
float2 spreadMaskFront(float2 coords) {
    float diag = length(uScreenSize);
    float2 rel = (coords * uScreenSize - uSpreadOrigin) / diag;
    float dist = length(rel);
    float jag = noiseTex(coords * 2.3 + uTime * 0.012) * 0.6
              + noiseTex(coords * 5.1 - uTime * 0.017) * 0.4;
    //墨须振幅随覆盖率成长：早期半径小，固定大振幅会把前沿拽成偏心歪圆
    float jagAmp = lerp(0.030, 0.160, smoothstep(0.10, 0.70, uSpreadProgress));
    float sd = dist + (jag - 0.5) * jagAmp - uSpreadProgress * 1.18;
    float useSpread = step(0.5, uSpreadMode);
    float mask = lerp(1.0, 1.0 - smoothstep(-0.012, 0.014, sd), useSpread);
    float front = exp(-sd * sd / 0.0011) * (0.45 + 0.55 * jag) * useSpread;
    return float2(mask, front);
}

//表世界：轻胶片质感，氛围主体交给天空层，死寂时才明显收紧
float3 GradeOmote(float3 src, float2 uv, float d) {
    float luma = dot(src, LUMA_W);
    float desat = 0.12 + uStillness * 0.32;
    float3 c = lerp(src, luma.xxx, desat);
    c *= WASHI_TINT;

    //细颗粒
    float grain = noiseTex(uv * float2(6.0, 6.0 * uScreenSize.y / uScreenSize.x) + frac(uTime * 7.31) * 3.7);
    c *= 0.968 + grain * 0.055;

    //暖褐暗角，死寂时收紧
    float vig = smoothstep(0.50 - uStillness * 0.12, 1.00 - uStillness * 0.20, d);
    c = lerp(c, c * float3(0.62, 0.52, 0.42), vig * (0.16 + uStillness * 0.30));
    return c;
}

//里世界：水墨三色
float3 GradeUra(float3 src, float2 uv, float2 px, float d) {
    //3x3 Sobel
    float lTL = dot(tex2D(uImage0, uv + float2(-px.x, -px.y)).rgb, LUMA_W);
    float lT  = dot(tex2D(uImage0, uv + float2(0, -px.y)).rgb, LUMA_W);
    float lTR = dot(tex2D(uImage0, uv + float2(px.x, -px.y)).rgb, LUMA_W);
    float lL  = dot(tex2D(uImage0, uv + float2(-px.x, 0)).rgb, LUMA_W);
    float lR  = dot(tex2D(uImage0, uv + float2(px.x, 0)).rgb, LUMA_W);
    float lBL = dot(tex2D(uImage0, uv + float2(-px.x, px.y)).rgb, LUMA_W);
    float lB  = dot(tex2D(uImage0, uv + float2(0, px.y)).rgb, LUMA_W);
    float lBR = dot(tex2D(uImage0, uv + float2(px.x, px.y)).rgb, LUMA_W);

    float gx = (lTR + 2.0 * lR + lBR) - (lTL + 2.0 * lL + lBL);
    float gy = (lBL + 2.0 * lB + lBR) - (lTL + 2.0 * lT + lTR);
    float edge = smoothstep(0.10, 0.45, sqrt(gx * gx + gy * gy));

    float luma = dot(src, LUMA_W);
    //暗部大幅上提：淡底浓墨，可读性优先
    float q = bandify(pow(saturate(luma * 1.35), 0.85));
    float3 ink = inkRamp(q);
    //回混一成半原始亮度：软化档间断层，让月亮/高光比淡墨阶更亮
    ink = lerp(ink, luma.xxx, 0.14);

    //墨线：亮处落黑线，暗处浮白线（剪影可读）
    float3 lineCol = lerp(INK_PALE * 0.85, INK_BLACK, smoothstep(0.30, 0.62, luma));
    //干笔飞白：墨线沿噪声断续
    float dryBrush = 0.72 + 0.28 * noiseTex(uv * 9.0 + float2(uTime * 0.011, 0));
    ink = lerp(ink, lineCol, edge * 0.60 * dryBrush);

    //纸纹叠底：细纹 + 大块墨晕
    float paper = noiseTex(uv * float2(7.0, 7.0 * uScreenSize.y / uScreenSize.x));
    float blotch = noiseTex(uv * 1.1 + uTime * 0.0025);
    ink *= (0.92 + paper * 0.11) * (0.95 + blotch * 0.08);

    //三色纪律：唯一的colour是红
    float redness = src.r - max(src.g, src.b);
    float redMask = smoothstep(0.05, 0.30, redness);
    float3 redCol = ONI_RED * (0.45 + luma * 1.45);
    float3 c = lerp(ink, redCol, redMask);

    //暗角收敛到氛围级，不吃可读性
    float vig = smoothstep(0.46, 1.05, d);
    c *= 1.0 - vig * 0.25;
    return c;
}

//====== TechGrade：环境调色（NPC 层之前执行，画面里只有环境） ======
float4 PSGrade(float2 coords : TEXCOORD0) : COLOR0 {
    float2 px = 1.0 / uScreenSize;
    float2 uv = coords;

    float3 src = tex2D(uImage0, uv).rgb;
    float d = length((uv - 0.5) * float2(uScreenSize.x / uScreenSize.y, 1.0)) * 1.15;

    //两世界都算全，step 选择：规避分支内梯度指令
    float3 omote = GradeOmote(src, uv, d);
    float3 ura = GradeUra(src, uv, px, d);
    float3 graded = lerp(omote, ura, step(0.5, uWorldBlend));

    float2 mf = spreadMaskFront(coords);
    float3 final = lerp(src, graded, mf.x);
    //前沿墨迹淤积带
    final = lerp(final, final * float3(0.22, 0.20, 0.27), mf.y * 0.75);
    //爆域浪头红烬
    final += float3(0.72, 0.09, 0.05) * mf.y * uFrontEmber;

    return float4(final, 1.0);
}

//====== TechUnify：全帧轻统一罩（EndCapture 执行，实体已在画面里） ======
float4 PSUnify(float2 coords : TEXCOORD0) : COLOR0 {
    float2 px = 1.0 / uScreenSize;

    //呼吸：表世界 8 秒周期的 0.35% 缩放，死寂时停摆
    float omoteF = 1.0 - uWorldBlend;
    float breath = sin(uTime * 0.785) * 0.0035 * omoteF * (1.0 - uStillness);
    float2 uv = (coords - 0.5) * (1.0 - breath) + 0.5;

    //错位帧：整帧平移数像素 + 红通道错开（pulse=0 时偏移归零，无需分支）
    uv += float2(px.x * 4.0, -px.y * 1.5) * uAnomalyPulse;
    float3 src = tex2D(uImage0, uv).rgb;
    src.r = tex2D(uImage0, uv + float2(px.x * 3.0 * uAnomalyPulse, 0)).r;

    float luma = dot(src, LUMA_W);
    //红色保真：里世界三色纪律下红仍是唯一的colour，实体的红光/红弹不减艳
    float redness = src.r - max(src.g, src.b);
    float redMask = smoothstep(0.05, 0.30, redness);

    //表世界罩：微量去饱和 + 和纸微暖，死寂时略深
    float3 warm = lerp(src, luma.xxx, 0.08 + uStillness * 0.22) * WASHI_TINT;

    //里世界罩：三成去饱和 + 冷灰染，实体在墨色环境里减艳但色相/轮廓仍清晰
    //红色双重豁免（去饱和+冷染）：三色纪律下红是唯一的colour，不减艳
    float3 cold = lerp(src, luma.xxx, 0.32 * (1.0 - redMask));
    cold *= lerp(float3(0.90, 0.92, 1.04), float3(1.0, 1.0, 1.0), redMask);

    float3 tone = lerp(warm, cold, step(0.5, uWorldBlend));

    //浸染遮罩内生效：开/收域时墨浪压过哪里，哪里才染上
    float2 mf = spreadMaskFront(coords);
    float3 final = lerp(src, tone, mf.x);

    //负片闪：全画面事件，旧世界日月化眼的一瞬
    final = lerp(final, 1.0 - final, uNegativeFlash * 0.92);

    return float4(final, 1.0);
}

technique TechGrade {
    pass P0 {
        PixelShader = compile ps_3_0 PSGrade();
    }
}

technique TechUnify {
    pass P0 {
        PixelShader = compile ps_3_0 PSUnify();
    }
}
