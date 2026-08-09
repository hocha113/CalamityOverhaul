// ============================================================================
//LonginusWing.fx 朗基努斯光之翼翼羽条带
//TriangleStrip Additive：白热芯 + 琥珀金外鞘 + 外流光丝 + 端部噪声羽化 + 边缘虹彩色散
//uv.x 0=翼根 1=羽端 弧长参数；uv.y 0~1 横截；顶点色=羽条tint(alpha 调强度)
//uOpen 0~1 按弧长揭示，前沿带白热尖；uPhase 羽条错相；全程线性 UV 无极角
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
    float cross = abs(uv.y - 0.5) * 2.0;

    float n = tex2D(noiseSamp, float2(u * 1.8 - uTime * 0.45 + uPhase, uv.y * 0.5 + uPhase * 0.7)).r;

    //横截轮廓：根满端细(几何另有物理收细，双重包络)
    float widthEnv = lerp(0.72, 0.26, u);
    float core = smoothstep(widthEnv * 0.42, widthEnv * 0.05, cross);
    float sheath = smoothstep(widthEnv * 1.30, widthEnv * 0.22, cross);

    //画布边界保险：横截与弧长两端都在 92% 内自然归零，杜绝硬切
    float guardEdge = smoothstep(1.0, 0.90, cross) * smoothstep(1.0, 0.93, u);

    //根部软入
    float rootIn = smoothstep(0.0, 0.05, u);

    //端部噪声羽化撕散，不许平滑收口
    float tipTear = 1.0 - smoothstep(0.68, 0.98, u + (n - 0.5) * 0.30);

    //uOpen 揭示：羽条自根长出，前沿白热尖
    float reveal = 1.0 - smoothstep(uOpen - 0.04, uOpen + 0.02, u);
    float tipHot = exp2(-abs(u - uOpen) * 24.0) * saturate(1.0 - uOpen * 1.02)
        * smoothstep(widthEnv * 1.6, 0.0, cross);

    //沿羽外流光丝
    float flow = tex2D(noiseSamp, float2(u * 2.6 - uTime * 1.1 + uPhase * 3.0, uv.y * 1.2)).r;
    float filament = smoothstep(0.56, 0.86, flow) * sheath * 0.40;

    //边缘虹彩色散，横截边缘带内三色相位错 120°
    float edgeBand = smoothstep(widthEnv * 0.45, widthEnv * 1.05, cross) * sheath;
    float iph = u * 7.0 + cross * 2.5 - uTime * 1.4 + uPhase * 6.0;
    float3 iri = float3(0.5 + 0.5 * sin(iph), 0.5 + 0.5 * sin(iph + 2.094), 0.5 + 0.5 * sin(iph + 4.189));

    float3 cCore = float3(1.30, 1.16, 0.95);
    float3 cSheath = float3(1.02, 0.70, 0.26);

    float fade = rootIn * tipTear * reveal * guardEdge;
    float3 color = cSheath * (sheath * 0.85 + filament);
    color += cCore * core * (0.75 + uHot * 0.5);
    color += iri * edgeBand * 0.15;
    color += cCore * tipHot * 1.3;

    float alpha = saturate(sheath * 0.55 + core * 0.95 + tipHot * 0.6) * fade * input.Color.a;

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
