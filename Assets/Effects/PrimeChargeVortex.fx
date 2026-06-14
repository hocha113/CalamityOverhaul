// ============================================================================
//PrimeChargeVortex.fx 充能漩涡
//头部中心方形面片；协议同 TwinsChargeVortex
//Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //Extra_193 噪声

float3 uColor;          //主题色
float3 uSecondaryColor; //高光色
float uTime;
float uProgress;        //蓄力进度 0~1：涡旋收紧、亮度提升
float uIntensity;
float uOpacity;

static const float PI = 3.14159265;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0)
    {
        return float4(0, 0, 0, 0);
    }
    float theta = atan2(p.y, p.x);

    //=== 螺旋臂：角向+径向耦合采样，随进度卷得更紧 ===
    float twist = lerp(2.0, 4.8, uProgress);
    float spin = uTime * (1.2 + uProgress * 2.0);
    float armCoord = theta / (2.0 * PI) * 5.0 + r * twist + spin;
    float arm = tex2D(uImage1, float2(armCoord * 0.2, r * 0.5 - uTime * 0.55)).r;
    arm = pow(saturate(arm * 1.4 - 0.28), 2.0);

    //=== 向心吸入流线 ===
    float streak = tex2D(uImage1, float2(theta / (2.0 * PI) * 3.0 + 0.37, r * 1.6 - uTime * 2.0)).g;
    streak = pow(saturate(streak * 1.5 - 0.42), 3.0);

    //=== 旋转齿轮辐条（机械标志：6 根暗辐条缓慢旋转调制亮度）===
    float spokes = 0.78 + 0.22 * sin(theta * 6.0 - uTime * 2.2);

    //=== 径向包络：外缘淡入、向心增强；进度提高时整体半径收缩 ===
    float shrink = lerp(1.0, 0.55, uProgress);
    float rr = saturate(r / shrink);
    float envelope = smoothstep(1.0, 0.55, rr) * smoothstep(0.0, 0.18, rr);

    //=== 中心核辉光（蓄力末期成型）===
    float corePow = lerp(6.0, 2.4, uProgress);
    float coreGlow = pow(saturate(1.0 - r * 2.3), corePow) * uProgress * 1.7;

    //=== 收缩呼吸环：从外向内坍缩的亮环，周期随进度加快 ===
    float ringPhase = frac(uTime * (0.55 + uProgress * 1.0));
    float ringR = (1.0 - ringPhase) * shrink;
    float ring = exp(-pow((r - ringR) * 15.0, 2.0)) * 0.8 * uProgress;

    //=== 合成 ===
    float intensity = 0.0;
    intensity += arm * envelope * spokes * (0.45 + uProgress * 0.85);
    intensity += streak * envelope * (0.35 + uProgress * 0.6);
    intensity += ring;
    intensity += coreGlow;
    intensity *= uIntensity * uOpacity;

    float3 col = lerp(uColor, uSecondaryColor, saturate(arm * 0.55 + coreGlow));
    col += float3(1.0, 1.0, 1.0) * coreGlow * 0.5;
    col *= vertexColor.rgb;

    return float4(col * intensity, saturate(intensity) * vertexColor.a);
}

technique Technique1
{
    pass PrimeChargeVortexPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
