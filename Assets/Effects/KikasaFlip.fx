//KikasaFlip.fx 血湖领域鬼雨异化翻转的镜面世界合成
//TechMirror: 血湖水线以下为真垂直镜像倒影，沸腾期镜面先行染向目标形态（血↔冷双向 uColdMix）
//            并被沸腾搅动（uBoil：水线鼓包/镜像翻滚扰动/碎泡辉光）；
//            翻转期镜像 x 随 uRollProgress 收敛为点反射，180°翻转∘点反射=恒等，
//            与真实渲染零跳变交接；结算后 uSwallow 把镜面向上吞满全屏，
//            uGrade 让位给已切换的真实氛围。uMix 起手淡入，与领域自带湖面镜面无缝交接。
//直线算术+平 tex2D，无分支；s0=屏幕帧 s1=PerlinNoise

float uTime;         //秒，与 KikasaGrade 同源（EffectTime）
float uPivotY;       //缝线枢轴 uv.y，翻转期间收敛到 0.5
float uRollProgress; //0-1 翻转进度：镜像 x 从垂直镜像收敛为点反射，θ=π 恒等
float uOriginU;      //施术者 uv.x（沸腾隆起与异样涟漪环圆心）
float uAspect;       //宽/高
float uWaterLevel;   //水位线 uv.y（湖已及脚，恒等于枢轴）
float uWaterWobble;  //水位线噪声波动幅度，沸腾期增大
float uFoamBoost;    //0-1 泡沫/浮渣增强
float uSwallow;      //0-1 结算后镜面向上吞没旧形态
float uGrade;        //0-1 镜像侧调色增益，结算后让位真实氛围
float uGlimpse;      //0-1 冷镜异样脉冲
float uGlimpseRing;  //0-1 异样涟漪环扩散进度
float uSeamGlow;     //0-1 水面水膜辉光
float uBoil;         //0-1 沸腾强度
float uColdMix;      //0-1 镜面预览调色：0=血湖材质 1=鬼雨浊水材质
float uMix;          //0-1 合成介入度，起手淡入

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

//====== 双向调色板：血湖 ↔ 鬼雨湿墨（浑浊偏冷）。血侧与 KikasaGrade 的 LAKE_TINT/LAKE_FOG 同值，起手才不跳色 ======
static const float3 BLOOD_TINT = float3(0.820, 0.400, 0.400);  //镜像血染乘色
static const float3 BLOOD_FOG  = float3(0.055, 0.018, 0.040);  //湖底墨雾
static const float3 BLOOD_FOAM = float3(0.965, 0.520, 0.440);  //缝线血沫
static const float3 RAIN_TINT  = float3(0.520, 0.620, 0.640);  //浊水灰青乘色
static const float3 RAIN_FOG   = float3(0.085, 0.108, 0.126);  //冷雨沉雾
static const float3 RAIN_FOAM  = float3(0.400, 0.470, 0.490);  //冷泡沫灰白

