sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float3 uColor;          //主题色
float3 uSecondaryColor; //辅助色(高光)
float uTime;
float uProgress;        //蓄力进度0~1:涡旋收紧、亮度提升
float uIntensity;       //总强度
float uOpacity;

static const float PI = 3.14159265;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

//双子蓄力能量汇聚涡:极坐标螺旋臂向心卷吸
//绘制在以眼睛为中心的方形面片上
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 p = input.TexCoords * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0)
    {
        return float4(0, 0, 0, 0);
    }
    float theta = atan2(p.y, p.x);

    //=== 螺旋臂 ===
    //角向+径向耦合采样:臂随时间向内卷吸，进度越高卷得越紧
    float twist = lerp(2.2, 4.6, uProgress);
    float spin = uTime * (1.4 + uProgress * 1.8);
    float armCoord = theta / (2.0 * PI) * 5.0 + r * twist + spin;
    float arm = tex2D(uImage1, float2(armCoord * 0.2, r * 0.5 - uTime * 0.6)).r;
    arm = pow(saturate(arm * 1.35 - 0.25), 2.0);

    //=== 向心吸入流线 ===
    //细密射向中心的须状流线，随进度变密变亮
    float streakNoise = tex2D(uImage1, float2(theta / (2.0 * PI) * 3.0 + 0.37, r * 1.7 - uTime * 1.9)).g;
    float streak = pow(saturate(streakNoise * 1.5 - 0.4), 3.0);

    //=== 径向包络 ===
    //外缘淡入、向心增强;进度提高时整体半径收缩
    float shrink = lerp(1.0, 0.55, uProgress);
    float rr = saturate(r / shrink);
    float envelope = smoothstep(1.0, 0.55, rr) * smoothstep(0.0, 0.18, rr);
    //中心核辉光(蓄力末期成型)
    float corePow = lerp(6.0, 2.6, uProgress);
    float coreGlow = pow(saturate(1.0 - r * 2.4), corePow) * uProgress * 1.6;

    //=== 收缩呼吸环 ===
    //一圈从外向内坍缩的亮环，周期随进度加快
    float ringPhase = frac(uTime * (0.5 + uProgress * 0.9));
    float ringR = (1.0 - ringPhase) * shrink;
    float ring = exp(-pow((r - ringR) * 16.0, 2.0)) * 0.8 * uProgress;

    //=== 合成 ===
    float intensity = 0.0;
    intensity += arm * envelope * (0.5 + uProgress * 0.8);
    intensity += streak * envelope * (0.35 + uProgress * 0.65);
    intensity += ring;
    intensity += coreGlow;
    intensity *= uIntensity * uOpacity;

    float3 col = lerp(uColor, uSecondaryColor, saturate(arm * 0.6 + coreGlow));
    col += float3(1.0, 1.0, 1.0) * coreGlow * 0.5;
    col *= input.Color.rgb;

    return float4(col * intensity, saturate(intensity) * input.Color.a);
}

technique Technique1
{
    pass ChargeVortexPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
