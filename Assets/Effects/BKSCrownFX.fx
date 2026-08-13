// ============================================================================
//BKSCrownFX.fx 史莱姆王之冠攻击视觉（顶点 quad + 解析成形，双 technique）
//GuideTech：天坠瞄准指引柱——中央呼吸金线 + 下行锁定箭标 + 向落点收拢的侧轨 +
//  落点结穴亮斑；uLock=1(天坠)轨并拢、芯变宽提亮。uv.y 0=王冠端 1=地面端
//HaloTech：大招指挥光环——软核衰减 + 双脉动金环 + 噪声破碎的旋转金丝弧 +
//  六道细辐条；alpha 全部解析生成并在 r=0.98 前归零，杜绝贴图多边形 alpha 边界
//全程笛卡尔坐标（辐条用线距，无 atan2）→ 极角审计合规
//Additive 叠加，输出预乘色
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;     //秒
float uSeed;     //实例随机相位
float uOpacity;  //整体不透明度
float uProg;     //Guide：瞄准蓄势进度 0..1 / Halo：登场展开进度 0..1
float uLock;     //Guide：0=瞄准 1=天坠锁定

// 采样器合同（硬规则）：显式 register，C# 侧 pass.Apply 前必须
// Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

#define PI 3.14159265

//皇冠金调色（与 KingSlimeGelFX.CrownGold 系一致）
static const float3 Gold = float3(1.00, 0.82, 0.35);
static const float3 Amber = float3(0.95, 0.58, 0.18);
static const float3 HotWhite = float3(1.30, 1.22, 1.00);

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

//==== 瞄准指引柱 ====
float4 PSGuide(PSInput input) : COLOR0
{
    float x = (input.TexCoords.x - 0.5) * 2.0; //-1..1 横向
    float v = input.TexCoords.y;               //0=王冠端 1=地面端

    //两端顺滑收口：冠端渐出、地端在结穴后收
    float capFade = smoothstep(0.0, 0.10, v) * smoothstep(1.0, 0.985, v);

    //噪声微光：柱内竖向流动的碎金屑
    float shimmer = tex2D(noiseSamp, float2(x * 0.6 + uSeed, v * 2.6 - uTime * 0.9)).r;

    //---- 中央金芯：呼吸宽度，锁定时变宽提亮 ----
    float breath = 0.85 + 0.15 * sin(uTime * 9.0 + uSeed * 6.28);
    float coreW = lerp(0.055, 0.16, uLock) * breath;
    float core = exp(-pow(x / coreW, 2.0));

    //---- 下行锁定箭标：V 形随时间向落点滚动（瞄准意图可读） ----
    float chevSpeed = lerp(1.6, 4.5, uLock);
    float chevPhase = frac(v * 9.0 - uTime * chevSpeed + uSeed);
    //abs(x) 前移相位 → 中间尖两翼拖后，读作向下的箭头
    float chev = smoothstep(0.16, 0.02, abs(chevPhase - 0.5 + abs(x) * 0.22));
    chev *= smoothstep(0.55, 0.10, abs(x));
    chev *= lerp(smoothstep(0.0, 0.35, uProg), 1.0, uLock);

    //---- 收拢侧轨：|x| 随 v 向落点收窄的两条细线（蓄势越满收得越拢） ----
    float railR = 0.85 - 0.58 * v * saturate(uProg + uLock);
    float rail = exp(-pow((abs(x) - railR) / 0.05, 2.0));
    rail *= smoothstep(0.05, 0.30, v) * saturate(uProg * 1.4) * (1.0 - uLock * 0.55);

    //---- 落点结穴：地面端亮斑膨缩 ----
    float node = exp(-pow((v - 0.965) / 0.05, 2.0)) * exp(-pow(x / (0.36 + uLock * 0.2), 2.0));
    node *= 0.7 + 0.3 * sin(uTime * 12.0 + uSeed * 3.0);

    //---- 合成 ----
    float baseA = lerp(0.35, 0.85, uLock);
    float alpha = saturate(core * baseA + chev * 0.5 + rail * 0.55 + node * 0.9);
    alpha *= capFade * uOpacity * (0.8 + shimmer * 0.2);
    //横缘保险：结穴宽斑在 x=±1 残留 ~3% alpha 会切出竖直边线，解析归零
    alpha *= 1.0 - smoothstep(0.90, 1.0, abs(x));
    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    float3 col = Gold * core * (1.0 + uLock * 0.5)
        + HotWhite * core * core * (0.35 + uLock * 0.6)
        + Amber * chev * 0.9
        + Gold * rail * 0.8
        + HotWhite * node * 0.8
        + Gold * node * 0.6;

    return float4(col * alpha, alpha);
}

//==== 大招指挥光环 ====
float4 PSHalo(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0; //-1..1
    float r = length(p);
    //解析外界：r>0.98 必为零，无任何贴图边界可暴露
    if (r > 0.98)
        return float4(0, 0, 0, 0);

    float grow = saturate(uProg);            //登场展开
    float pulse = 0.5 + 0.5 * sin(uTime * 5.5 + uSeed * 6.28);

    //---- 软核衬光：紧贴王冠的小半径金晕（衬光职责收归着色器） ----
    float coreGlow = exp(-r * 7.5) * (0.55 + 0.15 * pulse);

    //---- 内环：细金环，脉动半径 ----
    float r1 = (0.34 + 0.025 * sin(uTime * 3.2 + uSeed)) * grow;
    float ring1 = exp(-pow((r - r1) / 0.030, 2.0));

    //---- 外环：更淡更宽，反相呼吸 ----
    float r2 = (0.56 - 0.030 * sin(uTime * 2.1 + uSeed)) * grow;
    float ring2 = exp(-pow((r - r2) / 0.055, 2.0)) * 0.55;

    //---- 旋转金丝弧：外环带上被噪声掰碎的弧段，绕心缓转（旋转用坐标系旋转，无极角） ----
    float ca = cos(uTime * 0.55);
    float sa = sin(uTime * 0.55);
    float2 pr = float2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);
    float fil = tex2D(noiseSamp, pr * 0.85 + uSeed).r;
    float filament = exp(-pow((r - r2) / 0.10, 2.0)) * smoothstep(0.48, 0.72, fil) * grow;

    //---- 六道细辐条：三条过心直线的线距，缓转 ----
    float ray = 0.0;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float a = uTime * 0.22 + uSeed + i * (PI / 3.0);
        float2 dirv = float2(cos(a), sin(a));
        float dperp = abs(p.x * (-dirv.y) + p.y * dirv.x);
        ray = max(ray, exp(-pow(dperp / 0.022, 2.0)));
    }
    ray *= smoothstep(0.10, 0.30, r) * (1.0 - smoothstep(0.50, 0.90, r)) * grow * (0.5 + 0.3 * pulse);

    //---- 合成：全部解析项，向外整体羽化归零 ----
    float outerFade = 1.0 - smoothstep(0.70, 0.96, r);
    float alpha = saturate(coreGlow + ring1 * 0.9 + ring2 + filament * 0.75 + ray * 0.6);
    alpha *= outerFade * uOpacity;
    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    float3 col = Gold * coreGlow * 1.2
        + HotWhite * ring1 * 0.55 + Gold * ring1 * 0.9
        + Amber * ring2 * 0.9
        + Gold * filament * 1.0
        + HotWhite * ray * 0.45 + Gold * ray * 0.5;

    return float4(col * alpha, alpha);
}

technique GuideTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSGuide();
    }
}

technique HaloTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSHalo();
    }
}
