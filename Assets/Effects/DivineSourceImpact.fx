// ============================================================================
// DivineSourceImpact.fx 真·黄金斩命中爆点
// 采样 s0 占位 UV + s1 噪声；预乘 alpha AlphaBlend
// ps_3_0
// ============================================================================

sampler2D MainSampler : register(s0);

texture NoiseTexture;
sampler2D NoiseSampler = sampler_state
{
    Texture = (NoiseTexture);
    AddressU = Wrap;
    AddressV = Wrap;
    MagFilter = Linear;
    MinFilter = Linear;
    MipFilter = Linear;
};

float TotalTime;        //时间(秒)
float RingRadius;       //0~1 能量环半径(半幅)
float RingThickness;    //环厚度
float RingIntensity;    //环强度
float SphereRadius;     //0~1 球形高亮半径
float SphereIntensity;  //球形高亮强度

float4 CoreColor;       //核心白金
float4 RingColor;       //环金
float4 EmberColor;      //外围橙

float4 MainPS(float4 vertexColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;        // [-1,1] 居中坐标
    float dist = length(p);
    float ang = atan2(p.y, p.x) / 6.2831853 + 0.5;   // [0,1] 角向

    // 角向噪声扰动环
    float n = tex2D(NoiseSampler, float2(ang * 2.0 + TotalTime * 0.06, RingRadius * 0.45 + 0.2)).r;
    float n2 = tex2D(NoiseSampler, float2(ang * 5.0 - TotalTime * 0.12, 0.7)).r;

    // 球形高亮核心
    float sphere = exp(-pow(dist / max(SphereRadius, 0.001), 2.0) * 3.2) * SphereIntensity;

    // 扩散能量环
    float rr = RingRadius * (0.90 + 0.18 * n);
    float ring = exp(-pow((dist - rr) / max(RingThickness, 0.005), 2.0));
    ring *= 0.72 + 0.42 * n2;
    ring *= RingIntensity;

    // 环内残光
    float wake = smoothstep(rr, rr * 0.25, dist) * 0.22 * RingIntensity;

    // 合成
    float3 col = CoreColor.rgb * sphere
               + lerp(RingColor.rgb, EmberColor.rgb, saturate(dist * 1.15)) * (ring + wake);

    float alpha = saturate(sphere * 0.85 + ring * 0.65 + wake * 0.8);
    alpha *= vertexColor.a;

    return float4(col * vertexColor.a, alpha);
}

technique Technique1
{
    pass MainPass
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}
