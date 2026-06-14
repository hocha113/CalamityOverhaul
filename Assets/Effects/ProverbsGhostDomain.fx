// ============================================================================
//ProverbsGhostDomain.fx 箴言鬼域
//采样 s0 + s1 噪声；紧凑硫磺火鬼域单 DrawCall
//ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

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

float uTime;
float fadeAlpha;        //整体透明度
float pulsePhase;       //冲击波相位（连续递增值，frac后用于周期脉冲）
float dualMode;         //0=普通 1=双重增幅模式

float3 coreColor;       //核心亮橙
float3 midColor;        //中层血红
float3 edgeColor;       //边缘暗紫红
float3 voidColor;       //虚空黑

#define PI 3.14159265
#define TAU 6.28318530

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p, int octaves)
{
    float v = 0.0;
    float a = 0.5;
    float f = 1.0;
    for (int i = 0; i < octaves; i++)
    {
        v += a * valueNoise(p * f);
        f *= 2.13;
        a *= 0.47;
        p += float2(1.7, 9.2);
    }
    return v;
}

//幽火涌动
//域扭曲的鬼火，在领域内部流转翻滚，呈现幽灵般的火焰形态
float3 ghostFire(float2 centered, float dist, float angle, float time)
{
    float normAngle = (angle + PI) / TAU;

    //第一层鬼火：极坐标慢旋+噪声扭曲
    float2 uv1 = float2(normAngle * 4.0 + time * 0.22, dist * 3.0 - time * 0.35);
    float2 warp1 = tex2D(noiseSamp, frac(uv1 * 0.8)).rg * 0.45;
    float ghost1 = tex2D(noiseSamp, frac(float2(
        normAngle * 3.0 + warp1.x + time * 0.18,
        dist * 2.5 + warp1.y - time * 0.25
    ))).r;

    //第二层鬼火：反向旋转，不同频率，制造湍流交错
    float2 uv2 = float2(normAngle * 6.0 - time * 0.16, dist * 4.5 + time * 0.2);
    float2 warp2 = tex2D(noiseSamp, frac(uv2 * 0.7 + 0.33)).gb * 0.35;
    float ghost2 = tex2D(noiseSamp, frac(float2(
        normAngle * 5.0 - warp2.x - time * 0.14,
        dist * 3.5 + warp2.y + time * 0.28
    ))).g;

    //深层暗焰：极慢流动，大尺度明暗
    float2 deepUV = float2(centered.x * 1.2 + time * 0.05, centered.y * 1.2 - time * 0.04);
    float deep = tex2D(noiseSamp, frac(deepUV * 0.6)).r;
    deep = smoothstep(0.25, 0.75, deep);

    //混合 - 取加权最大值，模拟火焰亮纹
    float fireMix = max(ghost1 * 0.65, ghost2 * 0.55);
    fireMix = pow(abs(fireMix), 1.3) * 1.6;

    //中心区域更强烈，边缘自然衰减
    float centerGrad = smoothstep(0.85, 0.0, dist);
    fireMix *= 0.25 + centerGrad * 0.75;

    //颜色：核心亮→中层暗红→边缘暗紫
    float3 fireCol = lerp(edgeColor, coreColor, fireMix);
    fireCol = lerp(fireCol, midColor, deep * 0.35);
    //幽灵感：加入微弱的冷色偏移
    fireCol += float3(-0.02, 0.02, 0.05) * (1.0 - fireMix) * 0.5;

    return fireCol * fireMix;
}

//边缘烈焰跳动
//在域边界产生向外跳动的尖锐火舌，形态不规则，有鬼域的狰狞感
float edgeFlames(float2 centered, float dist, float angle, float time)
{
    float normAngle = (angle + PI) / TAU;

    //多频火舌：不同频率的正弦叠加噪声，制造参差不齐的火舌
    float2 flameUV = float2(normAngle * 8.0 + time * 0.4, time * 0.25);
    float flameSample = tex2D(noiseSamp, frac(flameUV)).r;

    float2 flameUV2 = float2(normAngle * 14.0 - time * 0.3, time * 0.15 + 0.5);
    float flameSample2 = tex2D(noiseSamp, frac(flameUV2)).g;

    //火舌高度（向域外延伸的长度）
    float tongueHeight = flameSample * 0.6 + flameSample2 * 0.4;
    tongueHeight = pow(abs(tongueHeight), 0.7);

    //尖锐化：让火舌更加锋利
    float sharpTongue = pow(abs(tongueHeight), 1.8) * 0.18;

    //域边界位置（0.75是基础边界）
    float edgeBorder = 0.75;
    float flameZone = edgeBorder + sharpTongue;

    //只在边缘区域显示
    float innerFade = smoothstep(edgeBorder - 0.12, edgeBorder, dist);
    float outerFade = smoothstep(flameZone + 0.02, flameZone - 0.01, dist);

    float intensity = innerFade * outerFade;

    //火舌尖端更亮
    float tipBrightness = smoothstep(edgeBorder, flameZone, dist);
    intensity *= 0.6 + tipBrightness * 0.8;

    //高频闪烁
    float flicker = 0.7 + 0.3 * sin(time * 8.0 + normAngle * 20.0 + flameSample * 10.0);
    intensity *= flicker;

    return intensity;
}

