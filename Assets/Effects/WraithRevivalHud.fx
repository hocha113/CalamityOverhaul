// WraithRevivalHud.fx
// 横向进度条：墨色（低值）→ 血色（高值），前沿噪声撕裂，危险区域脉冲心跳。
// UV: x = 沿条方向 0→1，y = 宽度方向 0→1

float4x4 transformMatrix;
float uTime;
float uProgress;      // 填充比例 0→1
float uDangerPulse;   // 进度>0.7 时注入的脉冲强度（0→1）
float3 uColInk;       // 墨色基底
float3 uColBlood;     // 血色高值

texture uNoiseTex;
sampler noiseSampler = sampler_state {
    texture   = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

struct VSInput {
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct PSInput {
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput input) {
    PSInput output;
    output.Position  = mul(input.Position, transformMatrix);
    output.Color     = input.Color;
    output.TexCoords = input.TexCoords;
    return output;
}

float4 PixelShaderFunction(PSInput input) : COLOR0 {
    float u   = input.TexCoords.x;
    float v   = input.TexCoords.y;
    float side = v * 2.0 - 1.0;   // -1..+1, 0=中轴

    // 前沿撕裂噪声：采样两层不同频率
    float noiseU1 = tex2D(noiseSampler, float2(u * 3.5 - uTime * 0.18, v * 1.8)).r;
    float noiseU2 = tex2D(noiseSampler, float2(u * 9.2 + uTime * 0.34, v * 2.6 + 0.5)).r;
    float tearNoise = noiseU1 * 0.6 + noiseU2 * 0.4;

    // 动态前沿：uProgress 加噪声扰动
    float fillFront = uProgress - (1.0 - smoothstep(0.0, 0.12, uProgress)) * 0.0;
    float jitter = (tearNoise - 0.5) * 0.055;
    float filled = step(u, fillFront + jitter);

    // 颜色：按进度线性混墨→血
    float colMix = smoothstep(0.0, 0.8, uProgress);
    float3 barColor = lerp(uColInk, uColBlood, colMix);

    // 中脊高光（细线，随时间缓移）
    float2 uvSpine = float2(u * 4.2 - uTime * 0.22, 0.5);
    float spineNoise = tex2D(noiseSampler, uvSpine).r;
    float spine = smoothstep(0.72, 0.95, spineNoise) * smoothstep(0.12, 0.0, abs(side)) * 0.55;
    barColor += spine * 0.35;

    // 危险脉冲心跳：全条闪
    float pulse = uDangerPulse * 0.22 * sin(uTime * 9.8) * smoothstep(0.5, 1.0, uProgress);
    barColor = saturate(barColor + pulse);

    // 边缘软渐变（避免硬边矩形感）
    float edgeFade = 1.0 - smoothstep(0.72, 1.0, abs(side));
    // 条形两端淡出
    float leftFade  = smoothstep(0.0, 0.018, u);
    float rightFade = 1.0 - smoothstep(0.982, 1.0, u);

    float alpha = filled * edgeFade * leftFade * rightFade;
    // 在填充前沿附近做渐进透明，让边缘看起来是"墨汁浸开"
    float frontBleed = smoothstep(0.0, 0.06, fillFront - u + jitter * 0.5) * 0.38;
    alpha = max(alpha, frontBleed * edgeFade * leftFade * rightFade);

    // 背底薄层：让未填充区域有隐约框架感
    float backAlpha = edgeFade * leftFade * rightFade * 0.12 * (1.0 - filled);
    float3 backColor = uColInk * 0.4;

    float3 finalColor = lerp(backColor, barColor, filled);
    float finalAlpha  = saturate(alpha + backAlpha);

    return float4(finalColor * finalAlpha, finalAlpha);
}

technique Technique1 {
    pass P0 {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 PixelShaderFunction();
    }
}