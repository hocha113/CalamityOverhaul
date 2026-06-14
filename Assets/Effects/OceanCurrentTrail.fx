// ============================================================================
//OceanCurrentTrail.fx 海洋洪流拖尾
//Trail 条带；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;        //整体淡出 0~1（用于飞溅/消散平滑过渡）
float pulse;            //0~1 呼吸脉动
float speedRatio;       //0~1 速度归一化（拉伸条纹强度）
float foamDensity;      //0~1 泡沫密度（越高越湍急）
float3 deepColor;       //深海色（中心深色）
float3 shallowColor;    //浅层色（边缘渐变）
float3 foamColor;       //泡沫高光色
float3 bioColor;        //生物荧光高光色

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

texture uFlowTex;
sampler flowSamp = sampler_state
{
    texture = <uFlowTex>;
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

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float along = input.TexCoords.x;            //0=头部, 1=尾端
    float cross_ = input.TexCoords.y;           //0..1 横向
    float crossDist = abs(cross_ - 0.5) * 2.0;  //0=中心, 1=边缘

    //=
    //A. 域扭曲（Domain Warp） ： 用 Flow 纹理偏移采样 UV
    //模拟流体漩涡卷动，让噪声纹理"流"起来
    //=
    float2 flowUV = frac(float2(along * 1.4 - uTime * 0.55, cross_ * 1.6 + uTime * 0.15));
    float2 flow = (tex2D(flowSamp, flowUV).rg - 0.5) * 0.22;
    flow.x += sin(along * 12.0 + uTime * 3.0) * 0.025;

    //=
    //B. 多频噪声组合（高低频水团结构）
    //=
    float n1 = tex2D(noiseSamp, frac(float2(along * 3.0 + flow.x - uTime * 0.95, cross_ * 1.5 + flow.y))).r;
    float n2 = tex2D(noiseSamp, frac(float2(along * 6.5 - uTime * 1.35 + flow.x * 0.5, cross_ * 2.5 + 0.31 + flow.y * 0.5))).g;
    float n3 = tex2D(noiseSamp, frac(float2(along * 13.0 + uTime * 1.8, cross_ * 4.2 + 0.12))).b;
    float waterField = n1 * 0.55 + n2 * 0.30 + n3 * 0.15;

    //=
    //C. 截面厚度 ： 噪声让边缘呈不规则水绺
    //=
    float thickness = 1.0 - crossDist;
    thickness *= 0.74 + waterField * 0.50;
    thickness = saturate(thickness);

    //=
    //D. 内核（亮白能量芯） ： 头部更宽，尾端收窄
    //=
    float coreWidth = 0.34 + n1 * 0.10;
    coreWidth *= lerp(1.20, 0.45, along);     //头宽尾窄
    float core = saturate(1.0 - smoothstep(0.0, coreWidth, crossDist));
    core = pow(core, 1.4);
    core *= 0.72 + 0.28 * pulse;

    //=
    //E. 焦散波光（Voronoi 风格 min(n2,n3) + 沿轴亮带）
    //=
    float caustic = min(n2, n3);
    caustic = pow(caustic, 0.4) * 1.3;
    float causticBand = sin(along * 24.0 - uTime * 8.5 + waterField * 7.0) * 0.5 + 0.5;
    caustic = saturate(caustic * (0.35 + causticBand * 0.65));
    caustic *= (1.0 - crossDist * 0.55);

    //=
    //F. 流动条纹（沿轴推进的水流条带，速度越快越明显）
    //=
    float stripe1 = frac(along * 8.0 - uTime * 3.0 + waterField * 0.5);
    float streamA = smoothstep(0.0, 0.10, stripe1) * smoothstep(0.42, 0.20, stripe1);
    float stripe2 = frac(along * 14.0 - uTime * 4.5 + n2 * 0.4);
    float streamB = smoothstep(0.0, 0.06, stripe2) * smoothstep(0.20, 0.10, stripe2);
    float streamStripes = (streamA + streamB * 0.5) * (1.0 - crossDist * 0.45) * speedRatio;

    //=
    //G. 边缘泡沫带 ： 噪声触发的高对比白色泡沫
    //比单纯的 alpha 衰减更具有"沸腾"感
    //=
    float foamCore = smoothstep(0.50, 0.92, crossDist + waterField * 0.20);
    foamCore *= smoothstep(1.05, 0.62, crossDist);
    float foamBubbles = step(0.55 - foamDensity * 0.15, n1 + n2 * 0.55) * foamCore;
    float foamMask = saturate(foamBubbles + foamCore * 0.32);
    //让泡沫沿水流推进（动态而非静止）
    float foamFlick = 0.65 + 0.35 * sin(uTime * 7.0 + n3 * 18.0 + along * 10.0);
    foamMask *= foamFlick;

    //=
    //H. 头部圆饼亮区 + 头部光环
    //=
    float headCap = 1.0 - smoothstep(0.0, 0.07, along);
    headCap *= 1.0 - crossDist * 0.55;
    float headRim = (1.0 - smoothstep(0.05, 0.18, along)) * (1.0 - crossDist * 0.7) * 0.65;

    //=
    //I. 尾端淡出
    //=
    float tailFade = 1.0 - smoothstep(0.72, 1.0, along);

    //颜色合成：海洋蓝主色，Additive 下禁 float3(1,1,1) 直叠
    float depth = 1.0 - crossDist;
    float3 bodyColor = lerp(shallowColor, deepColor, saturate(depth * 0.78 + waterField * 0.12));

    //极小的"水面反光"热斑色：偏冷的近白，仅用于 core 中心一个像素级别的小亮斑
    //不要直接用 (1,1,1)，否则在 Additive 下与 bio/foam 叠加后红通道也满，整片白
    float3 hotSpark = float3(0.74, 0.88, 1.0);

    //1. 身体主色：海洋蓝主导，权重显著提升
    float3 col = bodyColor * thickness * (0.95 + waterField * 0.55);

    //2. 内核：以生物荧光为主，叠加一道收得很紧的反光斑
    col += bioColor  * core * 0.78;
    col += hotSpark  * (core * core * core) * 0.22;     //core³ 把白光压缩到最中心

    //3. 焦散：青色为主，foam 仅做轻微"反光晶光"
    col += bioColor  * caustic * 0.40;
    col += foamColor * caustic * 0.22;

    //4. 流条：用 bio + 浅蓝混合，避免 foamColor 主导导致一道道白纹
    col += bioColor      * streamStripes * 0.55;
    col += shallowColor  * streamStripes * 0.30;

    //5. 边缘泡沫带：foamColor 已经被改为青调，权重降下来防止累计白化
    col += foamColor * foamMask * 0.75;

    //6. 头部端帽 / 光环：以 bio 为主，禁止纯白
    col += bioColor  * headCap * 0.80;
    col += foamColor * headCap * 0.35;
    col += hotSpark  * (headCap * headCap) * 0.18;      //headCap² 收紧，仅最中心一点有"反光"
    col += bioColor  * headRim * 1.05;

    //=
    //"蓝色保权"约束 ： 关键防白化手段
    //Additive 混合下，R/G 一旦先于 B 抵达 1.0，整体就会被显示为
    //带白调（甚至纯白）。即便中性色（如 hotSpark）单层下没问题，
    //多层叠加（trail+水头+泡沫粒子）也会迅速失衡
    //
    //本步骤在像素层面"硬性"压住 R 和 G，使其永远不会超过 B 的某个比例
    //- R 上限 = B * 0.45（保证蓝远高于红）
    //- G 上限 = B * 0.92（青色仍然保留，但仍小于蓝）
    //这样一来，即便 col 经 Additive 与亮背景累加，也会沿"海洋蓝→青蓝"
    //方向饱和，而不是沿"白"方向饱和
    //=
    col.r = min(col.r, col.b * 0.45);
    col.g = min(col.g, col.b * 0.92);

    float alpha = thickness * 0.85;
    alpha += core * 0.78;
    alpha += foamMask * 0.50;
    alpha += caustic * 0.16;
    alpha += headCap * 0.48;
    alpha = saturate(alpha) * tailFade * fadeAlpha;

    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass OceanCurrentTrailPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
