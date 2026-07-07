// ============================================================================
//OniOmokage.fx 面影：里世界的水墨留影挂轴
//纸面几何贴合身形（C# 端 ComputePaperSize），本 shader 负责竖幅挂轴的完整装裱：
//  上下各 uMountPad 高的"天地"暗蓝织锦带，最外 4.5px 为深漆轴棒（带高光线）；
//  中段本纸：和纸底 + 纵向旧化渐变 + 装裱分界墨线 + 侧缘细墨线，左右毛边；
//  拓印：去预乘取真彩亮度 → 三档墨阶 + alpha 梯度勾勒墨线，红色保真（三色纪律），只印在本纸内；
//  朱印：本纸右上角圆章 SDF，印泥噪声侵蚀，随 uSealGlow 呼吸；
//  溶解：噪声侵蚀 alpha，前沿红烬（烧散时最烈）；斩纸头帧裂口白闪。
//几何（整纸/裂开两半）在 C# 端完成，uv 恒为纸面归一坐标。
//极角审计：无 atan2/theta 消费，全部为笛卡尔噪声与线性距离场，无缝隙风险。
//预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;          //秒
float2 uSnapSize;     //快照 RT 像素尺寸
float2 uPaperSize;    //挂轴像素尺寸
float uMountPad;      //天地装裱带高度 px（上下各一段，含轴棒）
float uDissolve;      //0..1 墨晕溶解/烧散进度
float uDevelop;       //0..1 墨迹显影（挂出后短暂浮现）
float uCutFlash;      //0..1 斩纸裂口白闪
float uSeed;          //每幅随机相位
float uSealGlow;      //朱印亮度（呼吸 + 玩家靠近增亮）
float uEmber;         //溶解前沿红烬强度

#define LUMA_W float3(0.299, 0.587, 0.114)

static const float3 WASHI_BASE = float3(0.885, 0.850, 0.755);
static const float3 INK_BLACK = float3(0.085, 0.082, 0.098);
static const float3 INK_MID   = float3(0.400, 0.400, 0.450);
static const float3 INK_PALE  = float3(0.760, 0.742, 0.700);
static const float3 ONI_RED   = float3(0.760, 0.078, 0.092);
static const float3 SEAL_RED  = float3(0.780, 0.095, 0.085);
static const float3 MOUNT_INDIGO = float3(0.150, 0.142, 0.210);
static const float3 ROLLER_LACQUER = float3(0.070, 0.064, 0.082);

texture uSnapTex;
sampler snapSamp = sampler_state
{
    texture = <uSnapTex>;
    magfilter = POINT;
    minfilter = POINT;
    mipfilter = POINT;
    AddressU = clamp;
    AddressV = clamp;
};

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

float noiseTex(float2 uv)
{
    return tex2D(noiseSamp, uv).r;
}

