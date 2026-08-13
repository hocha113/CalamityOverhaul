// ============================================================================
//WofRetinaBeam.fx 视网膜扫描光束
//UV.x 0末端→1眼口 UV.y 横截面；有机血光：湿核+毛细血管缘+缓脉冲
//顶点带 transformMatrix，DrawUserPrimitives 使用；Additive
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;    //整体透明度 0~1
float seed;         //实例种子，上下眼错相
float uScanTurn;    //扫描折返增亮 0~1

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
    float along = uv.x;                  //0 末端 → 1 眼口
    float cross_ = (uv.y - 0.5) * 2.0;   //-1 ~ 1 横截面

    //末端撕散成血雾舌
    float tipTurb = tex2D(noiseSamp, float2(along * 2.8 - uTime * 1.4, cross_ * 0.6 + seed + 0.5)).r - 0.5;
    float alongTip = along + tipTurb * 0.16;
    float tailFade = smoothstep(0.0, 0.24, alongTip);
    if (tailFade * fadeAlpha < 0.002)
    {
        return float4(0, 0, 0, 0);
    }
    float taper = lerp(0.36, 1.0, smoothstep(0.0, 0.30, alongTip));
    float muzzleTaper = lerp(0.40, 1.0, smoothstep(1.0, 0.93, along));
    taper = min(taper, muzzleTaper);

    //主轴湿摆：比机械束慢而黏
    float wob = tex2D(noiseSamp, float2(along * 1.7 - uTime * 1.5, seed)).r - 0.5;
    float axis = wob * 0.42 * (1.0 - along);
    float d = abs(cross_ - axis) / taper;

    //湿核：柔宽体+窄亮芯(血不走白热，芯是苍白粉)
    float core = exp(-d * d * 34.0);
    float hot = exp(-d * d * 300.0);

    //毛细血管缘：两条细丝贴着核缘蜿蜒
    float cap1 = tex2D(noiseSamp, float2(along * 3.6 + uTime * 0.9, seed + 0.37)).g - 0.5;
    float dCap1 = abs(cross_ - (0.34 + cap1 * 0.30) * taper) / taper;
    float cap2 = tex2D(noiseSamp, float2(along * 3.1 - uTime * 1.1, seed + 0.71)).g - 0.5;
    float dCap2 = abs(cross_ + (0.36 + cap2 * 0.28) * taper) / taper;
    float capillary = exp(-dCap1 * dCap1 * 700.0) * 0.7 + exp(-dCap2 * dCap2 * 700.0) * 0.7;
    //血管断续蜿蜒
    float capGate = smoothstep(0.32, 0.55, tex2D(noiseSamp, float2(along * 1.2 + uTime * 0.6, seed + 0.53)).r);
    capillary *= capGate;

    //黏稠脉冲：厚亮团自眼口涌向末端(方向与机械束相反，是喷涌不是汇聚)
    float pulse = frac(along * 1.8 + uTime * 2.2);
    float pulseGlow = exp(-pow((pulse - 0.5) * 3.4, 2.0)) * 0.5 * core;

    //眼口汇聚辉
    float headFlare = smoothstep(0.86, 1.0, along) * core * (1.2 + uScanTurn * 0.8);
    float muzzle = smoothstep(1.0, 0.92, along);

    float halo = exp(-d * d * 2.6) * 0.5;
    float edgeMask = smoothstep(1.0, 0.80, abs(cross_));

    //调色：暗血→猩红→苍白粉芯
    float3 cBlood = float3(0.55, 0.05, 0.07);
    float3 cRed   = float3(0.92, 0.13, 0.10);
    float3 cCore  = float3(1.00, 0.62, 0.58);
    float3 cCap   = float3(0.98, 0.28, 0.20);

    float bodyMask = muzzle * tailFade * edgeMask;
    float turnBoost = 1.0 + uScanTurn * 0.5;
    float3 color = float3(0, 0, 0);
    color += cRed * core * 0.95 * turnBoost;
    color += cCore * hot * 1.05;
    color += cCap * capillary;
    color += cRed * pulseGlow;
    color += cBlood * halo;
    color *= bodyMask;
    color += cCore * headFlare * 0.9;
    color += cBlood * headFlare * 0.5;

    float alpha = saturate(
          (core * 0.72 + hot * 0.9 + capillary * 0.55 + pulseGlow * 0.45 + halo * 0.4) * bodyMask
        + headFlare * 0.9
    );
    alpha *= fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass WofRetinaBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
