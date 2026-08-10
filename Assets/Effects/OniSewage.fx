sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uProgress;      //0=未成形的污水 1=完全凝聚；消融期倒放
float uTime;
float2 uTextureSize;
float uScale;
float uRotation;
float uCenterY;       //绘制中心的世界Y
float uGroundY;       //地表世界Y，之下裁掉（身体从地里长出来）
float3 uSewageDeep;   //污水浊色
float3 uEdgeColor;    //湿缘高光

//污水凝聚：自下而上的噪声侵蚀显形 + 垂坠拉丝 + 湿缘高光 + 浊色澄清
float4 SewagePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float progress = saturate(uProgress);
    float rise = 1.0 - progress;

    //未成形区拉丝下坠：采样点上移=画面内容下垂，横向再随噪声蠕动
    float2 noiseUVa = coords * float2(2.6, 3.4) + float2(uTime * 0.06, -uTime * 0.05);
    float2 noiseUVb = coords * float2(5.4, 2.2) + float2(-uTime * 0.045, uTime * 0.07);
    float na = tex2D(uNoise, noiseUVa).r;
    float nb = tex2D(uNoise, noiseUVb).r;

    float2 warped = coords;
    float topness = saturate(1.15 - coords.y);
    warped.y -= rise * (na - 0.35) * 0.16 * topness;
    warped.x += rise * sin(coords.y * 21.0 + uTime * 5.2 + nb * 6.0) * 0.016 * topness;

    float4 source = tex2D(uImage0, warped);
    if (source.a <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    //噪声密度叠自下而上偏置：UV 的 y=1 是贴图底端，脚先成形，头肩最后从污流里析出
    float density = na * 0.42 + nb * 0.24 + warped.y * 0.52;
    float threshold = 1.16 - progress * 1.42;
    float outerReveal = smoothstep(threshold - 0.10, threshold + 0.04, density);
    float bodyReveal = smoothstep(threshold + 0.03, threshold + 0.17, density);

    //收尾还原原贴图
    float settle = smoothstep(0.85, 0.995, progress);
    bodyReveal = lerp(bodyReveal, 1.0, settle);
    outerReveal = lerp(outerReveal, 1.0, settle);
    float edge = saturate(outerReveal - bodyReveal);
    float effectFade = 1.0 - smoothstep(0.85, 0.98, progress);

    //地面裁切：世界Y在地表以下的部分不存在（从地里长出来）
    float2 localPx = (coords - 0.5) * uTextureSize * uScale;
    float worldY = uCenterY + localPx.x * sin(uRotation) + localPx.y * cos(uRotation);
    float groundMask = 1.0 - smoothstep(uGroundY - 1.0, uGroundY + 3.0, worldY);

    //浊色澄清：成形早期整体是污水色，随凝聚渐显本色
    float clarity = saturate(progress * 1.25 - 0.18);
    float3 murk = uSewageDeep * (0.55 + 0.45 * na);
    float3 flesh = lerp(murk, source.rgb, clarity);

    float revealAlpha = saturate(bodyReveal + edge * 0.55);
    float outputAlpha = source.a * revealAlpha * groundMask * vertexColor.a;
    float3 body = flesh * bodyReveal * groundMask * vertexColor.rgb;
    float3 glow = uEdgeColor * edge * 0.62;
    glow += uEdgeColor * (0.10 + nb * 0.16) * edge;
    glow *= source.a * groundMask * vertexColor.a * effectFade;

    return float4(body + glow, outputAlpha);
}

technique Technique1
{
    pass SewagePass
    {
        PixelShader = compile ps_3_0 SewagePS();
    }
}
