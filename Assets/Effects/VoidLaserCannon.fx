// ============================================================================
// VoidLaserCannon.fx 虚空聚落激光炮束
// 炽白核心+紫等离子外晕；s0 束体 s1 噪声；Additive
// ============================================================================

sampler uImage0 : register(s0); //束体
sampler uImage1 : register(s1); //扰动噪声

float uTime;
float uOpacity;               //整体透明度
float uIntensity;             //总强度
float uCoreIntensity;       //核心亮度倍率
float uPulseSpeed;          //沿束脉冲速度
float uDistortionStrength;  //湍流扰动强度
float uBeamLength;          //束长(顶点)
float uBeamWidth;           //束宽(顶点)
float uPhaseBlend;          //0细线预警 1全宽

static const float3 CoreWhite     = float3(1.0, 0.97, 1.0);
static const float3 HotPink       = float3(1.0, 0.55, 0.95);
static const float3 MagentaPlasma = float3(0.85, 0.22, 1.0);
static const float3 DeepViolet    = float3(0.4,  0.12, 0.9);
static const float3 VoidIndigo    = float3(0.12, 0.04, 0.55);

struct VertexShaderInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = input.Position;
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    return output;
}

float hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + float2(1, 0));
    float c = hash(i + float2(0, 1));
    float d = hash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += a * valueNoise(p);
        p *= 2.1;
        a *= 0.5;
    }
    return v;
}

//末端柔和衰减，避免硬截断
float tailFalloff(float along)
{
    return 1.0 - smoothstep(0.7, 1.0, along);
}

//低频呼吸脉动，不制造沿束的高频条纹
float beamPulse(float along, float t)
{
    float p = sin(along * 3.5 - t * uPulseSpeed * 0.9) * 0.5 + 0.5;
    return 0.75 + p * 0.25;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float distCenter = abs(uv.y - 0.5) * 2.0;

    //沿束高速流动的湍流(两层不同频率叠加)
    float2 flowUV1 = float2(along * 3.5 - uTime * 2.2, uv.y * 1.8);
    float flow1 = tex2D(uImage1, flowUV1).r;
    float2 flowUV2 = float2(along * 9.0 - uTime * 4.8, uv.y * 3.2 + uTime * 0.2);
    float flow2 = tex2D(uImage1, flowUV2).g;

    //将湍流转化为横向偏移，让能量柱像真实等离子体一样呈现不规则起伏
    float turbulence = (flow1 * 0.6 + flow2 * 0.4 - 0.5) * uDistortionStrength;
    float warpedDist = saturate(distCenter + turbulence * 0.14);

    //核心：极窄高亮白芯，决定曝光感
    float core = 1.0 - smoothstep(0.0, 0.09, warpedDist);
    core = pow(core, 1.3);

    //热心：略宽的炽热白粉过渡带
    float hot = 1.0 - smoothstep(0.04, 0.22, warpedDist);
    hot = pow(hot, 1.6);

    //等离子层：主色调洋红
    float mid = 1.0 - smoothstep(0.12, 0.42, warpedDist);
    mid = pow(mid, 1.9);

    //外晕：柔和扩散到远处的紫色光晕，单调下降不形成环
    float glow = 1.0 - smoothstep(0.2, 0.95, warpedDist);
    glow = pow(glow, 2.4);

    //横向呼吸脉动
    float pulse = beamPulse(along, uTime);

    //蓄能阶段主动收窄整体宽度，让蓄能时呈预警细线
    float phaseWidth = lerp(0.55, 1.0, uPhaseBlend);
    mid *= phaseWidth;
    glow *= phaseWidth;
    hot *= lerp(0.7, 1.0, uPhaseBlend);

    //沿束fbm云纹：制造能量流线质感(限定在核心附近，避免形成外层条纹)
    float streakMask = 1.0 - smoothstep(0.0, 0.32, warpedDist);
    float streak = fbm(float2(along * 14.0 - uTime * 4.2, uv.y * 4.5));
    streak = pow(streak, 2.2) * streakMask;

    //末端衰减
    float tail = tailFalloff(along);

    //颜色合成
    float3 color = float3(0, 0, 0);

    //外晕靛蓝紫(单调衰减，无环)
    color += VoidIndigo * glow * 1.1;
    //等离子洋红中层
    float3 midColor = lerp(DeepViolet, MagentaPlasma, pulse);
    color += midColor * mid * 1.25;
    //热粉过渡，衔接mid和hot之间的色温
    color += HotPink * hot * 0.7;
    //能量流线(限定核心区，与热心叠加)
    color += lerp(HotPink, CoreWhite, streak) * streak * 0.6;
    //炽白核心
    color += CoreWhite * core * (1.6 + uCoreIntensity * 1.6) * pulse;
    //核心超亮溢出(单调曲线，不形成环)
    float bloom = pow(1.0 - warpedDist, 16.0);
    color += CoreWhite * bloom * (1.4 + uCoreIntensity);

    //强度合成
    float intensity = 0.0;
    intensity += core * (1.3 + uCoreIntensity);
    intensity += hot * 0.8;
    intensity += mid * 0.7;
    intensity += glow * 0.4;
    intensity += streak * 0.3;
    intensity *= pulse;
    intensity *= uIntensity;
    intensity *= tail;

    color *= tail;

    float alpha = saturate(intensity * uOpacity * input.Color.a);
    color *= input.Color.rgb;

    return float4(color, alpha);
}

//简化通道(降级)
float4 SimplePixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float distCenter = abs(uv.y - 0.5) * 2.0;

    float core = 1.0 - smoothstep(0.0, 0.1, distCenter);
    core = pow(core, 1.4);
    float mid = 1.0 - smoothstep(0.1, 0.4, distCenter);
    mid = pow(mid, 1.9);
    float glow = 1.0 - smoothstep(0.22, 0.9, distCenter);
    glow = pow(glow, 2.4);

    float pulse = sin(along * 4.0 - uTime * uPulseSpeed * 1.1) * 0.2 + 0.8;

    float3 color = VoidIndigo * glow * 1.0;
    color += lerp(DeepViolet, MagentaPlasma, pulse) * mid * 1.2;
    color += CoreWhite * core * (1.4 + uCoreIntensity) * pulse;

    float intensity = (core * 1.3 + mid * 0.7 + glow * 0.35) * pulse * uIntensity * tailFalloff(along);

    return float4(color * input.Color.rgb, saturate(intensity * uOpacity * input.Color.a));
}

technique Technique1
{
    pass VoidLaserPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }

    pass SimpleVoidLaserPass
    {
        PixelShader = compile ps_3_0 SimplePixelShaderFunction();
    }
}
