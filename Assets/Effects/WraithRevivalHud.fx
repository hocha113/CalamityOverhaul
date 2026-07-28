// WraithRevivalHud.fx
// 横向进度条：墨色（低值）→ 血色（高值），前沿噪声撕裂，危险区域脉冲心跳。
// 只写 pixel shader，顶点变换由 SpriteBatch 内置管线处理。
// uImage0 (s0) = MagicPixel（SpriteBatch 主纹理，UV 0→1 覆盖整条）
// uNoiseTex    = 柔性噪声贴图

sampler uImage0 : register(s0);

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

struct PSInput {
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float4 PixelShaderFunction(PSInput input) : COLOR0 {
    float u   = input.TexCoords.x;
    float v   = input.TexCoords.y;
    float side = v * 2.0 - 1.0;   // -1..+1, 0=中轴

    // 前沿撕裂噪声：采样两层不同频率
    float noiseU1 = tex2D(noiseSampler, float2(u * 3.5 - uTime * 0.18, v * 1.8)).r;
    float noiseU2 = tex2D(noiseSampler, float2(u * 9.2 + uTime * 0.34, v * 2.6 + 0.5)).r;
    float tearNoise = noiseU1 * 0.6 + noiseU2 * 0.4;

    // 动态前沿：uProgress 加噪声扰动
    float fillFront = uProgress;
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

    // 危险脉冲心跳
    float pulse = uDangerPulse * 0.22 * sin(uTime * 9.8) * smoothstep(0.5, 1.0, uProgress);
    barColor = saturate(barColor + pulse);

    // 边缘软渐变
    float edgeFade = 1.0 - smoothstep(0.72, 1.0, abs(side));
    float leftFade  = smoothstep(0.0, 0.018, u);
    float rightFade = 1.0 - smoothstep(0.982, 1.0, u);

    float alpha = filled * edgeFade * leftFade * rightFade;
    float frontBleed = smoothstep(0.0, 0.06, fillFront - u + jitter * 0.5) * 0.38;
    alpha = max(alpha, frontBleed * edgeFade * leftFade * rightFade);

    // 背底薄层：未填充区域隐约框架感
    float backAlpha = edgeFade * leftFade * rightFade * 0.12 * (1.0 - filled);
    float3 backColor = uColInk * 0.4;

    float3 finalColor = lerp(backColor, barColor, filled);
    float finalAlpha  = saturate(alpha + backAlpha);

    return float4(finalColor * finalAlpha, finalAlpha);
}

technique Technique1 {
    pass P0 {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}