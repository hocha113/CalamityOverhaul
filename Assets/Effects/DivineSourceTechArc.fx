// ============================================================================
//DivineSourceTechArc.fx 金源灭却刃·科技蓝挥砍刀光
//UV.x 弧向 0起手 1终点；UV.y 径向 0网格外缘 1内缘，v<HaloFrac 为带外光晕区
//顶点色 rgb=深度明暗 a=透明度；输出预乘 alpha 进 AlphaBlend
//s1 绑定噪声(消费端 device.Textures[1])
// ============================================================================

float4x4 WorldViewProjection;

sampler2D NoiseSampler : register(s1);

//带外光晕占径向的比例，C# 网格外扩折算与此常量锁定
static const float HaloFrac = 0.26;

float TotalTime;        //时间(秒)
float SweepT;           //0~1 已扫过弧向比例，刀锋热区贴 u≈SweepT
float FadeOut;          //0~1 消散进度 1完整可见
float GlowBoost;        //刀锋前沿亮度
float RimIntensity;     //外缘切割线强度
float EmpowerMix;       //0~1 充能金色混入量

float4 LeadColor;       //前沿白青
float4 CoreColor;       //亮青高光
float4 BodyColor;       //碧蓝主体
float4 MidColor;        //电蓝过渡
float4 DeepColor;       //深海军蓝拖尾
float4 AccentColor;     //充能金

struct VSInput
{
    float3 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput MainVS(VSInput input)
{
    VSOutput o;
    o.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    o.Color = input.Color;
    o.TexCoord = input.TexCoord;
    return o;
}

//PerlinNoise.png 实测值域 0.227~0.776，归一化后再做阈值
float nrm(float x)
{
    return saturate((x - 0.227) / 0.549);
}

float4 MainPS(VSOutput input) : COLOR0
{
    float u = saturate(input.TexCoord.x);       //弧向绝对进度
    float vRaw = saturate(input.TexCoord.y);    //网格径向
    //带内坐标 0刃线 1内缘
    float v = saturate((vRaw - HaloFrac) / (1.0 - HaloFrac));
    //带/晕分区，交界留一丝 AA
    float bandMask = smoothstep(HaloFrac - 0.012, HaloFrac + 0.004, vRaw);
    float haloMask = 1.0 - bandMask;

    //相对刀锋的追迹坐标 1=贴刀锋 0=起手拖尾
    float rel = saturate(u / max(SweepT, 0.0001));

    //噪声三通道
    float flow = nrm(tex2D(NoiseSampler, float2(u * 3.2 - TotalTime * 1.4, v * 0.55 + 0.1)).r);  //能流
    float fine = nrm(tex2D(NoiseSampler, float2(u * 8.0 - TotalTime * 2.6, v * 0.22 + 0.5)).r);  //高频数据丝
    float diss = nrm(tex2D(NoiseSampler, float2(u * 2.4 + TotalTime * 0.2, v * 1.1 + 0.25)).r);  //消散

    //量化单元格，时间取整步进=事件化闪烁而非噪点
    float cellU = floor(u * 20.0);
    float cellV = floor(v * 3.0);
    float tStep = floor(TotalTime * 9.0);
    float h = frac(sin(cellU * 127.1 + cellV * 311.7 + tStep * 74.7) * 43758.5453);
    float hotCell = step(0.84, h);
    float cellLight = 0.85 + 0.42 * hotCell - 0.22 * step(h, 0.13);

    //主体四段渐变 深海军蓝→电蓝→碧蓝→亮青(贴刀锋)
    float3 col = lerp(DeepColor.rgb, MidColor.rgb, smoothstep(0.0, 0.30, rel));
    col = lerp(col, BodyColor.rgb, smoothstep(0.28, 0.62, rel));
    col = lerp(col, CoreColor.rgb, smoothstep(0.62, 0.93, rel));
    //径向渐变，内缘沉入深蓝，读出带厚
    col = lerp(col, DeepColor.rgb * 0.75, v * 0.42);
    col *= cellLight;

    //断续数据虚线，step 硬切出科技感
    float dash = step(0.66, fine) * step(flow, 0.74);
    col += CoreColor.rgb * dash * (0.35 + 0.65 * rel) * (1.0 - v * 0.6) * 0.8;

    //外缘主刃线+错位第二细线(双描边)
    float rim = pow(saturate(1.0 - v / 0.085), 2.6);
    float rim2 = pow(saturate(1.0 - abs(v - 0.16) / 0.04), 2.0);
    float edgeGlow = (rim * RimIntensity + rim2 * 0.5 * RimIntensity) * bandMask;
    col += lerp(CoreColor.rgb, LeadColor.rgb, rel) * edgeGlow * (0.4 + 0.6 * rel) * 0.75;

    //前沿白热=刀锋后一道窄能量闸线，白是结构不是增益
    float lead = smoothstep(0.82, 0.985, rel);
    float head = smoothstep(0.962, 0.982, rel) * (1.0 - smoothstep(0.986, 0.998, rel));
    col += LeadColor.rgb * (lead * 0.16 + head * 0.55) * GlowBoost * (1.0 - v * 0.5);

    //充能金色只染结构件(数据丝/亮格/第二缘线)，主体保持蓝
    float goldMask = saturate(dash * 0.9 + hotCell * 0.55 + rim2 * 0.8);
    col = lerp(col, AccentColor.rgb, goldMask * EmpowerMix * 0.75);

    //厚重体 alpha，内缘噪声撕裂，拖尾保持结实
    float innerEdge = v + (flow - 0.5) * 0.16;
    float alpha = smoothstep(1.02, 0.70, innerEdge);
    alpha = max(alpha, edgeGlow * 0.85);
    alpha *= 0.55 + 0.45 * smoothstep(0.0, 0.38, rel);
    alpha *= bandMask;

    //块状量化消散，拖尾侧先蚀，带与晕共用同一消散前沿
    float cut = (1.0 - FadeOut) * 1.28;
    float blockNoise = diss * 0.72 + h * 0.28;
    float dissolveMask = smoothstep(cut - 0.03, cut + 0.22, u + (blockNoise - 0.5) * 0.30);
    alpha *= dissolveMask;

    //带外光晕，从刃线向外软衰减融入环境，充能时染一点金
    float haloIn = saturate(vRaw / HaloFrac);   //0=网格外缘 1=刃线
    float haloGlow = pow(haloIn, 2.3) * haloMask * dissolveMask * FadeOut;
    float3 haloCol = lerp(BodyColor.rgb, CoreColor.rgb, rel) * 0.7 + LeadColor.rgb * 0.22 * rel;
    haloCol = lerp(haloCol, AccentColor.rgb, EmpowerMix * 0.45);
    float haloA = haloGlow * (0.30 + 0.24 * rel) * input.Color.a;

    alpha = saturate(alpha) * input.Color.a;
    col *= input.Color.rgb;

    //前沿颜色增益略高于 alpha 增益，半加法白热；光晕以低 alpha 叠进来
    float glowAlpha = saturate(alpha + (lead * 0.15 + rim * 0.16 * bandMask) * FadeOut + haloA * 0.6);
    float3 rgbOut = col * alpha
        + LeadColor.rgb * (head * 0.22 + rim * 0.12 * bandMask) * FadeOut * GlowBoost * 0.35
        + haloCol * haloA * input.Color.rgb;
    return float4(rgbOut, glowAlpha);
}

technique Technique1
{
    pass MainPass
    {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
}
