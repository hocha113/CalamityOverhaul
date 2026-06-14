// ============================================================================
//StormSlashTrail.fx 风暴女神之矛刀光
//Trail 条带 Additive
//UV.x 1=最新 0=尾 UV.y 0=外缘 1=内缘
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //风暴强度 0~1，上挑段提升

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
    float age = uv.x; //1=最新 越大越亮

    //风暴域扭曲：先用噪声扭曲采样坐标，制造闪电的抖动锯齿
    float warp = tex2D(noiseSamp, float2(uv.x * 3.4 - uTime * 3.2, uv.y * 1.2 + uTime * 0.5)).r;
    float2 warpedUV = uv + float2(0.0, (warp - 0.5) * 0.18);

    //气旋流：扭曲后的双层噪声
    float n1 = tex2D(noiseSamp, float2(warpedUV.x * 2.0 - uTime * 2.4, warpedUV.y * 1.0 + uTime * 0.3)).r;
    float n2 = tex2D(noiseSamp, float2(warpedUV.x * 4.2 + uTime * 1.6, warpedUV.y * 2.4 - uTime * 1.1)).r;
    float gale = n1 * 0.6 + n2 * 0.4;

    //外缘：闪电锯齿（使用扭曲后的 uv.y 计算，让边缘呈雷暴撕裂状）
    float outerMask = smoothstep(0.02, 0.16, warpedUV.y);
    //内缘渐隐
    float innerMask = smoothstep(1.0, 0.30, warpedUV.y);

    //尾迹老化：雷光残留迅速消散
    float ageMask = smoothstep(0.0, 0.58, age);
    ageMask *= ageMask;

    float intensity = outerMask * innerMask * ageMask;

    //雷光闪烁：整体亮度高频抖动，风暴的呼吸
    float flicker = 0.72 + 0.28 * tex2D(noiseSamp, float2(uTime * 5.0, uv.x * 0.5)).r;
    intensity *= flicker;

    //主电丝：窄阈值噪声脊线，高速抖动
    float bolt = smoothstep(0.60, 0.74, gale) * smoothstep(0.86, 0.74, gale);
    bolt *= intensity * 2.2;

    //细分支电纹：更高频更细
    float branchNoise = tex2D(noiseSamp, float2(uv.x * 6.5 - uTime * 4.5, warpedUV.y * 4.0 + uTime * 2.0)).r;
    float branch = smoothstep(0.68, 0.76, branchNoise) * smoothstep(0.86, 0.78, branchNoise)
                 * ageMask * innerMask * (0.8 + uHeat * 0.7);

    //刃口白热芯
    float hotCore = smoothstep(0.68, 1.0, age) * smoothstep(0.30, 0.04, warpedUV.y)
                  * outerMask * (0.55 + uHeat * 0.45);

    //颜色：深风暴蓝 → 风暴蓝 → 雷光白
    float3 cDark = float3(0.08, 0.18, 0.42);
    float3 cMain = float3(0.45, 0.70, 1.00);
    float3 cGlow = float3(0.93, 0.97, 1.00);

    float3 color = cDark * intensity * 1.2;
    color += cMain * intensity * 0.5;
    color += cGlow * bolt * (0.7 + uHeat * 0.4);
    color += cGlow * branch * 0.55;
    color += cGlow * hotCore;

    float alpha = saturate(intensity * 0.8 + bolt * 0.55 + branch * 0.35 + hotCore * 0.6);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass StormSlashTrailPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
