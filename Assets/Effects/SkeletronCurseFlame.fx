// ============================================================================
//SkeletronCurseFlame.fx 阴魂冷焰（批量顶点quad）
//UV.x 0~1 横截面 UV.y 0焰根→1焰尖；顶点色打包实例参数：
//R=heat 火势 G=seed 相位 B=curse 诅咒紫混比 A=opacity
//材质：阴魂冷焰。签名行为：①焰舌被噪声撕裂、尖端断离成舌屑
//②内芯骨白/外鞘幽青/缘深青三温层 ③根部致密、上舔流动、灰烬星点剥落
//加色批输出 (SourceAlpha, One)；无极角运算，噪声全走焰面uv（无接缝）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float3 uCoreColor;   //骨白芯
float3 uBodyColor;   //幽青体
float3 uEdgeColor;   //深青缘
float3 uCurseColor;  //诅咒紫

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
    float heat = input.Color.r;
    float seed = input.Color.g * 8.0;
    float curse = input.Color.b;
    float opacity = input.Color.a;

    float x = (input.TexCoords.x - 0.5) * 2.0;  //-1~1 横
    float y = input.TexCoords.y;                //0 根 → 1 尖

    //---- 上舔流动：双层反向速率 ----
    float n1 = tex2D(noiseSamp, float2(x * 0.32 + seed, y * 0.85 - uTime * (1.0 + heat * 0.8) + seed * 2.7)).r;
    float n2 = tex2D(noiseSamp, float2(x * 0.9 - seed * 1.3 + uTime * 0.18, y * 1.8 - uTime * 1.7 + seed)).r;
    float flow = n1 * 0.62 + n2 * 0.38;

    //---- 焰体轮廓：根宽尖窄，噪声咬边 ----
    float width = (1.0 - y * 0.78) * (0.40 + 0.60 * flow);
    float body = saturate((width - abs(x)) * 3.4);

    //---- 焰舌撕裂：尖部被第二层噪声断离 ----
    float tear = saturate((n2 - y * 0.75) * 3.2 + 0.55);
    body *= tear;

    //---- quad边缘保险切（根软起，尖必归零）----
    body *= smoothstep(0.0, 0.07, y);
    body *= smoothstep(1.0, 0.85, y);

    //---- 三温层 ----
    float coreW = 0.30 * (1.0 - y * 0.9);
    float core = saturate((coreW - abs(x)) * 5.0) * saturate(1.0 - y * 1.25);
    float rim = smoothstep(0.45, 1.0, abs(x) / max(width, 0.05)) * body;

    float3 bodyCol = lerp(uBodyColor, uCurseColor, curse);
    float3 col = uEdgeColor * rim * 0.85
        + bodyCol * body * (0.50 + flow * 0.55)
        + uCoreColor * core * (0.60 + heat * 0.60);

    //---- 灰烬星点剥落（焰体上方稀疏亮屑）----
    float speck = step(0.93, n2) * smoothstep(0.30, 0.85, y) * tear;
    col += uCoreColor * speck * (0.5 + heat);

    //根致密尖稀薄
    float dens = lerp(1.0, 0.30, y);
    float alpha = saturate(body * dens + speck * 0.35) * opacity * (0.75 + heat * 0.25);
    return float4(col, alpha);
}

technique Technique1
{
    pass SkeletronCurseFlamePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
