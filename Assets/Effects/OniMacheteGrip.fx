// ============================================================================
//OniMacheteGrip.fx 鬼手扼颈全屏后效（screenTarget ping-pong 回写）
//uGrip 0..1：颈部暗角收缩，屏幕边缘向内收拢的暗红晕 + 轻度失色 + 高频细颤
//设计约束：收缩是单调渐紧 + 挣扎微颤（sin 高频小幅），刻意不做低频节拍脉动
//（心跳节拍是姊妹武器的专属母题）；无极角，仅径向距离 → 无缝
//Opaque 输出（整屏重写）
//ps_3_0
// ============================================================================

float uGrip;    //0..1 扼颈强度包络
float uTime;    //秒
float uAspect;  //宽/高

sampler screenSamp : register(s0);

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(screenSamp, coords);
    if (uGrip < 0.005)
        return src;

    //以画面中心为锚的径向距离（修正纵横比）
    float2 p = coords - 0.5;
    p.x *= uAspect;
    float r = length(p);

    //挣扎细颤：高频小幅抖动收缩半径（读作"掐紧的手在使劲"，不构成节拍）
    float tremor = sin(uTime * 34.0) * 0.012 + sin(uTime * 51.0 + 1.7) * 0.008;

    //暗角：uGrip 越高收得越紧
    float inner = lerp(0.78, 0.30, uGrip) + tremor * uGrip;
    float vign = smoothstep(inner, inner + 0.55, r) * saturate(uGrip * 1.25);

    //暗红压边 + 轻度失色（缺氧感）
    float lum = dot(src.rgb, float3(0.30, 0.59, 0.11));
    float3 desat = lerp(src.rgb, lum.xxx, vign * 0.45);
    float3 col = lerp(desat, desat * float3(0.42, 0.07, 0.06), vign);

    //最内圈残留一点硫火红边光，读作鬼手的火照在脸上
    float rim = smoothstep(inner + 0.02, inner + 0.16, r) * (1.0 - smoothstep(inner + 0.16, inner + 0.42, r));
    col += float3(0.30, 0.05, 0.01) * rim * uGrip;

    return float4(col, src.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
