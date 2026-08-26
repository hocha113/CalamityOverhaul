// ============================================================================
//SHPCItemPanel.fx SHPC介绍面板背景，枪匠数据鉴:切角硬面 + 数据网格 + 能量扫线
//AlphaBlend 预乘 alpha 输出;色板内置,对齐 SHPCTheme(青)与 SHPCPanelState(霓虹紫副色)
//身份=数据硬面(快、直线、切角):chamfer 八边轮廓零噪声蚀边——机加工准直,
//四面板唯一直线轮廓;边带点阵网格 + 顶沿快速扫描头 + 角部检修括号刻线
//构图纪律:tooltip 满铺文字,网格/扫线只住边带,护字罩内纯净
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;       //面板体不透明度(近 1,要盖得住原生蓝框)
float2 uResolution; //含 edgePad 外扩的画布尺寸
float uEdgePad;

#define PI 3.14159265

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//色板(SHPCTheme/SHPCModPanel 同源 + 霓虹紫副色)
//基材亮度抬到可见档:硬面是"暗但有物",不是黑洞
static const float3 COL_BG_HI  = float3(0.022, 0.060, 0.086); //深青蓝上
static const float3 COL_BG_LO  = float3(0.010, 0.032, 0.050); //深青蓝下
static const float3 COL_CYAN   = float3(0.337, 0.863, 0.941); //SHPCTheme.Cyan
static const float3 COL_CYANHI = float3(0.667, 0.961, 1.000); //SHPCTheme.CyanHi
static const float3 COL_PURPLE = float3(0.392, 0.157, 0.784); //霓虹紫(SHPCPanelState.DeepPurple)
static const float3 COL_GRID   = float3(0.100, 0.400, 0.550); //数据网格点

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //====切角八边轮廓 SDF:box 与 45° 对角半平面取交,零噪声蚀边(机加工准直)====
    float2 q = abs(pixelPos - center);
    float2 bd = q - halfSize;
    float boxSDF = length(max(bd, 0.0)) + min(max(bd.x, bd.y), 0.0);
    float chamfer = 16.0;
    float cutSDF = (bd.x + bd.y + chamfer) * 0.70710678;
    float panelSDF = max(boxSDF, cutSDF);

    if (panelSDF > uEdgePad + 4.0) return float4(0, 0, 0, 0);

    //1px 抗锯齿硬边
    float edgeAlpha = 1.0 - smoothstep(-0.5, 1.0, panelSDF);
    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime;

    //====面板体:深青蓝竖直渐变 + 低频雾化====
    float3 bg = lerp(COL_BG_HI, COL_BG_LO, uv.y);
    float fog = vnoise(uv * 3.0 + t * 0.05);
    bg *= 0.88 + fog * 0.22;

    //护字静默罩:离边框带越深越安静
    float calm = 1.0 - smoothstep(-30.0, -10.0, panelSDF);
    float edgeBand = 1.0 - calm; //边带权重

    //====数据网格点阵:只住边带====
    float2 dotUV = pixelPos / 7.0;
    float2 dotF = frac(dotUV);
    float dots = step(0.80, dotF.x) * step(0.80, dotF.y);
    bg += COL_GRID * dots * 0.22 * edgeBand;

    //边带方格细线(更低频)
    float2 grid = pixelPos / 34.0;
    float2 gf = abs(frac(grid) - 0.5);
    bg += COL_GRID * step(0.47, max(gf.x, gf.y)) * 0.10 * edgeBand;

    //====顶沿快速能量扫描头:亮块沿顶缘往返,拖一条余辉====
    float sweepPh = frac(t * 0.45);
    //三角往返 0→1→0
    float sweepX = abs(sweepPh * 2.0 - 1.0);
    float headX = innerMin.x + sweepX * innerSize.x;
    float topBand = 1.0 - smoothstep(innerMin.y - 1.0, innerMin.y + 7.0, pixelPos.y);
    float dHead = pixelPos.x - headX;
    //运动方向拖尾:往右扫尾在左,往左扫尾在右
    float dir = sweepPh < 0.5 ? 1.0 : -1.0;
    float tail = exp(-abs(dHead) * (dHead * dir < 0.0 ? 0.030 : 0.35));
    bg += COL_CYAN * tail * topBand * 0.55;
    bg += COL_CYANHI * exp(-dHead * dHead * 0.08) * topBand * 0.5;

    //====底沿数据流:一行细碎亮点缓慢右移(机器心跳)====
    float botBand = smoothstep(innerMax.y - 7.0, innerMax.y - 1.0, pixelPos.y)
        * (1.0 - smoothstep(innerMax.y, innerMax.y + 1.0, pixelPos.y));
    float dataBit = step(0.72, vnoise(float2(pixelPos.x * 0.22 - t * 14.0, 3.7)));
    bg += COL_CYAN * dataBit * botBand * 0.30;
    bg = max(bg, 0.0);

    //====框线:1px 硬边,青→紫沿纵向渐变,切角段提亮====
    float rimLine = exp(-panelSDF * panelSDF * 0.55);
    float3 rimCol = lerp(COL_CYAN, COL_PURPLE, uv.y * 0.85);
    //切角斜段:cutSDF 主导处(角部)提亮为 CyanHi
    float onChamfer = smoothstep(-1.5, 0.5, cutSDF - boxSDF);
    rimCol = lerp(rimCol, COL_CYANHI, onChamfer * 0.55);
    float rimA = rimLine * (0.55 + onChamfer * 0.25);

    //====角部检修括号:内缩 6px 的等距刻线,只在四角附近====
    float2 cornerD = halfSize - q;
    float cornerNear = exp(-min(cornerD.x, cornerD.y) * 0.010) * exp(-(cornerD.x + cornerD.y) * 0.020);
    float bracket = exp(-pow(abs(panelSDF + 6.0), 2.0) * 1.2) * smoothstep(0.02, 0.30, cornerNear);
    float3 bracketCol = lerp(COL_CYAN, COL_PURPLE, uv.y * 0.6);
    float bracketA = bracket * 0.6;

    //====预乘 over 合成====
    float bodyA = edgeAlpha * uAlpha;
    rimA *= uAlpha;
    bracketA *= uAlpha;

    float3 C = bg * bodyA;
    float A = bodyA;
    C = bracketCol * bracketA + C * (1.0 - bracketA);
    A = bracketA + A * (1.0 - bracketA);
    C = rimCol * rimA + C * (1.0 - rimA);
    A = rimA + A * (1.0 - rimA);

    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass SHPCItemPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
