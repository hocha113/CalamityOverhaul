// ============================================================================
//BRelicTwinTether.fx 双瞳系绳：双端瞳孔收口的切割能量系绳
//UV.x 0=视界(红激光)端→1=焚瞳(青焰)端；Additive 批，a 携带包络
//语汇承 TwinsDeathRayBeam(三层流噪+分层光柱+边缘撕裂)，细化点：
//双端锥形收口(禁平切)、红绿双色对冲能流、中点驻波干涉亮斑
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);   //Extra_193
sampler uImage2 : register(s2);   //PerlinNoise(实测值域约0.23~0.78，只作扰动不作阈值门)

float3 uColorA;      //视界端主题色(红)
float3 uColorB;      //焚瞳端主题色(青焰绿)
float uTime;
float uOpacity;
float uIntensity;
float uCutFlash;     //切中敌人的增亮脉冲0~1
float uLenScale;     //长度px/240，稳定沿线噪声密度

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float dist = abs(uv.y - 0.5) * 2.0;

    //=== 双端瞳孔收口：横向半宽沿长度收束到两端归零 ===
    float taper = smoothstep(0.0, 0.085, along) * smoothstep(1.0, 0.915, along);
    taper = pow(taper, 0.62);
    float distorted0 = dist / max(taper, 0.001);

    //=== 双向对冲流噪声：两端能量向中点行进 ===
    float ax = along * uLenScale;
    float nA = tex2D(uImage2, float2(ax * 3.4 - uTime * 2.6, uv.y * 1.6)).r;
    float nB = tex2D(uImage2, float2(ax * 2.7 + uTime * 2.2, uv.y * 1.1 + 0.37)).g;
    float nS = tex2D(uImage1, float2(ax * 1.1 - uTime * 0.5, uv.y * 0.7 + uTime * 0.18)).r;
    float turbulence = (nA * 0.42 + nB * 0.38 + nS * 0.20 - 0.5) * 0.24;
    float distorted = distorted0 + turbulence * (0.5 + distorted0 * 1.5);

    //=== 分层光柱 ===
    float core = pow(saturate(1.0 - smoothstep(0.0, 0.20, distorted)), 1.5);
    float body = pow(saturate(1.0 - smoothstep(0.0, 0.66, distorted)), 1.9);
    float fringe = pow(saturate(1.0 - smoothstep(0.22, 1.0, distorted)), 2.3) * 0.45;

    //=== 对冲行波在中点相遇的驻波亮斑 ===
    float waveA = sin(ax * 16.0 - uTime * 7.0);
    float waveB = sin(ax * 16.0 + uTime * 7.0);
    float standing = saturate(waveA * waveB);
    float meetMask = exp(-pow((along - 0.5) * 4.2, 2.0));
    float interference = standing * meetMask * (1.0 - dist) * 0.9;

    //=== 沿线电离闪络 ===
    float flicker = frac(sin(dot(float2(floor(ax * 30.0), floor(uTime * 12.0)),
        float2(12.9898, 78.233))) * 43758.5453);
    flicker = step(0.91, flicker) * (1.0 - dist) * 1.4;

    //=== 边缘噪声撕裂，摆脱干净矩形 ===
    float bite = tex2D(uImage2, float2(ax * 5.5 - uTime * 1.9, uv.y * 3.1)).g;
    float edgeMask = smoothstep(0.95, 0.45, distorted + bite * 0.30 * (1.0 - core));

    //=== 合成 ===
    float cut = 1.0 + uCutFlash * 0.9;
    float intensity = core * 1.2 + body * 0.7 + fringe + interference + flicker * 0.45;
    intensity *= edgeMask * taper;   //端点强度也随收口归零，双保险
    intensity *= uIntensity * uOpacity * cut;

    //=== 双色映射：A端红→B端绿，白热只留芯部/驻波/闪络 ===
    float3 col = lerp(uColorA, uColorB, smoothstep(0.30, 0.70, along));
    col = lerp(col, float3(1.0, 1.0, 1.0),
        saturate(core * 0.35 + interference * 0.8 + flicker * 0.6));
    col += float3(1.0, 1.0, 1.0) * pow(saturate(1.0 - distorted), 8.0) * 0.5;

    col *= input.Color.rgb;
    return float4(col * intensity, saturate(intensity) * input.Color.a);
}

technique Technique1
{
    pass TetherPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
