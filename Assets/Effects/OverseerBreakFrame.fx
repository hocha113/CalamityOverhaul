// ============================================================================
//OverseerBreakFrame.fx 断轨大拍冲击帧（全场唯一，PrimeImpactFrame 血统的工业变体）
//黑白双阶调 + 开局负相帧之上，叠两笔铸造场身份：
//① 高温提边：4-tap 明度梯度提出的轮廓线染炉锈橙（黑白世界里只有高温轮廓着色）；
//② 机械震颤：开局 ~20% 时段的微竖向 UV 抖动（≤2px 折算，随 progress 平方衰减）。
//采样 uImage0 屏幕；uProgress=0 触发 → 1 结束；强度衰减曲线同 Prime 案。
//Opaque ping-pong 批
// ============================================================================

sampler uImage0 : register(s0);

float uIntensity;
float uProgress;      //0=刚触发 → 1=结束
float2 uScreenSize;   //屏幕像素尺寸（提边 texel 与震颤折算）

static const float3 EDGE_ORANGE = float3(1.00, 0.52, 0.16);

float lumaAt(float2 uv) {
    float3 c = tex2D(uImage0, uv).rgb;
    return dot(c, float3(0.299, 0.587, 0.114));
}

float4 PSBreakFrame(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 texel = 1.0 / max(uScreenSize, float2(64.0, 64.0));

    //====== 机械震颤：开局微竖向抖动，(1-progress)^2 快速平息 ======
    float settle = 1.0 - smoothstep(0.0, 0.22, uProgress);
    float judder = sin(uProgress * 340.0) * settle * settle * 2.0 * texel.y;
    float2 uv = coords + float2(0.0, judder);

    float4 src = tex2D(uImage0, uv);
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));

    //====== 高对比黑白：中灰推向两极 ======
    float bw = smoothstep(0.34, 0.62, lum);

    //开局 ~22% 为负相帧
    float invertPhase = 1.0 - smoothstep(0.10, 0.26, uProgress);
    float tone = lerp(bw, 1.0 - bw, invertPhase);
    float3 mono = float3(tone, tone, tone)
        * lerp(float3(1.0, 1.0, 1.0), float3(1.0, 0.93, 0.86), invertPhase);

    //====== 高温提边：4-tap 明度梯度 → 炉锈橙轮廓 ======
    float gx = lumaAt(uv + float2(texel.x * 1.5, 0.0)) - lumaAt(uv - float2(texel.x * 1.5, 0.0));
    float gy = lumaAt(uv + float2(0.0, texel.y * 1.5)) - lumaAt(uv - float2(0.0, texel.y * 1.5));
    float edge = saturate((abs(gx) + abs(gy)) * 3.2 - 0.10);
    mono = lerp(mono, EDGE_ORANGE * (0.55 + tone * 0.6), edge * 0.8);

    //====== 暗角收束 ======
    float2 c = coords * 2.0 - 1.0;
    mono *= 1.0 - dot(c, c) * 0.30;

    //====== 冲击强度：触发即满格，快速衰减 ======
    float flash = uIntensity * pow(saturate(1.0 - uProgress), 1.4);

    return float4(lerp(src.rgb, mono, saturate(flash)), src.a);
}

technique TechBreakFrame {
    pass P0 {
        PixelShader = compile ps_3_0 PSBreakFrame();
    }
}
