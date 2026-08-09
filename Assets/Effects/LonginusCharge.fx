// ============================================================================
//LonginusCharge.fx 朗基努斯充能吸入场
//枪尖锚定 quad Additive：向心吸入的圣光丝 + 呼吸聚核，替代旧星屑粒子
//p=(uv-0.5)*2；极角只进整数倍角 sin(7θ/9θ/12θ)，跨 ±π 连续
//uCharge 0~1 总强度(随充能进度)；uFull 满层聚核稳态；uPhase 错相种子
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uCharge;
float uFull;
float uPhase;

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

    //画布边界保险
    float guardEdge = 1.0 - smoothstep(0.90, 1.0, max(abs(p.x), abs(p.y)));

    //三组整数倍角光丝，相位含 +r 项使等相面随时间向内推(吸入)
    float t = uTime * (2.6 + uCharge * 2.0);
    float s1 = 0.5 + 0.5 * sin(7.0 * theta + r * 9.0 - t + uPhase * 6.2832);
    float s2 = 0.5 + 0.5 * sin(9.0 * theta - r * 11.0 + t * 0.77 + uPhase * 2.399);
    float s3 = 0.5 + 0.5 * sin(12.0 * theta + r * 14.0 - t * 1.21);
    float spokes = pow(s1, 7.0) * 0.55 + pow(s2, 8.0) * 0.35 + pow(s3, 9.0) * 0.28;

    //刚体旋转坐标采噪声打破均匀，连续安全
    float cs = cos(uTime * 0.4);
    float sn = sin(uTime * 0.4);
    float2 rp = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
    float n = tex2D(noiseSamp, rp * 0.7 + float2(uTime * 0.06, uPhase)).r;
    spokes *= 0.65 + n * 0.7;

    //半径包络：外缘淡入，近核留出净区
    float radEnv = smoothstep(1.0, 0.50, r) * smoothstep(0.05, 0.30, r);

    //聚核：呼吸 + 满层稳态增强
    float breathe = 0.5 + 0.5 * sin(uTime * 5.0 + uPhase * 3.0);
    float coreSize = 0.020 + uCharge * 0.018 + uFull * 0.022 + breathe * 0.008;
    float core = exp2(-r * r / max(coreSize, 0.001));

    float3 cWire = float3(1.00, 0.62, 0.20);
    float3 cHot = float3(1.30, 1.10, 0.80);

    float wireA = spokes * radEnv * uCharge;
    float coreA = core * (0.45 + uCharge * 0.55 + uFull * 0.5);

    float3 color = cWire * wireA + cHot * coreA;
    float alpha = saturate(wireA * 0.9 + coreA) * guardEdge;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass LonginusChargePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
