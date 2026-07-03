// ============================================================================
//OniCrimsonImpactPost.fx 绯红裂空斩屏幕后处理：压暗聚焦 + 冲击白闪 + 径向冲击拉丝
//采样 uImage0 屏幕；uDim/uFlash 均为 0 时透传
//ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

float uDim;         //0..1 场景压暗强度
float uFlash;       //0..1 冲击白闪强度
float2 uCenter;     //冲击/聚焦点 uv
float uAspect;      //宽高比，校正距离场
float3 uDimTint;    //压暗色调(暗紫)
float uDesat;       //压暗附带的去饱和量 0..1

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 toC = coords - uCenter;
    toC.x *= uAspect;
    float dist = length(toC);

    //冲击帧径向拉丝：白闪期间向冲击点方向做少量位移采样，拉出速度感
    float pull = uFlash * 0.020 * smoothstep(0.05, 0.55, dist);
    float2 dirC = dist > 1e-4 ? toC / dist : float2(0.0, 0.0);
    float2 pullUV = float2(dirC.x / max(uAspect, 1e-3), dirC.y) * pull;

    float4 src = tex2D(uImage0, coords);
    if (pull > 1e-4)
    {
        float3 acc = src.rgb * 0.4;
        acc += tex2D(uImage0, coords - pullUV * 0.5).rgb * 0.3;
        acc += tex2D(uImage0, coords - pullUV).rgb * 0.2;
        acc += tex2D(uImage0, coords - pullUV * 1.6).rgb * 0.1;
        src.rgb = acc;
    }

    //压暗聚焦：冲击点附近保亮，四周压向暗紫；边缘再叠暗角
    float focus = smoothstep(0.10, 0.72, dist);
    float dimW = uDim * (0.45 + 0.55 * focus);

    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float3 col = lerp(src.rgb, float3(lum, lum, lum), uDesat * dimW);
    col *= lerp(float3(1.0, 1.0, 1.0), uDimTint, dimW);

    float2 c = coords * 2.0 - 1.0;
    col *= 1.0 - dot(c, c) * 0.16 * uDim;

    //白闪：冲击点径向衰减，收紧半径、压低峰值，避免整屏白爆
    float flashFall = exp(-dist * dist * 13.0);
    col += float3(1.0, 0.93, 0.85) * uFlash * flashFall * 0.9;

    return float4(col, src.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
