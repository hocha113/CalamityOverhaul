// ============================================================================
//NeutronWarp.fx 中子星扭曲位移图
//输出 R方向 G强度 A混合；ps_3_0
// ============================================================================

float uTime;
float uIntensity;
float uProgress;
float uRadius;
float uRotation;

#define PI  3.14159265
#define TAU 6.28318530

//哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//分形噪声
float fbm2(float2 p)
{
    float v = 0.0;
    float amp = 0.5;
    float2 shift = float2(17.3, 31.7);
    for (int i = 0; i < 4; i++)
    {
        v += valueNoise(p) * amp;
        p = p * 2.17 + shift;
        amp *= 0.5;
    }
    return v;
}

//GravitationalVortex 重力漩涡
float4 GravitationalVortexPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normDist = dist / max(uRadius, 0.001);

    if (normDist > 1.5)
        return float4(0, 0, 0, 0);

    //同心引力波纹
    //模拟原33层DiffusionCircle叠绘产生的干涉环带
    float ringPhase = normDist * 7.0 - uTime * 4.0;
    float rings = pow(saturate(0.5 + 0.5 * sin(ringPhase * TAU)), 0.5);
    //次级高频细节波纹
    float subRings = 0.65 + 0.35 * sin(normDist * 22.0 - uTime * 10.0);

    //致密引力场核心
    float gravField = 1.0 / (normDist * normDist + 0.05);
    gravField = min(gravField, 15.0) / 15.0; //归一化到[0,1]

    //爱因斯坦环
    float eRingR = 0.5 + 0.04 * sin(uTime * 2.0);
    float einsteinRing = exp(-pow((normDist - eRingR) * 7.0, 2.0)) * 0.55;

    //差分旋转漩涡
    float angVel = 1.0 / (normDist + 0.08);
    float swirl = angVel * 0.65;

    //湍流磁场
    float noise = fbm2(centered * 7.0 + float2(uTime * 0.5, uTime * 0.4));
    float noiseOff = (noise - 0.5) * 0.35;

    //径向引力脉冲
    float pulse = sin(normDist * 16.0 - uTime * 8.0) * 0.18;
    pulse *= exp(-normDist * 2.5);

    //位移方向：径向 + 漩涡 + 湍流
    float direction = angle + swirl + noiseOff + uRotation;
    direction = frac(direction / TAU + 0.5);

    //位移强度：引力场 × 环带调制 + 爱因斯坦环 + 脉冲
    float magnitude = gravField * rings * subRings + einsteinRing + pulse;
    magnitude *= uProgress * uIntensity;
    magnitude = saturate(magnitude);

    //边缘衰减
    float alpha = smoothstep(1.5, 0.25, normDist) * uProgress;

    return float4(direction, magnitude, 0, saturate(alpha));
}

//ShockwaveRing 冲击波环
float4 ShockwaveRingPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normDist = dist / max(uRadius, 0.001);

    if (normDist > 1.8)
        return float4(0, 0, 0, 0);

    //主冲击波前沿
    float ringPos = uProgress * 1.2;
    float ringWidth = 0.06 + uProgress * 0.04;
    float ring = exp(-pow((normDist - ringPos) / ringWidth, 2.0));

    //次级反射冲击波
    float ring2Pos = ringPos * 0.6;
    float ring2 = exp(-pow((normDist - ring2Pos) / (ringWidth * 1.3), 2.0)) * 0.55;

    //第三级波
    float ring3Pos = ringPos * 0.3;
    float ring3 = exp(-pow((normDist - ring3Pos) / (ringWidth * 1.6), 2.0)) * 0.3;

    //中心残余引力坍缩
    float residual = exp(-normDist * normDist * 5.0) * 0.45 * (1.0 - uProgress);

    //高频环波纹
    float ripple = 0.65 + 0.35 * sin(normDist * 28.0 - uTime * 7.0);

    //方位角噪声扰动
    float edgeNoise = valueNoise(float2(angle * 3.0 / TAU + uTime * 0.3, normDist * 4.0));
    float noiseMod = 0.7 + edgeNoise * 0.6;

    //位移方向: 径向向外
    float direction = frac(angle / TAU + 0.5);

    //位移强度: 多环叠加 × 波纹 × 噪声
    float magnitude = (ring + ring2 + ring3 + residual) * noiseMod * ripple;
    magnitude *= uIntensity;
    magnitude = saturate(magnitude);

    float alpha = (ring + ring2 * 0.6 + ring3 * 0.3 + residual) * smoothstep(1.8, 0.7, normDist);

    return float4(direction, magnitude, 0, saturate(alpha));
}

