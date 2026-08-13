//KikasaDream.fx 鬼梦拉入/归返的镜面世界合成（KikasaFlip 血统，几何契约照搬）
//TechMirror: 水线以下真垂直镜像，镜面按 uDreamSide 在血湖材质与梦境红黑之间调色；
//            沸腾（uBoil）鼓包/翻滚/碎泡；翻转期镜像 x 随 uRollProgress 收敛为点反射，
//            θ=π 时 180°翻转∘点反射=恒等，与真实渲染零跳变交接；
//            结算后 uSwallow 吞满、uGrade 让位真实氛围。
//镜中黑犬直接把狼帧采进镜区（s2）：湿墨压黑 + 余烬双目（uGaze），
//替换玩家的镜像——梦里牵引一切的是它，不是你。
//直线算术+平 tex2D，无分支；s0=屏幕帧 s1=PerlinNoise s2=狼贴图

float uTime;         //秒（EffectTime）
float uPivotY;       //缝线枢轴 uv.y，翻转期间收敛到 0.5
float uRollProgress; //0-1 翻转进度
float uOriginU;      //施术者 uv.x（沸腾隆起与涟漪环圆心）
float uAspect;       //宽/高
float uWaterLevel;   //水位线 uv.y
float uWaterWobble;  //水位线噪声波动幅度
float uFoamBoost;    //0-1 泡沫增强
float uSwallow;      //0-1 结算后镜面向上吞没旧世界
float uGrade;        //0-1 镜像侧调色增益
float uGlimpse;      //0-1 异样脉冲（错位双曝）
float uGlimpseRing;  //0-1 异样涟漪环
float uSeamGlow;     //0-1 水面水膜辉光
float uBoil;         //0-1 沸腾强度
float uDreamSide;    //0=血湖材质 1=梦境红黑材质（拉入向 1 靠，归返向 0 靠）
float uMix;          //0-1 合成介入度，起手淡入
float4 uHoundRect;   //镜中黑犬 quad（屏幕 uv：xy=左上 zw=尺寸）
float4 uHoundUv;     //狼帧在贴图里的区域（纹理 uv：xy=偏移 zw=尺寸）
float uHoundFlipH;   //1=犬面朝右
float uHoundA;       //0-1 犬影在场
float uHoundAspect;  //犬 quad 宽/高（屏幕像素比），目晕保圆用
float2 uEyeUv;       //眼睛帧内原生 uv（面向左、未翻转）
float uGaze;         //0-1 双目余烬辉光

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);

//====== 双向调色板：血湖 ↔ 鬼梦红黑 ======
static const float3 BLOOD_TINT = float3(0.930, 0.300, 0.270);  //血湖镜像乘色
static const float3 BLOOD_FOG  = float3(0.170, 0.024, 0.036);  //湖底血雾
static const float3 BLOOD_FOAM = float3(0.965, 0.520, 0.440);  //缝线血沫
static const float3 DREAM_TINT = float3(0.700, 0.155, 0.125);  //梦境暗红乘色
static const float3 DREAM_FOG  = float3(0.078, 0.011, 0.015);  //梦底黑红雾
static const float3 DREAM_FOAM = float3(0.860, 0.310, 0.180);  //烬红沫
static const float3 HOUND_INK  = float3(0.034, 0.018, 0.024);  //犬体湿墨
static const float3 EMBER_CORE = float3(0.950, 0.340, 0.140);  //目芯
static const float3 EMBER_HALO = float3(0.620, 0.100, 0.060);  //目晕

