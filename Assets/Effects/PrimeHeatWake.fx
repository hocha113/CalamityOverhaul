// ============================================================================
//PrimeHeatWake.fx 冲刺热浪尾流屏幕扭曲
//采样 uImage0 屏幕快照；遮罩外透传
// ============================================================================

sampler uImage0 : register(s0);
texture uNoise;
sampler2D noiseTex = sampler_state
{
    texture = <uNoise>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;
float uIntensity;  //0~1 强度（按冲刺速度驱动，自然衰减）
float2 uCenter;    //冲刺源（归一化屏幕uv）
float uDir;        //运动方向（弧度，屏幕空间）
float uRadius;     //影响半径（以屏高归一）
float uAspect;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 d = (coords - uCenter) * float2(uAspect, 1.0);

    //旋入运动坐标系：x' 沿运动方向，y' 横向
    float2 ax = float2(cos(uDir), sin(uDir));
    float lon = dot(d, ax);
    float lat = dot(d, float2(-ax.y, ax.x));

    //尾流遮罩：前方短(0.7R)、后方长(2.4R) 的拉长椭圆
    float lonScale = lerp(2.4, 0.7, step(0.0, lon));
    float mask = exp(-pow(lon / (uRadius * lonScale), 2.0) - pow(lat / (uRadius * 0.85), 2.0));

    //热波纹：沿运动轴的高频抖动，噪声调相去机械感
    float n = tex2D(noiseTex, float2(lon * 2.5 - uTime * 1.6, lat * 2.5)).r;
    float wobble = sin(lon * 44.0 + uTime * 17.0 + n * 7.0);

    float2 perp = float2(-ax.y, ax.x);
    perp.x /= uAspect;
    float2 offset = perp * wobble * mask * 0.011 * uIntensity;

    float3 col = tex2D(uImage0, coords + offset).rgb;
    //尾流内一点热辉
    col += float3(1.0, 0.48, 0.18) * mask * abs(wobble) * 0.045 * uIntensity;

    return float4(col, 1.0);
}

technique Technique1
{
    pass PrimeHeatWakePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
