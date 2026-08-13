// ============================================================================
//DeerFrostFissure.fx 冰裂隙预兆(世界空间quad，Additive)
//uv.x 沿裂隙 0~1，uv.y 横截面 0~1；裂纹自中心向两端生长，生长头亮尖
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uProgress;   //0~1 生长(自中点向两端)
float uFade;       //0~1 整体透明度
float uSeed;       //本实例种子，错开多条裂隙
float uTime;

// 噪声固定 s1：sampler_state 自动分配会落 s0，图元路径今日侥幸、批次路径必坏；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

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
    float cross_ = (uv.y - 0.5) * 2.0;   //-1~1 横截面

    //主裂纹：中线被噪声扭出折痕
    float wob = tex2D(noiseSamp, float2(uv.x * 2.2 + uSeed * 7.3, uSeed * 3.1)).r - 0.5;
    float d = abs(cross_ - wob * 0.62);
    float crack = exp(-d * d * 30.0);

    //碎枝细纹：高频噪声在主纹旁劈出岔口
    float branchN = tex2D(noiseSamp, float2(uv.x * 5.6 + uSeed * 2.7, cross_ * 0.9 + uSeed)).r;
    float branch = smoothstep(0.58, 0.9, branchN) * exp(-d * d * 7.0) * 0.55;

    //两端收尖
    float endTaper = smoothstep(0.0, 0.10, uv.x) * smoothstep(1.0, 0.90, uv.x);

    //自中点向两端生长，生长头一点亮尖
    float distMid = abs(uv.x - 0.5) * 2.0;
    float visible = smoothstep(uProgress + 0.04, uProgress - 0.16, distMid);
    float tip = exp(-abs(distMid - uProgress) * 26.0) * step(uProgress, 0.98);

    //寒芒呼吸(纯时间相位，无极角)
    float pulse = 0.78 + 0.22 * sin(uTime * 8.5 + uSeed * 21.0 + uv.x * 5.2);

    float3 core = float3(0.78, 0.95, 1.0);
    float3 edge = float3(0.22, 0.46, 0.92);
    float3 col = lerp(edge, core, saturate(crack));

    float intensity = (crack + branch) * visible * endTaper * pulse + tip * endTaper * 1.2;
    intensity *= uFade;

    return float4(col, saturate(intensity));
}

technique Technique1
{
    pass DeerFrostFissurePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
