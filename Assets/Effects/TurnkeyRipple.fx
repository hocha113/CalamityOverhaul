// ============================================================================
//TurnkeyRipple.fx 沉波狱吏水面层（世界锚定长条 quad，画在水体渲染之上）
//TechRipple 单技法：潜航尾流 / 暴起蓄势沸腾共用，由 uniform 权重乘混合（禁动态分支）
//  中心隆起：贴水面下的身体把水膜顶起（受威胁/蓄势增高），隆起顶一线受光亮缘
//  水下暗影透镜：中心正下方的吸光暗体，"水面下有大东西"的唯一诚实证据（预乘真遮挡）
//  泡沫带：贴起伏水面的撕裂白沫，双频噪声阈值，横向拉伸各向异性，尾侧变宽变密
//  尾迹残沫：尾远端稀疏沫点，水记得它走过（余韵层）
//  暴起气泡柱 uBoil：出水点正下上升密泡，uQuiet 发令前静默拍整体压灭（隆起保留）
//  端部收口：x 两端阈值路撕散淡出，无平切
//坐标全笛卡尔（无 atan2）；直线算术+普通 tex2D，FNA3D 安全
//预乘输出，进 AlphaBlend 批（暗影透镜要真遮挡，加色批画不出暗）
//绑定噪声实测值域 0.227~0.776，阈值一律过 nrm() 归一
//消费入口 Scenarios/Dungeonworld/NPCs/Elites/TurnkeyRendering.cs（EndEntityDraw 世界 quad）
// ============================================================================

sampler uImage0 : register(s0);   //批主纹理：白像素画布（不采样内容）
sampler uNoiseTex : register(s1); //PerlinNoise，LinearWrap，消费端上 s1

float uTime;        //秒
float2 uQuadSize;   //quad 世界像素尺寸
float uWorldX0;     //quad 左缘世界 X（噪声锚定世界，quad 跟身走而水纹不跟）
float uCenterX;     //本体/出水点世界 X
float uWaterV;      //水面在 quad 内的 v
float uSpeed;       //水平速度 px/f（带符号，定尾流方向）
float uEnv;         //0~1 在场包络（出生淡入/离水收场撕散）
float uThreat;      //0~1 威胁度（近玩家/压制态：泡沫密、隆起高、暗影实）
float uBoil;        //0~1 暴起蓄势沸腾
float uQuiet;       //0~1 发令前静默拍（压泡沫与气泡，隆起与暗影保留）
float uSeed;        //个体相位

//====== 污水白沫色板 ======
static const float3 FOAM  = float3(0.780, 0.900, 0.870);  //白沫
static const float3 SWAMP = float3(0.160, 0.270, 0.240);  //沼水
static const float3 DEEP  = float3(0.030, 0.075, 0.065);  //暗影水

//绑定噪声实测值域归一（0.227~0.776）
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

