//============================================================================
//OniOmokage.fx 面影和纸拓影
//快照 alpha 生成纸偶轮廓，外扩仅保留 2-4px 纤维毛边
//预乘 alpha 输出，配合 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
//============================================================================

float4x4 transformMatrix;
float uTime;          //秒
float2 uSnapSize;     //快照 RT 像素尺寸
float2 uPaperSize;    //纸偶绘制尺寸
float uDissolve;      //0-1 纤维溶解
float uDevelop;       //0-1 整体显影
float uCutFlash;      //0-1 落刀闪白
float uSeed;          //个体相位
float uSealGlow;      //朱印显色强度
float uEmber;         //0-1 焦边余烬

#define LUMA_W float3(0.299, 0.587, 0.114)

static const float3 WASHI_BASE = float3(0.885, 0.850, 0.755);
static const float3 WASHI_EDGE = float3(0.655, 0.570, 0.455);
static const float3 INK_BLACK = float3(0.070, 0.066, 0.078);
static const float3 INK_MID   = float3(0.350, 0.350, 0.390);
static const float3 INK_PALE  = float3(0.720, 0.700, 0.650);
static const float3 ONI_RED   = float3(0.760, 0.078, 0.092);
static const float3 SEAL_RED  = float3(0.680, 0.060, 0.060);

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

PSInput VertexShaderFunction(VSInput input)
{
    PSInput output;
    output.Position = mul(input.Position, transformMatrix);
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    return output;
}

float noiseTex(float2 uv)
{
    return tex2D(noiseSamp, uv).r;
}

float snapAlpha(float2 uv)
{
    return tex2D(snapSamp, uv).a;
}

