// ============================================================================
//SHPCModSteadyVolt.fx 稳压枪托「稳压放电」双 pass
//pass0 电荷环规：1x1 白图 quad，画蓄压弧/刻度齿/谐振外环/泄压闪烁/放电爆闪
//pass1 电压纹路：稳压强化束身的锯齿电压条带，quad 左端=束头，向尾渐隐
//s1 噪声；Additive
//极坐标审计（VFX.md）：fillT 仅作与 uCharge 的阈值/高斯比较，2π 跳变点位于
//顶部=蓄压弧起口（设计边界）；周期项只用 cos(24*theta)/sin(3*theta) 整数倍；
//噪声输入全部为笛卡尔 UV 或条带参数域，无 theta/fillT 喂手写噪声
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float fadeAlpha;
float uCharge;     //0~1 蓄压进度（满档=1）
float uResonance;  //0~1 满档谐振浓度（平滑量）
float uWindow;     //0~1 稳压窗口剩余比，临尽外环转暗警示
float uLeak;       //0~1 泄压相标记
float uFlash;      //0~1 放电闪光
float3 goldColor;  //稳压电金
float3 ionColor;   //电离青

static const float TAU = 6.28318531;

//════════ pass0 电荷环规 ════════
float4 RingPS(float2 coords : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float2 p = coords - 0.5;
    float dist = length(p) * 2.0;

    //顶部为 0、顺时针增长；2π 跳变点在顶部，即蓄压弧的起口
    float theta = atan2(p.x, -p.y);
    float fillT = frac(theta / TAU);

    //放电时环整体外弹
    float rMain = 0.60 + uFlash * 0.09;

    //笛卡尔域噪声：电荷表面颤动
    float jitter = tex2D(noiseSamp, frac(p * 1.6 + float2(uTime * 0.7, uTime * 0.23))).r;

    float3 color = 0.0;
    float alpha = 0.0;

    //A. 底轨：全环极淡青轨，未充段隐约可见
    float track = smoothstep(0.075, 0.015, abs(dist - rMain));
    color += ionColor * track * 0.10;
    alpha += track * 0.05;

    //B. 蓄压弧：顺时针铺开，青→金；沿弧脉冲流动（sin 整数倍无缝）
    float arcOn = step(fillT, uCharge) * step(0.01, uCharge);
    float flow = 0.8 + 0.35 * pow(saturate(sin(theta * 3.0 - uTime * 4.5)), 4.0);
    float leakFlick = 1.0 - uLeak * (0.35 + 0.30 * sin(uTime * 34.0));
    float3 arcCol = lerp(ionColor, goldColor, uCharge * uCharge);
    float arcA = track * arcOn * (0.55 + 0.30 * jitter) * flow * leakFlick;
    color += arcCol * arcA;
    alpha += arcA * 0.75;

    //C. 刻度齿：24 齿电压表盘，弧内亮金、弧外微青
    float tick = pow(saturate(cos(theta * 24.0)), 22.0);
    float tickBand = smoothstep(0.10, 0.03, abs(dist - rMain)) * tick;
    color += lerp(ionColor * 0.25, goldColor, arcOn) * tickBand * (0.5 + 0.5 * arcOn) * leakFlick;
    alpha += tickBand * 0.25;

    //D. 弧头亮点：高斯钉在充能前端，满档后让位于整环呼吸
    float head = exp(-pow((uCharge - fillT) * 16.0, 2.0))
        * step(fillT, uCharge) * step(0.01, uCharge) * step(uCharge, 0.999);
    float headGlow = head * track;
    color += lerp(ionColor, float3(1.0, 0.98, 0.9), 0.4) * headGlow * 1.2;
    alpha += headGlow * 0.8;

    //E. 谐振外环：满档金白呼吸，随窗口耗尽收暗熄灭，提示玩家把握击发时机
    float breath = 0.55 + 0.45 * sin(uTime * 6.5);
    float resR = 0.78 - 0.04 * breath * uResonance;
    float resRing = smoothstep(0.035, 0.008, abs(dist - resR))
        * uResonance * uWindow * breath;
    color += lerp(goldColor, float3(1.0, 0.97, 0.88), 0.35) * resRing * 0.9;
    alpha += resRing * 0.6;

    //F. 中心稳压微光：满档呼吸 + 放电提亮
    float coreGlow = exp(-dist * dist * 7.0) * (uResonance * (0.10 + 0.06 * breath) + uFlash * 0.55);
    color += lerp(goldColor, float3(1.0, 1.0, 0.95), 0.5) * coreGlow;
    alpha += coreGlow * 0.5;

    //G. 放电爆闪：整环白金炸亮
    float flashBurst = track * uFlash;
    color += (goldColor * 0.8 + float3(0.5, 0.5, 0.45)) * flashBurst * 1.2;
    alpha += flashBurst * 0.8;

    alpha = saturate(alpha) * fadeAlpha;
    return float4(color * fadeAlpha, alpha) * vColor;
}

//════════ pass1 稳压强化束电压纹路 ════════
float4 VoltBeamPS(float2 coords : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float2 uv = coords;      //x：0=束头 1=尾；y：0..1 横向
    float cy = uv.y - 0.5;

    //尾部渐隐与头部聚能
    float axial = pow(saturate(1.0 - uv.x), 1.35);
    float headCore = exp(-uv.x * uv.x * 80.0) * (1.0 - smoothstep(0.0, 0.5, abs(cy)));

    //电压锯齿：两条反相三角波折线向尾高速流动（条带参数域，无极坐标）
    float t1 = uv.x * 7.0 - uTime * 8.0;
    float tri1 = abs(frac(t1) - 0.5) * 4.0 - 1.0;
    float zig1 = tri1 * 0.16;
    float t2 = uv.x * 11.0 - uTime * 12.5 + 0.37;
    float tri2 = abs(frac(t2) - 0.5) * 4.0 - 1.0;
    float zig2 = tri2 * -0.11;

    float w1 = smoothstep(0.07, 0.0, abs(cy - zig1));
    float w2 = smoothstep(0.055, 0.0, abs(cy - zig2));
    //折点火花：三角波峰谷处提亮
    float peak1 = pow(saturate(abs(tri1)), 6.0);

    //中央稳压准线：笔直细亮，象征"稳"
    float mid = smoothstep(0.02, 0.0, abs(cy));

    //条带参数域噪声闪烁
    float flick = tex2D(noiseSamp, frac(float2(uv.x * 2.3 - uTime * 1.6, 0.35))).r;

    float3 color = 0.0;
    color += goldColor * w1 * (0.7 + 0.6 * peak1);
    color += ionColor * w2 * 0.55;
    color += lerp(goldColor, float3(1.0, 0.98, 0.9), 0.55) * mid * 0.85;
    color += lerp(goldColor, float3(1.0, 0.97, 0.9), 0.4) * headCore * 1.3;
    color *= (0.72 + 0.42 * flick) * axial;

    float alpha = saturate(w1 * 0.65 + w2 * 0.45 + mid * 0.55 + headCore * 0.9) * axial;
    alpha = saturate(alpha) * fadeAlpha;
    return float4(color * fadeAlpha, alpha) * vColor;
}

technique Technique1
{
    pass SteadyRingPass
    {
        PixelShader = compile ps_3_0 RingPS();
    }
    pass SteadyVoltBeamPass
    {
        PixelShader = compile ps_3_0 VoltBeamPS();
    }
}
