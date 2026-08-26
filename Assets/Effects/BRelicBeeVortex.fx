// ============================================================================
//BRelicBeeVortex.fx 蜂涡信标·涡群流场
//placeholder2 方块quad，SpriteBatch Immediate + Additive(rgb不预乘，a=包络——加色批源因子SourceAlpha)
//差速旋转全走笛卡尔刚体旋转坐标，无atan2无极角(SkeletronSpinStorm血统，换蜂群材质)：
//蜂群不是连续能量流——噪声阈值撕成蜂团流带、高频阈值细粒=单蜂闪点、
//反向慢旋帧低频亮暗=指令密度波扫群，成形期向心丝缕收拢(uForm)
//绑定噪声实测值域0.227~0.776，阈值一律过nrm()归一
//ps_3_0
// ============================================================================

float uTime;
float uSpin;       //累计旋转角(消费端localAI累计)
float uIntensity;  //0~1.2 总包络
float uForm;       //0成形收拢→1稳态环涡
float uHole;       //内孔半径占比(目标可读性留窗)
float3 uColA;      //蜂黄
float3 uColB;      //琥珀深

//噪声固定s1：C#侧须显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

//PerlinNoise实测值域归一
float nrm(float v)
{
    return saturate((v - 0.227) / 0.549);
}

float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 BeeVortexPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float r = length(centered);

    //环带包络：内孔留给目标本体(公平阀，锁定者仍可读)，外缘撕散+画布护栏
    float ring = smoothstep(1.0, 0.66, r) * smoothstep(uHole, uHole + 0.22, r);
    float guard = smoothstep(0.99, 0.9, r);
    ring *= guard;

    //差速旋转：内圈快外圈慢，涡的剪切签名
    float swirl = uSpin * (1.7 - r * 0.95);
    float2 sp = Rot(centered, swirl);

    //蜂团流带：双频噪声阈值撕团(很多蜂挤成一股流，禁连续绸面)
    float bandA = nrm(tex2D(noiseSamp, sp * 0.72 + float2(uTime * 0.06, -uTime * 0.04)).r);
    float bandB = nrm(tex2D(noiseSamp, sp * 1.9 + float2(-uTime * 0.1, uTime * 0.07)).r);
    float stream = bandA * 0.58 + bandB * 0.42;
    float clump = smoothstep(0.42, 0.78, stream);

    //指令密度波：反向慢旋帧的低频亮暗扫过整群
    float2 wp = Rot(centered, -uSpin * 0.45);
    float densWave = 0.6 + 0.4 * nrm(tex2D(noiseSamp, wp * 0.34 + float2(uTime * 0.03, 0.0)).r);

    //单蜂闪点：高频细粒阈值
    float fleck = nrm(tex2D(noiseSamp, sp * 4.6 + float2(uTime * 0.16, uTime * 0.23)).r);
    float bee = smoothstep(0.78, 0.93, fleck) * ring;

    //成形向心丝缕：径向抽入，成形后自灭
    float2 cp = Rot(centered, uSpin * 0.3);
    float inflowN = nrm(tex2D(noiseSamp, cp * 1.25 + float2(0.0, r * 2.4 - uTime * 1.6)).r);
    float inflow = smoothstep(0.55, 0.88, inflowN) * (1.0 - uForm) * smoothstep(0.2, 0.6, r) * guard;

    float density = clump * densWave * ring * uIntensity;

    float3 col = uColB * density * 0.75
        + uColA * density * clump * 0.85
        + uColA * inflow * 0.9
        + float3(1.0, 0.93, 0.62) * bee * 0.5 * uIntensity;

    float alpha = saturate(density * 0.85 + inflow * 0.55 + bee * 0.35) * vertexColor.a;
    return float4(col, alpha);
}

technique BeeVortex
{
    pass P0
    {
        PixelShader = compile ps_3_0 BeeVortexPS();
    }
}