//周期冲击波
//周期性从中心向外扩散的能量环，带噪声扰动
float shockwaveRing(float dist, float angle, float time, float phase)
{
    float normAngle = (angle + PI) / TAU;
    float waves = 0.0;

    //同时存在两个冲击波，错开半周期
    for (int i = 0; i < 2; i++)
    {
        float p = frac(phase + (float)i * 0.5);
        float radius = p * 0.82;
        float width = 0.025 + p * 0.03;

        //噪声扰动环形
        float2 noiseUV = float2(normAngle * 6.0 + (float)i * 3.7, time * 0.2 + (float)i);
        float noiseDisplace = (tex2D(noiseSamp, frac(noiseUV)).r - 0.5) * 0.04;

        float ringDist = abs(dist - radius + noiseDisplace);
        float ring = smoothstep(width, 0.0, ringDist);

        //衰减：扩散越远越淡
        float fade = 1.0 - p;
        fade = fade * fade;

        //锐利的能量感
        float sharpRing = exp(-ringDist * ringDist / (width * width * 0.3));

        waves += (ring * 0.6 + sharpRing * 0.4) * fade;
    }

    return saturate(waves);
}

//鬼域法阵纹
//紧凑的魔法阵环线和简化符文
float ghostCircle(float2 centered, float dist, float angle, float time)
{
    float normAngle = (angle + PI) / TAU;
    float result = 0.0;

    //内环
    float ring1Dist = abs(dist - 0.28);
    float ring1 = exp(-ring1Dist * ring1Dist * 4000.0);
    result += ring1 * 0.7;

    //外环
    float ring2Dist = abs(dist - 0.72);
    float ring2 = exp(-ring2Dist * ring2Dist * 3000.0);
    result += ring2 * 0.6;

    //中间环（双重模式额外）
    float midRing = abs(dist - 0.48);
    float midR = exp(-midRing * midRing * 3500.0);
    result += midR * 0.5 * dualMode;

    //五芒星线条（程序化，距离场）
    float starResult = 0.0;
    float starRadius = 0.35;
    float starRot = time * 0.3;
    for (int i = 0; i < 5; i++)
    {
        float a1 = starRot + TAU * (float)i / 5.0;
        float a2 = starRot + TAU * (float)((i + 2) % 5) / 5.0;

        float2 p1 = float2(cos(a1), sin(a1)) * starRadius;
        float2 p2 = float2(cos(a2), sin(a2)) * starRadius;

        float2 pa = centered - p1;
        float2 ba = p2 - p1;
        float h = saturate(dot(pa, ba) / dot(ba, ba));
        float ld = length(pa - ba * h);

        float lineGlow = exp(-ld * ld / 0.00004);
        starResult += lineGlow * 0.4;
    }
    result += starResult;

    //符文槽位：在外环上等距排列小符号
    float runeCount = 6.0 + dualMode * 4.0;
    float runeAngle = frac(normAngle * runeCount + time * 0.08);
    float runeSlot = frac(runeAngle);
    float2 runeUV = float2((runeSlot - 0.5) * 2.5, (dist - 0.72) / 0.06);
    float diamond = abs(runeUV.x) + abs(runeUV.y);
    float rune = (1.0 - smoothstep(0.5, 0.6, diamond)) * step(diamond, 0.9);
    float runeZone = 1.0 - smoothstep(0.0, 0.06, abs(dist - 0.72));
    float runePulse = 0.6 + 0.4 * sin(time * 2.5 + floor(normAngle * runeCount) * 2.1);
    result += rune * runeZone * runePulse * 0.5;

    return saturate(result);
}

