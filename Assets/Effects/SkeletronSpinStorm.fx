// ============================================================================
//SkeletronSpinStorm.fx 旋杀骨风暴涡流 / 仪式向心汇聚
//placeholder2 方块quad，SpriteBatch Immediate + Additive
//差速旋转全走笛卡尔刚体旋转坐标，无 atan2、无极角噪声（无接缝风险）
// ============================================================================

float uTime;
float uSpin;        //累计旋转角（头部 rotation 直接喂入）
float uIntensity;   //强度 0~1.2
float uConverge;    //0=离心涡流 1=向心汇聚
float3 uColorA;     //幽青主色
float3 uColorB;     //深青
float3 uBone;       //骨白

// 噪声固定 s1：本 shader 不采样 s0（画布只是白像素 quad），
// 旧 sampler_state 自动分配落 s0，被 SpriteBatch 用画布贴图覆写→涡流/吸入纹全读成辉光渐变；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float r = length(centered);

    //径向包络：内孔留给颅骨本体，外缘归零 + 画布护栏
    float radial = smoothstep(1.0, 0.42, r) * smoothstep(0.10, 0.30, r);
    float guard = smoothstep(0.98, 0.90, r);
    radial *= guard;

    //---- 差速旋转场：内圈拖拽快、外圈慢（连续的笛卡尔刚体旋转）----
    float swirlAngle = uSpin * (1.35 - r * 0.75) + uTime * 0.4;
    float2 sp = Rot(centered, swirlAngle);

    //涡流焰体：两档频率叠加
    float vortexA = tex2D(noiseSamp, sp * 0.62 + float2(uTime * 0.05, -uTime * 0.03)).r;
    float vortexB = tex2D(noiseSamp, sp * 1.55 + float2(-uTime * 0.09, uTime * 0.06)).r;
    float vortex = vortexA * 0.6 + vortexB * 0.4;

    //---- 向心汇聚流：沿半径抽入的丝缕（converge 模式）----
    float2 cp = Rot(centered, uSpin * 0.3);
    float inflow = tex2D(noiseSamp, cp * 1.15 + float2(0.0, uTime * 0.9 * uConverge) + r * 1.8).r;
    float convergeStreak = smoothstep(0.55, 0.9, inflow) * uConverge;

    //---- 骨屑闪点：高频阈值细粒 ----
    float fleck = tex2D(noiseSamp, sp * 3.3 + float2(uTime * 0.13, uTime * 0.21)).r;
    float boneFleck = smoothstep(0.78, 0.95, fleck) * radial;

    //组装：涡焰过阈成舌，稀薄处压透明（不压黑）
    float tongue = smoothstep(0.42, 0.85, vortex);
    float density = tongue * radial * uIntensity;

    float3 color = uColorB * density * 0.7
        + uColorA * density * tongue * 0.8
        + uColorA * convergeStreak * radial * 0.85
        + uBone * boneFleck * 0.55 * uIntensity;

    float alpha = saturate(density * 0.8 + convergeStreak * radial * 0.6 + boneFleck * 0.3) * vertexColor.a;

    //加色批输出：颜色即能量
    return float4(color, alpha);
}

technique Technique1
{
    pass SkeletronSpinStormPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
