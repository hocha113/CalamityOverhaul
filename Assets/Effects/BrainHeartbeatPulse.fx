// ============================================================================
// BrainHeartbeatPulse.fx 克脑心跳压迫全屏效果
// 收缩拍径向挤压+色散 / 二阶段血幕 / 骤停黑幕（心口留视界）/ 终爆负片帧
// 直线算术+朴素 tex2D，无动态分支（FNA 翻译安全）
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图（全屏拷贝）

float uTime;
float uAspect;        //屏幕宽高比
float2 uCenter;       //脑心（归一化屏幕uv）
float uPulse;         //本帧心跳脉冲包络 0~1.5
float uIntensity;     //整体强度 0~1
float uVeil;          //血幕 0~1
float uBlackout;      //黑幕 0~1
float uFlash;         //负片帧 0~1

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 d = (coords - uCenter) * float2(uAspect, 1.0);
    float dist = length(d) + 1e-5;
    float2 dir = d / dist;
    dir.x /= uAspect;

    //收缩拍：向心挤压采样（近处强远处弱），带轻色散
    float squeeze = uPulse * uIntensity * 0.011 * exp(-dist * 1.6);
    float2 offset = dir * squeeze;

    float3 col;
    col.r = tex2D(uImage0, coords - offset * 1.35).r;
    col.g = tex2D(uImage0, coords - offset).g;
    col.b = tex2D(uImage0, coords - offset * 0.7).b;

    //亮度参考
    float lum = dot(col, float3(0.299, 0.587, 0.114));

    //血幕：暗部染血、轻度去饱和（组织液渗满视界的观感）
    float3 veilTone = float3(0.42, 0.06, 0.09);
    float veilAmt = uVeil * (1.0 - lum * 0.55);
    float3 desat = lerp(col, float3(lum, lum, lum), uVeil * 0.22);
    col = lerp(desat, veilTone * (0.35 + lum * 0.85), veilAmt * 0.5);

    //心跳拍点在血幕上再压一层边缘暗角（脉动的视界收缩）
    float vign = smoothstep(0.42, 1.05, dist);
    float vignAmt = (uVeil * 0.28 + uPulse * uIntensity * 0.34) * vign;
    col *= 1.0 - vignAmt;

    //骤停黑幕：全屏压黑，脑心附近留出呼吸视界；脉冲瞬间掀开黑幕（闪现拍照明）
    float sight = exp(-dist * 2.3);
    float dark = uBlackout * (1.0 - sight * 0.55) * (1.0 - saturate(uPulse) * 0.72);
    col *= 1.0 - dark;

    //黑幕下脑心一点余红（心口的位置感）
    col += veilTone * uBlackout * sight * 0.14;

    //终爆负片帧：亮度反相并染血
    float3 invTone = float3(1.0, 0.86, 0.82) * (1.0 - lum);
    col = lerp(col, invTone, saturate(uFlash) * 0.9);

    return float4(col, 1.0) * vertexColor;
}

technique Technique1
{
    pass BrainHeartbeatPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
