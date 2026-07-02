// ============================================================================
//HyperionExhaust.fx 海伯利昂巡航弹尾焰着色器
//Trail条带渲染:白炽喷口核心+等离子鞘+尾段噪声撕裂消散,thrust驱动引擎功率
//UV.x 0=尾迹末端 1=喷口   UV.y 0=上边 1=下边   Additive混合
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;       //整体透明度0-1
float thrust;          //引擎功率0-1,0时仅剩冷烟痕
float3 coreColor;      //喷口白炽核心色(线性HDR,>1灼烧)
float3 sheathColor;    //等离子鞘主色
float3 emberColor;     //尾段余烬色
texture uNoiseTex;     //Perlin灰度,流动扰动与尾段撕裂

sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

struct VSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position  = mul(v.Position, transformMatrix);
    o.Color     = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float head  = input.TexCoords.x;             //1=喷口 0=尾端
    float along = 1.0 - head;                    //0=喷口 1=尾端
    float d     = abs(input.TexCoords.y - 0.5) * 2.0;    //0=中轴 1=边缘

    //截面三层:核心极窄、等离子鞘中宽、羽流全宽
    float core   = pow(saturate(1.0 - d), 18.0);
    float sheath = pow(saturate(1.0 - d), 5.0);
    float plume  = pow(saturate(1.0 - d), 1.7);

    //流动噪声:沿轨迹回卷制造喷流感,双频叠加(纯笛卡尔输入,无极角接缝问题)
    float2 uv1 = float2(along * 2.6 - uTime * 2.3, input.TexCoords.y * 1.5 + uTime * 0.11);
    float2 uv2 = float2(along * 7.5 - uTime * 4.6, input.TexCoords.y * 2.9 - uTime * 0.21);
    float n1 = tex2D(noiseSamp, uv1).r;
    float n2 = tex2D(noiseSamp, uv2).r;

    //能量沿程分布:核心贴喷口、鞘中段、余烬居于中尾段
    float corePow   = pow(head, 0.45) * (0.20 + 0.80 * thrust);
    float sheathPow = pow(head, 1.15) * lerp(0.55, 1.45, n1) * (0.10 + 0.90 * thrust);
    float emberPow  = along * pow(head, 0.8) * 2.2;

    //尾段撕裂:噪声阈值随along收紧,烟舌参差散逸
    float breakup = smoothstep(along - 0.30, along + 0.06, n2 * 0.62 + 0.52);

    //喷口高频闪烁,营造引擎脉动
    float flicker = 0.88 + 0.24 * sin(uTime * 37.0 + n1 * 6.0);

    float3 col = 0;
    //1. 白炽核心
    col += coreColor * core * corePow * 4.2 * flicker;
    //2. 等离子鞘,噪声调制出流线
    col += sheathColor * sheath * sheathPow * 1.9;
    //3. 余烬羽流:鞘色向余烬色过渡
    float3 tailCol = lerp(emberColor, sheathColor, head * 0.7);
    col += tailCol * plume * emberPow * (0.25 + 0.75 * thrust);
    //4. 高频微丝:边缘带的细亮等离子线
    float filament = saturate(pow(n2, 3.5) * 2.2 - 0.25);
    col += sheathColor * filament * smoothstep(0.15, 0.85, d) * plume * thrust * 1.3;
    //5. 熄火冷烟:thrust归零后残留的暗淡烟痕
    col += float3(0.34, 0.30, 0.28) * plume * (1.0 - thrust) * 0.30 * (1.0 - along * 0.6);

    col *= fadeAlpha * breakup;

    float a = saturate(core * 1.6 + sheath * 0.8 + plume * 0.35) * fadeAlpha * breakup;
    return float4(col, a);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 PixelShaderFunction();
    }
};
