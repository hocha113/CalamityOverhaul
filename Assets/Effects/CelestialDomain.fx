// ============================================================================
// CelestialDomain.fx 天国领域
// 采样 s0 + s1 噪声；Immediate AlphaBlend 全屏
// ps_3_0
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
float fadeAlpha;
float expandProgress;
float revelationIntensity;
float3 coreColor;    //白金核心
float3 haloColor;    //金色光环
float3 divineColor;  //天蓝神圣
float3 gloryColor;   //柔紫荣光

struct VSOutput {
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

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

// 神圣以太云雾
// 多层噪声纹理采样+UV扭曲，产生缓慢流动的天光云层
float3 divineAether(float2 centered, float dist, float angle, float time, float expand)
{
    float normAngle = (angle + PI) / TAU;

    //主云层：极坐标映射+噪声扭曲
    float2 cloudUV1 = float2(normAngle * 2.0 + time * 0.08, dist * 1.5 - time * 0.04);
    float2 warp1 = tex2D(noiseSamp, frac(cloudUV1 * 0.7 + 0.13)).rg * 0.3;
    float cloud1 = tex2D(noiseSamp, frac(float2(
        normAngle * 3.0 + warp1.x + time * 0.06,
        dist * 2.0 + warp1.y - time * 0.05
    ))).r;

    //第二云层：反向流动，更大尺度
    float2 cloudUV2 = float2(normAngle * 1.5 - time * 0.05, dist * 1.0 + time * 0.03);
    float2 warp2 = tex2D(noiseSamp, frac(cloudUV2 * 0.6 + 0.57)).gb * 0.25;
    float cloud2 = tex2D(noiseSamp, frac(float2(
        normAngle * 2.5 - warp2.x - time * 0.04,
        dist * 1.8 + warp2.y + time * 0.06
    ))).g;

    //深层光晕：极缓慢的大尺度亮斑
    float2 deepUV = float2(centered.x * 0.5 + time * 0.02, centered.y * 0.5 - time * 0.015);
    float deepGlow = tex2D(noiseSamp, frac(deepUV + 0.3)).r;
    deepGlow = smoothstep(0.3, 0.7, deepGlow);

    //混合云层
    float cloudMix = cloud1 * 0.6 + cloud2 * 0.4;
    cloudMix = smoothstep(0.2, 0.8, cloudMix);
    cloudMix = pow(abs(cloudMix), 0.8);

    //径向衰减：中心亮，边缘柔和消散
    float radialFade = smoothstep(0.5 * expand, 0.0, dist) * 0.6 + 0.4;
    cloudMix *= radialFade;

    //颜色渐变：从白金核心到金色天光，噪声驱动色彩变化
    float3 aetherColor = lerp(haloColor * 0.5, coreColor * 0.7, cloudMix);
    aetherColor = lerp(aetherColor, divineColor * 0.3, deepGlow * 0.3);
    aetherColor += coreColor * deepGlow * 0.15;

    return aetherColor * cloudMix * 0.5;
}

// 体积光芒
// 噪声调制的径向光线，高斯截面，多频叠加
float volumetricRays(float2 centered, float dist, float angle, float time, float expand)
{
    float normAngle = (angle + PI) / TAU;
    float rays = 0.0;

    //主光线层：12条，噪声调制宽度和亮度
    float rayPhase1 = normAngle * 12.0 + time * 0.12;
    float rayCell1 = frac(rayPhase1);
    //高斯截面代替硬切割
    float rayShape1 = exp(-pow(abs(rayCell1 - 0.5) * 4.0, 2.0));
    //噪声调制光线亮度
    float rayNoise1 = tex2D(noiseSamp, frac(float2(normAngle * 3.0, time * 0.1 + dist * 0.5))).r;
    rayShape1 *= (0.5 + rayNoise1 * 0.5);
    //径向渐变：中心强→渐弱→边缘微亮
    float rayRadial1 = exp(-dist * 4.0) * 0.7 + smoothstep(0.0, 0.25 * expand, dist) * 0.2;
    rays += rayShape1 * rayRadial1;

    //次级光线层：8条，更宽更柔，反向旋转
    float rayPhase2 = normAngle * 8.0 - time * 0.08;
    float rayCell2 = frac(rayPhase2);
    float rayShape2 = exp(-pow(abs(rayCell2 - 0.5) * 3.0, 2.0));
    float rayNoise2 = tex2D(noiseSamp, frac(float2(normAngle * 2.0 + 0.5, time * 0.07 + dist * 0.3))).g;
    rayShape2 *= (0.4 + rayNoise2 * 0.6);
    float rayRadial2 = exp(-dist * 3.0) * 0.4;
    rays += rayShape2 * rayRadial2 * 0.5;

    //微细光丝：高频细线
    float rayPhase3 = normAngle * 24.0 + time * 0.2;
    float rayCell3 = frac(rayPhase3);
    float rayShape3 = exp(-pow(abs(rayCell3 - 0.5) * 6.0, 2.0));
    rayShape3 *= smoothstep(0.35 * expand, 0.1 * expand, dist); //仅中心附近可见
    rays += rayShape3 * 0.15;

    return saturate(rays);
}

// 光之曼陀罗
// 高斯辉光线条构成的旋转圆形几何，取代硬边SDF
float sacredMandala(float2 centered, float dist, float angle, float time, float expand)
{
    float result = 0.0;

    //外层八角星：8条辐射线
    float starRadius = 0.3 * expand;
    float rot1 = time * 0.1;
    for (int i = 0; i < 8; i++)
    {
        float a = rot1 + TAU * (float)i / 8.0;
        float2 lineEnd = float2(cos(a), sin(a)) * starRadius;

        //点到线段的距离
        float2 pa = centered;
        float2 ba = lineEnd;
        float h = saturate(dot(pa, ba) / dot(ba, ba));
        float lineDist = length(pa - ba * h);

        //高斯辉光
        float lineGlow = exp(-lineDist * lineDist * 8000.0);
        result += lineGlow * 0.4;
    }

    //内层六角形连线
    float hexRadius = 0.18 * expand;
    float rot2 = -time * 0.15;
    for (int j = 0; j < 6; j++)
    {
        float a1 = rot2 + TAU * (float)j / 6.0;
        float a2 = rot2 + TAU * (float)((j + 1) % 6) / 6.0;
        float2 p1 = float2(cos(a1), sin(a1)) * hexRadius;
        float2 p2 = float2(cos(a2), sin(a2)) * hexRadius;

        float2 pa = centered - p1;
        float2 ba = p2 - p1;
        float h = saturate(dot(pa, ba) / dot(ba, ba));
        float lineDist = length(pa - ba * h);

        float lineGlow = exp(-lineDist * lineDist * 12000.0);
        result += lineGlow * 0.35;
    }

    //中心十字光柱：域扭曲的柔和十字
    float crossRot = time * 0.08;
    float ca = cos(crossRot);
    float sa = sin(crossRot);
    float2 rotP = float2(centered.x * ca - centered.y * sa, centered.x * sa + centered.y * ca);
    //宽高斯截面十字
    float crossX = exp(-rotP.y * rotP.y * 600.0) * exp(-abs(rotP.x) * 8.0);
    float crossY = exp(-rotP.x * rotP.x * 600.0) * exp(-abs(rotP.y) * 8.0);
    float crossLight = (crossX + crossY) * 0.35 * expand;
    result += crossLight;

    return saturate(result);
}

// 神圣光环
float sacredHalos(float dist, float angle, float time, float expand)
{
    float rings = 0.0;

    //三层光环，各自独立呼吸
    float radii[3] = { 0.12, 0.24, 0.38 };
    float widths[3] = { 0.006, 0.008, 0.005 };
    float speeds[3] = { 2.5, 3.2, 1.8 };

    for (int i = 0; i < 3; i++)
    {
        float ringR = radii[i] * expand;
        float ringW = widths[i];
        //呼吸脉冲
        float pulse = sin(time * speeds[i] + (float)i * 1.5) * 0.002;
        float ringDist = abs(dist - ringR + pulse);

        //高斯辉光轮廓
        float ringGlow = exp(-ringDist * ringDist / (ringW * ringW));
        //噪声调制环的亮度
        float normAngle = (angle + PI) / TAU;
        float ringNoise = tex2D(noiseSamp, frac(float2(normAngle * (3.0 + (float)i), time * 0.05 + (float)i * 0.33))).r;
        ringGlow *= (0.6 + ringNoise * 0.4);

        //内环更亮
        float brightness = 1.0 - (float)i * 0.2;
        rings += ringGlow * brightness;
    }

    return saturate(rings);
}

// 上升圣灵
float risingSpirits(float2 centered, float time, float expand)
{
    float spirits = 0.0;

    for (int i = 0; i < 20; i++)
    {
        float id = (float)i;
        float h1 = hash11(id * 1.731);
        float h2 = hash11(id * 3.147);
        float h3 = hash11(id * 5.891);

        //径向分布
        float spAngle = h1 * TAU;
        float spDist = 0.08 + h2 * 0.35;

        //上升动画
        float speed = 0.15 + h3 * 0.25;
        float life = frac(time * speed + h1);

        //位置
        float2 spPos = float2(cos(spAngle), sin(spAngle)) * spDist * expand;
        spPos.y -= life * 0.2; //上升
        spPos.x += sin(life * PI * 2.0 + h2 * TAU) * 0.03; //飘动

        float spR = length(centered - spPos);

        //大小随生命变化：出现-膨胀-消散
        float spScale = sin(life * PI) * (0.005 + h3 * 0.004);
        float sp = exp(-spR * spR / (spScale * spScale + 0.00001));

        //域内衰减
        sp *= smoothstep(0.5 * expand, 0.1 * expand, length(spPos));

        spirits += sp;
    }

    return saturate(spirits);
}

// 边缘圣辉
float edgeAurora(float dist, float angle, float time, float expand)
{
    float domainR = 0.42 * expand;

    //边缘高斯光晕
    float edgeDist = abs(dist - domainR);
    float edgeGlow = exp(-edgeDist * edgeDist * 800.0);

    //噪声调制边缘形状，产生波动感
    float normAngle = (angle + PI) / TAU;
    float edgeNoise = tex2D(noiseSamp, frac(float2(normAngle * 4.0 + time * 0.1, time * 0.03))).r;
    float edgeNoise2 = tex2D(noiseSamp, frac(float2(normAngle * 6.0 - time * 0.08, time * 0.05 + 0.5))).g;
    float edgeWarp = (edgeNoise * 0.6 + edgeNoise2 * 0.4 - 0.3) * 0.04;

    //扭曲后的边缘距离
    float warpedEdgeDist = abs(dist - domainR + edgeWarp);
    float warpedGlow = exp(-warpedEdgeDist * warpedEdgeDist * 400.0);

    //尖刺光芒（向外辐射的柔和光锥）
    float spikePhase = normAngle * 16.0 + time * 0.06;
    float spikeCell = frac(spikePhase);
    float spikeShape = exp(-pow(abs(spikeCell - 0.5) * 5.0, 2.0));
    float spikeRadial = smoothstep(domainR * 0.85, domainR, dist) * smoothstep(domainR + 0.06, domainR, dist);
    float spike = spikeShape * spikeRadial * 0.4;

    return saturate(warpedGlow * 0.7 + edgeGlow * 0.3 + spike);
}

// 七印圣光
float3 sevenSeals(float2 centered, float time, float expand)
{
    float3 sealLight = float3(0, 0, 0);

    for (int i = 0; i < 7; i++)
    {
        float sAngle = (float)i * TAU / 7.0 + time * 0.12;
        float sR = 0.3 * expand;
        float2 sPos = float2(cos(sAngle), sin(sAngle)) * sR;
        float sDist = length(centered - sPos);

        //呼吸脉冲
        float pulse = sin(time * 3.0 + (float)i * 1.3) * 0.3 + 0.7;

        //核心辉光
        float coreGlow = exp(-sDist * sDist * 3000.0) * pulse;
        //外层柔光
        float outerGlow = exp(-sDist * sDist * 500.0) * pulse * 0.3;

        //颜色微变
        float3 sColor = lerp(coreColor, haloColor, hash11((float)i * 5.37) * 0.3 + 0.2);
        sColor = lerp(sColor, divineColor, hash11((float)i * 3.77 + 5.0) * 0.2);

        sealLight += sColor * (coreGlow + outerGlow);
    }

    return sealLight;
}

// 主像素着色器
float4 PSCelestialDomain(VSOutput input) : COLOR0
{
    float2 uv = input.UV;
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float expand = expandProgress;

    //域外柔化裁剪
    float domainR = 0.42 * expand;
    float domainMask = smoothstep(domainR + 0.05, domainR - 0.03, dist);

    //核心辉光：中心的柔和白光
    float centerGlow = exp(-dist * dist * 60.0) * expand;

    // (A) 天国云雾
    float3 aether = divineAether(centered, dist, angle, uTime, expand);

    // (B) 体积光芒
    float rays = volumetricRays(centered, dist, angle, uTime, expand);
    float3 rayColor = lerp(coreColor, haloColor, dist * 2.0);

    // (C) 光之曼陀罗
    float mandala = sacredMandala(centered, dist, angle, uTime, expand);
    float3 mandalaColor = lerp(coreColor, haloColor, mandala * 0.5);

    // (D) 神圣光环
    float halos = sacredHalos(dist, angle, uTime, expand);
    float3 haloMix = lerp(haloColor, coreColor, halos * 0.3);

    // (E) 上升圣灵
    float spirits = risingSpirits(centered, uTime, expand);
    float3 spiritColor = lerp(haloColor * 0.8, coreColor, spirits * 0.5);

    // (F) 边缘圣辉
    float edge = edgeAurora(dist, angle, uTime, expand);
    float3 edgeColor = lerp(haloColor, gloryColor, 0.3);

    // (G) 七印
    float3 seals = sevenSeals(centered, uTime, expand);

    //合成所有层
    float3 finalColor = float3(0, 0, 0);

    //基底：核心白光
    finalColor += coreColor * centerGlow * 0.5;
    //云雾层
    finalColor += aether;
    //体积光芒
    finalColor += rayColor * rays * 0.8;
    //曼陀罗几何
    finalColor += mandalaColor * mandala * 0.7;
    //光环
    finalColor += haloMix * halos * 0.6;
    //圣灵粒子
    finalColor += spiritColor * spirits * 0.5;
    //边缘
    finalColor += edgeColor * edge * 0.5;
    //七印
    finalColor += seals * 0.6;

    //启示录强度渐增
    finalColor *= (0.7 + revelationIntensity * 0.5);

    //总alpha
    float totalAlpha = saturate(
        centerGlow * 0.4
        + rays * 0.5
        + mandala * 0.4
        + halos * 0.4
        + spirits * 0.3
        + edge * 0.4
        + length(aether) * 0.6
        + length(seals) * 0.5
    );

    totalAlpha *= fadeAlpha * domainMask;

    return float4(finalColor * totalAlpha, totalAlpha);
}

technique CelestialDomainPass {
    pass P0 {
        PixelShader = compile ps_3_0 PSCelestialDomain();
    }
}
