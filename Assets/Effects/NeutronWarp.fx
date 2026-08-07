// ============================================================================
//NeutronWarp.fx 中子星扭曲位移图
//输出 R方向 G强度 A混合；ps_3_0
//
//硬约束：强度(G)必须在 quad 矩形边界之前归零。
//WarpShader 只读 R/G，不读 A；而 RT 用的 AlphaBlend 是预乘式(One,InvSrcAlpha)，
//A 既不参与消费也不会衰减已写入的 R/G。所以边缘羽化只能做在 G 上，
//否则位移场会满值写到 quad 边界被硬切，再被 shift*28 的蓝移放大成矩形亮边。
// ============================================================================

float uTime;
float uIntensity;
float uProgress;
float uRadius;
float uRotation;

#define PI  3.14159265
#define TAU 6.28318530

//逐轴羽化：对径向场和细长喷流都成立，四角自然归零
float QuadFeather(float2 centered)
{
    float2 e = saturate((0.5 - abs(centered)) / 0.055);
    e = e * e * (3.0 - 2.0 * e);
    return e.x * e.y;
}

//阈下四通道一起归零：让 WarpShader 的 any() 真能短路，
//也避免预乘混合下拿 alpha 擦掉别的扭曲源已写入的位移
float4 PackWarp(float direction, float magnitude, float alpha)
{
    magnitude = saturate(magnitude);
    float live = step(0.0008, magnitude);
    return float4(direction * live, magnitude * live, 0, saturate(alpha) * live);
}

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

    //边缘衰减必须作用在强度上，否则矩形边界处仍是满值硬切
    float edge = smoothstep(1.5, 0.25, normDist) * QuadFeather(centered);
    magnitude *= edge;

    return PackWarp(direction, magnitude, edge * uProgress);
}

//ShockwaveRing 冲击波环
float4 ShockwaveRingPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normDist = dist / max(uRadius, 0.001);

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

    //波前会扩到 normDist 1.2，必须靠羽化收尾，否则冲出 quad 时被切成矩形亮边
    float edge = smoothstep(1.8, 0.7, normDist) * QuadFeather(centered);
    magnitude *= edge;

    float alpha = (ring + ring2 * 0.6 + ring3 * 0.3 + residual) * edge;

    return PackWarp(direction, magnitude, alpha);
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

    //沿轴包络：原本只有横向衰减，柱子上下两端是齐平硬切(KamuiLine 早已羽化，此处漏改)
    float axial = smoothstep(0.5, 0.34, abs(centered.y));
    float edge = axial * QuadFeather(centered);
    magnitude *= edge;

    float alpha = (coreFalloff + wingFalloff * 0.5) * edge * uProgress;

    return PackWarp(direction, magnitude, alpha);
}

//GravitationalLens 引力透镜
float4 GravitationalLensPS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normDist = dist / max(uRadius, 0.001);

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

    float edge = smoothstep(1.8, 0.25, normDist) * QuadFeather(centered);
    magnitude *= edge;

    return PackWarp(direction, magnitude, edge * uProgress);
}

//KamuiLine 神威疾走沿线拉扯
//局部 Y 为线轴、X 为横向；uRotation = 位移的绝对屏幕角（几何由 CPU 侧旋转对齐）
//两端沿轴羽化防硬切，横向核+翼双高斯，沿线湍流让拉扯有呼吸
float4 KamuiLinePS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;

    float lateral = abs(centered.x);
    float core = exp(-pow(lateral / 0.10, 2.0));
    float wing = exp(-pow(lateral / 0.26, 2.0)) * 0.4;

    float endFade = smoothstep(0.50, 0.34, abs(centered.y));

    float turb = fbm2(centered * float2(6.0, 2.2) + uTime * float2(0.3, 1.6));
    float breath = 0.72 + 0.28 * turb;

    //翼高斯在 |x|=0.5 处尚有约 0.01 残值，仍需逐轴羽化收干净
    float feather = QuadFeather(centered);

    float direction = frac(uRotation / TAU + 0.5);
    float magnitude = (core + wing) * endFade * breath * uIntensity * uProgress * feather;
    float alpha = (core + wing * 0.6) * endFade * uProgress * feather;

    return PackWarp(direction, magnitude, alpha);
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

technique KamuiLine
{
    pass P0
    {
        PixelShader = compile ps_3_0 KamuiLinePS();
    }
}
