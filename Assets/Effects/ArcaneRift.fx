// ============================================================================
//ArcaneRift.fx 永恒奥秘之座奥术裂隙
//正方形画布，裂隙主轴沿 Y，旋转由 C# 精灵完成；s0+s1 AlphaBlend 预乘
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float riftOpen;      //0~1 裂隙开合
float pulse;         //0~1 爆发周期进度
float fadeAlpha;     //整体透明度
float seed;          //随机种子

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;

    //裂隙纵向能量分布：中段最宽，两端收尖
    float axial = 1.0 - p.y * p.y;
    if (axial <= 0.001) {
        return float4(0, 0, 0, 0);
    }
    float axialPow = pow(axial, 1.35);

    //裂隙中线有机扭曲
    float2 wob = tex2D(noiseTex, float2(p.y * 0.65 + seed, uTime * 0.11 + seed * 3.7)).rg;
    float centerline = (wob.r - 0.5) * 0.34 * axial;
    float halfWidth = (0.05 + 0.13 * riftOpen) * axialPow;
    halfWidth *= 0.85 + 0.3 * tex2D(noiseTex, float2(p.y * 2.2 + uTime * 0.27, seed)).g;

    float d = abs(p.x - centerline);

    //内部虚空
    float innerMask = smoothstep(halfWidth, halfWidth * 0.45, d) * riftOpen;

    //虚空内的缓慢星流
    float2 starUV1 = float2(p.x * 1.7 + seed * 5.0, p.y * 0.5 - uTime * 0.21);
    float2 starUV2 = float2(p.x * 3.1 - seed * 2.0, p.y * 0.9 + uTime * 0.13);
    float star1 = pow(tex2D(noiseTex, starUV1).r, 7.0) * 3.2;
    float star2 = pow(tex2D(noiseTex, starUV2).g, 9.0) * 4.5;

    float3 voidDeep = float3(0.015, 0.0, 0.045);
    float3 voidGlow = float3(0.13, 0.03, 0.28);
    float depthGrad = smoothstep(halfWidth * 0.45, 0.0, d);
    float3 voidColor = lerp(voidGlow, voidDeep, depthGrad);
    voidColor += float3(0.45, 0.85, 1.0) * star1 * innerMask;
    voidColor += float3(0.75, 0.4, 1.0) * star2 * innerMask;

    //灼热裂缘
    float rimBand = smoothstep(halfWidth * 1.65, halfWidth, d) - smoothstep(halfWidth, halfWidth * 0.45, d);
    rimBand = saturate(rimBand);
    float rimFlicker = 0.75 + 0.5 * tex2D(noiseTex, float2(p.y * 3.4 - uTime * 0.9, seed + 0.31)).r;
    float3 rimGold = float3(1.0, 0.83, 0.38);
    float3 rimViolet = float3(0.72, 0.28, 1.0);
    float rimMix = saturate(abs(p.y) * 1.2 + 0.25 * (wob.g - 0.5));
    float3 rimColor = lerp(rimGold, rimViolet, rimMix) * rimFlicker * 1.7;

    //侧向奥术电弧
    float arcNoise = tex2D(noiseTex, float2(p.y * 2.6 + seed * 7.0, uTime * 0.5)).g;
    float ridge = 1.0 - abs(arcNoise * 2.0 - 1.0);
    float arcReach = smoothstep(0.62 * axialPow + halfWidth, halfWidth, d);
    float arcs = pow(ridge, 5.5) * arcReach * axialPow * riftOpen;
    float arcGate = tex2D(noiseTex, float2(seed + floor(uTime * 2.5) * 0.173, p.y * 0.8)).r;
    arcs *= smoothstep(0.35, 0.75, arcGate);
    float3 arcColor = float3(0.62, 0.35, 1.0) * arcs * 2.1;

    //爆发脉冲
    float ellipseDist = length(float2(p.x - centerline, p.y * 0.55));
    float pulseRadius = pulse * 1.25;
    float pulseBand = 1.0 - smoothstep(0.0, 0.16 + pulse * 0.1, abs(ellipseDist - pulseRadius));
    float pulseFade = (1.0 - pulse) * (1.0 - pulse);
    float pulseNoise = 0.7 + 0.5 * tex2D(noiseTex, float2(atan2(p.y, p.x - centerline) * 0.6 + seed, pulse)).r;
    float3 pulseColor = float3(0.85, 0.55, 1.0) * pulseBand * pulseFade * pulseNoise * riftOpen * 1.5;

    //合成预乘 alpha
    float rimAlpha = rimBand * riftOpen;
    float alpha = saturate(innerMask + rimAlpha * 0.9 + arcs * 0.7 + pulseBand * pulseFade * 0.55);
    alpha *= fadeAlpha * axialPow;

    float3 color = voidColor * innerMask
                 + rimColor * rimAlpha
                 + arcColor
                 + pulseColor;
    color *= fadeAlpha;

    return float4(color * saturate(alpha + rimAlpha * 0.6), alpha) * vertexColor;
}

technique Technique1
{
    pass ArcaneRiftPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
