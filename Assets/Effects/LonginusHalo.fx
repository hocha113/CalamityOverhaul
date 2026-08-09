// ============================================================================
//LonginusHalo.fx 朗基努斯光轮
//quad 图元 Additive，金白圣光环；透视压扁由 C# 几何侧承担
//p=(uv-0.5)*2；上半(p.y<0)视作远侧压暗收细
//极角只以整数倍角进 sin(3θ/5θ)，跨 ±π 连续，审计安全
//uReveal 0~1 显现；uPulse 0~1 脉动增亮
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uReveal;
float uPulse;

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
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float r = length(p);

    //远侧(上半)收细压暗
    float far = saturate(-p.y);
    float thick = 0.10 * (1.0 - far * 0.35);
    float ringR = 0.70 * (0.85 + uReveal * 0.15);

    float d = abs(r - ringR);
    float ring = smoothstep(thick, thick * 0.15, d);
    float glow = exp2(-d * 9.0);

    //沿环流光：整数倍角谐波，连续
    float theta = atan2(p.y, p.x);
    float flow = 0.80 + 0.14 * sin(3.0 * theta - uTime * 2.1) + 0.06 * sin(5.0 * theta + uTime * 1.3);

    //噪声微闪
    float n = tex2D(noiseSamp, p * 0.4 + float2(uTime * 0.05, 0.13)).r;
    flow += (n - 0.5) * 0.10;

    float farDim = 1.0 - far * 0.42;
    float pulse = 1.0 + uPulse * 0.55;

    //画布边界保险
    float guardEdge = 1.0 - smoothstep(0.93, 1.0, max(abs(p.x), abs(p.y)));

    float3 cCore = float3(1.18, 1.00, 0.62);
    float3 cGlow = float3(0.95, 0.62, 0.22);

    float3 color = cCore * ring * flow + cGlow * glow * 0.55;
    float alpha = saturate(ring * 0.95 + glow * 0.4) * farDim * uReveal * pulse * guardEdge;

    return float4(color * alpha * farDim * pulse, alpha) * input.Color;
}

technique Technique1
{
    pass LonginusHaloPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
