// ============================================================================
// ArcaneHighDimension.fx 永恒奥秘之座高维领域
// 正方形画布中心=玩家；s0+s1 AlphaBlend 预乘
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float sphereProgress;   //0~1 领域展开进度
float fadeAlpha;        //整体透明度
float seed;             //随机种子

#define TAU 6.28318530

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float r = length(p);
    float angle = atan2(p.y, p.x);
    float normAngle = (angle + 3.14159265) / TAU;

    //领域半径（带轻微呼吸与噪声起伏）
    float breathe = 0.015 * sin(uTime * 1.7 + seed * 6.0);
    float edgeNoise = (tex2D(noiseTex, float2(normAngle * 3.0 + uTime * 0.05, seed)).r - 0.5) * 0.03;
    float radius = (0.9 + breathe + edgeNoise) * sphereProgress;

    if (r > radius + 0.1 || radius < 0.01) {
        return float4(0, 0, 0, 0);
    }

    // 界膜主边缘
    float rimDist = abs(r - radius);
    float rim = 1.0 - smoothstep(0.0, 0.035, rimDist);
    float rimWide = 1.0 - smoothstep(0.0, 0.1, rimDist);

    // 双层反向旋转符文环
    float segA = frac(normAngle * 24.0 + uTime * 0.08 + seed);
    float runeA = step(0.18, segA) * step(segA, 0.82);
    float ringA = (1.0 - smoothstep(0.0, 0.02, abs(r - radius * 0.965))) * runeA;
    //内环：14段，反向
    float segB = frac(normAngle * 14.0 - uTime * 0.12 - seed * 2.0);
    float runeB = step(0.3, segB) * step(segB, 0.7);
    float ringB = (1.0 - smoothstep(0.0, 0.016, abs(r - radius * 0.86))) * runeB;
    //刻度上的明暗扫动
    float sweep = 0.6 + 0.6 * sin(normAngle * TAU * 2.0 - uTime * 2.4);

    // 领域内部视差异界星空
    float inside = smoothstep(radius, radius * 0.92, r);
    float2 starUV1 = p * 0.55 + float2(uTime * 0.012, -uTime * 0.02) + seed;
    float2 starUV2 = p * 1.15 - float2(uTime * 0.03, uTime * 0.008) - seed * 2.0;
    float stars1 = pow(tex2D(noiseTex, starUV1).r, 8.0) * 4.0;
    float stars2 = pow(tex2D(noiseTex, starUV2).g, 10.0) * 6.0;
    float nebula = tex2D(noiseTex, p * 0.32 + float2(seed, uTime * 0.01)).b;

    float3 deepCol = float3(0.03, 0.01, 0.09);
    float3 nebulaCol = lerp(float3(0.1, 0.04, 0.25), float3(0.02, 0.16, 0.24), nebula);
    float3 interiorColor = deepCol + nebulaCol * 0.7;
    interiorColor += float3(0.5, 0.9, 1.0) * stars1;
    interiorColor += float3(0.8, 0.5, 1.0) * stars2;

    //内部菲涅尔：靠近界膜处微亮
    float fresnel = pow(saturate(r / radius), 5.0);
    interiorColor += float3(0.25, 0.45, 0.7) * fresnel * 0.6;

    // 合成
    float3 rimColor = float3(0.45, 0.9, 1.0) * rim * 1.8
                    + float3(0.55, 0.3, 1.0) * rimWide * 0.7;
    float3 runeColor = float3(0.95, 0.8, 0.45) * ringA * sweep * 1.4
                     + float3(0.5, 0.85, 1.0) * ringB * 1.2;

    float interiorAlpha = inside * 0.42;
    float alpha = saturate(interiorAlpha + rim * 0.85 + rimWide * 0.3 + ringA * 0.8 + ringB * 0.7);
    alpha *= fadeAlpha;

    float3 color = interiorColor * inside + rimColor + runeColor;
    color *= fadeAlpha;

    return float4(color * saturate(alpha * 1.6), alpha) * vertexColor;
}

technique Technique1
{
    pass ArcaneHighDimensionPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
