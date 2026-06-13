// ============================================================================
// WitchBrimstoneDomain.fx 硫火女巫留影鬼域
// 采样 s0 + s1 噪声；Immediate AlphaBlend 全屏单 DrawCall
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
float fadeAlpha;        //整体透明度
float expandProgress;   //法阵展开 0~1
float dissolveProgress; //消散进度 0~1
float pulseIntensity;   //脉冲强度

float3 coreColor;       //核心色
float3 midColor;        //中层色
float3 edgeColor;       //边缘色
float3 voidColor;       //虚空底色

#define PI 3.14159265
#define TAU 6.28318530

// 散列与噪声
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

// A 硫火等离子底色
float3 brimstonePlasma(float2 centered, float dist, float angle, float time)
{
    float normAngle = (angle + PI) / TAU;

    float2 fireUV1 = float2(normAngle * 3.0 + time * 0.35, dist * 2.5 - time * 0.2);
    float2 warp1 = tex2D(noiseSamp, frac(fireUV1)).rg * 0.4;
    float fire1 = tex2D(noiseSamp, frac(float2(
        normAngle * 5.0 + warp1.x + time * 0.25,
        dist * 3.0 + warp1.y - time * 0.18
    ))).r;

    float2 fireUV2 = float2(normAngle * 7.0 - time * 0.28, dist * 4.0 + time * 0.15);
    float2 warp2 = tex2D(noiseSamp, frac(fireUV2 + 0.37)).gb * 0.35;
    float fire2 = tex2D(noiseSamp, frac(float2(
        normAngle * 4.0 - warp2.x - time * 0.2,
        dist * 2.0 + warp2.y + time * 0.22
    ))).g;

    float2 magmaUV = float2(centered.x * 0.8 + time * 0.06, centered.y * 0.8 - time * 0.04);
    float magma = tex2D(noiseSamp, frac(magmaUV)).r;
    magma = smoothstep(0.3, 0.7, magma);

    float fireMix = max(fire1 * 0.7, fire2 * 0.6);
    fireMix = pow(abs(fireMix), 1.5) * 1.8;

    float centerIntensity = smoothstep(0.9, 0.0, dist);
    fireMix *= 0.3 + centerIntensity * 0.7;

    float3 fireColor = lerp(edgeColor, coreColor, fireMix);
    fireColor = lerp(fireColor, midColor, magma * 0.4);
    fireColor *= 0.7;

    return fireColor * fireMix;
}

// B 魔法阵环
float magicCircleRing(float dist, float angle, float ringRadius, float time,
    float rotSpeed, float runeCount, float runeSize)
{
    float normAngle = (angle + PI) / TAU;

    float ringDist = abs(dist - ringRadius);
    float ringLine = 1.0 - smoothstep(0.0, 0.008, ringDist);
    float glow = exp(-ringDist * ringDist * 3000.0);

    float runeAngle = frac(normAngle * runeCount + time * rotSpeed);
    float runeSlot = frac(runeAngle);

    float runePattern = 0.0;
    float2 runeUV = float2((runeSlot - 0.5) * 2.0, (dist - ringRadius) / runeSize);
    float diamond = abs(runeUV.x) + abs(runeUV.y);
    runePattern += (1.0 - smoothstep(0.5, 0.55, diamond)) * step(diamond, 0.8);

    float innerLine = (abs(runeUV.x) < 0.02 || abs(runeUV.y) < 0.02) ? 0.5 : 0.0;
    runePattern += innerLine * step(diamond, 0.5);

    float runePulse = 0.7 + 0.3 * sin(time * 3.0 + floor(normAngle * runeCount) * 1.7);
    runePattern *= runePulse;

    float runeZone = 1.0 - smoothstep(0.0, runeSize, abs(dist - ringRadius));
    runePattern *= runeZone;

    return ringLine + glow * 0.6 + runePattern * 0.5;
}

