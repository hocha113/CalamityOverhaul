// ============================================================================
//CommandersBeam.fx 统帅之杖热能/切割射线
//UV.x 0命中点(尾)→1杖口(头)；UV.y 横截面；Additive
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间
float fadeAlpha;    //整体透明度 0~1，随展开/收束动画变化
float exMode;       //0=统帅之杖热能分解 1=EX过载赤红切割，同属血红毁灭色系，仅纯度/炽白程度不同
float seed;         //本实例随机种子，错开多束相位

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;                  //0命中点(远) → 1杖口(近)
    float cross_ = (uv.y - 0.5) * 2.0;   //-1~1横截面

    //远端撕裂收尖：噪声打散平切口，自然收成尖锥而非硬切
    float tipTurb = tex2D(noiseSamp, float2(along * 3.6 - uTime * 2.4, cross_ * 0.7 + seed + 0.5)).r - 0.5;
    float alongTip = along + tipTurb * 0.12;
    float tailFade = smoothstep(0.0, 0.26, alongTip);
    if (tailFade * fadeAlpha < 0.002)
        return float4(0, 0, 0, 0);

    float taper = lerp(0.32, 1.0, smoothstep(0.0, 0.24, alongTip));
    float muzzleTaper = lerp(0.30, 1.0, smoothstep(1.0, 0.93, along));
    taper = min(taper, muzzleTaper);

    //主轴扭动：越靠近命中点(能量耗散端)摆动越明显
    float wob = tex2D(noiseSamp, float2(along * 2.6 - uTime * 2.8, seed)).r - 0.5;
    float axis = wob * 0.42 * (1.0 - along);
    float d = abs(cross_ - axis) / taper;

    //EX专属：高频锯齿撕咬边缘，模拟切割锯片的不规则刃口(幅度收敛，避免吃掉过多核心体积)
    float serration = 0.0;
    if (exMode > 0.5)
    {
        float saw = abs(frac(along * 22.0 - uTime * 7.5) * 2.0 - 1.0);
        serration = (1.0 - saw) * 0.3 * smoothstep(0.4, 0.95, abs(cross_) / taper);
    }
    d += serration;

    //EX核心刻意比基础版更宽更软：呈现"过载粗壮"而非"激光细丝"
    float coreSharpness = lerp(48.0, 24.0, exMode);
    float core = exp(-d * d * coreSharpness);
    float hot = exp(-d * d * lerp(420.0, 260.0, exMode));

    //推进能量脉冲：能量沿束从杖口涌向命中点
    float pulseSpeed = 4.4 + exMode * 3.4;
    float pulse = frac(along * 2.8 - uTime * pulseSpeed);
    float pulseGlow = exp(-pow((pulse - 0.5) * 4.4, 2.0)) * 0.55 * core;

    //杖口收束发光：核心在杖口处汇聚成点
    float muzzleBreath = 0.85 + 0.15 * sin(uTime * 36.0 + seed * 19.0);
    float muzzleFlare = smoothstep(0.86, 1.0, along) * core * (1.3 + exMode * 0.5) * muzzleBreath;
    float muzzleBody = smoothstep(1.0, 0.91, along);

    float halo = exp(-d * d * 1.8) * 0.6;
    float edgeMask = smoothstep(1.0, 0.8, abs(cross_));

    //EX与基础版同属血红毁灭色系(呼应毁灭者/机械骷髅王)：仅更深邃浓烈、核心更炽白，不偏离红色
    float3 cBlood = lerp(float3(0.82, 0.10, 0.07), float3(0.66, 0.02, 0.02), exMode);
    float3 cHot   = lerp(float3(1.00, 0.42, 0.16), float3(1.00, 0.16, 0.07), exMode);
    float3 cCore  = lerp(float3(1.00, 0.90, 0.74), float3(1.00, 0.95, 0.90), exMode);
    float3 cArc   = lerp(float3(1.00, 0.30, 0.14), float3(1.00, 0.10, 0.05), exMode);

    float bodyMask = muzzleBody * tailFade * edgeMask;
    float3 color = float3(0, 0, 0);
    color += cBlood * core * (1.0 + exMode * 0.3);
    color += cCore  * hot * (1.05 + exMode * 0.35);
    color += cArc   * pulseGlow;
    color += cBlood * halo * (1.0 + exMode * 0.25);
    color *= bodyMask;
    //杖口亮核单独叠加(不受muzzleBody压制，自身沿束+横向双向收窄为点)
    color += cCore  * muzzleFlare;
    color += cBlood * muzzleFlare * 0.5;

    float alpha = saturate(
          (core * 0.78 + hot * 0.92 + pulseGlow * 0.5 + halo * 0.42) * bodyMask
        + muzzleFlare * 0.92
    );
    alpha *= fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass CommandersBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