float4 PSRipple(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0 {
    float worldX = uWorldX0 + coords.x * uQuadSize.x;
    float dx = worldX - uCenterX;
    float h = (uWaterV - coords.y) * uQuadSize.y;   //px，正=水上

    //端部收口：两端 90px 阈值路淡出
    float endFade = saturate(coords.x * uQuadSize.x / 90.0)
                  * saturate((1.0 - coords.x) * uQuadSize.x / 90.0);

    //水面局部波动 + 中心隆起（身下的水被顶起）；hh=相对起伏面高度
    float wn = nrm(tex2D(uNoiseTex, float2(worldX * 0.011 + uTime * 0.09, uTime * 0.05)).r);
    float hump = exp2(-dx * dx * 0.00062) * (2.2 + uThreat * 3.2 + uBoil * 11.0);
    float hh = h - hump - (wn - 0.5) * 2.6;

    //尾流几何：中心之后（速度反侧）为尾，尾长随速度
    float sgn = sign(uSpeed + 0.0001);
    float behind = saturate(-dx * sgn / (46.0 + abs(uSpeed) * 26.0));
    float wakeAmp = saturate(abs(uSpeed) * 0.22);

    //泡沫带：贴 hh≈0 的撕裂白沫；带宽=基础+尾侧加宽+威胁/沸腾加宽
    //阈值随 behind 下降（尾侧沫更密），中心近旁再挖一口搅动低阈坑（身体正上方水最碎）
    float f1 = nrm(tex2D(uNoiseTex, float2(worldX * 0.014 - uTime * 0.30 * sgn, 0.23 + uSeed)).r);
    float f2 = nrm(tex2D(uNoiseTex, float2(worldX * 0.042 + uTime * 0.13 * sgn, 0.71 - uSeed)).r);
    float bandW = 2.0 + wakeAmp * (1.5 + behind * 5.0) + uThreat * 1.6 + uBoil * 4.0;
    float bandMask = exp2(-abs(hh) * (1.9 / bandW));
    float churn = exp2(-dx * dx * 0.0009) * (0.16 + uThreat * 0.08 + uBoil * 0.10);
    float foamThr = 0.46 - behind * wakeAmp * 0.55 - churn + (1.0 - uEnv) * 0.60 + uQuiet * 0.50;
    float foam = saturate((f1 * 0.6 + f2 * 0.4 - foamThr) * 4.5) * bandMask * endFade;

    //尾迹残沫：尾远端稀疏沫点（高分位阈值：nrm 后值域 0~1，0.88 合法）
    float sp = nrm(tex2D(uNoiseTex, float2(worldX * 0.030 + uSeed * 3.0, uTime * 0.02)).r);
    float speck = saturate((sp - 0.88) * 12.0) * saturate(behind * 1.4)
                * wakeAmp * exp2(-abs(hh) * 0.4) * endFade;

    //水下暗影透镜：中心正下的吸光暗体，向下 30px 内渐没
    float lens = exp2(-dx * dx * 0.00048) * saturate(-hh * 0.11) * saturate((hh + 30.0) * 0.08);
    lens *= (0.30 + uThreat * 0.45 + uBoil * 0.25) * endFade;

    //暴起气泡柱：出水点正下密泡上升（噪声 y 反向滚=上浮），静默拍压灭
    //x/y 采样频率拉近（0.060/0.052）泡才成圆点而非竖条
    float bub = nrm(tex2D(uNoiseTex, float2(worldX * 0.060 + uSeed, h * 0.052 - uTime * 0.24)).r);
    float bubbles = saturate((bub - 0.72) * 9.0) * saturate(-hh * 0.5)
                  * saturate((hh + 44.0) * 0.05) * exp2(-dx * dx * 0.0022)
                  * uBoil * (1.0 - uQuiet) * endFade;

    //隆起顶受光亮缘：只有隆起明显才亮（贴 hh≈1 的一线）
    float crest = exp2(-abs(hh - 1.0) * 0.9) * saturate(hump * 0.5 - 1.0) * endFade;

    //水下浑浊洗带：水线下一条宽而淡的沼色扰动（把白沫和暗影缝成一整片"被搅动的水"）
    float washN = nrm(tex2D(uNoiseTex, float2(worldX * 0.008 + uTime * 0.05, 0.55 + uSeed)).r);
    float wash = saturate(-hh * 0.22) * saturate((hh + 18.0) * 0.09)
               * (0.35 + wakeAmp * 0.45 + uThreat * 0.30 + uBoil * 0.35)
               * (0.55 + 0.45 * washN) * endFade;

    //合成（预乘）：白沫层 + 亮缘 + 暗影透镜 + 浑浊洗带
    float3 col = FOAM * (foam * (0.62 + 0.38 * f2) + speck * 0.80 + bubbles * 0.75);
    col += FOAM * crest * (0.20 + 0.20 * f1);
    col += lerp(SWAMP, DEEP, 0.7) * lens + SWAMP * wash * 0.50;
    float alpha = saturate(foam * 0.62 + speck * 0.40 + bubbles * 0.50 + crest * 0.16 + lens * 0.85 + wash * 0.40);
    return float4(col, alpha) * (uEnv * vc.a);
}

technique TechRipple {
    pass P0 {
        PixelShader = compile ps_3_0 PSRipple();
    }
}
