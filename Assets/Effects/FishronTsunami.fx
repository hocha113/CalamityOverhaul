// ============================================================================
// FishronTsunami.fx 海啸浪墙
// 世界 quad：uv.x 横向，uv.y 纵向 0(顶)~1(底)；uDir 控制前进朝向
// 浪形：前脸陡立 + 浪冠卷沫 + 面上攀爬水纹 + 浪脚翻涌
// 直线算术无分支，噪声全走绑定贴图，无极角
// ============================================================================

float uTime;
float uIntensity;   // 0~1 起浪/退浪包络
float uDir;         // +1 向右 / -1 向左
float uSeed;
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

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

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // 归一坐标：xu=1 是浪的前脸（按 uDir 代数翻转，无分支）
    float flip = step(0.0, uDir);
    float xu = lerp(1.0 - uv.x, uv.x, flip);

    // =========================================================
    // A. 浪面线：后坡缓、前脸陡；表面随噪声起伏
    // =========================================================
    float rise = smoothstep(0.05, 0.78, xu);          // 后→前逐渐立起
    float surfLine = lerp(0.66, 0.06, rise);          // 浪面高度（uv.y 越小越高）
    float surfN = tex2D(noiseSamp, float2(xu * 1.8 + uSeed - uTime * 0.35, 0.27)).r;
    surfLine += (surfN - 0.5) * 0.10;

    // 前脸截断：浪头前方没有水（噪声撕出参差前缘）
    float frontN = tex2D(noiseSamp, float2(uv.y * 2.2 + uSeed, xu * 3.0 - uTime * 0.5)).g;
    float frontCut = smoothstep(1.02, 0.86, xu + (frontN - 0.5) * 0.14);

    // 体掩码：浪面以下为水体
    float body = smoothstep(surfLine - 0.03, surfLine + 0.10, uv.y) * frontCut;

    // =========================================================
    // B. 水体：攀爬前脸的流纹（向上后掠），底部沉深
    // =========================================================
    float2 flowUV = float2(xu * 2.6 + uSeed, uv.y * 2.0 + uTime * 1.15);
    float flow1 = tex2D(noiseSamp, flowUV).b;
    float2 flowUV2 = float2(xu * 5.2 - uTime * 0.4, uv.y * 3.4 + 0.53);
    float flow2 = tex2D(noiseSamp, flowUV2).r;
    float field = flow1 * 0.6 + flow2 * 0.4;

    float depth = smoothstep(surfLine, 1.0, uv.y);    // 0 浪面 → 1 浪底
    float3 col = lerp(uSeaColor, uDeepColor, depth * 0.85);
    col *= 0.8 + field * 0.45;

    // =========================================================
    // C. 浪冠卷沫：沿浪面一条翻卷的白，前脸最厚
    // =========================================================
    float crestBand = smoothstep(surfLine + 0.075, surfLine, uv.y);
    float crestN = tex2D(noiseSamp, float2(xu * 4.0 - uTime * 1.5 + uSeed, uv.y * 6.0)).g;
    float crest = crestBand * smoothstep(0.30, 0.66, crestN) * (0.45 + rise * 0.8);
    col += uFoamColor * crest;

    // 冠前抛沫：越过浪头被甩出去的碎白
    float sprayZone = smoothstep(0.78, 0.99, xu) * smoothstep(surfLine + 0.02, surfLine - 0.16, uv.y);
    float sprayN = tex2D(noiseSamp, float2(xu * 6.5 - uTime * 2.2, uv.y * 5.0 + uSeed)).r;
    float spray = sprayZone * smoothstep(0.58, 0.85, sprayN);
    col += uFoamColor * spray * 0.85;

    // =========================================================
    // D. 浪脚翻涌 + 面上泡沫脉络
    // =========================================================
    float footChurn = smoothstep(0.86, 1.0, uv.y) * (0.35 + 0.65 * field);
    col += uFoamColor * footChurn * 0.35;
    float veins = smoothstep(0.68, 0.9, field) * (1.0 - depth * 0.6);
    col += uFoamColor * veins * 0.22;

    // =========================================================
    // 合成（预乘）：体密度 × 包络 × 画布护栏
    // =========================================================
    float density = body * (0.62 + field * 0.38) + spray * 0.5;
    float guard = smoothstep(0.0, 0.02, uv.x) * smoothstep(1.0, 0.98, uv.x)
        * smoothstep(0.0, 0.02, uv.y) * smoothstep(1.0, 0.99, uv.y);
    float alpha = saturate(density * uIntensity * guard) * 0.92;

    return float4(col * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass TsunamiPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
