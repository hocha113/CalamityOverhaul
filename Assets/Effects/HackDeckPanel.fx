// ============================================================================
//HackDeckPanel.fx 骇客UI面板与协议芯片材质
//燕尾旗SDF遮罩 + 微噪声 + 全息条带 + 刷新带 + 状态参数
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //绘制矩形像素尺寸
float uTaperLeft;    //左端斜切宽(px)，顶部最宽向下收窄
float uTaperRight;   //右端斜切宽(px)，底部最宽向上收窄
float3 uAccent;      //强调色 0..1
float uHover;        //悬停强度 0..1
float uDisabled;     //禁用压暗 0..1
float uProgress;     //上传进度 0..1，0 为无
float uGlitch;       //故障强度 0..1

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
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

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float t01 = px.y / max(uResolution.y, 1.0);

    //燕尾旗遮罩：左切上宽下窄，右切上窄下宽
    float leftEdge = uTaperLeft * (1.0 - t01);
    float rightEdge = uResolution.x - uTaperRight * t01;
    float mask = smoothstep(leftEdge - 1.0, leftEdge + 1.0, px.x)
               * smoothstep(rightEdge + 1.0, rightEdge - 1.0, px.x)
               * smoothstep(-1.0, 1.0, px.y)
               * smoothstep(uResolution.y + 1.0, uResolution.y - 1.0, px.y);
    if (mask < 0.003) return float4(0, 0, 0, 0);

    //故障行位移
    float rowSeed = floor(px.y / 3.0);
    float glitchGate = hash21(float2(rowSeed, floor(uTime * 24.0)));
    float glitchShift = (glitchGate - 0.5) * 26.0 * uGlitch;
    float2 gp = float2(px.x + glitchShift, px.y);

    //基底近黑 + 纵向渐变（保留可读对比度，别压到纯黑）
    float3 baseA = float3(0.055, 0.075, 0.100);
    float3 baseB = float3(0.032, 0.046, 0.066);
    float3 col = lerp(baseA, baseB, t01);

    //微噪声颗粒
    float grain = vnoise(gp * 0.55 + float2(0.0, uTime * 1.2));
    col += (grain - 0.5) * 0.020;

    //斜向全息条带（低对比缓移）
    float band = sin((gp.x + gp.y * 2.2) * 0.055 - uTime * 1.1);
    col += uAccent * (band * 0.5 + 0.5) * 0.016;

    //CRT横纹（压暗幅度收小，避免整体发闷）
    float crt = step(1.5, fmod(px.y, 3.0));
    col *= lerp(1.0, 0.94, crt);

    //刷新带，数秒一次自上而下
    float sweepT = frac(uTime * 0.22);
    float sweepY = sweepT * (uResolution.y + 60.0) - 30.0;
    float sweep = exp(-abs(px.y - sweepY) / 7.0)
                * smoothstep(0.0, 0.06, sweepT) * smoothstep(1.0, 0.94, sweepT);
    col += uAccent * sweep * 0.045;

    //上传进度填充与亮缘
    if (uProgress > 0.001) {
        float fillX = uProgress * uResolution.x;
        col += uAccent * step(px.x, fillX) * 0.055;
        col += uAccent * exp(-abs(px.x - fillX) / 5.0) * 0.35;
    }

    //左缘辉光（贴斜切边）
    float dLeft = px.x - leftEdge;
    col += uAccent * exp(-max(dLeft, 0.0) / 9.0) * (0.10 + uHover * 0.22);

    //顶部发丝线 / 底部暗线
    col += uAccent * smoothstep(1.6, 0.4, px.y) * 0.16;
    col *= lerp(1.0, 0.82, smoothstep(uResolution.y - 2.5, uResolution.y - 0.5, px.y));

    //悬停提亮
    col *= 1.0 + uHover * 0.55;
    col += uAccent * uHover * 0.035;

    //禁用压暗（色相偏移由CPU换uAccent承担）
    col *= 1.0 - uDisabled * 0.35;

    //故障亮线
    col += uAccent * uGlitch * step(0.82, hash21(float2(rowSeed, floor(uTime * 24.0) + 7.0))) * 0.30;

    float a = mask * uAlpha * (0.90 + uHover * 0.08);
    col = saturate(col);
    return float4(col * a, a) * vc;
}

technique Technique1
{
    pass HackDeckPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
