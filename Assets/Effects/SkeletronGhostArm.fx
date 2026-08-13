// ============================================================================
//SkeletronGhostArm.fx 幽灵臂条带材质
//UV.x 0肩根→1腕口 UV.y 横截面；预乘输出，AlphaBlend
//灵体三层：骨白芯线 / 幽青体 / 深青缘；噪声撕边+生长/侵蚀包络
//无极角运算；噪声全走条带平面uv（无接缝风险）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uGrow;        //生长头 0~1（自肩向腕显形）
float uDissolve;    //侵蚀 0~1（自肩向腕消解）
float uSeed;        //实例相位
float3 uCoreColor;  //骨白芯
float3 uBodyColor;  //幽青体
float3 uEdgeColor;  //深青缘

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
    float along = uv.x;                 //0 肩根 → 1 腕口
    float cross_ = (uv.y - 0.5) * 2.0;  //-1 ~ 1

    //---- 灵质流动：沿臂两层反向流 ----
    float flowA = tex2D(noiseSamp, float2(along * 1.7 - uTime * 0.9 + uSeed, uv.y * 0.6 + uSeed * 3.0)).r;
    float flowB = tex2D(noiseSamp, float2(along * 3.4 + uTime * 0.55 + uSeed * 7.0, uv.y * 1.1)).r;
    float flow = flowA * 0.65 + flowB * 0.35;

    //---- 撕裂边缘：横截面被噪声咬开 ----
    float edgeBite = (flow - 0.5) * 0.66;
    float halfWidth = 0.86 + edgeBite * 0.5;
    float body = saturate(1.0 - abs(cross_) / max(halfWidth, 0.05));
    //肩根撕散成灵雾，腕口结实
    float rootTear = smoothstep(0.0, 0.42, along + (flow - 0.5) * 0.22);
    body *= rootTear;

    //---- 生长/侵蚀包络（前沿被噪声打散，不是平切）----
    float growFront = uGrow * 1.25;
    float growMask = smoothstep(along - 0.16, along + 0.04, growFront + (flow - 0.5) * 0.1);
    float dissolveFront = uDissolve * 1.3;
    float dissolveMask = 1.0 - smoothstep(along - 0.05, along + 0.15, dissolveFront - 0.15 + (flow - 0.5) * 0.12);
    float envelope = growMask * dissolveMask;

    //---- 三层材质 ----
    float core = saturate(1.0 - abs(cross_) / 0.20) * smoothstep(0.25, 0.9, along);
    float rim = smoothstep(0.35, 0.95, abs(cross_) / max(halfWidth, 0.05)) * body;

    float3 color = uEdgeColor * rim * 0.9
        + uBodyColor * body * (0.45 + flow * 0.5)
        + uCoreColor * core * (0.55 + flowB * 0.35);

    //腕口聚能微亮
    color += uBodyColor * smoothstep(0.78, 1.0, along) * 0.35 * body;

    float alpha = saturate(body * envelope) * input.Color.a;
    //灵体半透，不压死黑
    alpha *= 0.82;

    //预乘输出
    return float4(color * alpha, alpha);
}

technique Technique1
{
    pass SkeletronGhostArmPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
