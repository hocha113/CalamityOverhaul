// ============================================================================
//ScrapSiegeFilter.fx 废钢统帅战场的全屏滤镜（Filters.Scene 通道）：
//锈尘暮色分级（暗部压冷褐、亮部染暖锈）+ 过载橙边晕（呼吸）
//+ impact frame（黑白双阶调，全场只在头坠地那一拍打满）
//+ 死亡转灰。全部通道由 ScrapSiegeScreen 每帧直喂参数。
//无噪声采样，纯分级；uColor/uOpacity 等原版槽位不占用。
//s0=屏幕
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uSiege;       //0..1 战场基调强度
float uOverloadHeat;//0..1 过载橙边晕
float uImpact;      //0..1 impact frame（黑白双阶调）
float uGrayness;    //0..1 死亡转灰
float2 uScreenResolution;

float4 PSSiege(float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);
    float3 col = src.rgb;
    float luma = dot(col, float3(0.299, 0.587, 0.114));

    //====== 锈尘暮色：暗部压冷褐、亮部染暖锈 ======
    float3 shadowTint = col * float3(0.86, 0.78, 0.72);
    float3 lightTint = col * float3(1.06, 0.97, 0.86);
    float3 graded = lerp(shadowTint, lightTint, saturate(luma * 1.4));
    col = lerp(col, graded, uSiege * 0.55);

    //====== 过载橙边晕：呼吸的热边 ======
    float2 c = uv - 0.5;
    float edge = saturate(dot(c, c) * 3.2);
    float breathe = 0.8 + 0.2 * sin(uTime * 6.0);
    col += float3(0.9, 0.34, 0.08) * edge * uOverloadHeat * 0.5 * breathe;

    //====== 死亡转灰 ======
    col = lerp(col, float3(luma, luma, luma) * float3(1.02, 0.98, 0.94), uGrayness * 0.8);

    //====== impact frame：黑白双阶调，保一丝锈调 ======
    float tone = step(0.34, luma);
    float3 impact = lerp(float3(0.04, 0.03, 0.03), float3(0.98, 0.94, 0.88), tone);
    col = lerp(col, impact, uImpact);

    return float4(col, src.a);
}

technique TechSiege {
    pass P0 {
        PixelShader = compile ps_3_0 PSSiege();
    }
}
