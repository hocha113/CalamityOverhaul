// ============================================================================
//PlanteraSporeFog.fx 世纪之花孢子雾
//quad 归一化UV，中心0.5；预乘输出+AlphaBlend
//材质=悬浮孢子云：噪声撕边软雾+内部漂浮孢子颗粒荧光+呼吸明灭
//全笛卡尔无极角，无分支
//ps_3_0
// ============================================================================

float uTime;
float uBirth;   //出生展开 0~1
float uDecay;   //消散 0~1(边缘先蚀)
float uPhase2;  //0绿 1品红混
float seed;

// 噪声固定 s1：本 shader 不采样 s0（画布只是白像素 quad），
// 旧 sampler_state 自动分配落 s0，被 SpriteBatch 用画布贴图覆写→孢子雾撕边读成辉光渐变；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

float4 SporeFogPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0; //-1~1
    float r = length(p);

    //---------------------------------------------------------
    //云体：径向软衰减×两尺度滚动噪声撕边(有机云形非正圆)
    //---------------------------------------------------------
    float n1 = tex2D(noiseSamp, coords * 0.9 + float2(uTime * 0.03, seed)).r;
    float n2 = tex2D(noiseSamp, coords * 2.3 + float2(seed, -uTime * 0.05)).r;
    float cloudEdge = 0.62 + (n1 - 0.5) * 0.5 + (n2 - 0.5) * 0.24;

    //消散把阈值往里推，边缘先被吃
    cloudEdge *= (1.0 - uDecay * 0.85);
    float body = smoothstep(cloudEdge, cloudEdge - 0.34, r);
    //出生自中心长开
    body *= smoothstep(uBirth + 0.02, uBirth - 0.24, r * 0.9);

    //内密外疏
    float density = body * (0.35 + 0.4 * n1) * (1.0 - r * 0.42);

    //---------------------------------------------------------
    //孢子颗粒：高频噪声阈值出点，缓慢漂移+个体明灭
    //---------------------------------------------------------
    float g1 = tex2D(noiseSamp, coords * 5.5 + float2(uTime * 0.014, seed * 3.0 + uTime * 0.02)).r;
    float g2 = tex2D(noiseSamp, coords * 9.0 + float2(seed * 5.0 - uTime * 0.018, uTime * 0.011)).r;
    float motes = smoothstep(0.78, 0.92, g1) + smoothstep(0.84, 0.95, g2) * 0.7;
    float twinkle = 0.55 + 0.45 * sin(uTime * 5.0 + g1 * 21.0 + seed * 13.0);
    motes *= twinkle * body;

    //呼吸整体明灭
    float breath = 0.85 + 0.15 * sin(uTime * 1.7 + seed * 9.0);

    //---------------------------------------------------------
    //调色：毒绿云体+查特绿荧光颗粒，二阶段混品红
    //---------------------------------------------------------
    float3 cFog = lerp(float3(0.14, 0.30, 0.08), float3(0.24, 0.12, 0.18), uPhase2 * 0.6);
    float3 cGlint = lerp(float3(0.55, 0.95, 0.30), float3(0.95, 0.42, 0.62), uPhase2 * 0.7);

    float3 color = cFog * density * breath + cGlint * motes * 0.85;
    float alpha = saturate(density * 0.8 + motes * 0.5) * (1.0 - uDecay);

    //预乘输出
    return float4(color * alpha * vertexColor.rgb, alpha) * vertexColor.a;
}

technique Technique1
{
    pass SporeFogPass
    {
        PixelShader = compile ps_3_0 SporeFogPS();
    }
}
