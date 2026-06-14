// ============================================================================
//WeaverSoulVortex.fx 纠缠之怨冲刺怨魂涡流
//以玩家为中心环状漩涡；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uSpinDir; //旋转方向 ±1，与冲刺翻滚一致

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
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float r = length(p);
    float theta = atan2(p.y, p.x);

    //环形遮罩：中心镂空，外缘渐隐
    float ring = smoothstep(1.0, 0.56, r) * smoothstep(0.14, 0.46, r);
    if (ring <= 0.002)
    {
        return float4(0, 0, 0, 0);
    }

    //双层旋转噪声：整场旋转 + 异速副层制造涡流剪切感
    float a1 = uTime * 2.6 * uSpinDir;
    float s1 = sin(a1);
    float c1 = cos(a1);
    float2 rp1 = float2(p.x * c1 - p.y * s1, p.x * s1 + p.y * c1);
    float n1 = tex2D(noiseSamp, rp1 * 0.55 + 0.5).r;

    float a2 = uTime * 1.25 * uSpinDir + 2.1;
    float s2 = sin(a2);
    float c2 = cos(a2);
    float2 rp2 = float2(p.x * c2 - p.y * s2, p.x * s2 + p.y * c2);
    float n2 = tex2D(noiseSamp, rp2 * 1.15 + 0.5).r;

    //三臂怨魂螺旋（sin(3θ) 周期连续，无极坐标接缝）
    float arm = 0.5 + 0.5 * sin(theta * 3.0 * uSpinDir + r * 6.5 - uTime * 9.0 * uSpinDir);
    float streak = smoothstep(0.40, 0.92, arm * (0.45 + n1 * 0.55) + n2 * 0.30 - 0.12);

    //内缘吸入增亮：怨魂被卷向玩家
    float suck = smoothstep(0.62, 0.30, r) * smoothstep(0.14, 0.30, r);

    //颜色：暗怨红底 → 魂粉丝流 → 亮芯
    float3 cDark = float3(0.28, 0.09, 0.20);
    float3 cMain = float3(0.80, 0.36, 0.54);
    float3 cGlow = float3(1.00, 0.75, 0.86);

    float3 color = cDark * ring * 0.8;
    color += cMain * streak * ring;
    color += cGlow * streak * streak * ring * 0.5;
    color += cMain * suck * (0.25 + streak * 0.35);

    float alpha = saturate(ring * (0.22 + streak * 0.78)) * uFade * 0.9;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass WeaverSoulVortexPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