//RelativisticJet 相对论性喷流
float4 RelativisticJetPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;

    //磁力管约束
    float lateralDist = abs(centered.x);
    float coreFalloff = exp(-pow(lateralDist / 0.08, 2.0));
    float wingFalloff = exp(-pow(lateralDist / 0.22, 2.0)) * 0.35;

    //冲击钻石结构
    float shockDiamonds = 0.55 + 0.45 * sin(centered.y * 18.0 + uTime * 5.0);
    shockDiamonds *= 0.7 + 0.3 * sin(centered.y * 7.0 - uTime * 2.5);

    //开尔文-亥姆霍兹不稳定性
    float kh = sin(centered.y * 6.0 + uTime * 4.5) * 0.2 * lateralDist;

    //扭结不稳定性
    float kink = sin(centered.y * 16.0 - uTime * 7.0) * 0.1;

    //磁场重联闪烁
    float reconnect = valueNoise(float2(centered.y * 4.0 + 0.5, uTime * 2.5));
    reconnect = smoothstep(0.35, 0.65, reconnect) * 0.4;

    //喷流湍流
    float turb = fbm2(centered * float2(10.0, 3.0) + uTime * float2(0.4, 2.0));
    float turbOff = (turb - 0.5) * 0.35;

    //方向: 沿轴 + 扰动
    float direction = PI * 0.5 + kh + kink + turbOff;
    direction = frac(direction / TAU + 0.5);

    //强度: 核心+翼 × 冲击结构 + 重联
    float jetPower = (coreFalloff + wingFalloff) * shockDiamonds + reconnect * coreFalloff;
    float magnitude = jetPower * uIntensity * uProgress;
    magnitude = saturate(magnitude);

    float alpha = (coreFalloff + wingFalloff * 0.5) * uProgress;

    return float4(direction, magnitude, 0, saturate(alpha));
}

//GravitationalLens 引力透镜
float4 GravitationalLensPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normDist = dist / max(uRadius, 0.001);

    if (normDist > 1.8)
        return float4(0, 0, 0, 0);

    //广义相对论偏转
    float deflection = 1.0 / (normDist * normDist + 0.08);
    deflection = min(deflection, 10.0) / 10.0;

    //菲涅尔环带
    float fresnelRings = 0.55 + 0.45 * sin(normDist * 10.0 * PI);

    //爱因斯坦环增亮
    float eRing = exp(-pow((normDist - 0.42) * 7.0, 2.0)) * 0.5;

    //闪烁调制
    float scintillation = 0.82 + 0.18 * sin(angle * 3.0 + uTime * 4.0);

    //径向向内
    float inwardAngle = angle + PI;
    float direction = frac(inwardAngle / TAU + 0.5);

    //强度: 偏转 × 菲涅尔环 × 闪烁 + 爱因斯坦环
    float magnitude = (deflection * fresnelRings + eRing) * scintillation;
    magnitude *= uIntensity * uProgress;
    magnitude = saturate(magnitude);

    float alpha = smoothstep(1.8, 0.25, normDist) * uProgress;

    return float4(direction, magnitude, 0, saturate(alpha));
}

technique GravitationalVortex
{
    pass P0
    {
        PixelShader = compile ps_3_0 GravitationalVortexPS();
    }
}

technique ShockwaveRing
{
    pass P0
    {
        PixelShader = compile ps_3_0 ShockwaveRingPS();
    }
}

technique RelativisticJet
{
    pass P0
    {
        PixelShader = compile ps_3_0 RelativisticJetPS();
    }
}

technique GravitationalLens
{
    pass P0
    {
        PixelShader = compile ps_3_0 GravitationalLensPS();
    }
}
