// ============================================================================
//BKSNinjaSlash.fx 史莱姆王体内忍者刀光（冷白/钢青，忍者尺度短促锐利）
//技法自 OniCrimsonSlash 降档移植：顶点 quad + 带内极坐标月牙/直线双模式，
//锋利侧剃刀线绝对光滑、拖尾暗侧有机破碎，扫掠揭开 1~3 帧砸满无减速尾巴。
//uMode=0 月牙带：phi=atan2 仅经 u=phi/span+0.5 做单调比较与 clamp 采样，
//  无 sin/cos/噪声直接消费原始极角，分支切线(±π)落在弧外 → 极角审计合规
//uMode=1 直线刀刃带：u 沿刃长，白热中脊、两缘渐暗（天袭直斩）
//uCore=1 白热薄芯层：只留锋线与高光，垫在主体上读作刀刃本体
//配色写死冷钢系（区别鬼切绯红墨系）：白热锋线→钢青主体→深青灰拖尾
//预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //秒
float uMode;      //0=弧形月牙 1=直线刀刃
float uSweep;     //0..1 扫掠揭开进度（1~3帧内砸满）
float uErode;     //0..1 生命期溶解（从暗侧向锋线推进）
float uFlash;     //0..1 张开瞬间全形白闪
float uOpacity;   //整体不透明度
float uFlip;      //+1/-1 挥动镜像
float uSeed;      //实例随机相位
float uArcSpan;   //弧总跨度(弧度，<2π)，仅弧模式
float uThick;     //带厚度(p 空间尺度)
float uFrontGlow; //扫掠前缘白热强度
float uCool;      //0..1 生命期降温（钢青→深青灰，冷钢余温散尽）
float uCore;      //0=主体层 1=白热薄芯层

// 采样器合同（硬规则）：显式 register，C# 侧 pass.Apply 前必须
// Textures[1]=SlashBrush01 Textures[2]=NoiseSoft01 + SamplerStates=LinearWrap
sampler brushSamp : register(s1);
sampler noiseSamp : register(s2);

#define PI 3.14159265

