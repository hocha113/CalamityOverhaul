// ============================================================================
// SHPCVoltArc.fx 高压核心放电电弧
// Trail 条带 Additive；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;        // 整体透明度 0~1
float arcSeed;          // 每次放电的随机种子，保证两次放电形态不同
float3 coreColor;       // 弧芯色（近白）
float3 glowColor;       // 辉光色（电蓝）
float3 auraColor;       // 外晕色（深蓝紫）

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;                       // 0=起点 1=终点
    float cross_ = uv.y;                      // 0=上边 1=下边

    // 弧芯游走：主弧 + 副弧两条独立路径
    // 时间离散化成"放电帧"，让弧线呈阶跃式折跳而不是平滑漂移
    float strobe = floor(uTime * 28.0);
    float n1 = tex2D(noiseSamp, float2(along * 3.0 + arcSeed, strobe * 0.07)).r;
    float n2 = tex2D(noiseSamp, float2(along * 7.0 - arcSeed * 1.7, strobe * 0.11 + 0.37)).g;
    float n3 = tex2D(noiseSamp, float2(along * 13.0 + arcSeed * 0.5, strobe * 0.05 + 0.71)).b;

    // 两端钉死在中心（电极锚点），中段允许大幅摆动
    float swing = sin(along * 3.14159);
    float path1 = 0.5 + (n1 - 0.5) * 0.55 * swing + (n2 - 0.5) * 0.25 * swing;
    float path2 = 0.5 + (n2 - 0.5) * 0.62 * swing - (n3 - 0.5) * 0.30 * swing;

    float d1 = abs(cross_ - path1);
    float d2 = abs(cross_ - path2);

    // 主弧芯：极窄白热线
    float core1 = 1.0 - smoothstep(0.0, 0.035, d1);
    float core2 = (1.0 - smoothstep(0.0, 0.025, d2)) * 0.65;

    // 辉光层：围绕双弧的电蓝光带
    float glow1 = 1.0 - smoothstep(0.0, 0.20, d1);
    float glow2 = (1.0 - smoothstep(0.0, 0.16, d2)) * 0.7;

    // 微枝杈：高频细分叉，随机闪现
    float branchHash = hash21(float2(floor(along * 24.0), strobe + arcSeed * 13.0));
    float branchOn = step(0.72, branchHash);
    float branchPath = 0.5 + (branchHash - 0.5) * 1.3 * swing;
    float branch = (1.0 - smoothstep(0.0, 0.05, abs(cross_ - branchPath))) * branchOn * 0.8;

    // 电离雾：弧道周围的淡色离子云
    float fog = (1.0 - smoothstep(0.05, 0.5, min(d1, d2))) * 0.30;
    float fogNoise = tex2D(noiseSamp, float2(along * 5.0 - uTime * 2.0, cross_ * 2.0 + arcSeed)).r;
    fog *= 0.6 + fogNoise * 0.8;

    // 全弧高频闪烁：放电的不稳定亮度
    float flicker = 0.72 + 0.28 * hash21(float2(strobe, arcSeed));

    // 端点渐隐
    float endFade = smoothstep(0.0, 0.04, along) * smoothstep(1.0, 0.96, along);

    float3 color = float3(0.0, 0.0, 0.0);
    color += coreColor * (core1 + core2) * 1.15;
    color += glowColor * (glow1 + glow2) * 0.75;
    color += coreColor * branch * 0.9;
    color += auraColor * fog;

    float alpha = saturate(core1 + core2 + (glow1 + glow2) * 0.5 + branch * 0.8 + fog * 0.6);
    alpha *= fadeAlpha * flicker * endFade;

    return float4(color * alpha * flicker, alpha) * input.Color;
}

technique Technique1
{
    pass SHPCVoltArcPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
