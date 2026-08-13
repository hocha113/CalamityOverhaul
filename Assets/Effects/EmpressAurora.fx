// ============================================================================
//EmpressAurora.fx 光之女皇·极光帘幕
//UV.x 0左→1右横截 UV.y 0顶→1底；帘心亮带的偏摆与C#判定同源(uSwayTime)
//绑定Perlin噪声出竖向幕褶，禁手搓fbm；Additive；无atan2无动态分支
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uIntensity;  //总强度包络 0~1
float uPhase;      //本帘相位（色相与褶皱错相）
float uSwayTime;   //帘心偏摆相位（与判定同源）
float uCoreRatio;  //判定亮带半宽/视觉半宽

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

float3 hueRGB(float h)
{
    h = frac(h);
    float r = abs(h * 6.0 - 3.0) - 1.0;
    float g = 2.0 - abs(h * 6.0 - 2.0);
    float b = 2.0 - abs(h * 6.0 - 4.0);
    return saturate(float3(r, g, b));
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float yNorm = uv.y * 2.0 - 1.0;

    //帘心偏摆：与C# SwayOffset同一公式，归一到UV（46px/190 与 22px/190）
    float sway = sin(uSwayTime + yNorm * 2.6) * 0.242 + sin(uSwayTime * 1.7 + yNorm * 5.2) * 0.116;
    float coreX = 0.5 + sway * 0.5;
    float dx = (uv.x - coreX) * 2.0;

    //亮带：判定区亮而清晰；外围宽幅垂帘
    float coreBand = exp(-dx * dx / max(uCoreRatio * uCoreRatio * 4.0, 0.002));
    float drape = exp(-dx * dx * 2.2);

    //竖向幕褶：噪声沿竖向慢流（绑定Perlin，横向频率高竖向拉长）
    float fold = tex2D(noiseSamp, float2(uv.x * 2.6 + uPhase * 0.37, uv.y * 0.7 - uTime * 0.07)).r;
    float fold2 = tex2D(noiseSamp, float2(uv.x * 5.2 - uPhase * 0.21, uv.y * 1.3 - uTime * 0.11)).r;
    float folds = 0.45 + 0.55 * saturate(fold * 0.65 + fold2 * 0.55);

    //极光色：竖向色相渐变+相位错开（青绿→紫粉的光谱帘）
    float hue = uPhase * 0.16 + uv.y * 0.34 + fold * 0.10 + uTime * 0.012;
    float3 aurora = hueRGB(hue);

    //上下端羽化+横向羽化
    float vFade = smoothstep(0.0, 0.16, uv.y) * (1.0 - smoothstep(0.80, 1.0, uv.y));
    float hFade = smoothstep(0.0, 0.06, uv.x) * (1.0 - smoothstep(0.94, 1.0, uv.x));

    //亮带内偏白（危险区读得出）
    float3 white = float3(1.0, 1.0, 1.0);
    float3 color = float3(0.0, 0.0, 0.0);
    color += aurora * drape * folds * 0.85;
    color += lerp(aurora, white, 0.6) * coreBand * folds * 1.15;

    float alpha = saturate(drape * folds * 0.42 + coreBand * 0.85);
    alpha *= vFade * hFade * uIntensity;
    return float4(color * alpha * vFade * hFade, alpha) * input.Color;
}

technique Technique1
{
    pass AuroraPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
