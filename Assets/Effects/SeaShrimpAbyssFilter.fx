// ============================================================================
//SeaShrimpAbyssFilter.fx 渊晶海虾战场的全屏滤镜（Filters.Scene 通道）：
//深海分级（暗部压深蓝、亮部染青）+ 阶段下潜（P1 青→P2 深蓝→P3 渊黑）
//+ 焦散微光（双向正弦干涉带，只提亮部）+ 深渊边晕
//+ impact frame（声致发光：白青/渊黑双阶调，全场只在死亡内爆打满）
//+ 死亡沉暗。通道由 SeaShrimpAbyssScreen 每帧直喂。
//无噪声采样，纯分级；s0=屏幕
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAbyss;   //0..1 战场基调强度
float uDepth;   //0..1 阶段深度（0=P1 1=P3）
float uImpact;  //0..1 impact frame
float uGloom;   //0..1 死亡沉暗
float2 uScreenResolution;

float4 PSAbyss(float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);
    float3 col = src.rgb;
    float luma = dot(col, float3(0.299, 0.587, 0.114));

    //====== 深海分级：暗部压深蓝，亮部染青 ======
    float3 shadowTint = col * lerp(float3(0.80, 0.90, 1.04), float3(0.55, 0.68, 0.95), uDepth);
    float3 lightTint = col * lerp(float3(0.92, 1.03, 1.06), float3(0.78, 0.96, 1.10), uDepth);
    float3 graded = lerp(shadowTint, lightTint, saturate(luma * 1.35));
    float gradeAmt = uAbyss * lerp(0.42, 0.72, uDepth);
    col = lerp(col, graded, gradeAmt);

    //====== 焦散微光：双向正弦干涉，只加在亮部 ======
    float2 suv = uv * float2(uScreenResolution.x / uScreenResolution.y, 1.0);
    float bandA = sin(suv.x * 9.0 + suv.y * 4.0 + uTime * 0.9);
    float bandB = sin(suv.x * 5.0 - suv.y * 7.0 - uTime * 0.63);
    float caustic = saturate(bandA * bandB);
    caustic = pow(caustic, 3.0) * saturate(luma * 1.6);
    col += float3(0.22, 0.42, 0.55) * caustic * uAbyss * lerp(0.16, 0.3, uDepth);

    //====== 深渊边晕：屏缘沉入蓝黑 ======
    float2 c = uv - 0.5;
    float edge = saturate(dot(c, c) * 2.6);
    float vig = edge * uAbyss * lerp(0.22, 0.5, uDepth);
    col = lerp(col, col * float3(0.3, 0.42, 0.62), vig);

    //====== 死亡沉暗：脱饱和 + 压暗，保一缕蓝 ======
    float3 gloomed = lerp(col, float3(luma, luma, luma) * float3(0.82, 0.9, 1.05), 0.7) * 0.62;
    col = lerp(col, gloomed, uGloom);

    //====== impact frame：声致发光双阶调（白青 / 渊黑）======
    float tone = step(0.32, luma);
    float3 impactCol = lerp(float3(0.02, 0.04, 0.09), float3(0.92, 0.98, 1.0), tone);
    col = lerp(col, impactCol, uImpact);

    return float4(col, src.a);
}

//注意：Filters.Scene 的 ScreenShaderData 按"通道名"查表，注册字符串必须与 pass 名一致
technique TechAbyss {
    pass AbyssPass {
        PixelShader = compile ps_3_0 PSAbyss();
    }
}