float4 PSMirror(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;

    //湿斑噪声：慢漂大团 + 反向细纹；nb=沸腾高频翻涌
    float n0 = tex2D(uImage1, float2(uv.x * 2.4 + uTime * 0.021, uTime * 0.012)).r;
    float n1 = tex2D(uImage1, float2(uv.x * 6.8 - uTime * 0.017, 0.37 + uTime * 0.03)).r;
    float nb = tex2D(uImage1, float2(uv.x * 9.5 + uTime * 0.35, uv.y * 3.0 - uTime * 0.22)).r;

    //水位线：吞没上移 + 沸腾多峰鼓包 + 噪声波动
    float boilSurge = (nb - 0.5) * 0.016 * uBoil;
    float lineWave = (n0 - 0.5) * 1.4 + (n1 - 0.5) * 0.6;
    float waterY = uWaterLevel - uSwallow * 1.3 + boilSurge + lineWave * uWaterWobble;

    //相对水位线的深度（向下为正），水下即镜区；uMix 起手淡入
    float below = uv.y - waterY;
    float mask = saturate(below * 320.0) * uMix;

    //镜像采样：稳态真垂直镜像；翻转期 x 向点反射收敛，θ=π 时 180°翻转∘点反射=恒等。
    //收敛用 smoothstep 陡化，rp≈0.5 的横向坍缩窗口压进峰值角速度+拖影+白闪的遮蔽区
    float flip = uRollProgress * uRollProgress * (3.0 - 2.0 * uRollProgress);
    float2 muv = float2(lerp(uv.x, 1.0 - uv.x, flip), 2.0 * uPivotY - uv.y);

    //近水面涟漪扰动 + 沸腾翻滚扰动（离水面越近越强）
    float seamProx = exp2(-abs(below) * 24.0);
    float wob = (n0 - 0.5) * (0.0045 + seamProx * 0.011) + (n1 - 0.5) * 0.0028;
    wob += (nb - 0.5) * (0.006 + seamProx * 0.016) * uBoil;
    muv.x += wob * mask;
    muv.y += ((n1 - 0.5) * 0.0042 + (nb - 0.5) * 0.010 * uBoil) * mask;

    //采样与越界雾（镜像纵深超出源画面时沉入雾底）
    float2 cuv = clamp(muv, 0.002, 0.998);
    float3 mcol = tex2D(uImage0, cuv).rgb;
    float srcOk = saturate(muv.y * 16.0) * saturate((1.0 - muv.y) * 16.0);

    //双向调色：去饱和量/乘色/纵深压暗/沉雾全按 uColdMix 混合（血湖↔鬼雨浊水）
    float grey = dot(mcol, float3(0.30, 0.55, 0.15));
    float3 graded = lerp(mcol, grey.xxx, lerp(0.45, 0.58, uColdMix));
    graded *= lerp(BLOOD_TINT, RAIN_TINT, uColdMix);
    float depth = saturate(below * 1.5);
    graded *= 1.0 - depth * lerp(0.30, 0.36, uColdMix);
    float3 fog = lerp(BLOOD_FOG, RAIN_FOG, uColdMix);
    graded = lerp(graded, fog, saturate(depth * lerp(0.42, 0.55, uColdMix) + (1.0 - srcOk)));

    //水面浮渣：沸腾时被打碎搅浓
    float scum = saturate((n0 - 0.56) * 4.0) * exp2(-max(below, 0.0) * 26.0);
    graded *= 1.0 - scum * (0.10 + 0.16 * uFoamBoost + 0.08 * uBoil);

    //镜内雨丝已删：水下噪声竖丝叠在红水上读成大块假雨；雨感交给雨帘倒影与天穹雨幡，勿加回

    //沸腾碎泡辉光：贴水下的碎亮点，滚水的光
    float bead = saturate((nb - 0.72) * 8.0) * exp2(-max(below, 0.0) * 14.0);
    graded += lerp(BLOOD_FOAM, RAIN_FOAM, uColdMix) * bead * 0.30 * uBoil;

    //冷镜异样：错位残影双重曝光 + 惨白压色
    float3 gcol = tex2D(uImage0, clamp(muv + float2(0.013, -0.021), 0.002, 0.998)).rgb;
    float gDark = 1.0 - dot(gcol, float3(0.33, 0.34, 0.33));
    float3 haunt = graded * 0.62 + float3(0.52, 0.58, 0.60) * gDark * 0.55;
    graded = lerp(graded, haunt, uGlimpse * 0.6);

    //调色增益门控，结算后镜像逐渐回落为素采样（真实世界已带异化氛围）
    float3 mirrorCol = lerp(mcol, graded, uGrade);

    float3 baseCol = tex2D(uImage0, uv).rgb;
    float3 col = lerp(baseCol, mirrorCol, mask);

    //水面泡沫/水膜：贴水位线的一线微光，沸腾期碎亮增强
    float seamBand = exp2(-abs(below) * 150.0);
    float foam = saturate((n1 - 0.35) * 2.2);
    float glintN = tex2D(uImage1, float2(uv.x * 5.0 - uTime * 0.05, 0.77)).r;
    col += lerp(BLOOD_FOAM, RAIN_FOAM, uColdMix) * seamBand * uSeamGlow * mask
        * (0.26 + 0.32 * glintN + 0.30 * foam * uFoamBoost + 0.35 * nb * uBoil);

    //异样瞬间从施术者荡开的一圈涟漪
    float2 rel = float2((uv.x - uOriginU) * uAspect, below);
    float dist = length(rel);
    float ringR = uGlimpseRing * 1.6;
    float ripple = exp2(-abs(dist - ringR) * 42.0)
        * saturate(uGlimpseRing * 8.0) * saturate((1.0 - uGlimpseRing) * 2.5);
    col += lerp(float3(0.55, 0.30, 0.27), float3(0.38, 0.45, 0.47), uColdMix)
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
