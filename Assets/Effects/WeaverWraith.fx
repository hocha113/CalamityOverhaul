// ============================================================================
//WeaverWraith.fx 纠缠之怨怨魂
//+X 为运动方向(头部)；C# 按速度旋转 quad
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;    //每只幽魂的随机种子
float uFade;    //整体透明度 0~1
float uRage;    //狂怒度 0~1，回归阶段眼洞红光

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
    //p: -1..1，+x 为运动方向
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //躯尾摆动：越靠尾部摆幅越大
    float tailAmount = smoothstep(0.45, -1.0, p.x);
    float wave = sin(p.x * 4.2 - uTime * 10.0 + uSeed * 19.0);
    float py = p.y - wave * 0.17 * tailAmount;

    //头部圆
    float headDist = length(float2((p.x - 0.40) * 1.1, py));
    float head = smoothstep(0.40, 0.17, headDist);

    //躯体：自头部向 -x 渐细
    float halfW = lerp(0.05, 0.33, smoothstep(-1.0, 0.40, p.x));
    float body = smoothstep(halfW, halfW * 0.32, abs(py));
    body *= smoothstep(-1.02, -0.60, p.x);  //尾端整体收没
    body *= step(p.x, 0.42);

    //尾部噪声撕裂成飘带
    float n = tex2D(noiseSamp, float2(p.x * 0.7 - uTime * 0.55 + uSeed * 3.1, py * 1.6 + uSeed * 7.7)).r;
    float shredZone = smoothstep(0.30, -0.75, p.x);
    float shred = lerp(1.0, smoothstep(0.26, 0.62, n + 0.30 * (1.0 - shredZone)), shredZone);
    body *= shred;

    float ghost = max(head, body);

    //体内发光核心
    float core = ghost * ghost;
    float headCore = smoothstep(0.30, 0.05, headDist);

    //空洞双眼（沿运动轴对称）
    float2 eyeP = float2((p.x - 0.50) * 1.4, abs(py) - 0.13);
    float eyeDist = length(eyeP);
    float eyeHole = smoothstep(0.105, 0.045, eyeDist) * head;
    float3 eyeGlow = float3(1.0, 0.22, 0.30) * eyeHole * uRage * 1.7;

    //嚎叫之口：头部前端开合的暗洞
    float mouthOpen = 0.55 + 0.45 * sin(uTime * 6.0 + uSeed * 9.0);
    float mouthDist = length(float2((p.x - 0.64) * 0.9, py * 1.7));
    float mouthHole = smoothstep(0.11, 0.05, mouthDist) * head * mouthOpen;

    float holes = saturate(eyeHole + mouthHole);

    //颜色：暗怨红边缘 → 魂粉躯体 → 亮芯
    float3 cEdge = float3(0.30, 0.10, 0.22);
    float3 cBody = float3(0.74, 0.34, 0.52);
    float3 cCore = float3(1.00, 0.80, 0.90);

    float3 color = cEdge * ghost;
    color = lerp(color, cBody, core * 0.9);
    color += cCore * headCore * 0.65;
    color += cBody * n * ghost * 0.30;
    color *= 1.0 - holes * 0.92;
    color += eyeGlow;
    color *= 0.85 + uRage * 0.35;

    float alpha = saturate(ghost * 0.9) * uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass WeaverWraithPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
