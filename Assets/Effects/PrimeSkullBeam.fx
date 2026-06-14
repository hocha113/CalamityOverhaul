// ============================================================================
//PrimeSkullBeam.fx 颅骨主炮巨型光束
//UV.x 0枪口→1末端 UV.y 0.5中心；协议同 TwinsDeathRayBeam
//Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //Extra_193：Voronoi 细胞灰度图（能量蜂窝）
sampler uImage2 : register(s2); //PerlinNoise：低频云状噪声

float3 uColor;          //内层主题色（橙红）
float3 uSecondaryColor; //外缘主题色（琥珀）
float uTime;
float uOpacity;
float uIntensity;
float uExpandProgress;  //展开进度 0~1：枪口聚拢与边缘撕裂量

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float hash(float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float dist = abs(uv.y - 0.5) * 2.0;

    //=== 流动噪声场（向末端高速流动，机械等离子感）===
    float n1 = tex2D(uImage1, float2(along * 4.5 - uTime * 3.6, uv.y * 2.0)).r;
    float n2 = tex2D(uImage2, float2(along * 2.2 - uTime * 1.9, uv.y * 1.2 + uTime * 0.2)).r;
    float turbulence = (n1 * 0.55 + n2 * 0.45 - 0.5) * 0.16;
    float distorted = dist + turbulence * (0.5 + dist * 1.5);

    //=== 分层光柱 ===
    float core = 1.0 - smoothstep(0.0, 0.20, distorted);
    core = pow(core, 1.5);
    float body = 1.0 - smoothstep(0.0, 0.60, distorted);
    body = pow(body, 2.0);
    float fringe = (1.0 - smoothstep(0.25, 1.0, distorted));
    fringe = pow(fringe, 2.6) * 0.45;

    //=== 蜂窝能量格栅（机械标志性纹理：沿束滚动的 Voronoi 脊线）===
    float cell = tex2D(uImage1, float2(along * 3.2 - uTime * 0.9, uv.y * 1.5)).g;
    float lattice = pow(saturate(cell * 1.4 - 0.35), 2.0) * body * 0.5;

    //=== 齿状行进脉冲（方波：机械感，区别于生物系正弦）===
    float pulse = step(0.5, frac(along * 9.0 - uTime * 5.0)) * 0.16 + 0.84;

    //=== 电离闪络 ===
    float flicker = hash(float2(floor(along * 52.0), floor(uTime * 14.0)));
    flicker = step(0.88, flicker) * (1.0 - dist) * 1.5;

    //=== 端点处理 ===
    float muzzle = smoothstep(0.0, 0.05 + 0.05 * (1.0 - uExpandProgress), along);
    float muzzleGlow = (1.0 - smoothstep(0.0, 0.10, along)) * 1.5;
    float tipFade = 1.0 - smoothstep(0.90, 1.0, along);

    //=== 边缘噪声撕裂 ===
    float bite = tex2D(uImage1, float2(along * 6.5 - uTime * 2.8, uv.y * 3.0)).g;
    float edgeMask = smoothstep(0.95, 0.45, distorted + bite * 0.30 * (1.0 - core));

    //=== 强度合成 ===
    float intensity = 0.0;
    intensity += core * 1.3 * pulse;
    intensity += body * 0.7 * pulse;
    intensity += fringe;
    intensity += lattice;
    intensity += flicker * 0.45;
    intensity *= muzzle * tipFade * edgeMask;
    intensity += muzzleGlow * core * 0.9;
    intensity *= uIntensity * uOpacity;

    //=== 色带映射 ===
    float3 col;
    if (distorted < 0.20)
    {
        col = lerp(float3(1.0, 1.0, 1.0), uColor, distorted / 0.20 * 0.5);
    }
    else
    {
        float t = saturate((distorted - 0.20) / 0.7);
        col = lerp(uColor, uSecondaryColor, t);
    }
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(flicker * 0.7));
    col += float3(1.0, 1.0, 1.0) * pow(saturate(1.0 - distorted), 8.0) * 0.7;

    col *= input.Color.rgb;
    return float4(col * intensity, saturate(intensity) * input.Color.a);
}

technique Technique1
{
    pass PrimeSkullBeamPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
