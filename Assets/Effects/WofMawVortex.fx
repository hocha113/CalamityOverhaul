// ============================================================================
//WofMawVortex.fx 口部吸引漩涡
//口器中心方形面片；肉质吞咽涡：暗喉+三臂肉旋+向心血流+湿光弧
//极角审计：theta 消费仅 sin(3θ...)与 sin(θ...)整数倍角、θ/2π×整数喂wrap采样器，连续
//预乘输出 AlphaBlend(暗喉需要遮蔽力，加色画不出吞光的洞)
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //PerlinNoise 512

float uTime;
float uProgress;   //展开进度 0~1
float uIntensity;
float uSuck;       //吸力强度 0~1(流速)

static const float PI = 3.14159265;

//刚体旋转(无接缝)
float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0)
    {
        return float4(0, 0, 0, 0);
    }
    float theta = atan2(p.y, p.x);
    float lr = log(r * 4.0 + 1.0);

    //吞咽脉动：整体半径周期收缩，叠加低频不对称(k=1 整数倍角连续)
    float gulp = 1.0 + 0.05 * sin(uTime * 3.6) + 0.028 * sin(uTime * 1.9 + theta);
    float rr = saturate(r * gulp);

    //=== 三臂肉旋：整数倍角+对数半径相位，向心卷入 ===
    float armPhase = sin(3.0 * theta - lr * 5.2 + uTime * (2.2 + uSuck * 2.4));
    float arm = pow(saturate(armPhase * 0.5 + 0.5), 3.2);
    //臂上肉质咬边：刚体旋转坐标采噪声
    float armMeat = tex2D(uImage1, Rot(p, uTime * 0.5) * 0.55 + 0.5).r;
    arm *= 0.55 + 0.75 * armMeat;

    //=== 向心血流：θ/2π×4(整数,wrap安全) × 对数半径滚动 ===
    float2 flowUV = float2(theta / (2.0 * PI) * 4.0, lr * 1.9 + uTime * (1.1 + uSuck * 1.6));
    float flow = tex2D(uImage1, flowUV).g;
    flow = pow(saturate(flow * 1.55 - 0.42), 2.6);

    //=== 暗喉：中心吸光肉洞 ===
    float throat = smoothstep(0.30, 0.10, rr);
    //喉缘湿光弧：偏心高光(各向异性，拒绝圆形塑料高光)
    float2 hp = p - float2(-0.10, -0.13);
    float wetArc = exp(-pow((length(hp) - 0.24) * 9.0, 2.0)) * smoothstep(0.6, 0.2, abs(hp.y * 2.2 - hp.x));

    //=== 包络 ===
    float reach = 0.55 + 0.45 * uProgress;
    float envelope = smoothstep(reach, reach * 0.5, rr) * smoothstep(0.04, 0.22, rr) * uProgress;

    //=== 调色 ===
    float3 cThroat = float3(0.05, 0.004, 0.008);
    float3 cMeat   = float3(0.52, 0.075, 0.08);
    float3 cHot    = float3(0.93, 0.24, 0.12);
    float3 cWet    = float3(1.0, 0.62, 0.46);

    //亮部贡献(自带遮罩=自带alpha)
    float armA = arm * envelope;
    float flowA = flow * envelope * (0.5 + uSuck * 0.6);
    float3 color = cMeat * armA + cHot * flowA;
    float alpha = saturate(armA * 0.8 + flowA * 0.7);

    //暗喉以吞咽进度介入：占据中心、遮蔽下层
    float throatA = throat * uProgress;
    color = lerp(color, cThroat * throatA, throatA);
    alpha = max(alpha, throatA * 0.94);
    //喉缘湿光弧叠加(预乘直加)
    color += cWet * wetArc * throatA * 0.7;

    color *= uIntensity;
    alpha = saturate(alpha * uIntensity);
    return float4(color, alpha) * vertexColor.a;
}

technique Technique1
{
    pass WofMawVortexPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
