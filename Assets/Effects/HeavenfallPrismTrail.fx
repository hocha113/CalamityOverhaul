// =====================================================================
// HeavenfallPrismTrail.fx 棱镜星河拖尾/光环着色器
// 服务于天堂陨落长弓家族 (InfiniteArrow / ParadiseArrow / HeavenfallLongbowHeldProj / VientianePunishment)
// 双 Technique:
//   Trail —— 带形拖尾, UV.x 沿拖尾走向 (0=头部 1=尾端), UV.y 横截
//   Aura  —— 中心辐射的圆形光环, UV 以 (0.5,0.5) 为中心
// 风格: 白热核心 + 程序化彩虹 + 棱镜色散 (RGB 三相位) + 流动极光丝带 + 星尘 + 渐隐
// =====================================================================

float4x4 transformMatrix;

float uTime;          // 时间, 用于流光
float fadeAlpha;      // 整体透明度 0~1
float coreIntensity;  // 0~1 核心强度, 控制白热程度
float dispersion;     // 棱镜色散强度 0.0~0.2 推荐
float flowSpeed;      // 彩虹色带滚动速度
float hueOffset;      // 整体色相偏移 (每个弹幕可错开)

texture uNoiseTex;
sampler2D noiseSamp = sampler_state
{
    texture   = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

struct VSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VS(VSInput v)
{
    PSInput o;
    o.Position  = mul(v.Position, transformMatrix);
    o.Color     = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

// 程序化彩虹 (hue 0~1 → RGB), 标准 HSV-style 转换
float3 hueToRGB(float h)
{
    h = frac(h);
    return saturate(float3(
        abs(h * 6.0 - 3.0) - 1.0,
        2.0 - abs(h * 6.0 - 2.0),
        2.0 - abs(h * 6.0 - 4.0)
    ));
}

// 棱镜色散: 在同一色相点采样 RGB 三相位, 模拟光的折射分光
float3 prismRainbow(float hue, float disp)
{
    float3 r = hueToRGB(hue - disp);
    float3 g = hueToRGB(hue);
    float3 b = hueToRGB(hue + disp);
    return float3(r.r, g.g, b.b);
}

// 简易 hash 用于星尘点阵
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// =====================================================================
// Trail Pass (拖尾)
// UV: x∈[0,1] 沿拖尾, 0=头部 1=尾端; y∈[0,1] 横截
// =====================================================================
float4 PS_Trail(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float cross_ = uv.y;
    float crossDist = abs(cross_ - 0.5) * 2.0; // 0=中心 1=边缘

    // 沿拖尾的自然收窄 (头部更鲜艳, 尾端逐渐淡)
    float lengthFade = 1.0 - smoothstep(0.65, 1.0, along);

    // ---- 噪声采样 ----
    float n1 = tex2D(noiseSamp, frac(float2(along * 3.5 + uTime * 0.8, cross_ * 1.4))).r;
    float n2 = tex2D(noiseSamp, frac(float2(along * 7.0 - uTime * 1.6, cross_ * 2.3 + 0.4))).g;
    float n3 = tex2D(noiseSamp, frac(float2(along * 1.8 + uTime * 0.4, cross_ * 4.0 + 0.7))).b;

    // ============================================================
    // A. 白热核心 (中心最亮)
    // ============================================================
    float coreW = 0.07 + n1 * 0.03;
    coreW *= lerp(1.0, 0.35, along);                // 头部更粗, 尾端收窄
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.2);
    float corePulse = 0.85 + 0.15 * sin(uTime * 15.0 + along * 30.0);
    core *= corePulse * (0.6 + coreIntensity * 0.8);

    // ============================================================
    // B. 彩虹本体 (程序化沿拖尾流动)
    // ============================================================
    // 沿拖尾的基础色相, 让七色随时间流过
    float baseHue = along * 1.4 - uTime * flowSpeed + hueOffset;
    // 加入横截轻微扭曲, 让彩虹看起来不是僵硬色带
    baseHue += (cross_ - 0.5) * 0.15 + n3 * 0.08;

    // 棱镜色散 RGB 三相位
    float3 rainbow = prismRainbow(baseHue, dispersion);

    // ============================================================
    // C. 极光丝带 (流动的横向亮带, 让拖尾有"丝绸"质感)
    // ============================================================
    float aurora1 = sin((along * 18.0 - uTime * 5.0) + n2 * 4.0) * 0.5 + 0.5;
    float aurora2 = sin((along * 11.0 + uTime * 3.2 + cross_ * 6.0) + n1 * 3.0) * 0.5 + 0.5;
    float aurora = pow(aurora1 * aurora2, 1.5);
    aurora *= (1.0 - crossDist * 0.7);
    aurora *= lengthFade;

    // ============================================================
    // D. 内层辉光 (彩虹本体在中段最强)
    // ============================================================
    float innerW = 0.30 + n2 * 0.08;
    innerW *= lerp(1.0, 0.55, along);
    float inner = 1.0 - smoothstep(coreW * 0.4, innerW, crossDist);
    inner *= (0.7 + aurora * 0.4);

    // ============================================================
    // E. 外层光晕 (柔和扩散到边缘)
    // ============================================================
    float outerFade = 1.0 - smoothstep(0.18, 0.98, crossDist);
    outerFade *= lerp(0.55, 0.12, along);

    // ============================================================
    // F. 星尘点阵 (沿拖尾闪烁的星点)
    // ============================================================
    float starID = floor(along * 80.0) + floor(cross_ * 12.0) * 41.0;
    float starHash = hash21(float2(starID, floor(uTime * 4.0)));
    float starPoint = step(0.94, starHash);
    // 让星点呈柔和小光斑, 不是死板像素
    float starGlow = starPoint * (0.5 + 0.5 * sin(uTime * 20.0 + starID * 0.13));
    starGlow *= (1.0 - crossDist * 0.5);

    // ============================================================
    // G. 头部光球 (拖尾起点位置的额外亮度)
    // ============================================================
    float headOrb = 1.0 - smoothstep(0.0, 0.10, along);
    headOrb *= (1.0 - crossDist * 0.7);
    float orbPulse = 0.8 + 0.2 * sin(uTime * 10.0);
    headOrb *= orbPulse;

    // ============================================================
    // H. 边缘有机溶解
    // ============================================================
    float edgeNoise = n2 * 0.18 + n3 * 0.12;
    float edgeMask = 1.0 - smoothstep(0.45 - edgeNoise, 0.95, crossDist);

    // ============================================================
    // I. 尾端渐隐
    // ============================================================
    float tailFade = 1.0 - smoothstep(0.55, 1.0, along);
    tailFade = saturate(tailFade + (1.0 - along) * 0.2);

    // ============================================================
    // 颜色合成
    // ============================================================
    float3 cWhite = float3(1.0, 0.98, 0.94);
    float3 color = float3(0, 0, 0);
    color += cWhite  * core;                       // 核心白热
    color += rainbow * inner * 1.05;               // 彩虹本体
    color += rainbow * outerFade * 0.85;           // 外层彩虹光晕
    color += rainbow * aurora * 0.7;               // 极光丝带
    color += cWhite  * starGlow * 1.4;             // 星尘
    color += cWhite  * headOrb * 0.9;              // 头部光球
    color += rainbow * headOrb * 0.4;              // 头部彩虹边

    float alpha = saturate(
          edgeMask * 0.9
        + core      * 0.6
        + aurora    * 0.25
        + headOrb   * 0.5
        + starGlow  * 0.3
    );
    alpha *= tailFade * fadeAlpha;

    return float4(color * alpha, alpha) * input.Color;
}

// =====================================================================
// Aura Pass (圆形辐射光环)
// UV: (0.5,0.5) 为中心, 半径 0~0.5
// =====================================================================
float4 PS_Aura(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 toC = uv - float2(0.5, 0.5);
    float dist = length(toC) * 2.0;            // 0=中心 1=边缘
    float ang = atan2(toC.y, toC.x);           // -PI..PI

    if (dist > 1.0)
        return 0;

    // 极坐标 UV 用于噪声 (让纹理沿光环旋转)
    float2 polarUV = float2(ang / 6.2831853 + uTime * 0.04, dist * 1.5);

    float n1 = tex2D(noiseSamp, frac(polarUV * 2.0 + float2(uTime * 0.05, 0))).r;
    float n2 = tex2D(noiseSamp, frac(polarUV * 4.0 + float2(0, uTime * 0.08))).g;

    // ---- 核心球 (中心白热) ----
    float core = exp(-dist * dist * 22.0);
    core *= 0.7 + coreIntensity * 0.6;

    // ---- 主光环 (中段彩虹辉光) ----
    float ringR = 0.42 + n2 * 0.05;
    float ringW = 0.20;
    float ring = exp(-pow((dist - ringR) / ringW, 2.0));

    // ---- 外缘扩散柔晕 ----
    float halo = (1.0 - smoothstep(0.4, 1.0, dist)) * 0.6;

    // ---- 程序化彩虹 (沿角度旋转的色相 + 时间脉动) ----
    float hue = ang / 6.2831853 + uTime * flowSpeed * 0.3 + hueOffset;
    hue += dist * 0.4;                          // 由内向外色相渐变
    float3 rainbow = prismRainbow(hue, dispersion);

    // ---- 旋转射线 (像太阳光的角向尖刺) ----
    float rays = sin(ang * 8.0 + uTime * 1.4) * 0.5 + 0.5;
    rays = pow(rays, 4.0) * (1.0 - dist);
    rays *= (n1 * 0.6 + 0.6);
    rays *= coreIntensity;

    // ---- 极坐标极光波纹 ----
    float aurora = sin(dist * 30.0 - uTime * 6.0 + n1 * 6.0) * 0.5 + 0.5;
    aurora = pow(aurora, 3.0) * (1.0 - dist) * 0.5;

    float3 cWhite = float3(1.0, 0.98, 0.94);
    float3 color = float3(0, 0, 0);
    color += cWhite  * core * 1.4;
    color += rainbow * ring * (1.0 + aurora);
    color += rainbow * halo * 0.55;
    color += cWhite  * rays * 0.7;
    color += rainbow * rays * 0.6;

    float alpha = saturate(core * 0.8 + ring * 0.7 + halo * 0.4 + rays * 0.3);
    // 边缘强制 0, 避免方块感
    alpha *= (1.0 - smoothstep(0.85, 1.0, dist));
    alpha *= fadeAlpha;

    return float4(color * alpha, alpha) * input.Color;
}

// =====================================================================
// Techniques
// =====================================================================
technique Trail
{
    pass P0
    {
        VertexShader = compile vs_2_0 VS();
        PixelShader  = compile ps_3_0 PS_Trail();
    }
}

// Aura 技术: 无自定义 VS, 走 SpriteBatch 默认顶点着色器, 配合 spriteBatch.Begin(..., effect, matrix) + sb.Draw(softGlow, ...) 使用
technique Aura
{
    pass P0
    {
        PixelShader = compile ps_3_0 PS_Aura();
    }
}