//鬼魂余烬
//在域内缓缓上升的幽灵火星
float ghostEmbers(float2 centered, float time)
{
    float embers = 0.0;
    float count = 12.0 + dualMode * 8.0;

    for (int i = 0; i < 20; i++)
    {
        if ((float)i >= count)
            break;

        float id = (float)i;
        float h1 = hash11(id * 1.731);
        float h2 = hash11(id * 3.147);
        float h3 = hash11(id * 5.891);

        float emAngle = h1 * TAU;
        float emDist = 0.1 + h2 * 0.55;

        float speed = 0.25 + h3 * 0.4;
        float life = frac(time * speed + h1);

        float2 emPos = float2(cos(emAngle), sin(emAngle)) * emDist;
        emPos.y -= life * 0.25;
        emPos.x += sin(life * PI * 2.0 + h2 * TAU) * 0.04;

        float emR = length(centered - emPos);
        float emScale = sin(life * PI) * (0.006 + h3 * 0.004);
        float em = exp(-emR * emR / (emScale * emScale));

        em *= smoothstep(0.8, 0.15, length(emPos));
        embers += em;
    }

    return saturate(embers);
}

//中心虚空漩涡
//域中心的幽暗漩涡，散发鬼域气息
float voidVortex(float2 centered, float dist, float angle, float time)
{
    float normAngle = (angle + PI) / TAU;

    float vortexMask = smoothstep(0.2, 0.0, dist);
    float swirl = sin(angle * 4.0 + time * 3.5 + dist * 25.0) * 0.5 + 0.5;

    float2 vortexUV = float2(normAngle * 3.0 + time * 0.6, dist * 6.0);
    float vortexNoise = tex2D(noiseSamp, frac(vortexUV)).r;

    return vortexMask * (0.4 + swirl * 0.3 + vortexNoise * 0.3);
}

//主像素着色器

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float time = uTime;

    //======== 域边界裁剪（柔和过渡） ========
    float edgeFade = 1.0 - smoothstep(0.78, 1.0, dist);
    if (edgeFade <= 0.001)
        return float4(0, 0, 0, 0);

    //======== A. 内部幽火 ========
    float3 fire = ghostFire(centered, dist, angle, time);

    //鬼域底色：从深渊黑到暗紫红
    float depthGrad = smoothstep(0.0, 0.8, dist);
    float3 baseColor = lerp(voidColor * 0.6, edgeColor * 0.25, depthGrad);

    //======== B. 边缘烈焰 ========
    float flames = edgeFlames(centered, dist, angle, time);

    //======== C. 冲击波 ========
    float shock = shockwaveRing(dist, angle, time, pulsePhase);

    //======== D. 法阵纹 ========
    float circle = ghostCircle(centered, dist, angle, time);

    //======== E. 余烬 ========
    float embers = ghostEmbers(centered, time);

    //======== F. 中心漩涡 ========
    float vortex = voidVortex(centered, dist, angle, time);

    //=
    //颜色合成
    //=
    float3 finalColor = baseColor;

    //幽火底层
    finalColor += fire * 0.7;

    //边缘烈焰（从中层红到核心亮橙）
    float3 flameColor = lerp(midColor, coreColor, flames * 0.7);
    finalColor += flameColor * flames * 1.2;

    //冲击波（明亮的核心色+白热）
    float3 shockColor = lerp(coreColor, float3(1.0, 0.9, 0.7), shock * 0.4);
    finalColor += shockColor * shock * 0.9;

    //法阵纹（中偏亮色调）
    float3 circleColor = lerp(midColor, coreColor, circle * 0.5);
    finalColor += circleColor * circle * 0.65;

    //余烬（最亮的火星色）
    float3 emberColor = lerp(coreColor, float3(1.0, 0.85, 0.4), 0.5);
    finalColor += emberColor * embers * 0.8;

    //中心漩涡（深红→暗紫渐变）
    float3 vortexCol = lerp(voidColor * 1.5, midColor, vortex);
    finalColor += vortexCol * vortex * 1.2;

    //======== 透明度合成 ========
    float alpha = 0.0;

    //基础填充（领域内部的底色透明度）
    float fillAlpha = lerp(0.06, 0.2, smoothstep(0.85, 0.0, dist));
    fillAlpha += length(fire) * 0.25;
    alpha += fillAlpha;

    alpha += flames * 0.8;
    alpha += shock * 0.7;
    alpha += circle * 0.6;
    alpha += embers * 0.6;
    alpha += vortex * 0.5;

    alpha = saturate(alpha);
    alpha *= edgeFade * fadeAlpha;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass ProverbsGhostDomainPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
