// ============================================================================
//WarpShader.fx 屏幕空间扭曲后处理
//采样 uImage0 场景 + uImage1 位移图；ps_3_0
//
//兼容性硬约束（2026-09 神威疾走首绘闪退排查）：全程直线代码，禁止 if/分支内 return。
//旧版在 if(!any(displace)) 分支里做 tex2D，经 FNA3D 关优化编译后 DXBC 里的 sample
//落在发散分支内，是旧驱动最脆弱的构造；现在改为无条件采样 + 算术选择，输出逐像素等价。
// ============================================================================

sampler uImage0 : register(s0);
texture2D tex0;
bool noBlueshift; //禁引力蓝移
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

    //无位移像素原样透传场景色（含其 alpha）。必须无条件采样并以乘法参与最终合成：
    //只在条件路径上用到的采样会被编译器重新沉进分支
    float4 passthrough = tex2D(uImage0, coords);
    float hasWarp = any(displace) ? 1.0 : 0.0;

    //解码位移向量
    float rot = displace.r * 6.28318;
    float2 dir = float2(cos(rot), sin(rot));
    float mag = displace.g * i;
    float2 offset = dir * mag;

    //色差强度与位移量成正比；禁蓝移时压到 0.15（uniform 选择，编译为 cmp）
    float aberration = length(offset) * 12.0 * (noBlueshift ? 0.15 : 1.0);

    //色差分离: R/G/B通道以不同偏移采样
    //模拟引力色散 — 高频光(蓝)偏折更强
    float2 offsetR = offset * (1.0 - aberration * 0.3);
    float2 offsetG = offset;
    float2 offsetB = offset * (1.0 + aberration * 0.3);

    //多重采样: 3点高斯加权，消除硬边锯齿
    //R通道
    float2 uvR = coords + offsetR;
    float3 colorR = tex2D(uImage0, uvR).rgb * 0.5;
    colorR += tex2D(uImage0, uvR + offsetR * 0.3).rgb * 0.25;
    colorR += tex2D(uImage0, uvR - offsetR * 0.15).rgb * 0.25;

    //G通道
    float2 uvG = coords + offsetG;
    float3 colorG = tex2D(uImage0, uvG).rgb * 0.5;
    colorG += tex2D(uImage0, uvG + offsetG * 0.3).rgb * 0.25;
    colorG += tex2D(uImage0, uvG - offsetG * 0.15).rgb * 0.25;

    //B通道
    float2 uvB = coords + offsetB;
    float3 colorB = tex2D(uImage0, uvB).rgb * 0.5;
    colorB += tex2D(uImage0, uvB + offsetB * 0.3).rgb * 0.25;
    colorB += tex2D(uImage0, uvB - offsetB * 0.15).rgb * 0.25;

    //组合色差
    float4 result;
    result.r = colorR.r;
    result.g = colorG.g;
    result.b = colorB.b;
    result.a = 1.0;

    //引力频移着色：蓝移（中子星引力场中光频率升高）与中性提亮二选一，uniform 选择无分支
    float shift = length(offset);
    float3 blueshift = float3(shift * 2.0, shift * 1.5, shift * 28.0);
    float3 neutral = float3(shift, shift, shift) * 0.15;
    result.rgb += noBlueshift ? neutral : blueshift;

    //乘法选择而非 lerp：hasWarp 只取 0/1，两路输出都与原值逐位相同
    return result * hasWarp + passthrough * (1.0 - hasWarp);
}

technique Technique1
{
    pass WarpShaderPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
};
