// ============================================================================
//OniGhostScroll.fx 点鬼簿卷轴纸体——竖挂墨染和纸 + 展卷开合 + 上下绫带 + 帘纹
//AlphaBlend 预乘 alpha 输出;调色板与绯红裂空斩共享(uCol* 由 CPU 传入,保证同源)
//构图纪律:纸面保持墨黑静场护住名录墨字,材质细节全部压低;绫带住在上下沿
//展卷:uReveal 0~1,纸自顶向下展开,前沿一线白热卷光(拔刀语言的竖向变奏)
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;       //纸体不透明度
float2 uResolution; //含 uEdgePad 外扩的画布尺寸
float uEdgePad;
float uReveal;      //0~1 展卷进度(CPU 喂原始值,此处做 easeOut)
float3 uColHot;     //白热
float3 uColBright;  //亮绯红
float3 uColDeep;    //深红
float3 uColDark;    //暗酒红

#define PI 3.14159265

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

float fbm4(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * valueNoise(p);
        p = p * 2.11 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

//基材:暗夜和纸是"暗但有物",不是黑洞(与叙事面板同档)
static const float3 COL_INK  = float3(0.092, 0.046, 0.056);
static const float3 COL_VOID = float3(0.048, 0.024, 0.031);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 pixelPos = coords * uResolution;
    float2 innerMin = float2(uEdgePad, uEdgePad);
    float2 innerMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 innerSize = innerMax - innerMin;
    float2 center = uResolution * 0.5;
    float2 halfSize = innerSize * 0.5;

    //====圆角矩形 SDF + 墨缘侵蚀:轮廓像裁纸刀裁过又被岁月啃过====
    float2 dd = abs(pixelPos - center) - halfSize;
    float cornerR = 5.0;
    float panelSDF = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - cornerR;
    float edgeNoise = fbm4(pixelPos * 0.030 + float2(uTime * 0.010, 0.0));
    panelSDF += (edgeNoise - 0.5) * 4.0;

    if (panelSDF > uEdgePad + 10.0) return float4(0, 0, 0, 0);

    float edgeAlpha = 1.0 - smoothstep(-1.0, 2.0, panelSDF);
    float2 uv = saturate((pixelPos - innerMin) / innerSize);
    float t = uTime;

    //====展卷编舞:自顶向下,前沿卷光====
    float unroll = saturate(uReveal);
    unroll = unroll * (2.0 - unroll); //easeOut
    float frontierY = innerMin.y + unroll * (innerSize.y + uEdgePad + 8.0);
    float openMask = 1.0 - smoothstep(frontierY - 2.0, frontierY + 2.0, pixelPos.y);

    //====纸体:墨染和纸====
    //纵向:顶沿吃一线绫光,中下段沉入最深
    float3 bg = lerp(COL_INK, COL_VOID, smoothstep(0.0, 0.62, uv.y));
    bg += uColDark * (1.0 - uv.y) * 0.16;
    bg += uColDark * pow(uv.y, 3.0) * 0.18;

    //名录静默罩:中央竖带去细节去运动,护住墨字
    float hCenter = abs(uv.x - 0.5) * 2.0;
    float textMask = 1.0 - smoothstep(0.18, 0.72, hCenter);

    //纸纤维颗粒
    float grain = valueNoise(pixelPos * 0.9) * 0.55 + valueNoise(pixelPos * 0.23) * 0.45;
    bg += (grain - 0.5) * 0.05 * lerp(1.0, 0.55, textMask);

    //帘纹:横向抄纸痕,极淡,颗粒噪声打散避免机械感
    float laid = sin(pixelPos.y * 1.35 + valueNoise(pixelPos * 0.05) * 2.6);
    bg += uColDark * laid * 0.016 * lerp(1.0, 0.4, textMask);

    //洇墨:低频云状斑驳,宣纸吸墨不均
    float2 washUV = uv * float2(2.2, 3.4) + float2(t * 0.006, -t * 0.004);
    float inkWash = valueNoise(washUV) * 0.62 + valueNoise(washUV * 2.3 + 5.1) * 0.38;
    bg += uColDark * (inkWash - 0.38) * 0.20 * lerp(1.0, 0.5, textMask);

    bg *= lerp(1.0, 0.94, textMask);

    //边缘吃墨:靠近裁边处纸面向暗酒红晕开
    float innerDist = max(-panelSDF, 0.0);
    float wash = exp(-innerDist * 0.06);
    bg = lerp(bg, uColDark * 0.55, wash * 0.32);

    //====上下绫带:装裱的织锦横带,斜纹织痕 + 深红渐变====
    float bandH = 15.0;
    float topBand = 1.0 - smoothstep(bandH - 2.0, bandH + 2.0, pixelPos.y - innerMin.y);
    float botBand = 1.0 - smoothstep(bandH - 2.0, bandH + 2.0, innerMax.y - pixelPos.y);
    float band = max(topBand, botBand);
    if (band > 0.003) {
        float weave = valueNoise(float2(pixelPos.x + pixelPos.y, pixelPos.x - pixelPos.y) * 0.22);
        float3 bandCol = lerp(uColDark * 1.5, uColDeep, 0.45 + weave * 0.5);
        //绫带内缘一线亮红镶边
        float hem = topBand * (1.0 - smoothstep(0.0, 2.4, abs(pixelPos.y - innerMin.y - bandH)))
                  + botBand * (1.0 - smoothstep(0.0, 2.4, abs(innerMax.y - pixelPos.y - bandH)));
        bandCol += uColBright * hem * 0.30;
        bg = lerp(bg, bandCol, band * 0.85);
    }

    //暗角 + 细尘
    float2 vig = uv * 2.0 - 1.0;
    float vigStr = dot(vig * float2(0.52, 0.44), vig * float2(0.52, 0.44));
    bg *= saturate(1.0 - vigStr) * 0.14 + 0.86;
    float dust = hash21(pixelPos + t * 25.0) * 0.04;
    bg *= 1.0 - dust * 0.5;
    bg = max(bg, 0.0);

    //====墨笔框线:框线本体在 SDF=0,四角晕开绯红====
    float rimLine = exp(-panelSDF * panelSDF * 0.32);
    float2 cornerD = halfSize - abs(pixelPos - center);
    float cornerNear = exp(-(cornerD.x + cornerD.y) * 0.045);
    float3 rimCol = lerp(uColDark * 1.7, uColBright, cornerNear * 0.80);
    float rimA = rimLine * (0.40 + cornerNear * 0.45);

    //====展卷前沿卷光:一线白热横光,纸未展尽时游走在前沿====
    float cutStrength = 1.0 - smoothstep(0.72, 0.98, uReveal);
    float dyF = abs(pixelPos.y - frontierY);
    float frontGlow = exp(-dyF * dyF * 0.10);
    float frontCore = exp(-dyF * dyF * 0.55);
    //横向须在纸幅内
    float inX = step(innerMin.x - uEdgePad * 0.5, pixelPos.x) * step(pixelPos.x, innerMax.x + uEdgePad * 0.5);
    float cutA = saturate(frontGlow * 0.5 + frontCore * 0.8) * cutStrength * inX;
    cutA *= saturate(uReveal * 8.0);
    float3 cutCol = lerp(uColBright, uColHot, frontCore);

    //====预乘 over 合成(后→前:纸体→框线→卷光)====
    float bodyA = edgeAlpha * openMask * uAlpha;
    rimA *= openMask * uAlpha;

    float3 C = bg * bodyA;
    float A = bodyA;
    C = rimCol * rimA + C * (1.0 - rimA);
    A = rimA + A * (1.0 - rimA);
    C = cutCol * cutA + C * (1.0 - cutA);
    A = cutA + A * (1.0 - cutA);

    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass OniGhostScrollPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
