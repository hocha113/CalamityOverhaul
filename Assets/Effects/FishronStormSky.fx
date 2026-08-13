// ============================================================================
// FishronStormSky.fx 猪龙鱼风暴天幕
// 全屏 quad；越打天越黑：uIntensity 压暗，uRain 雨幕，uFlash 雷闪
// 直线算术无分支，噪声全走绑定贴图（FNA3D 安全），无极角
// ============================================================================

float uTime;
float uIntensity;    // 0~1 风暴等级（含淡入）
float uRain;         // 0~1 雨量
float uFlash;        // 0~1 雷闪冲量
float uFlashX;       // 雷闪横位（屏幕 0~1）
float uAspectRatio;  // 宽高比

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
    // =========================================================
    // A. 天幕基调：顶黑压下来，地平线残留一线海青
    // =========================================================
    float3 topCol = float3(0.030, 0.052, 0.070);
    float3 midCol = float3(0.055, 0.115, 0.135);
    float3 horizonCol = float3(0.095, 0.230, 0.235);
    float horizonBand = smoothstep(0.45, 0.80, uv.y);
    float3 sky = lerp(topCol, midCol, smoothstep(0.0, 0.55, uv.y));
    sky = lerp(sky, horizonCol, horizonBand * 0.55);

    // =========================================================
    // B. 双层流云：不同尺度/速度视差滚动，云底吃雷闪的光
    // =========================================================
    float2 cuv1 = float2(uv.x * 1.6 + uTime * 0.014, uv.y * 0.9 + 0.13);
    float2 cuv2 = float2(uv.x * 3.1 - uTime * 0.027, uv.y * 1.7 + 0.57);
    float cloud1 = tex2D(noiseSamp, cuv1).r;
    float cloud2 = tex2D(noiseSamp, cuv2).g;
    float cloudField = cloud1 * 0.62 + cloud2 * 0.38;
    // 云聚在上半幕
    float cloudMask = smoothstep(0.85, 0.15, uv.y) * smoothstep(0.28, 0.62, cloudField);
    float3 cloudDark = float3(0.016, 0.026, 0.034);
    sky = lerp(sky, cloudDark, cloudMask * 0.85);

    // 云底受光：雷闪时云的下缘亮起惨白
    float cloudRim = smoothstep(0.42, 0.55, cloudField) * smoothstep(0.62, 0.45, cloudField);
    float flashLight = uFlash * (0.35 + 0.65 * exp2(-abs(uv.x - uFlashX) * 6.0));
    sky += float3(0.72, 0.86, 1.0) * cloudRim * flashLight * 0.9;

    // =========================================================
    // C. 雷闪整幕提亮 + 闪位竖向光柱
    // =========================================================
    sky += float3(0.55, 0.68, 0.85) * uFlash * 0.16;
    float column = exp2(-abs(uv.x - uFlashX) * uAspectRatio * 9.0);
    sky += float3(0.75, 0.88, 1.0) * column * uFlash * 0.5 * smoothstep(1.0, 0.25, uv.y);

    // =========================================================
    // D. 斜雨双层：高频噪声阈值切成雨丝，向下急滚
    //    近层粗且快，远层细且慢；整体随 uRain 起落
    // =========================================================
    float slant = 0.36;
    float2 ruv1 = float2((uv.x + uv.y * slant) * 34.0 * uAspectRatio, uv.y * 2.2 + uTime * 3.4);
    float rainN1 = tex2D(noiseSamp, ruv1).b;
    float rain1 = smoothstep(0.72, 0.92, rainN1);
    float2 ruv2 = float2((uv.x + uv.y * slant * 1.25) * 61.0 * uAspectRatio + 0.37, uv.y * 3.1 + uTime * 2.2);
    float rainN2 = tex2D(noiseSamp, ruv2).r;
    float rain2 = smoothstep(0.78, 0.95, rainN2);
    float rainAmt = (rain1 * 0.6 + rain2 * 0.4) * uRain;
    sky += float3(0.42, 0.60, 0.66) * rainAmt * 0.55;

    // =========================================================
    // E. 地平浪线：底缘一条起伏的泡沫微光
    // =========================================================
    float seaLine = smoothstep(0.905, 0.94, uv.y) * smoothstep(0.985, 0.945, uv.y);
    float glint = tex2D(noiseSamp, float2(uv.x * 5.0 + uTime * 0.09, 0.31)).g;
    sky += float3(0.30, 0.62, 0.62) * seaLine * (0.25 + glint * 0.5) * (0.4 + uFlash);

    // =========================================================
    // 合成：覆盖度随风暴等级推进（预乘输出）
    // =========================================================
    float alpha = uIntensity * (0.55 + 0.30 * cloudMask + 0.15 * horizonBand);
    alpha = saturate(alpha + uFlash * 0.10 + rainAmt * 0.20);
    return float4(sky * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass StormSkyPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
