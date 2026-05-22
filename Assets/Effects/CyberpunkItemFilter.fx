//============================================================================
//CyberpunkItemFilter.fx 赛博朋克2077风格的物品图标附魔滤镜
//通过对原贴图进行色相重映射、色彩偏移、扫描线、霓虹边缘描边、辉光闪烁
//为SHPC改件物品提供按色调区分的特殊外观
//输入参数：
//  uTime       累计时间，用于动画
//  uTint       识别色，用作高光区域调色与边缘霓虹描边主色
//  uTexSize    贴图像素尺寸，用于将uv换算为像素步进
//  uIntensity  整体滤镜强度，0为关闭，1为完整效果
//============================================================================

sampler uImage0 : register(s0);

float uTime;
float3 uTint;
float2 uTexSize;
float uIntensity;

float hash21(float2 p) {
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uTexSize;
    float2 texel = 1.0 / max(uTexSize, float2(1.0, 1.0));

    //横向毛刺扫描线偏移：以每两像素为一带，按时间随机抽取少量带触发偏移
    float band = floor(px.y * 0.5);
    float bandSeed = hash21(float2(band, floor(uTime * 6.0)));
    float glitchActive = step(0.93, bandSeed);
    float glitchOff = (bandSeed - 0.5) * 0.30 * glitchActive * uIntensity;
    float2 uv = coords + float2(glitchOff, 0.0);

    //RGB色散偏移：分别采样R/B分量制造色彩裂痕
    float ca = 1.6 * texel.x * uIntensity;
    float4 cR = tex2D(uImage0, uv + float2(ca, 0.0));
    float4 cG = tex2D(uImage0, uv);
    float4 cB = tex2D(uImage0, uv - float2(ca, 0.0));
    float a = cG.a;

    //提取亮度作为重映射输入
    float lum = dot(cG.rgb, float3(0.299, 0.587, 0.114));

    //双调色映射：阴影压向深蓝黑，高光导向识别色
    float3 shadow = float3(0.02, 0.04, 0.08);
    float3 highlight = uTint;
    float3 base = lerp(shadow, highlight, smoothstep(0.05, 0.85, lum));

    //叠加色散两端的颜色作为辉光感
    float3 split = float3(cR.r, cG.g, cB.b);
    float3 col = lerp(base, base + split * 0.40, uIntensity * 0.65);

    //CRT扫描线：每像素一行的明暗交替
    float scan = 0.86 + 0.14 * sin(px.y * 3.1416);
    col *= lerp(1.0, scan, uIntensity);

    //贴图描边检测：以alpha梯度找出图标边缘
    float aL = tex2D(uImage0, coords - float2(texel.x, 0.0)).a;
    float aR = tex2D(uImage0, coords + float2(texel.x, 0.0)).a;
    float aU = tex2D(uImage0, coords - float2(0.0, texel.y)).a;
    float aD = tex2D(uImage0, coords + float2(0.0, texel.y)).a;
    float edge = saturate(4.0 * a - aL - aR - aU - aD);

    //霓虹描边：识别色与暖黄高光呼吸切换，线条做垂直流光
    float shimmer = sin(px.y * 0.45 - uTime * 4.5) * 0.5 + 0.5;
    float3 edgeCol = lerp(uTint * 1.55, float3(1.0, 0.92, 0.28), shimmer);
    col += edgeCol * edge * (1.0 + uIntensity * 0.6);

    //像素级闪烁噪点
    float flicker = hash21(float2(floor(px.x * 0.5), floor(uTime * 14.0))) - 0.5;
    col += uTint * flicker * 0.10 * uIntensity;

    //每隔较长周期的全图脉冲增亮，模拟广告牌HUD刷新
    float pulse = exp(-frac(uTime * 0.6) * 4.0) * 0.25 * uIntensity;
    col += uTint * pulse;

    //恢复原图无效像素的透明
    return float4(col * a, a) * vertexColor;
}

technique Technique1
{
    pass CyberpunkItemFilterPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
