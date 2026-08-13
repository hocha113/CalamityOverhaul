// ============================================================================
//MLordEclipseSky.fx 日蚀天幕（月总）
//全覆盖预乘输出：天光被蚀的暗幕 + 蚀盘吞日冕环 + 星野浮现
//极角审计：冕焰噪声全走刚体旋转笛卡尔坐标；角向只用整数倍 sin(6θ)
//直线算术无分支，s1 绑定 PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float uEclipse;      //0~1 蚀度
float uAgitation;    //0~1 冕环躁动（蓄力）
float2 uScreenSize;
float uCamX;         //视差微移

//二维刚体旋转
float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

//格点哈希（星野闪烁）
float Hash21(float2 p)
{
    float v = sin(dot(p, float2(127.1, 311.7))) * 43758.5453;
    return v - floor(v);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float aspect = uScreenSize.x / max(uScreenSize.y, 1.0);
    float2 p = float2((coords.x - 0.5) * aspect + uCamX, coords.y - 0.5);

    //=========================================================
    //暗幕：自上而下的深空压顶，蚀度驱动
    //=========================================================
    float skyDarkA = lerp(0.0, 0.72, uEclipse) * (1.0 - coords.y * 0.35);
    float3 skyDark = float3(0.035, 0.03, 0.08);

    //=========================================================
    //蚀盘：上中位的巨盘吞日，冕环沿边缘燃烧
    //=========================================================
    float2 discCenter = float2(0.0, -0.24);
    float2 dp = p - discCenter;
    float r = length(dp);
    float discR = 0.185 + 0.012 * sin(uTime * 0.7);

    //盘体：近全黑
    float disc = smoothstep(discR + 0.004, discR - 0.02, r);

    //冕环：边缘环带 + 刚体旋转噪声舌 + 整数倍角瓣(sin 6θ 连续)
    float ring = exp(-pow((r - discR) * 34.0, 2.0));
    float2 flameUV = Rot(dp, uTime * 0.12) * 2.6;
    float flameN = tex2D(noiseSamp, flameUV + float2(uTime * 0.03, 0.0)).r;
    float2 dir = dp / max(r, 1e-4);
    float lobes = 0.5 + 0.5 * sin(6.0 * atan2(dir.y, dir.x) + uTime * 0.4);
    float corona = ring * (0.55 + 0.45 * flameN) * (0.7 + 0.3 * lobes);
    corona *= 1.0 + uAgitation * 1.6;
    //外冕柔散
    float outerGlow = exp(-pow(max(r - discR, 0.0) * 7.0, 1.4)) * 0.34 * (1.0 + uAgitation);

    //=========================================================
    //星野：蚀度越深星越显，格点哈希闪烁
    //=========================================================
    float2 starCell = floor(p * 42.0 + float2(uCamX * 6.0, 0.0));
    float starSeed = Hash21(starCell);
    float2 starLocal = frac(p * 42.0 + float2(uCamX * 6.0, 0.0)) - 0.5;
    float starDot = exp(-dot(starLocal, starLocal) * 90.0);
    float twinkle = 0.55 + 0.45 * sin(uTime * (2.0 + starSeed * 5.0) + starSeed * 40.0);
    float stars = starDot * step(0.82, starSeed) * twinkle * uEclipse;

    //=========================================================
    //合成（预乘输出）
    //=========================================================
    float3 cGold = float3(1.00, 0.79, 0.44);
    float3 cTeal = float3(0.38, 0.88, 0.82);
    float3 cWhite = float3(0.88, 0.95, 1.00);

    float3 color = skyDark * skyDarkA;
    float discA = disc * uEclipse * 0.96;
    //蚀盘吸光：直接压向近黑
    color = lerp(color, float3(0.012, 0.01, 0.03), discA);
    color += cGold * corona * uEclipse * 0.85;
    color += cGold * outerGlow * uEclipse * 0.5;
    color += cTeal * outerGlow * uEclipse * uAgitation * 0.4;
    color += cWhite * stars * 0.8;

    float alpha = saturate(skyDarkA + discA + corona * uEclipse * 0.6 + outerGlow * uEclipse * 0.35 + stars * 0.5);
    return float4(color, alpha) * vertexColor;
}

technique Technique1
{
    pass MLordEclipseSkyPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