// C 星形/多边形几何
float starGeometry(float2 centered, float dist, float angle, float radius,
    int points, float rotation, float thickness)
{
    float result = 0.0;
    for (int i = 0; i < points; i++)
    {
        float a1 = rotation + TAU * (float)i / (float)points;
        int skip = points == 5 ? 2 : (points == 6 ? 2 : 1);
        float a2 = rotation + TAU * (float)((i + skip) % points) / (float)points;

        float2 p1 = float2(cos(a1), sin(a1)) * radius;
        float2 p2 = float2(cos(a2), sin(a2)) * radius;

        float2 pa = centered - p1;
        float2 ba = p2 - p1;
        float h = saturate(dot(pa, ba) / dot(ba, ba));
        float lineDist = length(pa - ba * h);

        float lineGlow = exp(-lineDist * lineDist / (thickness * thickness * 0.5));
        float lineSharp = 1.0 - smoothstep(0.0, thickness * 0.5, lineDist);

        result += lineSharp * 0.6 + lineGlow * 0.3;
    }
    for (int j = 0; j < points; j++)
    {
        float a = rotation + TAU * (float)j / (float)points;
        float2 vertex = float2(cos(a), sin(a)) * radius;
        float vDist = length(centered - vertex);
        result += exp(-vDist * vDist * 800.0) * 0.5;
    }
    return saturate(result);
}

// D 符文光环带
float runeArcBand(float dist, float angle, float bandRadius, float bandWidth,
    float time, float rotSpeed, float segCount)
{
    float normAngle = (angle + PI) / TAU;
    float rotAngle = frac(normAngle + time * rotSpeed);

    float bandMask = 1.0 - smoothstep(0.0, bandWidth, abs(dist - bandRadius));
    if (bandMask < 0.001)
        return 0.0;

    float segAngle = frac(rotAngle * segCount);
    float segID = floor(rotAngle * segCount);
    float h = hash11(segID * 7.13 + bandRadius * 3.7);

    float2 runeUV = float2((segAngle - 0.5) * 2.0, (dist - bandRadius) / bandWidth);
    float pattern = 0.0;
    float type = frac(h * 7.0);

    if (type < 0.2)
    {
        pattern = (abs(runeUV.x) < 0.06 ? 1.0 : 0.0) + (abs(runeUV.y) < 0.06 ? 1.0 : 0.0);
        pattern = saturate(pattern);
    }
    else if (type < 0.4)
    {
        float r = length(runeUV) * 1.5;
        pattern = 1.0 - smoothstep(0.3, 0.35, abs(r - 0.5));
    }
    else if (type < 0.6)
    {
        float tri = abs(runeUV.x) + runeUV.y * 0.5 + 0.25;
        pattern = 1.0 - smoothstep(0.3, 0.35, tri);
        pattern *= step(-0.4, runeUV.y);
    }
    else if (type < 0.8)
    {
        float lines = sin(runeUV.x * 25.0);
        pattern = step(0.7, lines) * step(abs(runeUV.y), 0.35);
    }
    else
    {
        float d1 = abs(runeUV.x) + abs(runeUV.y);
        float d2 = abs(runeUV.x * 0.7) + abs(runeUV.y * 1.3);
        pattern = (1.0 - smoothstep(0.3, 0.35, d1)) + (1.0 - smoothstep(0.2, 0.25, d2)) * 0.5;
        pattern = saturate(pattern);
    }

    float gap = smoothstep(0.0, 0.08, segAngle) * smoothstep(1.0, 0.92, segAngle);
    pattern *= gap;

    float flicker = 0.6 + 0.4 * sin(time * 2.5 + segID * 2.3 + bandRadius * 5.0);
    return pattern * bandMask * flicker;
}

// E 女巫签印（逆五芒星 + 外圈）
//缓慢旋转的倒置五芒星作为女巫个人签印，替代darkPulseWave的节律冲击
float witchSigil(float2 centered, float time, float expand)
{
    //签印整体旋转速度慢
    float rot = time * 0.18 + PI;
    //签印半径：最内环外、中环内
    float sigilR = 0.3 * expand;
    float dist = length(centered);

    float circleDist = abs(dist - sigilR);
    float ringLine = 1.0 - smoothstep(0.0, 0.004, circleDist);
    float ringGlow = exp(-circleDist * circleDist * 5000.0);

    //五芒星连线（跨2个顶点形成星形）
    float star = 0.0;
    for (int i = 0; i < 5; i++)
    {
        float a1 = rot + TAU * (float)i / 5.0;
        float a2 = rot + TAU * (float)((i + 2) % 5) / 5.0;
        float2 p1 = float2(cos(a1), sin(a1)) * sigilR;
        float2 p2 = float2(cos(a2), sin(a2)) * sigilR;
        float2 pa = centered - p1;
        float2 ba = p2 - p1;
        float h = saturate(dot(pa, ba) / dot(ba, ba));
        float ld = length(pa - ba * h);
        float lineSharp = 1.0 - smoothstep(0.0, 0.004, ld);
        float lineGlow = exp(-ld * ld * 9000.0);
        star += lineSharp * 0.7 + lineGlow * 0.3;
    }

    //顶点辉光
    float verts = 0.0;
    for (int j = 0; j < 5; j++)
    {
        float a = rot + TAU * (float)j / 5.0;
        float2 v = float2(cos(a), sin(a)) * sigilR;
        float vd = length(centered - v);
        verts += exp(-vd * vd * 1200.0) * 0.6;
    }

    //整体呼吸
    float breath = 0.7 + 0.3 * sin(time * 0.9);
    return saturate((ringLine * 0.7 + ringGlow * 0.4 + star + verts) * breath);
}

