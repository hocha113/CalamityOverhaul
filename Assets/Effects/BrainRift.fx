// ============================================================================
// BrainRift.fx 瞬移预兆裂隙，空间上撕开的一道立式血肉创口
// 画布：placeholder2 白像素 quad（UV 0~1），几何全程序化
// 全笛卡尔坐标，无 atan2 无极角（接缝审计天然通过）
// uOpen=撕开量 uPulse=心跳搏动（真假裂隙的搏动时机由 CPU 决定）
// ============================================================================

// 噪声固定 s1：本 shader 不采样 s0（画布只是白像素 quad），
// 旧 sampler_state 自动分配落 s0，被 SpriteBatch 用画布贴图覆写→毛边/湿纹全读成辉光渐变；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseTex : register(s1);

float uTime;
float uOpen;     //0~1 撕开
float uPulse;    //0~1 收缩拍包络
float uSeed;     //个体差异

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //中心化坐标：x 压扁成立式创口
    float2 p = (coords - 0.5) * 2.0;

    //创口毛边：沿缘噪声起伏（笛卡尔采样+时间蠕动）
    float edgeNoise = tex2D(noiseTex, float2(p.y * 0.9 + uSeed, uSeed * 3.1 + uTime * 0.05)).r;
    float edgeNoise2 = tex2D(noiseTex, float2(p.y * 2.3 - uSeed * 2.0, uSeed - uTime * 0.037)).r;

    //梭形创口 SDF：竖长横窄，端部收尖
    float taper = 1.0 - p.y * p.y;                    //端部收窄
    float halfWidth = uOpen * 0.34 * saturate(taper) * (0.72 + edgeNoise * 0.42);
    float dist = abs(p.x) - halfWidth;

    //搏动：整体轻胀
    dist -= uPulse * 0.045 * saturate(taper);

    //口内：深处近黑的血肉暗腔，带向内流动的湿纹
    float inside = saturate(-dist * 9.0);
    float flow = tex2D(noiseTex, float2(p.x * 1.4 + uSeed, p.y * 0.5 - uTime * 0.11 + uSeed)).r;
    float depth = saturate(abs(p.x) / max(halfWidth, 1e-4));   //0 中轴 ~ 1 缘
    float3 cavity = lerp(float3(0.055, 0.004, 0.010), float3(0.30, 0.035, 0.06), depth * depth);
    cavity += float3(0.16, 0.02, 0.03) * flow * (1.0 - depth) * 0.6;

    //缘唇：亮血描边（贴创缘一窄条），搏动时炽亮
    float lip = saturate(1.0 - abs(dist) * 16.0) * step(0.02, uOpen);
    float3 lipColor = float3(0.66, 0.08, 0.11) * (0.8 + uPulse * 1.5) * lip;

    //外泌血雾光晕：创口外软衰减
    float halo = exp(-max(dist, 0.0) * 5.5) * (0.28 + uPulse * 0.5) * saturate(taper) * uOpen;
    float3 haloColor = float3(0.36, 0.04, 0.07) * halo * (0.7 + edgeNoise2 * 0.5);

    //边界保险：画布边缘归零
    float guard = saturate((1.0 - abs(p.x)) * 6.0) * saturate((1.0 - abs(p.y)) * 6.0);

    float alpha = saturate(inside + lip * 0.9 + halo * 0.8) * guard;
    float3 color = (cavity * inside + lipColor + haloColor) * guard;

    //预乘输出
    return float4(color, alpha) * vertexColor;
}

technique Technique1
{
    pass BrainRiftPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
