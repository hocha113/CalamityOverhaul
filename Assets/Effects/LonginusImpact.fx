// ============================================================================
//LonginusImpact.fx 朗基努斯处决冲击帧
//全屏后效：头几帧高对比双色调(白炽底+暗红剪影)急速退潮回原画面
//直线算术 + 平 tex2D，无分支，FNA 全屏铁律
//uProgress 0=触发帧 → 1=结束；uCenter 处决点屏幕UV
// ============================================================================

sampler uImage0 : register(s0);

float uProgress;
float uIntensity;
float2 uCenter;

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(uImage0, coords);
    float luma = dot(scene.rgb, float3(0.299, 0.587, 0.114));

    //双色调：暗部压成近黑剪影，亮部炸白微暖
    float tone = smoothstep(0.30, 0.62, luma);
    float3 cLow = float3(0.10, 0.012, 0.02);
    float3 cHigh = float3(1.55, 1.46, 1.30);
    float3 flat2 = lerp(cLow, cHigh, tone);

    //处决点径向聚焦，边缘略回原色
    float dist = length(coords - uCenter);
    float focus = 0.72 + 0.28 * exp2(-dist * dist * 5.0);

    //头2~3帧全量，随后急速退潮
    float w = pow(saturate(1.0 - uProgress), 2.4) * uIntensity * focus;

    float3 color = lerp(scene.rgb, flat2, saturate(w));
    return float4(color, scene.a);
}

technique Technique1
{
    pass LonginusImpactPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