float4 PSMirror(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;

    //湿斑噪声：慢漂大团 + 反向细纹；nb=沸腾高频翻涌
    float n0 = tex2D(uImage1, float2(uv.x * 2.4 + uTime * 0.021, uTime * 0.012)).r;
    float n1 = tex2D(uImage1, float2(uv.x * 6.8 - uTime * 0.017, 0.37 + uTime * 0.03)).r;
    float nb = tex2D(uImage1, float2(uv.x * 9.5 + uTime * 0.35, uv.y * 3.0 - uTime * 0.22)).r;

    //水位线：吞没上移 + 沸腾多峰鼓包 + 噪声波动（拉入的沸腾比异化更凶）
    float boilSurge = (nb - 0.5) * 0.022 * uBoil;
    float lineWave = (n0 - 0.5) * 1.4 + (n1 - 0.5) * 0.6;
    float waterY = uWaterLevel - uSwallow * 1.3 + boilSurge + lineWave * uWaterWobble;

    float below = uv.y - waterY;
    float mask = saturate(below * 320.0) * uMix;

    //镜像采样：稳态真垂直镜像；翻转期 x 向点反射收敛，θ=π 恒等
    float flip = uRollProgress * uRollProgress * (3.0 - 2.0 * uRollProgress);
    float2 muv = float2(lerp(uv.x, 1.0 - uv.x, flip), 2.0 * uPivotY - uv.y);

    //近水面涟漪扰动 + 沸腾翻滚扰动
    float seamProx = exp2(-abs(below) * 24.0);
    float wob = (n0 - 0.5) * (0.0045 + seamProx * 0.011) + (n1 - 0.5) * 0.0028;
    wob += (nb - 0.5) * (0.008 + seamProx * 0.020) * uBoil;
    muv.x += wob * mask;
    muv.y += ((n1 - 0.5) * 0.0042 + (nb - 0.5) * 0.013 * uBoil) * mask;

    //采样与越界雾
    float2 cuv = clamp(muv, 0.002, 0.998);
    float3 mcol = tex2D(uImage0, cuv).rgb;
    float srcOk = saturate(muv.y * 16.0) * saturate((1.0 - muv.y) * 16.0);

    //双向调色：血湖 ↔ 梦境红黑。梦侧去饱和更重、压得更沉——梦里只剩红与黑
    float grey = dot(mcol, float3(0.30, 0.55, 0.15));
    float3 graded = lerp(mcol, grey.xxx, lerp(0.40, 0.66, uDreamSide));
    graded *= lerp(BLOOD_TINT, DREAM_TINT, uDreamSide);
    float depth = saturate(below * 1.5);
    graded *= 1.0 - depth * lerp(0.30, 0.42, uDreamSide);
    float3 fog = lerp(BLOOD_FOG, DREAM_FOG, uDreamSide);
    graded = lerp(graded, fog, saturate(depth * lerp(0.42, 0.60, uDreamSide) + (1.0 - srcOk)));

    //水面浮渣：沸腾时被打碎搅浓
    float scum = saturate((n0 - 0.56) * 4.0) * exp2(-max(below, 0.0) * 26.0);
    graded *= 1.0 - scum * (0.10 + 0.16 * uFoamBoost + 0.10 * uBoil);

    //镜内烬火：梦侧缓慢上浮的碎红星，替代异化的镜内雨丝
    float ash = tex2D(uImage1, float2(uv.x * 5.6 + uTime * 0.02, uv.y * 1.7 + uTime * 0.16)).r;
    float mote = saturate((ash - 0.74) * 9.0);
    graded += DREAM_FOAM * mote * 0.22 * uDreamSide;

    //沸腾碎泡辉光
    float bead = saturate((nb - 0.72) * 8.0) * exp2(-max(below, 0.0) * 14.0);
    graded += lerp(BLOOD_FOAM, DREAM_FOAM, uDreamSide) * bead * 0.34 * uBoil;

    //异样脉冲：错位残影双重曝光，向暗红压
    float3 gcol = tex2D(uImage0, clamp(muv + float2(0.013, -0.021), 0.002, 0.998)).rgb;
    float gDark = 1.0 - dot(gcol, float3(0.33, 0.34, 0.33));
    float3 haunt = graded * 0.58 + float3(0.50, 0.22, 0.17) * gDark * 0.60;
    graded = lerp(graded, haunt, uGlimpse * 0.6);

    //====== 镜中黑犬：狼帧采样压成湿墨影，替换玩家的镜像 ======
    float2 hl = (uv - uHoundRect.xy) / max(uHoundRect.zw, 0.0001);
    //沸腾中影子在抖
    hl.x += (nb - 0.5) * 0.028 * uBoil;
    float insideH = step(0.0, hl.x) * step(hl.x, 1.0) * step(0.0, hl.y) * step(hl.y, 1.0);
    //倒影垂直翻转：quad 顶是爪线（贴图底行）；水平按面朝翻
    float texU = uHoundUv.x + lerp(hl.x, 1.0 - hl.x, uHoundFlipH) * uHoundUv.z;
    float texV = uHoundUv.y + (1.0 - hl.y) * uHoundUv.w;
    float houndA = tex2D(uImage2, float2(texU, texV)).a * insideH * uHoundA * mask;
    //犬体：湿墨近黑，深处沉入雾底，沸腾时微微翻光
    float3 houndBody = HOUND_INK * (0.85 + nb * 0.5 * uBoil);
    houndBody = lerp(houndBody, fog, saturate(hl.y * 0.5));
    graded = lerp(graded, houndBody, houndA * 0.94);
    //余烬双目：帧内锚点经翻转映到 quad 局部，目晕按 quad 宽高比保圆
    float2 eyeLocal = float2(lerp(uEyeUv.x, 1.0 - uEyeUv.x, uHoundFlipH), 1.0 - uEyeUv.y);
    float2 ed = (hl - eyeLocal) * float2(uHoundAspect, 1.0);
    float eyeCore = exp2(-dot(ed, ed) * 4200.0);
    float eyeHalo = exp2(-dot(ed, ed) * 480.0);
    float breath = 0.86 + 0.14 * sin(uTime * 2.1);
    graded += (EMBER_CORE * eyeCore * 1.3 + EMBER_HALO * eyeHalo * 0.55)
        * uGaze * breath * insideH * uHoundA * mask;

    //调色增益门控，结算后镜像回落为素采样
    float3 mirrorCol = lerp(mcol, graded, uGrade);

    float3 baseCol = tex2D(uImage0, uv).rgb;
    float3 col = lerp(baseCol, mirrorCol, mask);

    //水面泡沫/水膜：贴水位线的一线微光
    float seamBand = exp2(-abs(below) * 150.0);
    float foam = saturate((n1 - 0.35) * 2.2);
    float glintN = tex2D(uImage1, float2(uv.x * 5.0 - uTime * 0.05, 0.77)).r;
    col += lerp(BLOOD_FOAM, DREAM_FOAM, uDreamSide) * seamBand * uSeamGlow * mask
        * (0.26 + 0.32 * glintN + 0.30 * foam * uFoamBoost + 0.40 * nb * uBoil);

    //异样瞬间从施术者荡开的一圈涟漪
    float2 rel = float2((uv.x - uOriginU) * uAspect, below);
    float dist = length(rel);
    float ringR = uGlimpseRing * 1.6;
    float ripple = exp2(-abs(dist - ringR) * 42.0)
        * saturate(uGlimpseRing * 8.0) * saturate((1.0 - uGlimpseRing) * 2.5);
    col += lerp(float3(0.55, 0.30, 0.27), float3(0.62, 0.24, 0.15), uDreamSide)
        * ripple * mask * 0.5;

    return float4(col, 1.0);
}

technique TechMirror
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSMirror();
    }
}
