// ============================================================================
//SeaShrimpCavitation.fx 渊晶海虾 空化崩爆
//材质=空化腔坍缩:声致发光白点先行,冲击波前撕裂外扩,碎水尾随,腔雾回填
//泡体本体复用 FishronBubble.fx(参数化色板),本文件只负责崩爆一拍
//TechCollapse 画布契约:终环半径 r=0.40,C# quadPx = 终环px / 0.40 * 2
//整文件 ps-only(SpriteBatch 家族),禁加带 VS 的 technique(混批污染案 2026-08-27)
//噪声 s1=PerlinNoise LinearWrap;G 通道实测值域 0.227~0.776,阈值一律过 nrm()
// ============================================================================

float uTime;
float uSeed;      //每泡相位差
float uProgress;  //崩爆生命周期 0~1
float fadeAlpha;

sampler noiseSamp : register(s1);

//PerlinNoise G 通道实测 0.227~0.776,先归一再做阈值,防死代码
float nrm(float v)
{
    return saturate((v - 0.227) / 0.549);
}

//笛卡尔刚体旋转,避开 atan2 缝
float2 rot2(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

struct SBInput
{
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float4 CollapsePS(SBInput input) : COLOR0
{
    float2 p = input.TexCoords - 0.5;
    float r = length(p);
    float canvas = 1.0 - smoothstep(0.455, 0.495, r);
    if (canvas < 0.01)
        return float4(0, 0, 0, 0);

    float t = saturate(uProgress);
    float n = nrm(tex2D(noiseSamp, rot2(p * 1.7, uSeed * 3.7) * 0.5 + 0.5
        + float2(uTime * 0.06, 0.23)).g);
    float n2 = nrm(tex2D(noiseSamp, rot2(p * 3.4, uSeed * 7.1) * 0.5 + 0.5
        + float2(0.47, uSeed * 0.29)).g);

    //声致发光:前 30% 的白热针点,急速塌灭
    float flashT = 1.0 - saturate(t / 0.30);
    float flashR = 0.020 + 0.030 * (1.0 - flashT);
    float flash = exp(-r * r / (flashR * flashR)) * flashT * flashT;

    //冲击环:EaseOut 外扩 0.05→0.40,行进中变薄变淡,噪声撕缘
    float ringT = 1.0 - (1.0 - t) * (1.0 - t);
    float ringR = lerp(0.05, 0.40, ringT) * (1.0 + (n - 0.5) * 0.10);
    float ringW = lerp(0.030, 0.013, ringT);
    float ring = exp(-pow((r - ringR) / ringW, 2.0)) * (1.0 - t * 0.55);
    float ringCore = exp(-pow((r - ringR) / (ringW * 0.35), 2.0)) * (1.0 - t * 0.7);

    //环后碎水:环外窄带里的稀疏噪声水屑
    float sprayBand = smoothstep(ringR - 0.01, ringR + 0.02, r)
                    * smoothstep(ringR + 0.14, ringR + 0.03, r);
    float spray = sprayBand * step(0.62, n2) * saturate(t * 2.0) * (1.0 - t * 0.6);

    //腔体回填水雾:环内弱暗雾,尾段消散
    float mist = (1.0 - smoothstep(0.0, ringR, r)) * (0.5 + 0.5 * n)
               * saturate(t * 1.6) * (1.0 - t) * 0.5;

    float3 cDeep = float3(0.03, 0.07, 0.13);
    float3 cBody = float3(0.06, 0.17, 0.30);
    float3 cCyan = float3(0.24, 0.88, 0.95);
    float3 cCore = float3(0.90, 0.99, 1.0);

    float3 color = cDeep * mist * 1.3;
    color += cBody * spray * 1.0;
    color += cCyan * ring * 1.05;
    color += cCore * ringCore * 1.3;
    color += cCore * flash * 1.7;

    float alpha = saturate(mist * 0.5 + spray * 0.6 + ring * 0.8 + ringCore * 0.9 + flash * 0.95);
    alpha *= canvas * fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique TechCollapse
{
    pass P0
    {
        PixelShader = compile ps_3_0 CollapsePS();
    }
}
