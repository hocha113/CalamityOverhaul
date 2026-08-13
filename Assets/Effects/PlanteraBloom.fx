// ============================================================================
//PlanteraBloom.fx 世纪之花绽放花环
//quad 归一化UV，中心0.5；加色/预乘两相宜(输出已乘alpha)
//花瓣扇边环+可选双安全缺口+内域微光
//极角审计：theta 唯一消费是 sin(6θ)/sin(12θ)(整数倍连续)与 cos(θ-uGap)(差角连续)
//噪声走刚体旋转笛卡尔坐标；无分支
//ps_3_0
// ============================================================================

float uTime;
float uProgress;   //环扩张进度 0~1
float uIntensity;  //总强度
float uPhase2;     //0绿粉 1品红
float uGapOn;      //0无缺口 1双缺口
float uGap1;       //缺口1方位角
float uGap2;       //缺口2方位角
float uGapCos;     //cos(缺口半宽)
float seed;

// 噪声固定 s1：本 shader 不采样 s0（画布只是白像素 quad），
// 旧 sampler_state 自动分配落 s0，被 SpriteBatch 用画布贴图覆写→花瓣颗粒读成辉光渐变；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

float4 BloomRingPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);
    float theta = atan2(p.y, p.x);

    float ringR = uProgress * 0.82;

    //花瓣扇边：整数倍角波浪，随时间慢转(相位加常数不破坏连续性)
    float spin = uTime * 0.8 + seed * 7.0;
    float petal = sin(6.0 * theta + spin) * 0.038 + sin(12.0 * theta - spin * 0.7) * 0.014;
    float band = exp(-pow((r - ringR - petal) * 16.0, 2.0));

    //第二圈内衬余波
    float echo = exp(-pow((r - ringR * 0.82 + petal * 0.6) * 22.0, 2.0)) * 0.4;

    //内域充盈微光(蓄力/爆心读数)
    float fill = saturate(1.0 - r / max(ringR, 0.05)) * 0.20;

    //细颗粒：刚体旋转笛卡尔噪声，不吃 theta
    float cs = cos(uTime * 0.11 + seed);
    float sn = sin(uTime * 0.11 + seed);
    float2 rp = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
    float grain = tex2D(noiseSamp, rp * 1.4 + seed).r;
    band *= 0.75 + 0.5 * grain;

    //双安全缺口：cos(差角)连续无缝
    float gapMask = 1.0;
    float inGap1 = smoothstep(uGapCos, uGapCos + 0.08, cos(theta - uGap1));
    float inGap2 = smoothstep(uGapCos, uGapCos + 0.08, cos(theta - uGap2));
    gapMask = 1.0 - uGapOn * saturate(inGap1 + inGap2);

    //缺口边缘反而更亮一线(告诉玩家这是门)
    float gapEdge = uGapOn * saturate(
        exp(-pow((cos(theta - uGap1) - uGapCos) * 30.0, 2.0))
      + exp(-pow((cos(theta - uGap2) - uGapCos) * 30.0, 2.0))) * band * 0.8;

    //调色
    float3 cCore = lerp(float3(0.72, 1.00, 0.42), float3(1.00, 0.50, 0.78), uPhase2);
    float3 cEdge = lerp(float3(0.95, 0.55, 0.68), float3(0.85, 0.20, 0.50), uPhase2);

    float3 color = cCore * (band * gapMask + echo * gapMask + fill)
                 + cEdge * band * gapMask * 0.6
                 + cCore * gapEdge;

    //扩张末期整体衰减+画布边缘保险
    float fade = (1.0 - uProgress * uProgress * 0.75) * uIntensity;
    float guard = 1.0 - smoothstep(0.90, 0.99, r);

    float a = saturate((band * gapMask + echo * gapMask * 0.6 + fill + gapEdge) * fade) * guard;
    return float4(color * a, a) * vertexColor;
}

technique Technique1
{
    pass BloomRingPass
    {
        PixelShader = compile ps_3_0 BloomRingPS();
    }
}
