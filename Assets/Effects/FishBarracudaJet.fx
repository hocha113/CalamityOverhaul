// ============================================================================
//FishBarracudaJet.fx 热带梭鱼白沫水射流条带（呼啸横穿段的速度尾迹）
//材质：被高速鱼体犁开的白沫水射流。纵向流线 = 沿流向强拉伸的噪声；尾端被高频
//噪声撕成断沫；卷入气泡场在射流里蚀出暗孔；头段海沫细芯与白沫点均为小面积。
//色彩：uColDark 深礁青外缘 / uColMid 绿松石中层 / uColFoam 海沫细芯（非纯白）。
//热带条纹色不进条带（由 C# 端残影链承担），射流保持纯水语言。
//uv.x: 0=头端(最新，GraniteMarbleVFX.DrawTrailFromOldPos 的 oldPos[0] 侧) 1=尾端(最旧)；
//像素内翻转为 along（1=头 0=尾）再做侵蚀/提亮。uv.y: 0..1 跨带。顶点色承载 C# 端包络。
//极角审计：无 atan2/theta/phi 消费，全部为笛卡尔 uv + wrap 采样，无缝隙风险。
//Additive 输出（调用方 GraniteMarbleVFX.DrawTrailFromOldPos 设 BlendState.Additive）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //流动相位（每鱼 whoAmI 派生偏移防同相）
float3 uColDark;  //深礁青（外缘压底）
float3 uColMid;   //绿松石（中层主色）
float3 uColFoam;  //海沫（头端细芯与沫点，非纯白）

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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    //along: 1=头端(最新) 0=尾端(最旧)
    float along = 1.0 - uv.x;
    float across = abs(uv.y * 2.0 - 1.0);

    //窄锐射流剖面：边缘快速归零，读作高压水线而非软膜
    float profile = pow(saturate(1.0 - across), 2.0);
    float headBoost = smoothstep(0.45, 1.0, along);

    //纵向流线：沿流向强拉伸的低频噪声，被犁开的水
    float streak = tex2D(noiseSamp, float2(along * 0.7 - uTime * 1.8, uv.y * 3.2)).r;
    //高频沫粒：撕尾与白沫点共用
    float grain = tex2D(noiseSamp, float2(along * 2.6 - uTime * 2.9, uv.y * 6.0 + 0.37)).r;
    //卷入气泡场：中频独立相位
    float bubble = tex2D(noiseSamp, float2(along * 1.6 - uTime * 2.3, uv.y * 2.2 + 0.71)).r;

    //尾端撕沫：越靠尾阈值越高，噪声决定断沫形状（禁平滑收口）
    float erode = smoothstep(along - 0.34, along + 0.08, grain * 0.86 + 0.06);
    //气泡孔：射流卷入的空气蚀出暗斑，头端少尾端多
    float hole = smoothstep(0.80, 0.94, bubble) * (1.0 - 0.55 * headBoost);

    float body = profile * erode * (0.45 + 0.55 * streak) * (1.0 - 0.75 * hole);

    //深礁青外缘 → 绿松石中层 → 头段海沫细芯
    float3 col = lerp(uColDark, uColMid, saturate(profile * 1.7));
    float foamCore = pow(profile, 3.6) * (0.4 + 0.6 * grain) * headBoost;
    col = lerp(col, uColFoam, saturate(foamCore));
    //白沫点：高频阈值、只在头半段、极小面积
    float spray = smoothstep(0.82, 0.97, grain) * headBoost * profile;
    col += uColFoam * spray * 0.5;

    float alpha = body * (0.22 + 0.78 * headBoost);

    //Additive：预乘颜色，顶点色承载 C# 端速度/透明包络
    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