// F 硫火余烬
float risingEmbers(float2 centered, float time)
{
    float embers = 0.0;
    float count = 28.0;
    for (int i = 0; i < 30; i++)
    {
        if ((float)i >= count)
            break;

        float id = (float)i;
        float h1 = hash11(id * 1.731);
        float h2 = hash11(id * 3.147);
        float h3 = hash11(id * 5.891);

        float emAngle = h1 * TAU;
        float emDist = 0.15 + h2 * 0.65;
        float speed = 0.3 + h3 * 0.5;
        float life = frac(time * speed + h1);

        float2 emPos = float2(cos(emAngle), sin(emAngle)) * emDist;
        emPos.y -= life * 0.3;
        emPos.x += sin(life * PI * 2.0 + h2 * TAU) * 0.05;

        float emR = length(centered - emPos);
        float emScale = sin(life * PI) * (0.008 + h3 * 0.005);
        float em = exp(-emR * emR / (emScale * emScale));
        em *= smoothstep(0.85, 0.2, length(emPos));
        embers += em;
    }
    return saturate(embers);
}

// 主像素着色
float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + PI) / TAU;

    float time = uTime;
    float expand = saturate(expandProgress);
    float dissolve = saturate(dissolveProgress);

    float edgeFade = 1.0 - smoothstep(0.82, 1.0, dist);
    if (edgeFade <= 0.001)
        return float4(0, 0, 0, 0);

    float expandMask = 1.0 - smoothstep(expand, expand + 0.06, dist);

    //A 底色
    float3 plasma = brimstonePlasma(centered, dist, angle, time);
    float depthGrad = smoothstep(0.0, 0.85, dist);
    float3 baseColor = lerp(voidColor * 0.5, edgeColor * 0.3, depthGrad);

    //B 魔法阵环
    float ringTotal = 0.0;
    ringTotal += magicCircleRing(dist, angle, 0.18 * expand, time, 0.12, 8.0, 0.04);
    ringTotal += magicCircleRing(dist, angle, 0.42 * expand, time, -0.08, 14.0, 0.034) * 0.8;
    ringTotal += magicCircleRing(dist, angle, 0.66 * expand, time, 0.06, 20.0, 0.028) * 0.65;
    ringTotal = saturate(ringTotal);

    //C 几何
    float geomTotal = 0.0;
    geomTotal += starGeometry(centered, dist, angle, 0.22 * expand, 5, time * 0.5, 0.006);
    geomTotal += starGeometry(centered, dist, angle, 0.5 * expand, 6, -time * 0.35, 0.005) * 0.85;
    geomTotal += starGeometry(centered, dist, angle, 0.72 * expand, 8, time * 0.15, 0.004) * 0.6;
    geomTotal = saturate(geomTotal);

    //D 符文光环带
    float runeTotal = 0.0;
    runeTotal += runeArcBand(dist, angle, 0.3 * expand, 0.035, time, 0.15, 12.0) * 0.6;
    runeTotal += runeArcBand(dist, angle, 0.56 * expand, 0.03, time, -0.1, 18.0) * 0.5;
    runeTotal = saturate(runeTotal);

    //E 女巫签印
    float sigil = witchSigil(centered, time, expand);

    //F 余烬
    float embers = risingEmbers(centered, time);

    //G 核心凝视漩涡 + 竖瞳
    float vortexDist = smoothstep(0.18, 0.0, dist);
    float vortexSwirl = sin(angle * 3.0 + time * 2.5 + dist * 18.0) * 0.5 + 0.5;
    float2 vortexUV = float2(normAngle * 2.0 + time * 0.5, dist * 5.0);
    float vortexNoise = tex2D(noiseSamp, frac(vortexUV)).r;
    float vortex = vortexDist * (0.45 + vortexSwirl * 0.3 + vortexNoise * 0.25);

    //竖瞳
    float slit = exp(-pow(centered.x / 0.012, 2.0)) * exp(-pow(centered.y / 0.11, 2.0));
    slit *= smoothstep(0.25, 0.0, dist);

    //H 环间电弧
    float arcEffect = 0.0;
    {
        float2 arcUV = float2(normAngle * 20.0 + time * 3.0, dist * 8.0);
        float arcNoise = tex2D(noiseSamp, frac(arcUV)).r;
        float arcLine = pow(arcNoise, 8.0) * 3.0;

        float arcZone = 0.0;
        arcZone += smoothstep(0.0, 0.03, abs(dist - 0.26 * expand))
                 * (1.0 - smoothstep(0.0, 0.03, abs(dist - 0.38 * expand)));
        arcZone += smoothstep(0.0, 0.03, abs(dist - 0.44 * expand))
                 * (1.0 - smoothstep(0.0, 0.03, abs(dist - 0.56 * expand)));
        arcEffect = arcLine * saturate(arcZone) * 0.35;

        float arcFlicker = step(0.85, hash21(float2(floor(normAngle * 30.0), floor(time * 12.0))));
        arcEffect *= 0.3 + arcFlicker * 0.7;
    }

    //I 外层暗能量
    float2 darkFlowUV = float2(normAngle * 3.0 + time * 0.18, dist * 2.0 - time * 0.12);
    float darkFlow = tex2D(noiseSamp, frac(darkFlowUV)).r;
    float2 darkFlowUV2 = float2(normAngle * 5.0 - time * 0.14, dist * 3.0 + time * 0.1);
    float darkFlow2 = tex2D(noiseSamp, frac(darkFlowUV2)).g;
    float outerDark = (darkFlow + darkFlow2) * 0.5;
    outerDark = smoothstep(0.3, 0.7, outerDark) * 0.2;
    outerDark *= smoothstep(0.4, 0.85, dist);

    //合成
    float3 finalColor = baseColor;
    finalColor += plasma * 0.6;
    finalColor += voidColor * outerDark;

    float3 ringColor = lerp(midColor, coreColor, ringTotal * 0.6);
    finalColor += ringColor * ringTotal * (0.7 + pulseIntensity * 0.3);

    float3 geomColor = lerp(coreColor, float3(1.0, 0.85, 0.6), geomTotal * 0.3);
    finalColor += geomColor * geomTotal * 0.6;

    float3 runeColor = lerp(midColor, coreColor, 0.5);
    finalColor += runeColor * runeTotal * 0.5;

    //签印偏血红，略暗于普通几何，体现其"旧签印"的阴郁气质
    float3 sigilColor = lerp(midColor, coreColor, 0.6);
    finalColor += sigilColor * sigil * (0.55 + pulseIntensity * 0.25);

    float3 emberColor = lerp(coreColor, float3(1.0, 0.9, 0.5), 0.4);
    finalColor += emberColor * embers * 0.9;

    float3 vortexColor = lerp(voidColor, coreColor, vortex);
    finalColor += vortexColor * vortex * 1.4;

    //竖瞳用最亮的硫火白橙
    finalColor += float3(1.0, 0.85, 0.5) * slit * 1.6;

    float3 arcColor = lerp(coreColor, float3(1.0, 0.95, 0.8), 0.5);
    finalColor += arcColor * arcEffect;

    //消散阶段整体去饱和向黑
    if (dissolve > 0.01)
    {
        float charFade = smoothstep(0.0, 1.0, dissolve);
        finalColor = lerp(finalColor, voidColor * 0.3, charFade * 0.8);
    }

    //透明度合成
    float alpha = 0.0;
    float fillAlpha = lerp(0.08, 0.25, smoothstep(0.8, 0.0, dist));
    fillAlpha += length(plasma) * 0.3;
    alpha += fillAlpha;
    alpha += ringTotal * 0.6;
    alpha += geomTotal * 0.5;
    alpha += runeTotal * 0.4;
    alpha += sigil * 0.55;
    alpha += embers * 0.7;
    alpha += vortex * 0.8;
    alpha += slit * 0.9;
    alpha += arcEffect * 0.6;
    alpha += outerDark * 0.3;

    alpha = saturate(alpha);
    alpha *= edgeFade * fadeAlpha * expand * expandMask;
    alpha *= 1.0 - dissolve * 0.75;

    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass WitchBrimstoneDomainPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
