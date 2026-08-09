// ============================================================================
//DawnshatterSlash.fx 苍穹破晓刀光,日冕之火
//TriangleStrip 预乘输出,C# 侧 BlendState.AlphaBlend
//UV.x 0尾→1头 UV.y 0外缘→1内缘
//顶点色 R=z(0远~1近,0.5平面) G=热度 B=股序(0焦暗衬 0.5主焰) A=不透明度
//焰芯不再是独立条带,由主焰内沿撕裂外缘生成衬线
//直线算术+plain tex2D,无分支无 atan2
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;    //整体透明度 0~1
float uHeat;    //点燃度 0~1,爆发帧后升满
float uFlash;   //过曝脉冲 0~1,命中/爆发,≤2帧
float uStrokeLen; //本笔画保留段弧长(px),噪声按像素尺度采样;600=中性刻度
float uStrokeOff; //尾侧已裁弧长(px),相位锚定笔画起点,裁尾推进时纹理不滑动

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

    //噪声横坐标换算到弧长像素尺度:UV 按保留段归一,直接采样会把噪声拉伸十几倍,
    //一切侵蚀边都被抻成光滑缎带(平滑断口病根);相位锚定笔画起点,增长或裁尾都不滑动
    float sx = (uStrokeOff + uv.x * max(uStrokeLen, 60.0)) / 600.0;

    //双层滚动噪声,火体沿笔画向尾平流
    float n1 = tex2D(noiseSamp, float2(sx * 1.6 - uTime * 1.15, uv.y * 0.85 + uTime * 0.12)).r;
    float n2 = tex2D(noiseSamp, float2(sx * 3.7 - uTime * 2.0, uv.y * 2.1 - uTime * 0.5)).r;
    float flow = n1 * 0.62 + n2 * 0.38;

    //火舌:低频叶瓣定指形+高频毛边,外缘侵蚀成舌
    float lobe = tex2D(noiseSamp, float2(sx * 2.3 - uTime * 1.5, 0.31)).r;
    float ragged = tex2D(noiseSamp, float2(sx * 6.5 - uTime * 2.6, uv.y * 3.0)).r;
    //啃噬深度钳位非负:负值会把火体压上条带几何外缘,裁出一条平滑硬弧线
    float bite = max((lobe - 0.42) * 0.34 + (ragged - 0.5) * 0.12, 0.0);
    float outerMask = smoothstep(0.02 + bite, 0.30 + bite, uv.y);
    float innerMask = smoothstep(1.0, 0.40, uv.y);

    //端头撕裂场,只随横向位置起伏:同一条端线上不同高度在不同时刻断开,端口才不是平切面
    //带宽约 200px,取 5/12 个周期得 40px/17px 量级的齿;上版只有 2/5 个周期,端线只有几个大缓坡=仍读作直线
    float tearA = tex2D(noiseSamp, float2(uv.y * 5.3 + uTime * 0.09, 0.29)).r;
    float tearB = tex2D(noiseSamp, float2(uv.y * 11.7 - uTime * 0.17, 0.68)).r;
    float tear = tearA * 0.62 + tearB * 0.38;

    //尾端老化:淡出阈值随撕裂起伏;撕碎区只许啃尾部三成,伸进弧身正中会咬出平滑豁口(上版病根之一)
    float tailEdge = 0.06 + tear * 0.16;
    float ageMask = smoothstep(0.0, tailEdge + 0.26, age);
    ageMask *= ageMask;
    float shred = smoothstep(0.30, 0.62, flow + (age - tailEdge) * 2.2);
    ageMask *= lerp(shred, 1.0, smoothstep(tailEdge, tailEdge + 0.30, age));

    //头端参差:几何头端已钉在刃上(最新的火最亮,不该淡出),只把端线本身咬碎
    //归零位置必须逐行不同——上版让斜坡起点起伏却把终点钉死在 0.995,alpha 仍在同一 age 归零,照样是直线
    float headEnd = 1.0 - tear * 0.05 - ragged * 0.02;
    float headMask = 1.0 - smoothstep(headEnd - 0.05 - flow * 0.03, headEnd, age);

    float body = outerMask * innerMask * ageMask * headMask;

    //流光丝:噪声脊线高光
    float filament = smoothstep(0.56, 0.82, flow) * body;

    //贴尾焰屑点剥离
    float dotN = tex2D(noiseSamp, float2(sx * 7.2 - uTime * 0.6, uv.y * 5.4 + uTime * 1.2)).r;
    //焰屑游离在火体外是有意的,但贴近几何外缘要压零,免得屑点被条带边界切出平边
    float emberDot = smoothstep(0.80, 0.93, dotN) * innerMask * smoothstep(0.0, 0.08, uv.y)
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

    //股序权重,B 每股常量(0焦暗衬 0.5主焰)
    float layer = input.Color.b;
    float wBack = 1.0 - smoothstep(0.15, 0.40, layer);
    float wBody = 1.0 - wBack;

    //焦暗衬:暗体高遮盖,给发光体实体轮廓
    float3 colBack = cChar * 0.9;
    float aBack = body * 0.78;

    //焰芯:金白衬线贴着被啃噬后的外缘轮廓走,跟随火舌起伏
    //旧版是独立窄条带,自身羽化只有十几像素,最亮的线以刀切边贴光滑螺线=断口主犯;刺模式沿中脊
    float coreHot = smoothstep(0.30, 0.95, age) * (0.3 + 0.7 * uHeat);
    float edgeDist = uv.y - 0.02 - bite;
    float coreArc = smoothstep(0.02, 0.10, edgeDist) * (1.0 - smoothstep(0.16, 0.34, edgeDist));
    float core = lerp(smoothstep(0.55, 0.85, uv.y), coreArc, arcMode) * coreHot * body;

    //主焰:斜坡+丝+焰屑+焰芯衬线
    float3 colBody = ramp * (0.55 + 0.5 * flow)
                   + cGold * filament * (0.28 + 0.5 * uHeat)
                   + cGold * emberDot * 0.8
                   + lerp(cGold, float3(1.15, 1.02, 0.72), 0.5) * core * 1.2;
    float aBody = saturate(body * 0.85 + filament * 0.30 + emberDot * 0.35 + core * 0.55);

    float3 color = colBack * wBack + colBody * wBody;
    float alpha = aBack * wBack + aBody * wBody;

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
