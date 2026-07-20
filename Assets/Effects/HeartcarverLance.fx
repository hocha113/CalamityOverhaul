// ============================================================================
//HeartcarverLance.fx 刻心者针状白热刺线
//静态 quad 图元 Additive：刺出瞬间世界锚定，向针尖收细的白热光束
//UV.x 0=手部端 1=针尖端 UV.y 0~1 横截面
//uLife 0=刚刺出 → 1=消散完毕；uCarve=剜心击强化
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uLife;
float uCarve;
float uSeed;

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
    float cross = abs(uv.y - 0.5) * 2.0; //0=中线 1=边缘

    //针形宽度包络：手部端最宽，向针尖幂次收细成点
    float widthEnv = lerp(0.62, 0.05, pow(uv.x, 1.7));
    //手部端软入
    float rootFade = smoothstep(0.0, 0.14, uv.x);

    //消散侵蚀：血线自手部端向针尖蒸发，噪声啃出破碎前沿
    float n = tex2D(noiseSamp, float2(uv.x * 2.3 + uSeed * 7.0, uv.y * 0.9 + uSeed)).r;
    float erodeFront = pow(uLife, 1.5) * 1.15;
    float erode = smoothstep(erodeFront - 0.22, erodeFront + 0.05, uv.x + (n - 0.5) * 0.24);

    //白热核心与外鞘
    float core = smoothstep(widthEnv * 0.55, widthEnv * 0.08, cross);
    float sheath = smoothstep(widthEnv * 2.1, widthEnv * 0.4, cross);

    //出生过曝：刺出头两成生命亮度过冲
    float flash = 1.0 + 2.2 * pow(saturate(1.0 - uLife * 5.0), 2.0);

    //沿刃速度丝：噪声脊线断续高光
    float filament = smoothstep(0.58, 0.85, n) * sheath;

    //颜色：动脉暗红外鞘 + 心肌粉白核心；剜心击整体升温
    float3 cSheath = float3(0.60, 0.045, 0.085);
    float3 cCore = float3(1.30, 1.02, 1.06);
    float hot = 0.65 + uCarve * 0.45;

    float fade = pow(saturate(1.0 - uLife), 1.25) * erode * rootFade;

    float3 color = cSheath * sheath * 1.1;
    color += cCore * core * hot * flash;
    color += cCore * filament * (0.20 + uCarve * 0.30);

    float alpha = saturate(sheath * 0.55 + core * 0.9) * fade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass HeartcarverLancePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
