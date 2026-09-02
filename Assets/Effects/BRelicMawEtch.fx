// ============================================================================
//BRelicMawEtch.fx 蚀界之颚·酸蚀覆膜（残酷遗物系列，NPC 帧后处理）
//TechEtch: 被酸蚀敌人的体表重绘——
//  湿膜蠕动位移（液体爬行，幅度小于火的热浪、带向下偏置）
//  + 蚀坑（双频噪声阈值暗斑，随层数蔓延；坑缘腐化紫淤痕，坑心沉绿蚀肉）
//  + 下淌酸膜水光（低频噪声带下滚增亮）
//  + 轮廓滴酸（邻域 alpha 四采样找轮廓，下缘加权，酸往下积）
//  + 酸泡闪点（高分位阈值碎亮点，快闪）
//采样：coords 落在 uUvRect 内才钳帧界（原版帧表防串帧）；对不上只钳 [0,1]
//GlobalNPC 只能按 TextureAssets.Npc+npc.frame 猜帧界，灾厄 Boss 常换图，错钳=马赛克
//坐标全笛卡尔无 atan2；无动态分支；预乘语义进 AlphaBlend 批
//绑定噪声 PerlinNoise 实测值域 0.227~0.776，阈值一律过 nrm() 归一
//消费入口 BrutalRelics/EaterOfWorlds/MawCorrosionNPC.cs（PreDraw Immediate 重绘）
// ============================================================================

sampler uImage0 : register(s0);   //批主纹理：NPC 帧图
sampler uNoiseTex : register(s1); //PerlinNoise，LinearWrap，消费端上 s1

float uTime;        //秒
float2 uTexelSize;  //1/贴图尺寸
float4 uUvRect;     //帧界（xy=min zw=max，半像素内缩）
float uEtchT;       //0~1 覆膜强度包络（淡入淡出）
float uStackT;      //0~1 层数比（蚀坑蔓延度）
float uSeed;        //个体相位

//====== 腐化酸蚀色板（与 EowMotionFX 配色同源）======
static const float3 ACID_BRIGHT = float3(0.808, 0.957, 0.580); //酸液高亮(膜面反光)
static const float3 ACID_GREEN  = float3(0.588, 0.847, 0.329); //酸液主绿
static const float3 ACID_DEEP   = float3(0.329, 0.557, 0.180); //酸液沉色
static const float3 BRUISE_PURP = float3(0.541, 0.369, 0.804); //腐化紫(蚀缘淤痕)

//绑定噪声实测值域归一（0.227~0.776）
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

float4 PSEtch(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0 {
    float2 span = max(uUvRect.zw - uUvRect.xy, 0.00001);
    float2 flGuess = (coords - uUvRect.xy) / span;
    //半像素内缩让帧缘略越界，放 8% 余量；整精灵对不上猜测帧则 inFrame=0
    float inFrame = step(-0.08, flGuess.x) * step(flGuess.x, 1.08)
        * step(-0.08, flGuess.y) * step(flGuess.y, 1.08);
    float2 fl = lerp(coords, saturate(flGuess), inFrame);
    float2 clampLo = lerp(float2(0.0, 0.0), uUvRect.xy, inFrame);
    float2 clampHi = lerp(float2(1.0, 1.0), uUvRect.zw, inFrame);

    //湿膜蠕动：小幅 UV 位移，向下偏置（酸在淌不是在烧）
    float wn0 = tex2D(uNoiseTex, float2(fl.x * 2.2 + uSeed, fl.y * 1.6 - uTime * 0.22)).r;
    float wn1 = tex2D(uNoiseTex, float2(fl.x * 4.6 - uSeed, fl.y * 3.1 - uTime * 0.36)).r;
    float2 wob = float2((wn0 - 0.5) * 1.6 + (wn1 - 0.5) * 0.7, (wn1 - 0.5) * 1.0 + 0.35);
    float2 suv = clamp(coords + wob * uTexelSize * (1.8 * uEtchT), clampLo, clampHi);
    float4 base = tex2D(uImage0, suv) * vc;

    //蚀坑：双频噪声阈值，坑随层数蔓延；坑心沉绿蚀肉、坑缘腐化紫淤痕
    float p0 = nrm(tex2D(uNoiseTex, fl * float2(3.0, 2.6) + uSeed).r);
    float p1 = nrm(tex2D(uNoiseTex, fl * float2(7.4, 6.2) - uSeed * 1.7).r);
    float pitN = p0 * 0.58 + p1 * 0.42;
    float pitThr = 1.02 - uStackT * 0.50 - uEtchT * 0.12;
    float pit = smoothstep(pitThr, pitThr - 0.14, pitN) * uEtchT;
    float rimRing = smoothstep(pitThr + 0.12, pitThr, pitN) * (1.0 - pit);
    float srcLuma = dot(base.rgb, float3(0.333, 0.333, 0.333));
    base.rgb = lerp(base.rgb, ACID_DEEP * (0.30 + 0.55 * srcLuma) * base.a, pit * 0.78);
    base.rgb = lerp(base.rgb, BRUISE_PURP * (0.45 + 0.55 * srcLuma) * base.a, rimRing * 0.50 * uEtchT);

    //下淌酸膜水光：低频噪声带缓慢下滚，膜面湿亮
    float film = nrm(tex2D(uNoiseTex, float2(fl.x * 1.8 + uSeed * 0.7, fl.y * 0.9 - uTime * 0.30)).r);
    float filmBand = smoothstep(0.55, 0.85, film) * uEtchT;
    base.rgb += ACID_GREEN * base.a * filmBand * 0.20;

    //轮廓滴酸：邻域 alpha 四采样找轮廓缺口，下缘加权（液体向下积）
    float2 t3 = uTexelSize * 3.0;
    float aL = tex2D(uImage0, clamp(suv - float2(t3.x, 0.0), clampLo, clampHi)).a;
    float aR = tex2D(uImage0, clamp(suv + float2(t3.x, 0.0), clampLo, clampHi)).a;
    float aU = tex2D(uImage0, clamp(suv - float2(0.0, t3.y), clampLo, clampHi)).a;
    float aD = tex2D(uImage0, clamp(suv + float2(0.0, t3.y), clampLo, clampHi)).a;
    float edge = saturate(base.a * 1.2 - min(min(aL, aR), min(aU, aD)));
    float downBias = 0.40 + saturate(base.a - aD) * 1.35;
    float drip = nrm(tex2D(uNoiseTex, float2(fl.x * 3.2 + uSeed, fl.y * 1.4 - uTime * 0.45)).r);
    base.rgb += ACID_GREEN * edge * (0.22 + 0.70 * drip) * downBias * uEtchT * 0.80;

    //酸泡闪点：高分位阈值碎亮点，短周期明灭（配合CPU侧的PRT酸泡）
    float sp = nrm(tex2D(uNoiseTex, float2(fl.x * 5.5 + uTime * 0.05, fl.y * 4.8 - uTime * 0.10)).r);
    float speck = saturate((sp - 0.84) * 10.0) * uEtchT * base.a;
    float flick = 0.5 + 0.5 * sin(uTime * 7.0 + uSeed * 9.0 + fl.y * 18.0);
    base.rgb += ACID_BRIGHT * speck * flick * 0.55;

    return base;
}

technique TechEtch {
    pass P0 {
        PixelShader = compile ps_3_0 PSEtch();
    }
}
