// ============================================================================
// TwinsDeathRayBeam.fx 双子死亡射线
// UV.x 0枪口→1末端；s0+s1+s2 Additive 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);

//主题色:核心向外渐变的两级颜色
float3 uColor;          //内层主题色
float3 uSecondaryColor; //外缘主题色
float uTime;
float uOpacity;
float uIntensity;       //总强度
float uPulseSpeed;      //能量脉冲速度
float uFlameMode;       //0=电浆死光(锐利) 1=烈焰射流(汹涌)
float uExpandProgress;  //展开进度0~1，控制枪口聚拢与边缘撕裂量

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float noise(float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

//双子死亡射线像素着色器
//uv.x = 沿射线方向(0枪口 1末端), uv.y = 横向(0.5中心)
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float dist = abs(uv.y - 0.5) * 2.0;
    float along = uv.x;

    //=== 流动噪声场 ===
    //快速向末端流动的细噪声(电浆纤维感)
    float2 flowUV1 = float2(along * 5.0 - uTime * 3.2, uv.y * 2.2);
    float n1 = tex2D(uImage1, flowUV1).r;
    //中频反向流动，制造干涉
    float2 flowUV2 = float2(along * 2.6 - uTime * 1.7, uv.y * 1.1 + uTime * 0.25);
    float n2 = tex2D(uImage2, flowUV2).r;
    //低频缓慢翻涌(火焰模式权重更大)
    float2 flowUV3 = float2(along * 1.2 - uTime * 0.8, uv.y * 0.6 - uTime * 0.12);
    float n3 = tex2D(uImage2, flowUV3).g;

    float turbAmount = lerp(0.10, 0.30, uFlameMode);
    float turbulence = (n1 * 0.45 + n2 * 0.35 + n3 * 0.20 - 0.5) * turbAmount;

    //扰动后的横向距离场:火焰模式边缘翻滚更猛
    float distorted = dist + turbulence * (0.6 + dist * 1.6);

    //=== 分层光柱 ===
    //白热核心:电浆模式更锐
    float coreWidth = lerp(0.16, 0.24, uFlameMode);
    float core = 1.0 - smoothstep(0.0, coreWidth, distorted);
    core = pow(core, lerp(1.6, 1.2, uFlameMode));

    //主体层
    float body = 1.0 - smoothstep(0.0, 0.62, distorted);
    body = pow(body, 1.9);

    //外缘弥散
    float fringe = 1.0 - smoothstep(0.25, 1.0, distorted);
    fringe = pow(fringe, 2.4) * 0.5;

    //=== 能量行进脉冲 ===
    float pulse = sin(along * 14.0 - uTime * uPulseSpeed) * 0.5 + 0.5;
    pulse = pulse * 0.22 + 0.78;

    //=== 电离闪络(沿线随机亮斑) ===
    float flicker = noise(float2(floor(along * 46.0) , floor(uTime * 13.0)));
    flicker = step(0.9, flicker) * (1.0 - dist) * 1.6;

    //=== 端点处理 ===
    //枪口聚拢:起始8%由细变宽，跟随展开进度
    float muzzle = smoothstep(0.0, 0.06 + 0.05 * (1.0 - uExpandProgress), along);
    //枪口高亮
    float muzzleGlow = (1.0 - smoothstep(0.0, 0.12, along)) * 1.4;
    //末端衰减
    float tipFade = 1.0 - smoothstep(0.86, 1.0, along);

    //=== 边缘噪声撕裂 ===
    //用噪声咬掉光柱边缘，摆脱"干净矩形"的廉价感
    float bite = tex2D(uImage1, float2(along * 7.5 - uTime * 2.4, uv.y * 3.4)).g;
    float edgeMask = smoothstep(0.95, 0.45, distorted + bite * 0.32 * (1.0 - core));

    //=== 强度合成 ===
    float intensity = 0.0;
    intensity += core * 1.25 * pulse;
    intensity += body * 0.75 * pulse;
    intensity += fringe;
    intensity += flicker * 0.5;
    intensity *= muzzle * tipFade * edgeMask;
    intensity += muzzleGlow * core * 0.8;
    intensity *= uIntensity * uOpacity;

    //=== 色带映射 ===
    float3 col;
    if (distorted < coreWidth)
    {
        //白热核心→内层主题色
        col = lerp(float3(1.0, 1.0, 1.0), uColor, distorted / coreWidth * 0.55);
    }
    else
    {
        //内层→外缘
        float t = saturate((distorted - coreWidth) / 0.7);
        col = lerp(uColor, uSecondaryColor, t);
    }

    //闪络点亮成白色
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(flicker * 0.7));
    //核心溢光
    col += float3(1.0, 1.0, 1.0) * pow(saturate(1.0 - distorted), 9.0) * 0.8;

    col *= input.Color.rgb;
    return float4(col * intensity, saturate(intensity) * input.Color.a);
}

technique Technique1
{
    pass DeathRayPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
