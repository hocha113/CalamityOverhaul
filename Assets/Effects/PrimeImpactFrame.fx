// ============================================================================
//PrimeImpactFrame.fx 死亡演出终爆冲击帧
//采样 uImage0 屏幕；uProgress=0 时透传
// ============================================================================

sampler uImage0 : register(s0);

float uIntensity;
float uProgress; //0=刚触发 → 1=结束

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 src = tex2D(uImage0, coords);
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));

    //高对比黑白：中灰被推向两极
    float bw = smoothstep(0.34, 0.62, lum);

    //开局 ~22% 时间为负相帧（黑白反转），之后转正
    float invertPhase = 1.0 - smoothstep(0.10, 0.26, uProgress);
    float tone = lerp(bw, 1.0 - bw, invertPhase);

    //负相帧带一点暖色偏移，正相纯黑白
    float3 mono = float3(tone, tone, tone) * lerp(float3(1.0, 1.0, 1.0), float3(1.0, 0.94, 0.88), invertPhase);

    //暗角收束视线
    float2 c = coords * 2.0 - 1.0;
    mono *= 1.0 - dot(c, c) * 0.30;

    //冲击强度曲线：触发即满格，快速衰减
    float flash = uIntensity * pow(saturate(1.0 - uProgress), 1.4);

    return float4(lerp(src.rgb, mono, saturate(flash)), src.a);
}

technique Technique1
{
    pass PrimeImpactFramePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
