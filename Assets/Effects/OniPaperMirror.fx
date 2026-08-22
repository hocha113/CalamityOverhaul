// ============================================================================
//OniPaperMirror.fx 鏡樋「镜写」：疾走终点立起的那面纸镜
//
//MirrorTech：纸裱的立镜，不是一块半透明玻璃板。
//  1) 三层结构：外框和纸(有纤维)→内嵌镜面→框内一圈压暗的合缝，
//     靠层次立住"这是个物件"，不靠整块发光；
//  2) 镜面不是纯色：极低频的水银不匀 + 一道斜掠高光带，
//     高光带随 uTime 极缓移动，读作"面在反光"而不是贴了张灰纸；
//  3) 立牌不是矩形贴图：外缘按噪声轻微起伏，底部收窄成插地的楔，
//     顶部两角略圆，纸裱的边不会是数学直角；
//  4) uRise 0→1 展开：先横向压扁成一条线再翻正，读作"从纸里立起来"；
//  5) uShatter 0→1 碎裂：按噪声把面切成不规则碎片各自外移并转，
//     碎的是镜面，纸框先裂后散。
//
//SheenTech：复刻那一刀落下时，镜面上顺着刀线扫过的一道冷白。
//  与镜身分开画，好压在镜中人之上。
//
//极角审计：本文件无 atan2/极角，全部为 quad uv 的笛卡尔坐标，无接缝风险。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uSeed;        //实例随机相位
float uRise;        //0..1 立起进度
float uShatter;     //0..1 碎裂进度
float uSheen;       //0..1 复刻那一刀的扫光进度(SheenTech)
float uOpacity;

float3 uColPaper;   //纸框和纸色
float3 uColGlass;   //镜面冷白
float3 uColDeep;    //合缝/背影暗部
float3 uColRim;     //绯红朱线

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

#define PI 3.14159265

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

//圆角矩形 SDF：负值在内
float RoundBox(float2 p, float2 b, float r)
{
    float2 q = abs(p) - b + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

float4 MirrorPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //立起：先横向压成一条线再翻正
    float rise = saturate(uRise);
    float openX = lerp(0.10, 1.0, rise * rise);
    p.x /= max(openX, 0.02);
    if (abs(p.x) > 1.4)
        return float4(0, 0, 0, 0);

    //碎裂：把面按格子切成碎片，各自朝外平移+转，碎的是镜面
    float shatter = saturate(uShatter);
    float2 cell = floor(p * 2.6 + uSeed * 7.0);
    float shardA = tex2D(noiseSamp, cell * 0.17 + 0.5).r;
    float shardB = tex2D(noiseSamp, cell * 0.17 + float2(0.31, 0.77)).r;
    float2 fling = (float2(shardA, shardB) - 0.5) * 2.0;
    //碎片外移量随进度加速，读作"崩开"而不是"淡出"
    p += fling * shatter * shatter * 1.15 + normalize(p + 1e-4) * shatter * 0.30;

    //立牌轮廓：底部收窄成插地的楔，外缘噪声起伏
    float wedge = 1.0 - saturate((p.y - 0.42) / 0.58) * 0.34;
    float edgeN = (tex2D(noiseSamp, float2(p.x * 0.7 + uSeed, p.y * 0.7)).r - 0.5) * 0.030;
    float outer = RoundBox(float2(p.x / max(wedge, 0.2), p.y), float2(0.86, 0.94), 0.16) + edgeN;
    float card = 1.0 - smoothstep(0.0, 0.035, outer);
    if (card <= 0.004)
        return float4(0, 0, 0, 0);

    //纸框纤维：和纸的经纬，只调明度不改形
    float fiber = tex2D(noiseSamp, float2(p.x * 2.1, p.y * 9.0) + uSeed).r;
    fiber = 0.88 + fiber * 0.24;

    //内嵌镜面
    float inner = RoundBox(float2(p.x / max(wedge, 0.2), p.y + 0.03), float2(0.62, 0.70), 0.10);
    float glass = 1.0 - smoothstep(0.0, 0.030, inner);
    //合缝：框与面之间压一圈暗，层次靠它立住
    float seam = saturate(1.0 - abs(inner + 0.045) * 26.0);

    //水银不匀 + 斜掠高光带（缓慢移动）
    float mercury = tex2D(noiseSamp, p * 0.55 + float2(uSeed * 2.0, uTime * 0.010)).r;
    float sweep = saturate(1.0 - abs((p.x * 0.72 + p.y * 0.70) - (sin(uTime * 0.35 + uSeed * 6.28) * 0.55)) * 2.6);

    float3 col = uColPaper * fiber;
    col = lerp(col, uColGlass * (0.80 + mercury * 0.34), glass);
    col = lerp(col, uColGlass, saturate(sweep * glass * 0.45));
    col = lerp(col, uColDeep, saturate(seam * 0.85));
    //朱线：框内一线绯红，把它认成鬼切的物件而不是通用镜子
    float vermilion = saturate(1.0 - abs(outer + 0.075) * 30.0);
    col = lerp(col, uColRim, saturate(vermilion * 0.60));

    //碎裂末段整体退色，碎片走远即散
    float alpha = card * uOpacity * input.Color.a * (1.0 - smoothstep(0.55, 1.0, shatter));
    if (alpha <= 0.004)
        return float4(0, 0, 0, 0);
    return float4(col * alpha, alpha);
}

//复刻扫光：一道冷白顺刀线扫过镜面
float4 SheenPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float sheen = saturate(uSheen);
    //沿 +x 推进的窄带，前缘锐后缘拖尾
    float front = sheen * 2.6 - 1.3;
    float dist = p.x - front;
    float band = saturate(1.0 - abs(dist) * 5.0) * saturate(1.0 - dist * 2.2);
    //纵向收口，别铺满整块
    band *= 1.0 - smoothstep(0.72, 1.0, abs(p.y));
    //生命包络：起落都快
    band *= sin(saturate(sheen) * PI);
    if (band <= 0.004)
        return float4(0, 0, 0, 0);

    float3 col = lerp(uColGlass, uColRim, saturate(1.0 - band) * 0.35);
    float alpha = band * uOpacity * input.Color.a * 0.85;
    return float4(col * alpha, alpha);
}

technique MirrorTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 MirrorPS();
    }
}

technique SheenTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 SheenPS();
    }
}
