// ============================================================================
//DawnshatterSlash.fx 苍穹破晓刀光,日冕之火
//TriangleStrip 预乘输出,C# 侧 BlendState.AlphaBlend
//UV.x 0尾→1头 UV.y 0外缘→1内缘
//顶点色 R=z(0远~1近,0.5平面) G=热度 B=股序(0焦暗衬 0.5主焰 1焰芯) A=不透明度
//直线算术+plain tex2D,无分支无 atan2
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //点燃度 0~1,爆发帧后升满
float uFlash;   //过曝脉冲 0~1,命中/爆发,≤2帧

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
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
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

float4 CorePS(PSInput input, float arcMode)
{
    float2 uv = input.TexCoords;
    float age = uv.x; //1=头 最热

    //双层滚动噪声,火体沿笔画向尾平流
    float n1 = tex2D(noiseSamp, float2(uv.x * 1.6 - uTime * 1.15, uv.y * 0.85 + uTime * 0.12)).r;
    float n2 = tex2D(noiseSamp, float2(uv.x * 3.7 - uTime * 2.0, uv.y * 2.1 - uTime * 0.5)).r;
    float flow = n1 * 0.62 + n2 * 0.38;

    //火舌:低频叶瓣定指形+高频毛边,外缘侵蚀成舌
    float lobe = tex2D(noiseSamp, float2(uv.x * 2.3 - uTime * 1.5, 0.31)).r;
    float ragged = tex2D(noiseSamp, float2(uv.x * 6.5 - uTime * 2.6, uv.y * 3.0)).r;
    float bite = (lobe - 0.42) * 0.34 + (ragged - 0.5) * 0.12;
    float outerMask = smoothstep(0.02 + bite, 0.30 + bite, uv.y);
    float innerMask = smoothstep(1.0, 0.40, uv.y);

    //尾迹老化
    float ageMask = smoothstep(0.0, 0.5, age);
    ageMask *= ageMask;
    float body = outerMask * innerMask * ageMask;

    //流光丝:噪声脊线高光
    float filament = smoothstep(0.56, 0.82, flow) * body;

    //贴尾焰屑点剥离
    float dotN = tex2D(noiseSamp, float2(uv.x * 7.2 - uTime * 0.6, uv.y * 5.4 + uTime * 1.2)).r;
    float emberDot = smoothstep(0.80, 0.93, dotN) * innerMask
                   * smoothstep(0.55, 0.15, age) * smoothstep(0.0, 0.25, age);

    //温度:头热尾冷,顶点热度与全局点燃度共同抬升
    float heatV = saturate(age * (0.45 + 0.55 * input.Color.g) + uHeat * 0.25);

    //冷却斜坡 焦暗→深红→橙→金
    float3 cChar = float3(0.10, 0.035, 0.025);
    float3 cRed = float3(0.48, 0.09, 0.035);
    float3 cOrn = float3(0.98, 0.40, 0.09);
    float3 cGold = float3(1.05, 0.80, 0.34);
    float3 ramp = lerp(cChar, cRed, saturate(heatV * 3.0));
    ramp = lerp(ramp, cOrn, saturate(heatV * 2.0 - 0.6));
    ramp = lerp(ramp, cGold, saturate(heatV * 2.2 - 1.25));

    //股序权重,B 每股常量
    float layer = input.Color.b;
    float wBack = 1.0 - smoothstep(0.15, 0.40, layer);
    float wCore = smoothstep(0.72, 0.92, layer);
    float wBody = saturate(1.0 - wBack - wCore);

    //焦暗衬:暗体高遮盖,给发光体实体轮廓
    float3 colBack = cChar * 0.9;
    float aBack = body * 0.78;

    //主焰:斜坡+丝+焰屑
    float3 colBody = ramp * (0.55 + 0.5 * flow)
                   + cGold * filament * (0.28 + 0.5 * uHeat)
                   + cGold * emberDot * 0.8;
    float aBody = saturate(body * 0.85 + filament * 0.30 + emberDot * 0.35);

    //焰芯:金白窄芯,点燃后才亮
    float coreHot = smoothstep(0.30, 0.95, age) * (0.3 + 0.7 * uHeat);
    float3 colCore = lerp(cGold, float3(1.15, 1.02, 0.72), 0.5) * coreHot * 1.3;
    float aCore = body * coreHot * 0.9;

    float3 color = colBack * wBack + colBody * wBody + colCore * wCore;
    float alpha = aBack * wBack + aBody * wBody + aCore * wCore;

    //过曝脉冲,金白不驻留
    color += float3(1.2, 1.05, 0.75) * uFlash * body * 0.8;

    //远近分层,仅弧光模式消费 z
    float zk = lerp(1.0, lerp(0.68, 1.14, input.Color.r), arcMode);
    color *= zk;

    alpha = saturate(alpha) * uFade * input.Color.a;
    return float4(color * alpha, alpha);
}

float4 PSThrust(PSInput input) : COLOR0
{
    return CorePS(input, 0.0);
}

float4 PSArc(PSInput input) : COLOR0
{
    return CorePS(input, 1.0);
}

technique TechThrust
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSThrust();
    }
}

technique TechArc
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSArc();
    }
}
