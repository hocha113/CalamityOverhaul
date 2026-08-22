// ============================================================================
//KikasaMoonRay.fx 噬月心藏·幻月血芒
//贯屏毁灭射线：白炽核→血色体→暗色吸光外缘，三层同束；
//噪声沸腾蚀边把两侧撕成滚沸的舌状，末端收纺锤尖、心缝口收喷口；
//预乘 alpha 输出走 AlphaBlend：暗缘要压暗背景，纯加色画不出有分量的光，
//白炽核 rgb 超出 alpha 时天然获得加色亮溢。
//uv.x 1=心缝口 → 0=远端；uv.y 横截面；直线算术+平贴 tex2D，无动态分支
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //滚动时间
float uFade;      //展开/收束包络 0~1
float uSeed;      //本次开火随机相位
float uPulse;     //残余心跳 0~1：核闪与束宽微搏

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
    float along = uv.x;                  //1 心缝口 → 0 远端
    float cross_ = (uv.y - 0.5) * 2.0;   //-1 ~ 1 横截面

    //=========================================================
    //末端塑形：淡出前沿被噪声撕成舌状，横截面越靠边撕得越碎
    //=========================================================
    float tipTurb = tex2D(noiseSamp, float2(along * 2.8 - uTime * 1.8, cross_ * 0.6 + uSeed + 0.5)).r - 0.5;
    float alongTip = along + tipTurb * 0.11;
    float tailFade = smoothstep(0.0, 0.24, alongTip);
    if (tailFade * uFade < 0.002)
        return float4(0, 0, 0, 0);
    //末端收纺锤尖 + 心缝口收喷口
    float taper = lerp(0.34, 1.0, smoothstep(0.0, 0.22, alongTip));
    float muzzleTaper = lerp(0.30, 1.0, smoothstep(1.0, 0.90, along));
    taper = min(taper, muzzleTaper);

    //=========================================================
    //沸腾蚀边：两侧独立的滚沸噪声啃咬边界，血被烧滚的边不是直线
    //=========================================================
    float sideSel = step(0.0, cross_);
    float boilA = tex2D(noiseSamp, float2(along * 4.6 - uTime * 2.4, uSeed + 0.13)).r;
    float boilB = tex2D(noiseSamp, float2(along * 4.6 - uTime * 2.1, uSeed + 0.57)).r;
    float boil = lerp(boilB, boilA, sideSel) - 0.5;
    //高频碎沸叠一层
    float fizz = tex2D(noiseSamp, float2(along * 9.0 - uTime * 3.6, cross_ * 0.8 + uSeed * 1.7)).r - 0.5;
    float edgeBite = boil * 0.16 + fizz * 0.07;

    //主轴微扭：远端摆动大（能量耗散），口端稳
    float wob = tex2D(noiseSamp, float2(along * 2.0 - uTime * 2.6, uSeed)).r - 0.5;
    float axis = wob * 0.34 * (1.0 - along);
    float d = abs(cross_ - axis) / taper + edgeBite;
    d = max(d, 0.0);

    //=========================================================
    //三层同束：白炽核 / 血色体 / 暗色吸光外缘
    //=========================================================
    float hot = exp(-d * d * 300.0) * (1.0 + uPulse * 0.35);
    float core = exp(-d * d * 52.0);
    float body = exp(-d * d * 11.0);
    //暗缘：体层之外的一圈环带，吞掉背景光
    float rim = saturate(exp(-d * d * 3.2) - exp(-d * d * 9.0)) * 1.35;

    //推进脉冲：亮带自口端涌向远端（血被泵出去）
    float pulse = frac(along * 2.2 + uTime * 3.4);
    float pulseGlow = exp(-pow((pulse - 0.5) * 4.0, 2.0)) * 0.5 * core;

    //束内流层：顺束血纹
    float streak = tex2D(noiseSamp, float2(along * 3.2 + uTime * 2.8, cross_ * 1.6 + uSeed * 3.1)).r;
    float flow = saturate(streak - 0.42) * body * 0.8;

    //口器灼亮：心缝端白热汇聚
    float headBreath = 0.86 + 0.14 * sin(uTime * 34.0 + uSeed * 19.0);
    float headFlare = smoothstep(0.82, 1.0, along) * core * 1.5 * headBreath;
    float muzzle = smoothstep(1.0, 0.92, along);

    //横向总遮罩
    float edgeMask = smoothstep(1.0, 0.82, abs(cross_));
    float bodyMask = muzzle * tailFade * edgeMask * uFade;

    //=========================================================
    //调色：白炽核带一丝幻月苍青，血色体，暗缘几乎无色只吃亮度
    //=========================================================
    float3 cCore = float3(1.00, 0.96, 0.90);
    float3 cMoon = float3(0.66, 0.94, 0.88);
    float3 cBlood = float3(0.86, 0.10, 0.07);
    float3 cDeep = float3(0.34, 0.035, 0.045);
    float3 cRim = float3(0.085, 0.012, 0.018);

    //苍青环带：贴着白核外侧一圈幻月色
    float moonBand = saturate(exp(-d * d * 26.0) - exp(-d * d * 90.0));

    //预乘合成：每层 rgb 自带各自权重，暗缘 rgb 极低但 alpha 实，压暗背景
    float aHot = hot * 0.95;
    float aCore = core * 0.85;
    float aBody = body * 0.72;
    float aRim = rim * 0.5;
    float3 rgb =
          cCore * aHot * 1.5
        + cMoon * moonBand * 0.4
        + cBlood * aCore
        + cDeep * aBody * 0.9
        + cRim * aRim
        + cBlood * pulseGlow
        + cBlood * flow * 0.5;
    float alpha = saturate(aHot + aCore + aBody + aRim + pulseGlow * 0.4);

    rgb *= bodyMask;
    alpha *= bodyMask;
    //口端白热单独叠加，自身双向收窄成喷口亮核
    rgb += (cCore * 1.2 + cMoon * 0.3) * headFlare * tailFade * uFade;
    alpha = saturate(alpha + headFlare * 0.85 * tailFade * uFade);

    return float4(rgb, alpha) * input.Color;
}

technique Technique1
{
    pass KikasaMoonRayPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
