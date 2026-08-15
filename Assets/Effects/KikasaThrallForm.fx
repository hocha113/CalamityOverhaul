// ============================================================================
//KikasaThrallForm.fx 伞奴形体材质：污水凝聚/融化，通用于任意帧化贴图。
//融化尸影与伞奴成形共用一套——uProgress 1→0 播融化（头肩先蚀、躯体拉丝下坠），
//0→1 播凝聚（脚先成形、伞面最后析出）。
//在 OniSewage 的侵蚀+垂坠+浊化之上加帧矩形（uUvRect）钳制采样，
//精灵表逐帧绘制不串帧；地面裁切可选（uGroundY 传远值即关闭）。
//s0=本体贴图 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uProgress;      //0=未成形的污水 1=完全凝聚；融化/溶解期倒放
float uTime;
float4 uUvRect;       //帧区域 xy=偏移 zw=尺寸（纹理uv空间）
float2 uTexel;        //一像素的uv尺寸，帧内钳制用
float2 uFrameSize;    //帧像素尺寸，地面裁切的世界换算用
float uScale;
float uRotation;
float uCenterY;       //绘制中心的世界Y
float uGroundY;       //地表世界Y，之下裁掉；不需要时传极大值
float3 uSewageDeep;   //污水浊色
float3 uEdgeColor;    //湿缘高光

//帧内钳制采样：warp 后的坐标不越出本帧，防精灵表串帧
float4 frameSample(float2 uv)
{
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi));
}

float4 ThrallPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float progress = saturate(uProgress);
    float rise = 1.0 - progress;

    //帧内归一坐标，全部形体逻辑跑在这套局部空间
    float2 luv = (coords - uUvRect.xy) / max(uUvRect.zw, 0.0001);

    //未成形区拉丝下坠：采样点上移=画面内容下垂，横向再随噪声蠕动
    float2 noiseUVa = luv * float2(2.6, 3.4) + float2(uTime * 0.06, -uTime * 0.05);
    float2 noiseUVb = luv * float2(5.4, 2.2) + float2(-uTime * 0.045, uTime * 0.07);
    float na = tex2D(uNoise, noiseUVa).r;
    float nb = tex2D(uNoise, noiseUVb).r;

    float topness = saturate(1.15 - luv.y);
    float2 warpedL = luv;
    warpedL.y -= rise * (na - 0.35) * 0.16 * topness;
    warpedL.x += rise * sin(luv.y * 21.0 + uTime * 5.2 + nb * 6.0) * 0.016 * topness;

    float4 source = frameSample(uUvRect.xy + warpedL * uUvRect.zw);
    if (source.a <= 0.001)
    {
        return float4(0, 0, 0, 0);
    }

    //噪声密度叠自下而上偏置：luv.y=1 是帧底端，脚先成形，头肩最后从污流里析出
    float density = na * 0.42 + nb * 0.24 + warpedL.y * 0.52;
    float threshold = 1.16 - progress * 1.42;
    float outerReveal = smoothstep(threshold - 0.10, threshold + 0.04, density);
    float bodyReveal = smoothstep(threshold + 0.03, threshold + 0.17, density);

    //收尾还原原贴图
    float settle = smoothstep(0.85, 0.995, progress);
    bodyReveal = lerp(bodyReveal, 1.0, settle);
    outerReveal = lerp(outerReveal, 1.0, settle);
    float edge = saturate(outerReveal - bodyReveal);
    float effectFade = 1.0 - smoothstep(0.85, 0.98, progress);

    //地面裁切：世界Y在地表以下的部分不存在（从地里长出来/化回地里）
    float2 localPx = (luv - 0.5) * uFrameSize * uScale;
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
    pass ThrallPass
    {
        PixelShader = compile ps_3_0 ThrallPS();
    }
}
