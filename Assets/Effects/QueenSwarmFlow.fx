// ============================================================================
//QueenSwarmFlow.fx 蜂群编队信息素辉光带
//InnoVault Trail 条带：TexCoords.x 沿带 0尾→1头，TexCoords.y 横向 0→1；Additive
//材质是"花粉金尘流"：密集颗粒被气流拽成断续丝缕，不是连续能量光束
//全程笛卡尔坐标，无极角；闭环阵型由CPU侧拆成端点羽化的开弧
//vs_2_0 + ps_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uIntensity;   //整体强度(含淡入淡出)
float uAspect;      //长宽比，保持噪声各向同性
float3 uColor;      //主色(蜂蜜金)
float uFlowSpeed;   //沿带流速

//哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

struct VertexShaderInput
{
    float4 Position : POSITION;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

VertexShaderOutput SwarmFlowVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    output.Position = mul(input.Position, transformMatrix);
    return output;
}

float4 SwarmFlowPS(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float lat = (uv.y - 0.5) * 2.0;    //-1..1 横向
    float x = uv.x * uAspect;          //沿带等比坐标

    //端点羽化(闭环拆弧的接缝由此遮盖)
    float endFade = smoothstep(0.0, 0.08, uv.x) * smoothstep(1.0, 0.92, uv.x);

    //带芯横向包络：中间实两边散
    float body = exp(-lat * lat * 5.0);

    //中轴蛇摆：带芯不是死直线
    float wobble = valueNoise(float2(x * 0.35 - uTime * uFlowSpeed * 0.4, 7.7)) - 0.5;
    float latShift = lat + wobble * 0.55;
    float core = exp(-latShift * latShift * 26.0);

    //颗粒丝缕：两层反向流动的高频噪声阈值化，撕成断续金尘
    float grain1 = valueNoise(float2(x * 1.6 - uTime * uFlowSpeed, latShift * 3.0));
    float grain2 = valueNoise(float2(x * 3.1 - uTime * uFlowSpeed * 1.7 + 41.0, latShift * 5.5));
    float specks = smoothstep(0.42, 0.78, grain1 * 0.6 + grain2 * 0.55);

    //稀疏闪点：格子哈希脉冲(蜂翅反光)
    float2 cell = float2(floor(x * 2.2 - uTime * uFlowSpeed * 0.8), floor(latShift * 2.0));
    float twinkleSeed = hash21(cell);
    float twinkle = pow(abs(sin(uTime * 6.0 + twinkleSeed * 6.2831)), 14.0) * step(0.62, twinkleSeed);

    //亮度合成：颗粒承担主体，芯只是余温
    float lum = body * specks * (0.85 + twinkle * 1.4) + core * 0.28;
    lum *= endFade;

    //颜色：金尘为主，芯部偏蜡白但压住不发白热
    float3 col = uColor * lum;
    col += float3(0.95, 0.88, 0.65) * core * specks * 0.35 * endFade;

    return float4(col * uIntensity * input.Color.a, 1.0);
}

technique SwarmFlow
{
    pass P0
    {
        VertexShader = compile vs_2_0 SwarmFlowVS();
        PixelShader = compile ps_3_0 SwarmFlowPS();
    }
}
