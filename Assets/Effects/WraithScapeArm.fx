// WraithScapeArm.fx
// 世界空间三角带：0=玩家端腕根，1=替死目标端枯手。
// 材质是湿血与枯皮；噪声 UV 由 uLenScale 钉在世界空间，保证近/远距离纹路密度一致。

float4x4 transformMatrix;
float uTime;
float uOpacity;
float uRetract;
float uSeed;
float uTearAmp;
float uPulse;
float uPulseAmp;    // 宽度脉动幅度，传自 C# 端 sin(time*9+seed)
float uLenScale;    // totalLen / 260f，钉噪声到世界空间
float3 uColBase;
float3 uColVein;
float3 uColHot;

texture uNoiseTex;
sampler noiseSampler = sampler_state {
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput {
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

struct PSInput {
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput input) {
    PSInput output;
    output.Position  = mul(input.Position, transformMatrix);
    output.Color     = input.Color;
    output.TexCoords = input.TexCoords;
    return output;
}

float4 PixelShaderFunction(PSInput input) : COLOR0 {
    float u    = input.TexCoords.x;
    float side = input.TexCoords.y * 2.0 - 1.0;
    float flowTime = uTime * (0.65 + uPulse * 0.12);

    // 长度归一化噪声 UV，不同臂长下筋脉密度一致
    float2 uvLarge = float2(u * uLenScale * 1.10 - flowTime * 0.20 + uSeed * 0.013, side * 0.72 + uSeed * 0.031);
    float2 uvSmall = float2(u * uLenScale * 3.20 - flowTime * 0.52 + uSeed * 0.037, side * 1.35 - uSeed * 0.017);
    float large = tex2D(noiseSampler, uvLarge).r;
    float small = tex2D(noiseSampler, uvSmall).r;
    float tissue = saturate(large * 0.68 + small * 0.32);

    // 边缘撕裂：每个实例 V 偏移不同，避免形状雷同
    float edgeNoise = tex2D(noiseSampler,
        float2(u * 4.6 - flowTime * 0.32 + uSeed, 0.23 + uSeed * 0.07)).r;
    // uPulseAmp 产生轮廓轻微呼吸感
    float pulse = uPulseAmp * 0.04 * sin(uTime * 8.5 + u * 12.0);
    float edge = 0.82
        - uTearAmp * (0.10 + 0.18 * edgeNoise) * (0.35 + 0.65 * sin(u * 3.1415926))
        - pulse;
    float bodyMask = 1.0 - smoothstep(edge - 0.10, edge, abs(side));

    // 展开方向：从玩家端向目标端
    float revealFront = 1.0 - uRetract;
    float reveal    = 1.0 - smoothstep(revealFront - 0.075, revealFront + 0.085, u);
    float rootFade  = smoothstep(0.005, 0.075, u);
    float handFade  = 1.0 - smoothstep(0.965, 1.0, u);

    // 血肉基底 + 动脉筋络
    float vein = smoothstep(0.58, 0.92, small) * (0.30 + tissue * 0.70);
    float wet  = smoothstep(0.72, 0.98, large) * (0.22 + 0.20 * uPulse);
    float3 color = lerp(uColBase, uColVein, vein * 0.78);
    color += uColVein * wet * 0.30;
    color += uColHot  * smoothstep(0.88, 1.0, tissue) * reveal * 0.16;
    color  = max(color, float3(0.005, 0.001, 0.002));

    float alpha = bodyMask * reveal * rootFade * handFade;
    alpha *= (0.48 + tissue * 0.60) * uOpacity;
    alpha  = saturate(alpha);

    float alive = step(0.004, alpha);
    return float4(color * alpha * alive, alpha * alive);
}

technique Technique1 {
    pass P0 {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 PixelShaderFunction();
    }
}