float3 inkRamp(float luminance)
{
    float3 ink = INK_BLACK;
    ink = lerp(ink, INK_MID, smoothstep(0.24, 0.46, luminance));
    ink = lerp(ink, INK_PALE, smoothstep(0.58, 0.82, luminance));
    return ink;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 safeSnapSize = max(uSnapSize, float2(1.0, 1.0));
    float2 safePaperSize = max(uPaperSize, float2(1.0, 1.0));
    float2 paperLocal = (uv - 0.5) * safePaperSize;
    float2 snapUV = paperLocal / safeSnapSize + 0.5;
    float insideSnap = step(abs(snapUV.x - 0.5), 0.5)
        * step(abs(snapUV.y - 0.5), 0.5);

    float4 snap = tex2D(snapSamp, snapUV) * insideSnap;
    float snapA = snap.a;
    float2 texel = 1.0 / safeSnapSize;

    //快照边缘与 2-4px 纸纤维外扩
    float edgeNoise = noiseTex(snapUV * float2(5.7, 7.9) + uSeed * 13.1);
    float radius = lerp(2.0, 4.0, edgeNoise);
    float2 oneX = float2(texel.x * 1.25, 0.0);
    float2 oneY = float2(0.0, texel.y * 1.25);
    float aL = snapAlpha(snapUV - oneX);
    float aR = snapAlpha(snapUV + oneX);
    float aT = snapAlpha(snapUV - oneY);
    float aB = snapAlpha(snapUV + oneY);

    float2 farX = float2(texel.x * radius, 0.0);
    float2 farY = float2(0.0, texel.y * radius);
    float2 farD = texel * (radius * 0.70710678);
    float farA = max(max(snapAlpha(snapUV - farX), snapAlpha(snapUV + farX)),
        max(snapAlpha(snapUV - farY), snapAlpha(snapUV + farY)));
    farA = max(farA, max(max(snapAlpha(snapUV - farD), snapAlpha(snapUV + farD)),
        max(snapAlpha(snapUV + float2(farD.x, -farD.y)),
            snapAlpha(snapUV + float2(-farD.x, farD.y)))));

    float nearA = max(max(aL, aR), max(aT, aB));
    float core = smoothstep(0.07, 0.42, snapA);
    float dilationField = max(nearA * 0.92, farA * (0.68 + edgeNoise * 0.18));
    float expanded = smoothstep(0.14 + (1.0 - edgeNoise) * 0.06, 0.45, dilationField);
    float paperShape = max(core, expanded) * insideSnap;
    float fringe = saturate(paperShape - core);

    //暖和纸底与纤维
    float grain = noiseTex(snapUV * float2(7.0, 10.0) + uSeed * 3.7);
    float fiber = noiseTex(snapUV * float2(31.0, 4.2) + float2(uSeed * 5.9, uSeed * 2.1));
    float blotch = noiseTex(snapUV * 1.6 + uSeed * 7.3);
    float3 color = WASHI_BASE * (0.91 + grain * 0.10 + fiber * 0.035);
    color *= 0.94 + blotch * 0.08;
    color *= lerp(1.025, 0.925, saturate(snapUV.y));
    color = lerp(color, WASHI_EDGE * (0.88 + grain * 0.12),
        fringe * (0.52 + edgeNoise * 0.22));

    //三阶墨色与鬼切红
    float3 trueColor = snap.rgb / max(snapA, 1e-4);
    float bleed = noiseTex(snapUV * 2.3 + uSeed * 9.1);
    float luminance = dot(saturate(trueColor), LUMA_W) + (bleed - 0.5) * 0.11;
    float3 ink = inkRamp(saturate(luminance));
    float figure = smoothstep(0.09, 0.40, snapA);
    color = lerp(color, ink, figure * 0.91);

    float redness = trueColor.r - max(trueColor.g, trueColor.b);
    float redMask = smoothstep(0.055, 0.30, redness) * figure;
    color = lerp(color, ONI_RED * (0.52 + luminance * 0.72), redMask * 0.92);

    //轮廓内钥线与中折痕
    float innerMin = min(min(aL, aR), min(aT, aB));
    float alphaGradient = max(abs(aR - aL), abs(aB - aT));
    float keyline = smoothstep(0.08, 0.34,
        max(alphaGradient, saturate(snapA - innerMin))) * core;
    color = lerp(color, INK_BLACK, keyline * (0.78 + grain * 0.14));

    float creaseDist = abs(paperLocal.x + (fiber - 0.5) * 1.2);
    float crease = (1.0 - smoothstep(0.55, 2.25, creaseDist)) * figure;
    color *= 1.0 - crease * 0.065;

    //残缺朱印，始终受纸偶剪影裁切
    float markScale = clamp(min(safePaperSize.x, safePaperSize.y) * 0.13, 6.0, 24.0);
    float2 markCenter = float2(markScale * 1.15, markScale * 0.75);
    float2 markP = (paperLocal - markCenter) / markScale;
    float markRadius = length(markP);
    float sealRing = (1.0 - smoothstep(0.88, 1.03, markRadius))
        * smoothstep(0.57, 0.72, markRadius);
    float sealStrokeA = (1.0 - smoothstep(0.065, 0.15, abs(markP.x + markP.y * 0.42)))
        * (1.0 - smoothstep(0.52, 0.76, abs(markP.y)));
    float sealStrokeB = (1.0 - smoothstep(0.060, 0.14, abs(markP.y - markP.x * 0.28)))
        * (1.0 - smoothstep(0.38, 0.62, abs(markP.x)));
    float sealGap = smoothstep(0.16, 0.37, length(markP - float2(-0.70, -0.58)));
    float sealNoise = noiseTex(snapUV * 13.7 + uSeed * 17.9);
    float seal = saturate(sealRing + sealStrokeA * 0.72 + sealStrokeB * 0.60)
        * sealGap * lerp(0.50, 1.0, smoothstep(0.28, 0.62, sealNoise)) * figure;
    float sealStrength = seal * (0.38 + saturate(uSealGlow) * 0.34);
    color = lerp(color, SEAL_RED * (0.70 + saturate(uSealGlow) * 0.16), sealStrength);

    //显影直接裁切完整纸偶，不先显示空纸
    float developNoise = noiseTex(snapUV * float2(4.1, 5.8) + uSeed * 4.3);
    float developFront = lerp(-0.18, 1.18, saturate(uDevelop));
    float developField = snapUV.y + (developNoise - 0.5) * 0.20;
    float developed = smoothstep(developField - 0.075, developField + 0.075, developFront)
        * smoothstep(0.001, 0.035, uDevelop);

    //纤维溶解与红烬
    float dissolveNoise = noiseTex(snapUV * 3.4 + uSeed * 11.7) * 0.68
        + noiseTex(snapUV * 8.6 + float2(uSeed * 5.3, -uSeed * 3.1)) * 0.32;
    float erode = saturate(uDissolve) * 1.22 - 0.12;
    float survive = smoothstep(erode - 0.07, erode + 0.08, dissolveNoise);
    float emberBand = exp(-pow((dissolveNoise - erode) * 11.5, 2.0))
        * step(0.001, uDissolve);
    float emberFlicker = 0.82 + 0.18 * sin(uTime * 6.8 + paperLocal.y * 0.17 + uSeed * 9.0);
    float emberStrength = saturate(emberBand * uEmber * emberFlicker);
    color = lerp(color, ONI_RED * (0.76 + emberFlicker * 0.18), emberStrength * 0.88);
    color = lerp(color, ONI_RED * 0.72, fringe * uEmber * 0.16);

    //落刀仅作短帧暖白过曝
    float cutFlash = saturate(uCutFlash);
    color = lerp(color, float3(1.0, 0.90, 0.76), cutFlash * 0.78);

    float materialAlpha = saturate(core + fringe * 0.78);
    float alpha = materialAlpha * developed * survive * insideSnap * 0.96;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
