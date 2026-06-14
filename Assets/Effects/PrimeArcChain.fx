// ============================================================================
//PrimeArcChain.fx 臂-头电弧链锁束带
//UV.x 0头→1臂 UV.y 0.5中心；Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //Extra_193 噪声

float3 uColor;          //主题色（特斯拉橙金）
float3 uSecondaryColor; //高光色
float uTime;
float uIntensity;
float uProgress;        //功率 0~1：预警期细弱 → 全功率
float uSeed;            //链实例区分

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float lat = (uv.y - 0.5) * 2.0;

    //=== 中轴噪声摆动（两端固定锚点，中段游走）===
    float envelope = sin(along * 3.14159);
    float n1 = tex2D(uImage1, float2(along * 2.4 - uTime * 1.8, uSeed * 0.37)).r - 0.5;
    float n2 = tex2D(uImage1, float2(along * 5.2 + uTime * 2.5, uSeed * 0.61 + 0.5)).g - 0.5;
    float yOffset = (n1 * 0.55 + n2 * 0.30) * envelope * uProgress;

    float d = abs(lat - yOffset);

    //=== 分层束带 ===
    float core = exp(-d * d * 46.0) * 1.25;
    float halo = exp(-d * d * 7.0) * 0.40;

    //=== 行进光珠（能量从头部泵向机械臂）===
    float beadPhase = frac(along * 2.6 - uTime * 2.4 + uSeed * 0.5);
    float bead = smoothstep(0.40, 0.50, beadPhase) * smoothstep(0.62, 0.50, beadPhase);
    bead *= exp(-d * d * 20.0) * 1.3;

    //=== 端点锚光（连接处常亮）===
    float anchorA = 1.0 - smoothstep(0.0, 0.10, along);
    float anchorB = 1.0 - smoothstep(0.0, 0.10, 1.0 - along);
    float anchor = (anchorA + anchorB) * exp(-d * d * 9.0) * 0.8;

    //=== 高频闪烁（电气不稳定感）===
    float flick = 0.85 + 0.15 * sin(uTime * 27.0 + uSeed * 6.28 + along * 14.0);

    float intensity = (core + halo + bead + anchor) * flick * uIntensity * uProgress;

    float3 col = lerp(uColor, uSecondaryColor, saturate(core * 0.7 + bead * 0.5));
    col += float3(1.0, 1.0, 1.0) * pow(saturate(1.0 - d * 3.0), 9.0) * 0.5 * uProgress;
    col *= input.Color.rgb;

    return float4(col * intensity, saturate(intensity) * input.Color.a);
}

technique Technique1
{
    pass PrimeArcChainPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
