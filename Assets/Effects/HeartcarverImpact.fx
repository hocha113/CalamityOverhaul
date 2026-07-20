// ============================================================================
//HeartcarverImpact.fx 剜心瞬间红黑高对比 impact frame
//采样 uImage0 屏幕；开局短负相，随后红黑三阶色调分离快速衰减
//uProgress=0 触发 → 1 结束；uIntensity=强度
// ============================================================================

sampler uImage0 : register(s0);

float uIntensity;
float uProgress;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 src = tex2D(uImage0, coords);
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));

    //高对比：中灰推向两极
    float tone = smoothstep(0.30, 0.62, lum);

    //开局 ~20% 为负相帧
    float invertPhase = 1.0 - smoothstep(0.08, 0.22, uProgress);
    tone = lerp(tone, 1.0 - tone, invertPhase);

    //红黑三阶：黑 → 动脉暗红 → 心肌粉白
    float3 cNight = float3(0.05, 0.008, 0.02);
    float3 cArterial = float3(0.64, 0.04, 0.09);
    float3 cMyocard = float3(1.0, 0.84, 0.86);
    float3 graded = tone < 0.5
        ? lerp(cNight, cArterial, tone * 2.0)
        : lerp(cArterial, cMyocard, tone * 2.0 - 1.0);

    //暗角收束视线
    float2 c = coords * 2.0 - 1.0;
    graded *= 1.0 - dot(c, c) * 0.34;

    //触发即满格，快速衰减
    float flash = uIntensity * pow(saturate(1.0 - uProgress), 1.35);

    return float4(lerp(src.rgb, graded, saturate(flash)), src.a);
}

technique Technique1
{
    pass HeartcarverImpactPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
