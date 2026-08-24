// ============================================================================
//KikasaTalisman.fx 唤雨符纸，噪声阈值雨浸:纸自下缘被雨水浸润,
//浸线沿噪声轮廓爬行,浸透处纸色沉深偏墨且微微透光,
//墨晕顺纸纤维自浸线向上洇爬,浸线上一线水光,浸区内湿光慢漂
//quad 即符纸本体:含和纸纤维底、上折角、双侧压边,浸到哪儿纸就湿到哪儿
//AlphaBlend 预乘 alpha 输出;全程序化噪声、直线算术无动态分支
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uSoak;          //0~1 雨浸量(纸条自下而上被浸润的比例上限)
float2 uSize;         //符纸像素尺寸(纤维/边线按物理像素算)
float3 uColPaper;     //纸白
float3 uColHem;       //压边深红(血湖系)
float3 uColInk;       //浸墨深色
float3 uColSheen;     //水光冷青

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
    col = lerp(col, uColHem, hem * 0.55);

    //====雨浸:阈值浸润,浸线沿噪声轮廓爬====
    //soakNeed:该像素被浸到所需的雨浸进度;底缘最先湿,噪声揉出参差
    float n = fbm3(float2(px.x * 0.10, px.y * 0.15) + float2(0.0, uTime * 0.04));
    float soakNeed = (1.0 - uv.y) + (n - 0.5) * 0.20;
    float tide = uSoak * (0.95 + 0.05 * sin(uTime * 1.1)); //潮在呼吸

    //浸透区:纸色沉深偏墨,微微透光
    float wet = 1.0 - smoothstep(tide - 0.030, tide + 0.045, soakNeed);
    float3 wetCol = lerp(col * 0.70, uColInk, 0.50);
    col = lerp(col, wetCol, wet * 0.88);

    //墨晕洇爬:浸线之上顺纤维的纵向须状渗透(噪声沿 x 变化快、沿 y 拉长)
    float fiber = valueNoise(float2(px.x * 0.85, px.y * 0.12) + 3.3);
    float bleedBand = 1.0 - smoothstep(tide, tide + 0.15, soakNeed);
    float bleed = saturate((bleedBand - wet) * (0.25 + fiber * 1.05));
    col = lerp(col, uColInk, bleed * 0.38);

    //浸线水光:一线冷青玻璃光,随雨点节律微闪
    float lineW = abs(soakNeed - tide);
    float glint = 0.60 + 0.40 * sin(uTime * 2.6 + px.x * 0.33);
    float sheen = exp(-lineW * lineW * 1500.0) * glint * step(0.015, uSoak);
    col += uColSheen * sheen * 0.34;

    //浸区湿光:水面反光似的高光带在湿纸上慢漂(fbm 破方块格,窄阈成丝)
    float gloss = fbm3(float2(px.x * 0.12, px.y * 0.05 - uTime * 0.35));
    col += uColSheen * wet * smoothstep(0.62, 0.80, gloss) * 0.07;

    //湿纸微透:浸透处 alpha 略降
    float A = 1.0 - wet * 0.12;

    return float4(col * A, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass KikasaTalismanPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
