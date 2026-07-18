// ============================================================================
// OniVigorInk.fx 气力墨脉——宣纸底痕、湿墨笔锋、干笔飞白与消耗绯红残痕
// s1 使用可平铺噪声纹理，以纹理采样取代大段程序噪声，控制 ps_3_0 指令规模
// AlphaBlend 预乘 alpha 输出
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
float uAlpha;
float2 uResolution;
float4 uStroke;       //起点X、终点X、中心Y、最大半宽（像素）
float uFill;
float uTrailFill;
float uFlow;          //-1消耗 / +1恢复
float uSpendPulse;
float uGainPulse;
float uFullPulse;
float uSeed;
float3 uColPaper;
float3 uColInk;
float3 uColDeep;
float3 uColBright;
float3 uColHot;

#define PI 3.14159265

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float startX = uStroke.x;
    float endX = uStroke.y;
    float centerBase = uStroke.z;
    float maxHalfWidth = uStroke.w;
    float span = max(endX - startX, 1.0);
    float x = (px.x - startX) / span;
    float xn = saturate(x);

    //====整笔轮廓：固定种子决定起伏，时间不参与外形====
    float shapeNoise = tex2D(uNoise, float2(xn * 0.72 + uSeed, uSeed * 0.31 + 0.17)).r;
    float pressure = sqrt(saturate(sin(xn * PI)));
    float centerY = centerBase - sin(xn * PI) * 1.25 + (shapeNoise - 0.5) * 1.9;
    float halfWidth = lerp(1.15, maxHalfWidth, pressure) * (0.90 + shapeNoise * 0.18);
    float signedY = px.y - centerY;
    float relativeY = abs(signedY) / max(halfWidth, 0.8);
    float strokeSDF = abs(signedY) - halfWidth;
    float rangeMask = smoothstep(-0.025, 0.012, x)
        * (1.0 - smoothstep(0.988, 1.025, x));
    float softRange = smoothstep(-0.09, 0.015, x)
        * (1.0 - smoothstep(0.985, 1.09, x));
    float strokeMask = smoothstep(1.05, -1.05, strokeSDF) * rangeMask;

    //同一噪声贴图以不同尺度采样：低频纸纹、纵向纤维、细碎飞白
    float grain = tex2D(uNoise, px * float2(0.021, 0.135) + float2(uSeed * 2.1, uSeed)).r;
    float fiber = tex2D(uNoise, px * float2(0.055, 0.52) + float2(uSeed * 3.7, 0.29)).r;
    float fine = tex2D(uNoise, px * float2(0.12, 0.46) + float2(0.41, uSeed * 4.3)).r;

    //====完整宣纸底痕：空掉的气力仍留下极淡的最大行程====
    float trackBreak = smoothstep(0.67, 0.89, fiber * 0.76 + relativeY * 0.22);
    float trackAlpha = strokeMask * (0.14 + grain * 0.055) * (1.0 - trackBreak * 0.64);
    float3 trackColor = lerp(uColPaper * 0.40, uColDeep * 0.42,
        xn * 0.55 + grain * 0.18);

    //====湿墨与消耗残痕：噪声只扰动前沿数像素，不破坏读数====
    float fillX = lerp(startX, endX, saturate(uFill));
    float trailX = lerp(startX, endX, saturate(uTrailFill));
    float frontNoise = tex2D(uNoise,
        float2(px.y * 0.045 + uSeed * 2.7, uSeed * 0.53 + 0.61)).r;
    float frontRag = (frontNoise - 0.5) * 5.0 + sin(px.y * 1.31 + uSeed * 19.0) * 0.45;
    float trailRag = (frontNoise - 0.5) * 3.3 + sin(px.y * 0.77 + uSeed * 11.0) * 0.55;
    float frontDistance = px.x - (fillX + frontRag);
    float wetSide = smoothstep(1.25, -1.25, frontDistance) * step(0.001, uFill);
    float trailSide = smoothstep(1.55, -1.55, px.x - (trailX + trailRag))
        * step(0.001, uTrailFill);
    float wetMask = strokeMask * wetSide;
    float trailMask = strokeMask * saturate(trailSide - wetSide)
        * step(0.001, uTrailFill - uFill);

    //低气力时飞白加重，但中央骨架保留，避免漂亮却读不出数值
    float low = 1.0 - smoothstep(0.08, 0.36, uFill);
    float bristleHole = smoothstep(0.72, 0.91, fine * 0.77 + relativeY * 0.12);
    float edgeWeight = smoothstep(0.28, 0.92, relativeY);
    float holeStrength = (0.16 + low * 0.55) * (0.22 + edgeWeight * 0.78);
    float wetBody = wetMask * (1.0 - bristleHole * holeStrength);

    float wash = tex2D(uNoise,
        px * float2(0.018, 0.11) + float2(uSeed * 5.1, uSeed * 1.7)).r;
    float redVein = smoothstep(0.58, 0.88, wash);
    float3 wetColor = lerp(uColInk * 0.92, uColDeep * 0.64, redVein * 0.34);
    wetColor *= 0.87 + grain * 0.22;
    wetColor = lerp(wetColor, uColDeep * 0.70,
        smoothstep(0.58, 1.0, relativeY) * 0.32);

    //上侧湿光缓行，恢复时更亮；外形仍保持静止
    float movingNoise = tex2D(uNoise,
        float2(px.x * 0.035 - uTime * 0.035, px.y * 0.17 + uSeed * 3.1)).r;
    float glintBand = exp(-pow(signedY / max(halfWidth, 1.0) + 0.43, 2.0) * 18.0);
    float wetGlint = glintBand * smoothstep(0.64, 0.88, movingNoise)
        * wetBody * (0.07 + uGainPulse * 0.11);
    wetColor += uColPaper * wetGlint;

    //消耗后的绯红湿痕沿纤维逐渐淡去
    float trailAlpha = trailMask * (0.12 + uSpendPulse * 0.48)
        * (0.58 + wash * 0.50);
    float3 trailColor = lerp(uColDeep * 0.62, uColBright * 0.82,
        smoothstep(0.58, 0.88, wash) * uSpendPulse);

    //墨锋：恢复为纸白洇光，消耗为绯红刀口
    float frontCore = exp(-frontDistance * frontDistance * 0.34)
        * strokeMask * step(0.001, uFill);
    float frontHaze = exp(-abs(frontDistance) * 0.28)
        * exp(-max(strokeSDF, 0.0) * 0.36) * softRange * step(0.001, uFill);
    float gain = saturate(uFlow) * 0.45 + uGainPulse;
    float spend = saturate(-uFlow) * 0.45 + uSpendPulse;
    float frontAlpha = frontCore * (0.16 + gain * 0.42 + spend * 0.34);
    float3 frontColor = lerp(uColDeep, uColPaper, saturate(gain * 0.72));
    frontColor = lerp(frontColor, uColBright, saturate(spend * 0.72));

    //断口前方用高频噪声阈值生成少量飞墨，无循环粒子展开
    float ahead = px.x - fillX;
    float scatterZone = smoothstep(1.0, 4.0, ahead)
        * (1.0 - smoothstep(19.0, 28.0, ahead))
        * exp(-abs(signedY) * 0.13);
    float scatter = step(0.82, fine * 0.72 + movingNoise * 0.38)
        * scatterZone * uSpendPulse;

    //回满时一线收笔白光沿整笔掠过
    float fullAge = 1.0 - saturate(uFullPulse);
    float sweepX = lerp(startX, endX, saturate(fullAge * 1.25));
    float fullSweep = exp(-pow(px.x - sweepX, 2.0) * 0.16)
        * strokeMask * uFullPulse;

    //====轻量预乘合成====
    float wetAlpha = wetBody * (0.84 + grain * 0.12);
    float materialAlpha = max(trackAlpha, max(trailAlpha, wetAlpha));
    float3 materialColor = trackColor;
    materialColor = lerp(materialColor, trailColor, saturate(trailAlpha * 1.8));
    materialColor = lerp(materialColor, wetColor, saturate(wetAlpha * 1.12));

    float aura = exp(-max(strokeSDF, 0.0) * 0.21) * softRange
        * (0.052 + abs(uFlow) * 0.024);
    float wetRim = exp(-strokeSDF * strokeSDF * 0.44)
        * wetSide * rangeMask * (0.20 + low * 0.13);
    float hazeAlpha = frontHaze * (0.035 + gain * 0.055 + spend * 0.045);

    float3 C = materialColor * materialAlpha;
    C += uColDeep * 0.54 * aura;
    C += uColDeep * 0.82 * wetRim;
    C += frontColor * (frontAlpha + hazeAlpha);
    C += uColBright * scatter * 0.72;
    C += uColHot * fullSweep * 0.82;

    float A = saturate(materialAlpha + aura * 0.45 + wetRim
        + frontAlpha + hazeAlpha + scatter * 0.72 + fullSweep * 0.82);
    C *= uAlpha;
    A *= uAlpha;
    return float4(C, A) * vertexColor;
}

technique Technique1
{
    pass OniVigorInkPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
