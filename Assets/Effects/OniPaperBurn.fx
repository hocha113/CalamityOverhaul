// ============================================================================
//OniPaperBurn.fx 封印札焚烧，噪声阈值溶解:纸自下缘被火焰舔穿,
//烧穿处透空,燃线沿噪声轮廓爬行(暗红至橙黄),线内侧一圈炭黑焦痕
//quad 即纸条本体:含和纸纤维底、上折角、双侧压边,烧到哪儿纸就没到哪儿
//AlphaBlend 预乘 alpha 输出
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uBurn;          //0~1 焚烧量(纸条自下而上被烧去的比例上限)
float2 uSize;         //纸条像素尺寸(纤维/边线按物理像素算)
float3 uColPaper;     //纸白
float3 uColEdge;      //压边深红
float3 uColChar;      //炭黑
float3 uColFireDim;   //火焰暗红外缘
float3 uColFireHot;   //火焰橙黄高温芯

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
        p = p * 2.13 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 px = uv * uSize;

    //====纸体:纤维颗粒 + 纵向三段明暗(顶承光) + 上折角 + 双侧压边====
    float grain = valueNoise(px * 0.8) * 0.6 + valueNoise(px * 0.31 + 7.7) * 0.4;
    float lit = lerp(1.06, 0.86, smoothstep(0.0, 1.0, uv.y));
    float3 col = uColPaper * lit * (0.94 + (grain - 0.5) * 0.10);

    //上折角:顶端一条压暗横带
    float fold = 1.0 - smoothstep(2.0, 6.5, px.y);
    col = lerp(col, uColPaper * 0.62, fold * 0.55);

    //双侧压边:边缘 1.5px 深红
    float edgeDist = min(px.x, uSize.x - px.x);
    float hem = 1.0 - smoothstep(0.8, 2.2, edgeDist);
    col = lerp(col, uColEdge, hem * 0.55);

    float A = 1.0;

    //====焚烧:阈值溶解,烧线沿噪声轮廓爬====
    //burnFront:该像素被烧掉所需的焚烧进度;底缘最先烧,噪声揉出参差
    //参差幅度收窄:燃缘是"舔过"的痕迹,不许一口吞掉半张札
    float n = fbm3(float2(px.x * 0.11, px.y * 0.16) + float2(0.0, uTime * 0.06));
    float burnFront = (1.0 - uv.y) + (n - 0.5) * 0.22;
    float burn = uBurn * (0.92 + 0.08 * sin(uTime * 2.3)); //火在呼吸

    //烧穿(透空)
    float burned = smoothstep(burn - 0.020, burn + 0.020, burnFront);
    //炭黑焦痕带
    float charBand = smoothstep(burn + 0.015, burn + 0.10, burnFront);
    col = lerp(uColChar, col, charBand);
    //焦痕外再一圈微褐过渡
    float scorch = smoothstep(burn + 0.10, burn + 0.24, burnFront);
    col = lerp(col * 0.72, col, scorch);

    //燃线:烧穿边界上的暖色火焰,由暗红外缘渐变至橙黄高温芯
    float flick = 0.72 + 0.28 * sin(uTime * 6.1 + px.x * 0.35);
    float rimW = abs(burnFront - burn);
    float rim = exp(-rimW * rimW * 900.0) * flick;
    float rimCore = exp(-rimW * rimW * 3600.0) * flick;
    //用归一化芯宽选择色温,再覆盖到炭边；避免两层加色把通道夹成银白
    float heat = saturate(rimCore / max(rim, 0.001));
    float flameMask = saturate(rim * 0.82 + rimCore * 0.35);
    float3 flameCol = lerp(uColFireDim, uColFireHot, heat);
    col = lerp(col, flameCol, flameMask);

    A *= burned;
    A = max(A, saturate(rim * 0.8 + rimCore) * step(0.001, uBurn));

    return float4(col * A, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniPaperBurnPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
