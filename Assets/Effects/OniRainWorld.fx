//OniRainWorld.fx 入雨演出的镜面世界合成
//TechMirror: 阴冷的水从屏幕下方涨起，水下即绕枢轴点反射的世界镜像（点反射而非
//            垂直镜像，翻转180°后与真实渲染恒等）+ 鬼雨湿墨调色 + 水下深度雾 +
//            水面泡沫线/浮渣 + 屏幕空间雨丝 + 近水涟漪扰动；
//            水位锁定缝线后 uSwallow 把边界向上吞满全屏。
//直线算术+平 tex2D，无分支；s0=屏幕帧 s1=PerlinNoise

float uTime;      //秒
float uPivotY;    //缝线枢轴 uv.y，翻转期间收敛到 0.5
float uOriginU;   //伞的 uv.x（水从伞底先漫出，兼异样涟漪环圆心）
float uAspect;    //宽/高
float uFront;     //泡沫辉光横向展开半径（等距空间），C#侧按宽高比算好铺满全屏的上限
float uWaterLevel; //水位线 uv.y：1.15(屏下)涨到 uPivotY，翻转期与枢轴同步收敛
float uWaterWobble;//水位线噪声波动幅度，涨水期大、锁定后静水微澜
float uFoamBoost;  //0-1 涨水期泡沫/浮渣增强，触脚后回落
float uSwallow;   //0-1 结算后水面向上吞没旧世界
float uGrade;      //0-1 镜像侧鬼雨调色增益，结算后让位给真实氛围
float uGlimpse;    //0-1 镜中异样脉冲
float uGlimpseRing;//0-1 异样涟漪环扩散进度
float2 uGhostPos;  //镜中人影中心 uv（玩家倒影旁）
float uSeamGlow;   //0-1 水面水膜辉光

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float4 PSMirror(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;

    //湿斑噪声：慢漂大团 + 反向细纹（水位线波动与后续扰动共用）
    float n0 = tex2D(uImage1, float2(uv.x * 2.4 + uTime * 0.021, uTime * 0.012)).r;
    float n1 = tex2D(uImage1, float2(uv.x * 6.8 - uTime * 0.017, 0.37 + uTime * 0.03)).r;

    //水位线：吞没上移 + 伞侧隆起（水从伞底先漫出）+ 噪声波动
    float bulge = exp2(-abs((uv.x - uOriginU) * uAspect) * 4.5) * 0.020 * uFoamBoost;
    float lineWave = (n0 - 0.5) * 1.4 + (n1 - 0.5) * 0.6;
    float waterY = uWaterLevel - uSwallow * 1.3 - bulge + lineWave * uWaterWobble;

    //相对水位线的深度（向下为正），水下即镜区，几像素软边保水面干脆
    float below = uv.y - waterY;
    float mask = saturate(below * 320.0);

    //镜像采样：绕(0.5, uPivotY)点反射——枢轴恒等契约，与水位无关
    float2 muv = float2(1.0 - uv.x, 2.0 * uPivotY - uv.y);

    //近水面涟漪扰动，离水面越近越强
    float seamProx = exp2(-abs(below) * 24.0);
    float wob = (n0 - 0.5) * (0.0045 + seamProx * 0.011) + (n1 - 0.5) * 0.0028;
    muv.x += wob * mask;
    muv.y += (n1 - 0.5) * 0.0042 * mask;

    //采样与越界雾（镜像纵深超出源画面时沉入雨雾）
    float2 cuv = clamp(muv, 0.002, 0.998);
    float3 mcol = tex2D(uImage0, cuv).rgb;
    float srcOk = saturate(muv.y * 16.0) * saturate((1.0 - muv.y) * 16.0);

    //鬼雨湿墨调色：去饱和→冷灰青→水下纵深压暗→沉雾（深度以水位线起算）
    float grey = dot(mcol, float3(0.30, 0.55, 0.15));
    float3 graded = lerp(mcol, grey.xxx, 0.55);
    graded *= float3(0.60, 0.70, 0.78);
    float depth = saturate(below * 1.5);
    graded *= 1.0 - depth * 0.34;
    float3 fog = float3(0.085, 0.108, 0.126);
    graded = lerp(graded, fog, saturate(depth * 0.38 + (1.0 - srcOk)));

    //水面浮渣：贴着水面漂的暗斑块，涨水期最显
    float scum = saturate((n0 - 0.56) * 4.0) * exp2(-max(below, 0.0) * 26.0);
    graded *= 1.0 - scum * (0.10 + 0.16 * uFoamBoost);

    //水下屏幕空间雨丝：斜向细纹快速下刷
    float rain = tex2D(uImage1, float2(uv.x * 6.5 + uv.y * 0.9, uv.y * 0.45 - uTime * 0.9)).r;
    float streak = saturate((rain - 0.62) * 6.0);
    graded += float3(0.50, 0.57, 0.59) * streak * 0.15;

    //镜中异样：错位残影双重曝光 + 惨白压色
    float3 gcol = tex2D(uImage0, clamp(muv + float2(0.013, -0.021), 0.002, 0.998)).rgb;
    float gDark = 1.0 - dot(gcol, float3(0.33, 0.34, 0.33));
    float3 haunt = graded * 0.62 + float3(0.52, 0.58, 0.60) * gDark * 0.55;
    graded = lerp(graded, haunt, uGlimpse * 0.6);

    //镜中人影：倒影旁多出来的一道湿影，随镜像世界倒悬（头朝屏幕下方），
    //头部远离缝线不会被镜面遮罩裁掉；噪声撕边防光球感
    float2 gvec = float2((uv.x - uGhostPos.x) * uAspect, uv.y - uGhostPos.y);
    float torso = saturate(1.0 - length(gvec * float2(14.0, 7.5)));
    float head = saturate(1.0 - length((gvec - float2(0.0, 0.155)) * float2(20.0, 18.0)));
    float tear = saturate(tex2D(uImage1, uv * 3.7 + float2(0.0, uTime * 0.05)).r * 1.7);
    float figure = saturate(torso * 0.75 + head * 0.95) * tear * uGlimpse;
    graded *= 1.0 - figure * 0.30;
    graded += float3(0.55, 0.61, 0.62) * figure * 0.42;

    //调色增益门控，结算后镜像逐渐回落为素采样（真实世界已带鬼雨氛围）
    float3 mirrorCol = lerp(mcol, graded, uGrade);

    float3 baseCol = tex2D(uImage0, uv).rgb;
    float3 col = lerp(baseCol, mirrorCol, mask);

    //水面泡沫/水膜：贴着水位线的一线灰白微光+雨点涟漪闪烁，
    //辉光从伞侧向两边蔓延（uFront 门控），涨水期泡沫碎亮增强
    float2 rel = float2((uv.x - uOriginU) * uAspect, below);
    float dist = length(rel);
    float seamBand = exp2(-abs(below) * 150.0);
    float seamWave = saturate((uFront * 1.5 - abs(rel.x)) * 8.0);
    float ringGlint = 0.5 + 0.5 * sin(dist * 46.0 - uTime * 5.5);
    float foam = saturate((n1 - 0.35) * 2.2);
    float glow = seamBand * seamWave * uSeamGlow * (1.0 + uFoamBoost * 1.1);
    col += float3(0.40, 0.47, 0.49) * glow
        * (0.30 + 0.30 * n1 + 0.18 * ringGlint + 0.34 * foam * uFoamBoost);

    //异样瞬间从伞荡开的一圈涟漪（异样段水位已锁缝线，dist 与旧几何等价）
    float ringR = uGlimpseRing * uFront;
    float ripple = exp2(-abs(dist - ringR) * 42.0)
        * saturate(uGlimpseRing * 8.0) * saturate((1.0 - uGlimpseRing) * 2.5);
    col += float3(0.38, 0.45, 0.47) * ripple * mask * 0.5;

    return float4(col, 1.0);
}

technique TechMirror
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSMirror();
    }
}