//亮度量化为 3 档墨阶，档间软过渡
float3 inkRamp(float l)
{
    float3 c = INK_BLACK;
    c = lerp(c, INK_MID, smoothstep(0.22, 0.50, l));
    c = lerp(c, INK_PALE, smoothstep(0.55, 0.88, l));
    return c;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 paperLocal = (uv - 0.5) * uPaperSize;
    float2 paperHalf = uPaperSize * 0.5;

    //====== 分区：本纸 / 天地装裱带 / 轴棒 ======
    float absY = abs(paperLocal.y);
    float inMount = step(paperHalf.y - uMountPad, absY);
    float bonshi = 1.0 - inMount;
    float rollerBand = step(paperHalf.y - 4.5, absY);

    //====== 轮廓：本纸侧缘毛边，装裱/轴棒段直缘 ======
    float xDist = paperHalf.x - abs(paperLocal.x);
    float yDist = paperHalf.y - absY;
    float edgeN = noiseTex(uv * 5.7 + uSeed * 13.1);
    float xErode = edgeN * lerp(4.5, 0.8, inMount);
    float paperA = smoothstep(0.0, 2.2, min(xDist - xErode, yDist));

    //====== 本纸：和纸底 + 纵向旧化 ======
    float grain = noiseTex(uv * float2(6.0, 9.0) + uSeed * 3.7);
    float blotch = noiseTex(uv * 1.4 + uSeed * 5.9 + uTime * 0.002);
    float3 col = WASHI_BASE * (0.90 + grain * 0.14) * (0.95 + blotch * 0.07);
    col *= lerp(1.05, 0.90, uv.y);
    //边缘旧渍
    col *= lerp(float3(0.82, 0.74, 0.62), float3(1.0, 1.0, 1.0), smoothstep(0.0, 9.0, min(xDist, yDist)));

    //====== 拓印：墨阶 + 墨线（只印在本纸内）======
    float2 snapUV = paperLocal / uSnapSize + 0.5;
    float inside = step(abs(snapUV.x - 0.5), 0.5) * step(abs(snapUV.y - 0.5), 0.5);

    float4 snap = tex2D(snapSamp, snapUV);
    float snapA = snap.a * inside;
    float3 trueCol = snap.rgb / max(snap.a, 1e-4);

    //墨晕：溶解时亮度被噪声搅浑，档位边缘熔开
    float bleed = noiseTex(uv * 2.1 + uSeed * 7.3);
    float luma = dot(saturate(trueCol), LUMA_W) + (bleed - 0.5) * uDissolve * 0.4;
    float3 ink = inkRamp(saturate(luma));

    //三色纪律：唯一的colour是红
    float redness = trueCol.r - max(trueCol.g, trueCol.b);
    float redMask = smoothstep(0.06, 0.32, redness) * step(0.02, snapA);
    ink = lerp(ink, ONI_RED * (0.55 + luma * 1.1), redMask);

    //墨线：alpha 梯度勾勒轮廓，干笔飞白断续
    float2 px = 1.0 / uSnapSize;
    float aL = tex2D(snapSamp, snapUV - float2(px.x * 1.5, 0)).a;
    float aR = tex2D(snapSamp, snapUV + float2(px.x * 1.5, 0)).a;
    float aT = tex2D(snapSamp, snapUV - float2(0, px.y * 1.5)).a;
    float aB = tex2D(snapSamp, snapUV + float2(0, px.y * 1.5)).a;
    float edge = smoothstep(0.18, 0.62, (abs(aR - aL) + abs(aB - aT))) * inside;
    float dryBrush = 0.62 + 0.38 * noiseTex(uv * 8.5 + uSeed * 11.7);

    float figA = smoothstep(0.10, 0.45, snapA) * uDevelop * bonshi;
    col = lerp(col, ink, figA * 0.92);
    col = lerp(col, INK_BLACK, edge * dryBrush * uDevelop * bonshi * 0.85);

    //====== 天地织锦与轴棒 ======
    float weave = noiseTex(uv * float2(16.0, 24.0) + uSeed * 2.3) * 0.6
                + noiseTex(uv * 6.0 - uSeed * 4.1) * 0.4;
    float3 fabric = MOUNT_INDIGO * (0.80 + weave * 0.40);
    col = lerp(col, fabric, inMount);

    //装裱分界墨线（压在本纸与织锦交界上）
    float sepLine = 1.0 - smoothstep(0.3, 1.6, abs(absY - (paperHalf.y - uMountPad)));
    col = lerp(col, INK_BLACK, sepLine * 0.65);
    //本纸侧缘内衬细墨线
    float sideLine = (1.0 - smoothstep(0.3, 1.4, abs(xDist - 3.5))) * bonshi;
    col = lerp(col, INK_BLACK, sideLine * 0.45);

    //轴棒：深漆 + 高光线
    float rollerT = saturate((absY - (paperHalf.y - 4.5)) / 4.5);
    float spec = exp(-pow((rollerT - 0.42) * 3.0, 2.0));
    float3 lacquer = ROLLER_LACQUER + float3(0.28, 0.24, 0.30) * spec;
    col = lerp(col, lacquer, rollerBand);

    //====== 朱印：本纸右上角圆章 ======
    float2 sealCenter = float2(paperHalf.x - 15.0, -(paperHalf.y - uMountPad) + 16.0);
    float sealDist = length(paperLocal - sealCenter);
    float sealMask = 1.0 - smoothstep(7.0, 8.4, sealDist);
    //印泥不匀
    sealMask *= 0.70 + 0.30 * noiseTex(uv * 9.3 + uSeed * 17.9);
    //中央留白鬼瞳
    float pupil = 1.0 - smoothstep(2.0, 3.0, sealDist);
    sealMask *= 1.0 - pupil * 0.62;
    col = lerp(col, SEAL_RED * uSealGlow, sealMask * 0.92 * bonshi);

    //====== 溶解侵蚀与前沿红烬 ======
    float n = noiseTex(uv * 3.3 + uSeed * 9.1);
    float erode = uDissolve * 1.12;
    float dissolveMask = smoothstep(erode - 0.12, erode, n);
    float emberBand = smoothstep(erode - 0.04, erode, n) * (1.0 - smoothstep(erode, erode + 0.10, n));
    col += ONI_RED * emberBand * (1.4 * uEmber + 0.3);

    //====== 斩纸裂口白闪 ======
    col = lerp(col, float3(1.0, 0.97, 0.92), uCutFlash * 0.85);

    float a = paperA * dissolveMask * 0.94;
    //预乘输出，顶点色承载整体淡出（rgba 同乘）
    return float4(col * a, a) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