//冷钢调色（写死）：白热锋线 / 钢青亮部 / 深钢青 / 青灰描边
static const float3 ColHot = float3(1.55, 1.65, 1.80);
static const float3 ColSteel = float3(0.55, 0.72, 1.05);
static const float3 ColDeep = float3(0.16, 0.24, 0.42);
static const float3 ColDark = float3(0.045, 0.06, 0.11);

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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    p.y *= uFlip;

    float isArc = uMode < 0.5 ? 1.0 : 0.0;

    //---- 模式坐标 ----
    float r = length(p);
    float phi = atan2(p.y, p.x);
    float uRawArc = phi / uArcSpan + 0.5;
    float uRawLine = input.TexCoords.x;
    float uRaw = lerp(uRawLine, uRawArc, isArc);
    float uc = saturate(uRaw);

    //厚度包络：弧峰值偏收笔端(力点前压)，直线近中央，两端收尖
    float envPow = lerp(1.30, 1.75, isArc);
    float env = sin(pow(uc, envPow) * PI);
    float w = uThick * pow(max(env, 0.0), 0.70);
    if (w < 0.004)
        return float4(0, 0, 0, 0);

    //参差噪声：只骚扰暗侧与消散期，锋线活跃期绝对光滑
    float jag1 = tex2D(noiseSamp, float2(uc * 3.1 + uSeed, 0.23 + uSeed * 0.7)).r - 0.5;
    float jag2 = tex2D(noiseSamp, float2(uc * 7.0 - uTime * 0.12 + uSeed, 0.67)).r - 0.5;
    float jag = jag1 * 0.05 + jag2 * 0.028;

    //---- v/h 坐标：v 径向位置，h 热度(0=锋利侧 1=暗侧) ----
    float v;
    float h;
    if (uMode < 0.5)
    {
        float outerR = 0.90 + jag * uErode * 1.4;
        float innerR = outerR - w;
        v = (r - innerR) / w;      //0=内(拖尾暗侧) 1=外(剃刀线)
        if (v < -0.20 || v > 1.10)
            return float4(0, 0, 0, 0);
        h = 1.0 - saturate(v);
    }
    else
    {
        v = abs(p.y) / w + jag * (0.5 + uErode * 1.6);
        if (v > 1.20)
            return float4(0, 0, 0, 0);
        h = saturate(v);
    }
    float vc = saturate(v);

    //---- 笔刷拉丝：沿刃细密行笔感（忍者刀快，滚动快） ----
    float flow = uTime * 0.55 + uSeed;
    float4 b1 = tex2D(brushSamp, float2(uc * 1.6 - flow * 0.3, vc));
    float4 b2 = tex2D(brushSamp, float2(uc * 3.8 + flow * 0.5 + 0.37, vc * 0.6 + 0.2));
    float streak = b1.r * b1.a * 0.8 + b2.r * b2.a * 0.5;

    //---- 扫掠揭开 + 前缘白热 ----
    float edge = uSweep * 1.10 - 0.04;
    float reveal = smoothstep(edge + 0.010, edge - 0.045, uRaw);
    float front = exp(-pow((uRaw - edge) / 0.045, 2.0)) * uFrontGlow;

    //---- 溶解：暗侧先死锋线最后死，短命干脆 ----
    float eN = tex2D(noiseSamp, float2(uc * 2.6 + uSeed * 3.1, vc * 0.9 + uSeed)).r * 0.65
             + tex2D(noiseSamp, float2(uc * 5.6 - uTime * 0.2, vc * 1.7 + 0.4)).r * 0.35;
    float eTh = uErode * 1.16 - (1.0 - h) * 0.28;
    float survive = smoothstep(eTh - 0.02, eTh + 0.10, eN);
    float burn = smoothstep(eTh - 0.13, eTh - 0.02, eN) * (1.0 - survive);

    //---- alpha 合成：锋利侧光滑紧致，暗侧笔刷撕裂 ----
    float aSharp;
    float aDark;
    if (uMode < 0.5)
    {
        aSharp = smoothstep(1.06, 0.96, v);
        aDark = smoothstep(-0.06, 0.20, v + jag * (0.8 + uErode * 1.8) + (0.5 - streak) * 0.38);
    }
    else
    {
        aSharp = smoothstep(1.06, 0.88, v);
        aDark = 1.0;
    }
    float tipFeather = smoothstep(0.0, 0.045, uc) * smoothstep(1.0, 0.955, uc);

    float alpha = aSharp * aDark * tipFeather * reveal * survive;

    //薄芯层收得更紧：只留贴锋线的窄带
    if (uCore > 0.5)
        alpha *= smoothstep(0.55, 0.15, h);

    //透密调制：笔刷透密让主体读作一束速度而非实心色块，白闪帧抬下限
    alpha *= lerp(saturate(0.45 + streak * 0.9), 1.0, max(uFlash * 0.5, uCore));
    alpha = saturate(alpha) * uOpacity;

    //---- 色带（沿 h：锋线→钢青高光→深钢→青灰拖尾），随生命降温 ----
    float3 col = lerp(ColHot, ColSteel, smoothstep(0.02, 0.24, h));
    col = lerp(col, ColDeep, smoothstep(0.28, 0.62, h));
    col = lerp(col, ColDark, smoothstep(0.60, 1.0, h));
    col = lerp(col, lerp(ColDeep, ColDark, h), uCool * 0.75);

    //剃刀细线：贴 h=0 一条冷白高光，活跃期恒亮
    float razor = exp(-pow(h / 0.07, 2.0));
    col += ColHot * razor * (uCore > 0.5 ? 1.5 : 1.05) * (1.0 - uCool * 0.55);

    //笔刷高光 + 燃边(冷钢烧蓝) + 前缘 + 白闪增益
    col += ColSteel * streak * (1.0 - h * 0.5) * 0.45;
    col += float3(0.5, 0.75, 1.35) * burn * 1.8;
    col += ColHot * front * 2.4;
    col = lerp(col, col + ColHot * 0.5, saturate(uFlash));

    //锋线/前缘/白闪 alpha 外增益 → 半加法辉光
    float glowA = saturate(alpha + (front * 0.5 + burn * 0.2 + razor * 0.15 + uFlash * 0.14)
        * uOpacity * reveal * tipFeather * survive);
    return float4(col * alpha + ColHot * front * 0.3 * uOpacity * tipFeather * reveal, glowA);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
