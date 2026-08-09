// ============================================================================
//LonginusWing.fx 朗基努斯光之翼翼羽光带（二稿：不对称截面）
//TriangleStrip Additive。光带≠发光香蕉的分水岭在截面：
//  uv.y=0 前缘(上缘)白热锐边 → 金→深琥珀渐变 → uv.y=1 后缘噪声撕散成光缕
//  撕散量随弧长 u 加深，端部整条散开；极光竖纹沿带外流；虹彩收进撕散区
//uv.x 0=翼根 1=羽端；uOpen 弧长揭示带白热前沿；uPhase 羽条错相；全程线性 UV 无极角
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uOpen;
float uPhase;
float uHot;

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
    float u = uv.x;
    float v = uv.y;

    //低频窗帘褶 / 高频撕散细节
    float n1 = tex2D(noiseSamp, float2(u * 1.5 - uTime * 0.50 + uPhase, v * 0.40 + uPhase * 1.3)).r;
    float n2 = tex2D(noiseSamp, float2(u * 3.2 - uTime * 0.95 + uPhase * 2.0, v * 0.90 + uPhase)).r;

    //主体：后缘基准随 u 前移，噪声大摆幅撕散——端部整条散成光缕，不许平滑收口
    float bodyEnd = lerp(0.94, 0.34, u * u);
    float body = 1.0 - smoothstep(bodyEnd - 0.34, bodyEnd + 0.10, v + (n2 - 0.5) * 0.55);

    //前缘白热锐边(v~0.10)：锐芯 + 窄晕，贯穿全长直到端部
    float leadDist = abs(v - 0.10);
    float lead = exp2(-leadDist * leadDist * 320.0);
    float leadGlow = exp2(-leadDist * leadDist * 42.0) * 0.55;

    //极光竖纹：噪声脊线沿带外流(能量从爆心涌出)
    float ridge = smoothstep(0.34, 0.78, n1);

    //根部软入 + uOpen 揭示(白热前沿在揭示途中最亮，展开完成即熄)
    float rootIn = smoothstep(0.0, 0.06, u);
    float reveal = 1.0 - smoothstep(uOpen - 0.05, uOpen + 0.02, u);
    float tipHot = exp2(-abs(u - uOpen) * 20.0) * saturate(1.0 - uOpen * 1.02);

    //画布边界保险：横截两端与弧长末端自然归零，杜绝硬切(前缘锐边不撕散，全靠此处收尾)
    float guardEdge = smoothstep(0.0, 0.055, v) * smoothstep(1.0, 0.93, v) * smoothstep(1.0, 0.90, u);

    //虹彩色散：收进后缘撕散区与端部，三色相位错 120°
    float iriZone = smoothstep(0.38, 0.85, v + u * 0.22);
    float iph = u * 6.0 + v * 3.0 - uTime * 1.6 + uPhase * 5.0;
    float3 iri = float3(0.5 + 0.5 * sin(iph), 0.5 + 0.5 * sin(iph + 2.094), 0.5 + 0.5 * sin(iph + 4.189));

    float3 cCore = float3(1.34, 1.24, 1.04);
    float3 cBody = float3(1.06, 0.74, 0.30);
    float3 cDeep = float3(0.84, 0.32, 0.15);

    //前缘亮金→后缘深琥珀，极光纹调制明暗
    float3 bandCol = lerp(cBody, cDeep, smoothstep(0.16, 0.92, v));
    float bodyA = body * (0.60 + ridge * 0.26);

    float fade = rootIn * reveal * guardEdge;

    float3 color = bandCol * bodyA;
    color += cCore * (lead * (0.92 + uHot * 0.55) + leadGlow) * body;
    color += iri * iriZone * bodyA * 0.22;
    color += cCore * tipHot * 1.25;

    float alpha = saturate(bodyA * 0.62 + lead * body * 0.90 + leadGlow * body * 0.30 + tipHot * 0.55)
        * fade * input.Color.a;

    return float4(color * alpha, alpha) * float4(input.Color.rgb, 1.0);
}

technique Technique1
{
    pass LonginusWingPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
