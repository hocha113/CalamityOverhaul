// ============================================================================
// DestroyerBeam.fx 毁灭者光束
// UV.x 0尾→1弹头 UV.y 横截面；Additive
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间
float fadeAlpha;    //整体透明度 0~1
float exMode;       //0=普通版 1=EX版（更宽更炽白）
float seed;         //本实例随机种子，错开多发激光的电弧相位

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
    float along = uv.x;                  //0 尾(远端) → 1 头(口器)
    float cross_ = (uv.y - 0.5) * 2.0;   //-1 ~ 1 横截面

    //=========================================================
    //末端塑形：把垂直"平切口"沿横截面用噪声撕成舌状，并收束成纺锤尖
    //=========================================================
    float tipTurb = tex2D(noiseSamp, float2(along * 3.4 - uTime * 2.2, cross_ * 0.7 + seed + 0.5)).r - 0.5;
    float alongTip = along + tipTurb * 0.13;   //淡出前沿随横截面错动，撕成舌状
    //尾部淡出（前沿已被噪声打散，不再是垂直直线）
    float tailFade = smoothstep(0.0, 0.30, alongTip);
    if (tailFade * fadeAlpha < 0.002)
        return float4(0, 0, 0, 0);
    //末端横截面收窄：along→0 收成尖，破坏等宽矩形末端
    float taper = lerp(0.30, 1.0, smoothstep(0.0, 0.26, alongTip));
    //口器收窄：along→1 收成喷口，光柱自漏斗喷涌而非沿头部齐口硬切
    float muzzleTaper = lerp(0.34, 1.0, smoothstep(1.0, 0.92, along));
    taper = min(taper, muzzleTaper);

    //=========================================================
    //主轴扭动：噪声驱动中轴偏移，越靠尾部摆动越大（能量耗散）
    //=========================================================
    float wob = tex2D(noiseSamp, float2(along * 2.4 - uTime * 3.0, seed)).r - 0.5;
    float axis = wob * 0.50 * (1.0 - along);
    float d = abs(cross_ - axis) / taper;   //除以 taper，两端等效收窄

    //主激光体 + 白热芯
    float core = exp(-d * d * (46.0 - exMode * 14.0));
    float hot = exp(-d * d * 420.0);

    //=========================================================
    //缠绕电弧：第二条更细的弧线绕主轴游走（机械放电感）
    //=========================================================
    float wob2 = tex2D(noiseSamp, float2(along * 4.2 + uTime * 2.3, seed + 0.41)).r - 0.5;
    float d2 = abs(cross_ - wob2 * 1.15 * (0.25 + 0.75 * (1.0 - along))) / taper;   //电弧随核心一并收束
    float arc = exp(-d2 * d2 * 850.0) * 0.85;
    //电弧随机断续
    float arcGate = step(0.30, tex2D(noiseSamp, float2(along * 1.3 + uTime * 1.7, seed + 0.77)).r);
    arc *= arcGate;

    //=========================================================
    //推进能量脉冲：亮带从尾部冲向弹头
    //=========================================================
    float pulse = frac(along * 2.6 - uTime * 4.6);
    float pulseGlow = exp(-pow((pulse - 0.5) * 4.2, 2.0)) * 0.60 * core;

    //=========================================================
    //口器收口（仿骷髅王 muzzle/muzzleGlow）：主体沿束向口器渐入，中轴亮核乘 core 汇聚成点
    //=========================================================
    float headBreath = 0.85 + 0.15 * sin(uTime * 40.0 + seed * 17.0);             //灼热呼吸(毁灭者个性)
    float headFlare = smoothstep(0.84, 1.0, along) * core * (1.4 + exMode * 0.4) * headBreath;
    float muzzle = smoothstep(1.0, 0.90, along);    //口器端主体淡入：边界收到 0，只留亮核成喷口

    //=========================================================
    //外覆宽晕 + 横向遮罩
    //=========================================================
    float halo = exp(-d * d * 2.2) * 0.55;                  //随 taper 两端收束的宽幅红晕
    float edgeMask = smoothstep(1.0, 0.78, abs(cross_));    //横向边缘渐隐，消除上下平切口

    //=========================================================
    //调色板
    //=========================================================
    float3 cBlood = lerp(float3(0.90, 0.10, 0.06), float3(0.98, 0.14, 0.10), exMode);
    float3 cHot   = lerp(float3(1.00, 0.45, 0.15), float3(1.00, 0.55, 0.25), exMode);
    float3 cCore  = lerp(float3(1.00, 0.85, 0.62), float3(1.00, 0.95, 0.85), exMode);
    float3 cArc   = float3(1.00, 0.30, 0.16);

    //主体三向收束：口器淡入 muzzle × 末端淡出 tailFade × 横向收窄 edgeMask
    float bodyMask = muzzle * tailFade * edgeMask;
    float3 color = float3(0, 0, 0);
    color += cBlood * core * 1.05;
    color += cCore  * hot * (1.1 + exMode * 0.4);
    color += cArc   * arc;
    color += cHot   * pulseGlow;
    color += cBlood * halo;
    color *= bodyMask;
    //口器亮核单独叠加(不受 muzzle 压，自身沿束+横向双向收窄为点)
    color += cCore  * headFlare;
    color += cBlood * headFlare * 0.5;       //口器灼热血晕(毁灭者个性)

    float alpha = saturate(
          (core * 0.80 + hot * 0.95 + arc * 0.60 + pulseGlow * 0.50 + halo * 0.45) * bodyMask
        + headFlare * 0.95
    );
    alpha *= fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DestroyerBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
