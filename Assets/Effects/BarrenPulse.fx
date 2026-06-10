// ============================================================================
// BarrenPulse.fx —— 荒芜电流爆发着色器
// 荒芜弓箭矢命中后的电涌爆发：参差的沙金冲击环 + 放射状电弧裂纹 + 中心闪光
// 画布为以爆心为中心的正方形白图，uv(0.5,0.5)为爆心
// ============================================================================

float uTime;           //时间
float ringProgress;    //0~1 扩散进度
float fadeAlpha;       //整体透明度
float3 coreColor;      //核心颜色（亮金白）
float3 midColor;       //中层颜色（沙金）
float3 edgeColor;      //边缘颜色（焦褐）

sampler uNoiseTex : register(s1);

struct VSOutput {
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

float hash(float2 p) {
    float h = dot(p, float2(127.1, 311.7));
    return frac(sin(h) * 43758.5453);
}

float4 PSBarrenPulse(VSOutput input) : COLOR0 {
    float2 centered = input.UV - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float angNorm = angle / 6.2832 + 0.5;

    float progress = ringProgress;
    float invProgress = 1.0 - progress;

    //极坐标噪声：让所有结构都带上电涌的抖动
    float jitterN = tex2D(uNoiseTex, float2(angNorm * 4.0 + uTime * 0.7, dist * 2.0 - uTime * 1.3)).r;
    float crackleN = tex2D(uNoiseTex, float2(angNorm * 9.0 - uTime * 2.2, progress * 0.8)).r;

    //========== (A) 参差的电涌冲击环 ==========
    float ringRadius = 0.06 + progress * 0.38;
    //电弧环不是圆的：被噪声强烈撕扯
    float displaced = dist + (jitterN - 0.5) * 0.10 * (0.4 + progress);
    float ringDist = abs(displaced - ringRadius);
    float ringWidth = 0.025 + progress * 0.05;
    float ring = smoothstep(ringWidth, 0.0, ringDist) * invProgress;

    //环上随机明暗跳动，模拟电流在环上奔走
    float surge = 0.65 + 0.35 * sin(angNorm * 40.0 + uTime * 30.0 + crackleN * 12.0);
    ring *= surge;

    //========== (B) 放射状电弧裂纹 ==========
    float spokeCount = 7.0;
    float spokeSeed = floor(angNorm * spokeCount);
    float spokePhase = frac(angNorm * spokeCount);
    //每条裂纹的角向扭动
    float wiggle = (tex2D(uNoiseTex, float2(spokeSeed * 0.137 + uTime * 0.4, dist * 3.0)).r - 0.5) * 0.45;
    float spoke = smoothstep(0.16, 0.0, abs(spokePhase - 0.5 + wiggle));
    //裂纹只存在于环内侧，并随时间闪烁
    float spokeMask = smoothstep(ringRadius + 0.02, ringRadius * 0.15, dist);
    float flicker = step(0.35, frac(hash(float2(spokeSeed, floor(uTime * 18.0))) + crackleN * 0.5));
    float crack = spoke * spokeMask * flicker * invProgress;

    //========== (C) 中心闪光 ==========
    float burst = exp(-dist * 16.0) * invProgress * invProgress * 1.6;

    //========== (D) 环外残沙 ==========
    float dustZone = smoothstep(ringRadius, ringRadius + 0.12, dist)
                   * smoothstep(ringRadius + 0.22, ringRadius + 0.06, dist);
    float dust = dustZone * jitterN * invProgress * 0.45;

    //合成
    float3 color = float3(0.0, 0.0, 0.0);
    color += lerp(coreColor, midColor, saturate(ringDist / max(ringWidth, 0.001))) * ring * 1.6;
    color += coreColor * crack * 1.2;
    color += midColor * crack * 0.4;
    color += coreColor * burst;
    color += edgeColor * dust;

    float alpha = saturate(ring + crack * 0.8 + burst + dust * 0.6);
    alpha *= fadeAlpha;

    //画布边缘柔化，避免方形截断
    alpha *= smoothstep(0.5, 0.40, dist);

    return float4(color * alpha, alpha);
}

technique BarrenPulsePass {
    pass P0 {
        PixelShader = compile ps_3_0 PSBarrenPulse();
    }
}
