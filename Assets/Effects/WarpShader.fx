//=============================================================================
// WarpShader.fx — 屏幕空间扭曲后处理 v2
// ps_3_0 — 色差分离 + 多重采样平滑 + 引力蓝移模拟
//=============================================================================

sampler uImage0 : register(s0);
texture2D tex0;
bool noBlueshift;
sampler2D uImage1 = sampler_state
{
    texture = <tex0>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float i;

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 displace = tex2D(uImage1, coords);

    if (!any(displace))
        return tex2D(uImage0, coords);

    // 解码位移向量
    float rot = displace.r * 6.28318;
    float2 dir = float2(cos(rot), sin(rot));
    float mag = displace.g * i;
    float2 offset = dir * mag;

    // 色差强度与位移量成正比
    float aberration = length(offset) * 12.0;
    if (noBlueshift)
        aberration *= 0.15;

    // 色差分离: R/G/B通道以不同偏移采样
    // 模拟引力色散 — 高频光(蓝)偏折更强
    float2 offsetR = offset * (1.0 - aberration * 0.3);
    float2 offsetG = offset;
    float2 offsetB = offset * (1.0 + aberration * 0.3);

    // 多重采样: 3点高斯加权，消除硬边锯齿
    // R通道
    float2 uvR = coords + offsetR;
    float3 colorR = tex2D(uImage0, uvR).rgb * 0.5;
    colorR += tex2D(uImage0, uvR + offsetR * 0.3).rgb * 0.25;
    colorR += tex2D(uImage0, uvR - offsetR * 0.15).rgb * 0.25;

    // G通道
    float2 uvG = coords + offsetG;
    float3 colorG = tex2D(uImage0, uvG).rgb * 0.5;
    colorG += tex2D(uImage0, uvG + offsetG * 0.3).rgb * 0.25;
    colorG += tex2D(uImage0, uvG - offsetG * 0.15).rgb * 0.25;

    // B通道
    float2 uvB = coords + offsetB;
    float3 colorB = tex2D(uImage0, uvB).rgb * 0.5;
    colorB += tex2D(uImage0, uvB + offsetB * 0.3).rgb * 0.25;
    colorB += tex2D(uImage0, uvB - offsetB * 0.15).rgb * 0.25;

    // 组合色差
    float4 result;
    result.r = colorR.r;
    result.g = colorG.g;
    result.b = colorB.b;
    result.a = 1.0;

    // 引力频移着色
    float shift = length(offset);
    if (!noBlueshift)
    {
        // 蓝移：中子星引力场中光频率升高
        result.b += shift * 28.0;
        result.r += shift * 2.0;
        result.g += shift * 1.5;
    }
    else
    {
        result.rgb += shift * 0.15;
    }

    return result;
}

technique Technique1
{
    pass WarpShaderPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
};
