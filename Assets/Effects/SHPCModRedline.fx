// ============================================================================
//SHPCModRedline.fx 速射枪管「过热红线」枪口热场仪表
//画布中心=枪口，+X 沿射击方向；Additive
//极坐标审计：theta 仅以 |theta| 形式消费（-X 缝两侧同值，处处连续），
//且全部乘 arcMask(|theta|<ARC_HALF<PI) 后使用；动效噪声均为笛卡尔输入，无接缝路径
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float fadeAlpha;        //整体透明度 0~1
float heatRatio;        //0~1 平滑显示热量
float ventFlash;        //0~1 过热喷射态
float coolProgress;     //0~1 冷却复位进度（非冷却期恒 0）
float alarmBlink;       //0~1 红线警报强度

static const float ARC_HALF = 1.9;   //弧表半张角(rad)，<PI 保证不触及 atan2 缝
static const float ARC_R = 0.335;    //弧表半径

//黑体色温近似：暗红→炽橙→白炽
float3 heatRamp(float t)
{
    float3 c = lerp(float3(0.45, 0.08, 0.03), float3(1.0, 0.45, 0.10), saturate(t * 2.0));
    return lerp(c, float3(1.0, 0.93, 0.80), saturate(t * 2.0 - 1.0));
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float r = length(p);
    float cooling = step(0.001, coolProgress);

    float heatGlow = pow(saturate(heatRatio), 1.5);
    float3 hotCol = heatRamp(max(heatRatio, ventFlash * 0.95));
    float3 steelBlue = float3(0.32, 0.45, 0.60);

    float3 color = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    //---- A. 枪口色温辉光核：热量越高越亮越白，冷却期转冷钢蓝并压暗
    float core = exp(-r * r * 14.0) * saturate(heatGlow + ventFlash);
    float3 coreCol = lerp(hotCol, steelBlue, cooling * 0.85);
    core *= 1.0 - cooling * 0.55;
    color += coreCol * core * (0.75 + ventFlash * 0.55);
    alpha += core * 0.5;

    //---- B. 前向热浪羽流：第一层噪声偏移第二层采样（自扰动=热浪扭曲），沿 +X 喷涌
    float coneAxis = saturate(p.x * 1.6);
    float coneEdge = 1.0 - smoothstep(0.0, 0.16 + p.x * 0.42, abs(p.y));
    float coneFade = 1.0 - smoothstep(0.15, 0.95, p.x);
    float cone = coneAxis * coneEdge * coneFade;
    float nA = tex2D(noiseSamp, float2(p.x * 1.4 - uTime * 2.2, p.y * 2.6)).r;
    float2 uvB = float2(p.x * 2.3 - uTime * 3.4, p.y * 3.4 + 0.37) + (nA - 0.5) * 0.34;
    float nB = tex2D(noiseSamp, uvB).g;
    float plume = cone * (0.35 + 0.65 * nB) * (heatGlow * 0.5 + ventFlash * 1.05);
    plume *= 1.0 - cooling * 0.8;
    color += hotCol * plume * 0.8;
    color += float3(1.0, 0.93, 0.80) * plume * plume * ventFlash * 0.5;
    alpha += plume * 0.5;

    //---- C. 侧向泄压蒸汽刀：过热喷射专属，垂直枪管 ±Y 双喷口
    float jetSpan = abs(p.y);
    float jetEdge = 1.0 - smoothstep(0.0, 0.10 + jetSpan * 0.34, abs(p.x) - 0.04);
    float jetFade = smoothstep(0.05, 0.16, jetSpan) * (1.0 - smoothstep(0.35, 0.85, jetSpan));
    float jetPulse = 0.72 + 0.28 * sin(uTime * 11.0 + jetSpan * 9.0);
    float nJet = tex2D(noiseSamp, float2(jetSpan * 2.4 - uTime * 2.8, p.x * 3.1 + 0.61)).b;
    float jet = jetEdge * jetFade * (0.4 + 0.6 * nJet) * jetPulse * ventFlash;
    color += float3(1.0, 0.96, 0.88) * jet * 0.9;
    color += hotCol * jet * 0.35;
    alpha += jet * 0.6;

    //---- D. 热量弧表：唯一极坐标消费区，自正前方向两端对称生长（镜像/旋转等变）
    float absT = abs(atan2(p.y, p.x));
    float arcMask = smoothstep(ARC_HALF, ARC_HALF - 0.10, absT);
    float ringMask = smoothstep(0.034, 0.012, abs(r - ARC_R)) * arcMask;
    float prog = saturate(absT / ARC_HALF);   //0=正前方 1=弧末端

    //槽底暗带
    color += float3(0.16, 0.19, 0.24) * ringMask * 0.5;
    alpha += ringMask * 0.22;

    //填充：积热=热色随热量生长；冷却=复位进度蓝条
    float fillLevel = lerp(heatRatio, coolProgress, cooling);
    float fillOn = smoothstep(fillLevel + 0.015, fillLevel - 0.015, prog);
    float3 fillCol = lerp(heatRamp(prog * max(heatRatio, ventFlash)), float3(0.35, 0.62, 0.85), cooling);
    float fillGlow = ringMask * fillOn * (0.8 + 0.2 * sin(uTime * 7.0 + prog * 12.0));
    color += fillCol * fillGlow * 1.15;
    alpha += fillGlow * 0.55;

    //游标亮点：填充前沿
    float cursor = exp(-pow((prog - fillLevel) * 26.0, 2.0)) * ringMask * step(0.02, fillLevel);
    color += lerp(float3(1.0, 0.9, 0.7), float3(0.75, 0.9, 1.0), cooling) * cursor * 1.2;
    alpha += cursor * 0.6;

    //红线区：弧两端 15% 常亮暗红，警报时方波爆闪
    float redZone = smoothstep(0.82, 0.87, prog) * ringMask;
    float blink = 0.35 + 0.65 * step(0.5, frac(uTime * 1.55));
    float redPulse = redZone * (0.4 + alarmBlink * blink * 1.2) * (1.0 - cooling * 0.75);
    color += float3(1.0, 0.14, 0.07) * redPulse;
    alpha += redPulse * 0.5;

    //刻度：每 10% 一道细亮线
    float tickLine = smoothstep(0.045, 0.0, abs(frac(prog * 10.0 + 0.5) - 0.5)) * ringMask;
    color += float3(0.5, 0.6, 0.7) * tickLine * 0.22;
    alpha += tickLine * 0.08;

    //---- E. 枪口警报灯环：临界与喷射时红闪
    float lamp = smoothstep(0.026, 0.010, abs(r - 0.135)) * alarmBlink * blink;
    color += float3(1.0, 0.16, 0.08) * lamp * 0.9;
    alpha += lamp * 0.4;

    alpha = saturate(alpha) * fadeAlpha;
    return float4(color * fadeAlpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModRedlinePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
