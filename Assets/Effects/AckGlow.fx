// ============================================================================
// AckGlow.fx  通用软径向辉光（替代 1px 纹理径向堆叠的劣质拼接辉光）
// 绘于方形 quad，纯径向距离，边缘前归零，杜绝方形硬边
// AlphaBlend 预乘 alpha，作为暗底之上的加性辉光
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uFalloff;   //衰减指数，越大核心越收紧
float3 uAccent;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 d = coords - 0.5;
    float r = length(d) * 2.0;

    float core = pow(saturate(1.0 - r), uFalloff);
    core += exp(-r * r * 6.0) * 0.55;
    //边缘遮罩，确保 quad 四边前辉光已归零
    core *= 1.0 - smoothstep(0.86, 1.0, r);

    float a = saturate(core) * uAlpha;
    float3 col = uAccent * (0.7 + core * 0.6);
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(core - 0.85));

    return float4(col * a, a) * vertexColor;
}

technique Technique1
{
    pass AckGlowPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
