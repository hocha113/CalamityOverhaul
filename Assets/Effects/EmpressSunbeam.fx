// ============================================================================
//EmpressSunbeam.fx 光之女皇·日舞光束
//UV.x 0根→1梢 UV.y 横截面；预告细线→白热锐芯+光谱缘+外流干涉带
//Additive；无atan2无动态分支
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uHue;        //本束色相
float uTelegraph;  //>0 预告态进度（0=正式束）
float uWidthRatio; //宽度包络 0~1

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
    float along = uv.x;
    float cross_ = (uv.y - 0.5) * 2.0;

    //预告混合：telegraph>0 时束体是细亮线
    float tele = saturate(uTelegraph);
    float isTele = step(0.001, tele);

    //正式束宽随包络；预告束极细
    float widthK = lerp(max(uWidthRatio, 0.03), 0.06 + 0.05 * tele, isTele);
    float d = abs(cross_) / max(widthK, 0.02);

    float core = exp(-d * d * 8.0);
    float hot = exp(-d * d * 90.0);

    //光谱缘：越远离芯越offset色相
    float3 spectral = hueRGB(uHue + d * 0.12);

    //外流干涉带：亮带自根向梢流动（能量在输送）
    float flow = 0.72 + 0.28 * sin(along * 34.0 - uTime * 9.0);

    //根部辉花+梢端渐隐
    float rootFlare = (1.0 - smoothstep(0.0, 0.14, along)) * 1.3;
    float tipFade = 1.0 - smoothstep(0.82, 1.0, along);

    //预告呼吸
    float telePulse = lerp(1.0, 0.5 + 0.5 * sin(uTime * 18.0), isTele * (1.0 - tele));

    float3 white = float3(1.0, 1.0, 1.0);
    float3 color = float3(0.0, 0.0, 0.0);
    color += spectral * core * flow;
    color += white * hot * 1.35;
    color += lerp(spectral, white, 0.5) * rootFlare * core;
    //宽晕
    float halo = exp(-d * d * 1.6) * 0.36;
    color += hueRGB(uHue) * halo;

    float alpha = saturate(core * 0.8 * flow + hot * 0.95 + halo * 0.4 + rootFlare * core * 0.5);
    alpha *= tipFade * telePulse;
    //预告态整体压暗
    alpha *= lerp(1.0, 0.5, isTele);
    return float4(color * alpha * tipFade * telePulse, alpha) * input.Color;
}

technique Technique1
{
    pass SunbeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
