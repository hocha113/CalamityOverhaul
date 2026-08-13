// ============================================================================
//QuestChronicleBg.fx 任务书「远征纪要」——皮革桌板 + 摊开的双页羊皮纸
//AlphaBlend 预乘 alpha 输出;色板由 CPU 传入,与 ChroniclePalette 同源
//材质分野:页眉页脚是皮面(压印线住在这里),内页是纸面(毛边/起皱/斑/页缘吃暗)
//构图纪律:纸面必须安静,全部细节压在 ±0.05 以内,让 CPU 层的金线与墨字站得住
//直线算术,无动态分支,无 tex2Dlod(FNA3D 会静默毁掉整个 effect)
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //整屏尺寸 px
float uBodyTop;      //内页上缘 px
float uBodyBottom;   //内页下缘 px
float uGutterX;      //装订中缝 px
float3 uColLeather;  //皮革基色
float3 uColPaper;    //纸面基色
float3 uColPaperDeep;//页缘吃暗与斑色
float3 uColSeal;     //蜡封绯,只用于皮面压印的一点暖调

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
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.07 + float2(3.1, 7.7);
        a *= 0.5;
    }
    return v;
}

//矩形 SDF,正为外
float rectSDF(float2 p, float2 center, float2 halfSize, float radius) {
    float2 d = abs(p - center) - halfSize + radius;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 P = coords * uResolution;
    float2 uv = coords;
    float t = uTime;

    //==================== 皮革桌板 ====================
    //粗革粒 + 低频云斑,两个尺度叠出"鞣过的整张皮"而非平涂色
    float grain = valueNoise(P * 0.85) * 0.6 + valueNoise(P * 2.3) * 0.4;
    float mottle = fbm3(P * 0.011);
    float3 leather = uColLeather * (0.86 + mottle * 0.34);
    leather += uColLeather * (grain - 0.5) * 0.20;
    //屏角压暗,桌面在视野边缘沉下去
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = saturate(dot(vig * float2(0.62, 0.54), vig * float2(0.62, 0.54)));
    leather *= 1.0 - vigStr * 0.30;

    //盲压线:皮面上烙出来的双线,凹槽=上缘吃暗 + 下唇受光,而不是描一圈边框
    float toolSDF = rectSDF(P, uResolution * 0.5, uResolution * 0.5 - 11.0, 3.0);
    float toolGroove = exp(-toolSDF * toolSDF * 0.55);
    float toolLip = exp(-(toolSDF + 1.6) * (toolSDF + 1.6) * 0.55);
    leather *= 1.0 - toolGroove * 0.42;
    leather += (uColSeal * 0.5 + 0.5 * uColLeather) * toolLip * 0.14;
    //第二道细线,间距 4px,老书封的做法
    float toolSDF2 = rectSDF(P, uResolution * 0.5, uResolution * 0.5 - 16.0, 3.0);
    leather *= 1.0 - exp(-toolSDF2 * toolSDF2 * 1.1) * 0.22;

    //==================== 内页纸张 ====================
    float2 paperMin = float2(14.0, uBodyTop);
    float2 paperMax = float2(uResolution.x - 14.0, uBodyBottom);
    float2 paperCenter = (paperMin + paperMax) * 0.5;
    float2 paperHalf = (paperMax - paperMin) * 0.5;
    float paperSDF = rectSDF(P, paperCenter, paperHalf, 2.0);
    //毛边:手裁纸的边不是直线,低频噪声啃出起伏
    paperSDF += (fbm3(P * 0.045 + float2(t * 0.008, 0.0)) - 0.5) * 3.4;

    //纸落在皮面上的贴身投影,只向右下偏,不做同心放大
    float shadowSDF = rectSDF(P - float2(3.0, 4.0), paperCenter, paperHalf, 2.0);
    float paperShadow = (1.0 - smoothstep(-1.0, 9.0, shadowSDF)) * 0.55;
    leather *= 1.0 - paperShadow;

    float paperMask = 1.0 - smoothstep(-0.8, 1.2, paperSDF);
    float innerDist = max(-paperSDF, 0.0);

    //纸基色:低频斑驳,一张旧纸从不匀色
    float wash = fbm3(P * 0.006 + float2(11.3, 4.7));
    float3 paper = uColPaper * (0.94 + wash * 0.13);

    //起皱:极低频亮暗带,给平面一点厚度
    float cockle = valueNoise(P * 0.0075 + float2(0.0, t * 0.004)) * 0.65
                 + valueNoise(P * 0.019 + float2(t * 0.003, 0.0)) * 0.35;
    paper *= 0.965 + cockle * 0.07;

    //帘纹纤维:横向抄纸痕,幅度必须小于墨字对比度
    float fiber = valueNoise(float2(P.x * 0.30, P.y * 3.1));
    paper += (fiber - 0.5) * 0.035;

    //霉斑:阈值切出的稀疏褐点,不规整才像真的
    float foxSeed = valueNoise(P * 0.075 + float2(21.7, 8.3));
    float fox = smoothstep(0.74, 0.96, foxSeed) * (0.35 + valueNoise(P * 0.5) * 0.65);
    paper = lerp(paper, uColPaperDeep, fox * 0.30);

    //页缘吃暗:纸摊在皮面上,四边总是暗的
    float edgeWash = exp(-innerDist * 0.032);
    paper = lerp(paper, uColPaperDeep * 0.92, edgeWash * 0.42);

    //装订中缝:两页之间的沟,沟底吃暗、两侧纸面各自隆起受光
    float g = abs(P.x - uGutterX);
    float gutter = exp(-g * g * 0.0055);
    float lift = exp(-(g - 26.0) * (g - 26.0) * 0.0022);
    paper *= 1.0 - gutter * 0.46;
    paper *= 1.0 + lift * 0.055;
    //缝里透出下层皮革的暗红
    paper = lerp(paper, uColLeather * 0.7, gutter * 0.35);

    //烛光:一盏暖灯在左上,慢呼吸,幅度极小
    float breath = 0.5 + 0.5 * sin(t * 0.7);
    float2 lightUV = (uv - float2(0.17, -0.06)) * float2(1.0, 1.45);
    float lightFall = 1.0 - smoothstep(0.0, 1.25, length(lightUV));
    float3 warm = float3(1.0, 0.87, 0.66);
    paper += warm * lightFall * (0.055 + breath * 0.018);
    leather += warm * lightFall * (0.030 + breath * 0.010);

    //==================== 合成 ====================
    float3 C = lerp(leather, paper, paperMask);
    //细尘,压住纸面的数字平滑感
    float dust = hash21(P + floor(t * 12.0) * 31.7);
    C *= 1.0 - dust * 0.022;
    C = max(C, 0.0);

    float A = uAlpha;
    return float4(C * A, A) * vertexColor;
}

technique Technique1
{
    pass QuestChroniclePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
