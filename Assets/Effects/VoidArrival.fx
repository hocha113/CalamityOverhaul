// ============================================================================
//VoidArrival.fx 虚空抵达门全屏后处理
//径向爆发隧道+冲击波环；s0 场景 s1 噪声
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float intensity;//整体强度
float expandProgress;//展开进度 0~1
float ejectBurst;//抛出瞬间闪光 0~1
float portalRadius;//门的基准半径(世界像素)
float seed;

//三路冲击波各自独立相位与触发时间
float shockTime0;
float shockTime1;
float shockTime2;

float2 portalCenter;
float2 screenPosition;
float2 worldViewSize;

static const float PI = 3.14159265;
static const float TAU = 6.28318530;

//工具

float hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float sampleNoise(float2 uv)
{
    float3 n = tex2D(noiseTex, frac(uv)).rgb;
    return n.r * 0.5 + n.g * 0.35 + n.b * 0.15;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);

    if (intensity < 0.001 && ejectBurst < 0.001)
        return original;

    //世界坐标系重建
    float2 worldPos = screenPosition + worldViewSize * coords;
    float2 rel = worldPos - portalCenter;
    float dist = length(rel);
    float angle = atan2(rel.y, rel.x);
    float2 outDir = rel / max(dist, 0.1);

    float expT = saturate(expandProgress);
    float radius = portalRadius * pow(expT, 0.6);//主门半径
    float normDist = saturate(dist / max(radius, 1.0));

    //=== 1. 径向UV扭曲（向心吸入+切向涡流） ===
    //吸入强度：远处弱，近门口强，核心反而平缓
    float suckEnvelope = exp(-pow(normDist - 0.95, 2.0) * 6.0);
    float2 inward = -outDir * 0.004 * intensity * suckEnvelope;

    //切向涡流：环绕门旋转
    float2 tangent = float2(-outDir.y, outDir.x);
    float swirlStr = 0.003 * intensity * expT * exp(-pow(normDist - 1.0, 2.0) * 4.0);
    swirlStr *= sin(uTime * 1.8 - dist * 0.004) * 0.5 + 0.7;
    float2 swirl = tangent * swirlStr;

    //噪声扰动
    float2 nUV = float2(angle * 0.5 / PI + uTime * 0.05, dist * 0.003 - uTime * 0.04);
    float2 noiseWarp = (tex2D(noiseTex, frac(nUV)).rg - 0.5) * 0.004 * intensity * expT;

    float2 warpOffset = (inward + swirl + noiseWarp) * worldViewSize / max(worldViewSize.x, 1.0);
    float2 warpedCoords = clamp(coords + warpOffset, 0.002, 0.998);
    original = tex2D(uImage0, warpedCoords);

    //=== 2. 极端色差（仅靠近门边缘时强烈） ===
    float caBand = exp(-pow(normDist - 1.0, 2.0) * 10.0);
    float caPx = caBand * 8.0 * intensity;
    float2 caOff = outDir * caPx / worldViewSize;
    original.r = tex2D(uImage0, warpedCoords + caOff * 1.2).r;
    original.g = tex2D(uImage0, warpedCoords + caOff * 0.2).g;
    original.b = tex2D(uImage0, warpedCoords - caOff * 1.0).b;

    //=== 3. 背景压暗 + 暗红映射 ===
    float dimRange = radius * 3.2;
    float dimFactor = saturate(1.0 - dist / max(dimRange, 1.0));
    dimFactor = dimFactor * dimFactor;
    float dimPulse = 0.82 + 0.18 * sin(uTime * 2.2);
    float actualDim = lerp(1.0, lerp(0.5, 0.08, dimFactor), intensity * 0.9 * dimPulse);
    original.rgb *= actualDim;

    float lum = dot(original.rgb, float3(0.299, 0.587, 0.114));
    float desatAmt = 0.5 * intensity * dimFactor;
    original.rgb = lerp(original.rgb, lum.xxx, desatAmt);

    float3 shadowVoid = float3(0.02, 0.002, 0.006);
    float3 midCrim = float3(0.38, 0.02, 0.04);
    float3 ember = float3(0.72, 0.14, 0.05);
    float3 redMap;
    if (lum < 0.25)
        redMap = lerp(shadowVoid, midCrim, lum / 0.25);
    else
        redMap = lerp(midCrim, ember, saturate((lum - 0.25) / 0.75));
    float redTint = 0.40 * intensity * dimFactor;
    original.rgb = lerp(original.rgb, redMap * (lum * 0.55 + 0.45), redTint);

    //=== 4. 核心圆盘隧道（深度井） ===
    //当距离小于门半径：呈现虚空内部
    float insideFactor = saturate((radius - dist) / max(radius * 0.35, 1.0));
    insideFactor = pow(insideFactor, 1.4);

    //径向深度：越靠中心，深度越大，隧道消失点
    float r01 = saturate(dist / max(radius, 1.0));
    float depth = pow(1.0 - r01, 1.6);

    //极坐标扭曲采样，形成旋转隧道
    float tunnelAng = angle + uTime * 0.55 - dist * 0.0035;
    float tunnelU = tunnelAng * 0.5 / PI;
    float tunnelV = log(max(dist, 8.0)) * 0.35 - uTime * 0.35;
    float tn1 = tex2D(noiseTex, frac(float2(tunnelU * 3.0, tunnelV))).r;
    float tn2 = tex2D(noiseTex, frac(float2(tunnelU * 7.0 + seed * 0.2, tunnelV * 1.7 + seed * 0.3))).g;
    float tunnelVol = tn1 * 0.6 + tn2 * 0.4;

    //反向回旋层
    float tunnelAng2 = angle - uTime * 0.85 + dist * 0.0022;
    float tn3 = tex2D(noiseTex, frac(float2(tunnelAng2 * 0.6 / PI, tunnelV * 0.7 + seed))).b;
    tunnelVol = tunnelVol * 0.7 + tn3 * 0.3;

    //深处星光
    float starsUV = sampleNoise(rel * 0.018 + float2(uTime * 0.02, -uTime * 0.015));
    float stars = smoothstep(0.93, 0.99, starsUV) * (0.4 + depth);

    //深处红色裂痕泄露
    float deepCrack = pow(saturate(tn2 * tn3 * 1.5), 4.5);

    float3 tunnelBase = float3(0.018, 0.004, 0.008) * tunnelVol * (0.35 + depth);
    float3 tunnelGlow = float3(0.6, 0.08, 0.03) * deepCrack * (0.3 + depth);
    float3 starCol = float3(1.0, 0.5, 0.25) * stars;
    float3 tunnelColor = tunnelBase + tunnelGlow + starCol;

    //隧道中心暗井
    float wellDarkness = pow(1.0 - r01, 2.2);
    tunnelColor = lerp(tunnelColor, float3(0.0, 0.0, 0.0), wellDarkness * 0.75);

    original.rgb = lerp(original.rgb, tunnelColor, insideFactor * expT);

    //=== 5. 门环能量边缘 ===
    //以dist 到 radius 的差为指标
    float edgeDist = abs(dist - radius);
    float innerRing = exp(-edgeDist / max(radius * 0.03, 1.0));
    float midRing = exp(-edgeDist / max(radius * 0.12, 1.0));
    float outerRing = exp(-edgeDist / max(radius * 0.35, 1.0));

    //环上高频闪烁
    float ringFlick = tex2D(noiseTex, frac(float2(angle * 2.0 / PI + uTime * 0.8, dist * 0.01))).r;
    ringFlick = 0.55 + 0.45 * ringFlick;
    float ringFlick2 = tex2D(noiseTex, frac(float2(angle * 0.8 / PI - uTime * 0.25, uTime * 0.05))).g;

    float3 hotCore = float3(1.2, 0.72, 0.32) * innerRing * ringFlick * 2.0;
    float3 crimRing = float3(0.95, 0.1, 0.04) * midRing * (0.6 + 0.4 * ringFlick2);
    float3 deepRing = float3(0.32, 0.015, 0.008) * outerRing * 0.6;

    //门呼吸
    float breathe = 0.88 + 0.12 * sin(uTime * 1.1);
    float3 ringEnergy = (hotCore + crimRing + deepRing) * expT * breathe * intensity;

    //=== 6. 放射状能量尖刺（从门向外喷射） ===
    float spikeAng = angle * (6.0 + 4.0 * (0.5 + 0.5 * sin(uTime * 0.4 + seed)));
    float spikeN = tex2D(noiseTex, frac(float2(spikeAng * 0.12 + seed, uTime * 0.3))).r;
    spikeN = pow(spikeN, 3.0);
    float spikeRange = smoothstep(radius * 2.2, radius * 0.95, dist) *
                      smoothstep(radius * 0.8, radius * 1.0, dist);
    float spikeBand = exp(-pow((dist - radius * 1.25) / max(radius * 0.25, 1.0), 2.0));
    float spikeStr = spikeN * spikeBand * spikeRange * intensity * expT;
    float3 spikeColor = float3(1.1, 0.35, 0.12) * spikeStr * 1.8;

    //=== 7. 多层同心冲击波 ===
    float3 shockAll = float3(0, 0, 0);
    float shockPush = 0.0;

    //#0: 开启冲击波 (白热)
    if (shockTime0 >= 0.0 && shockTime0 < 2.5)
    {
        float st = shockTime0;
        float r0 = radius * (0.4 + st * 2.8);
        float wN = tex2D(noiseTex, frac(float2(angle * 0.5 / PI + seed, st * 0.1))).r;
        float rDist = dist - (r0 + (wN - 0.5) * radius * 0.35);
        float frontW = 30.0 + 90.0 * saturate(st * 0.7);
        float front = saturate(1.0 - abs(rDist) / frontW);
        front = pow(front, 1.8);
        float fade = exp(-st * 1.6);
        float3 c = float3(1.6, 1.0, 0.6) * front * fade;
        shockAll += c * 2.0;
        shockPush += front * exp(-st * 5.0) * 0.015;
    }
    //#1: 第二冲击波 (橙红, 错相)
    if (shockTime1 >= 0.0 && shockTime1 < 2.8)
    {
        float st = shockTime1;
        float r0 = radius * (0.3 + st * 2.3);
        float wN = tex2D(noiseTex, frac(float2(angle * 1.2 / PI - seed * 0.7, st * 0.07))).g;
        float rDist = dist - (r0 + (wN - 0.5) * radius * 0.5);
        float frontW = 40.0 + 110.0 * saturate(st * 0.6);
        float front = saturate(1.0 - abs(rDist) / frontW);
        front = pow(front, 1.5);
        float fade = exp(-st * 1.2);
        float3 c = float3(1.3, 0.38, 0.12) * front * fade;
        shockAll += c * 1.4;
    }
    //#2: 抛出瞬间冲击波 (深红, 最猛)
    if (shockTime2 >= 0.0 && shockTime2 < 2.0)
    {
        float st = shockTime2;
        float r0 = radius * (0.2 + st * 3.4);
        float wN1 = tex2D(noiseTex, frac(float2(angle * 2.0 / PI + seed * 1.5, st * 0.12))).b;
        float wN2 = tex2D(noiseTex, frac(float2(angle * 6.0 / PI, st * 0.06))).r;
        float rDist = dist - (r0 + (wN1 - 0.5) * radius * 0.65 + (wN2 - 0.5) * radius * 0.28);
        float frontW = 20.0 + 130.0 * saturate(st * 0.9);
        float front = saturate(1.0 - abs(rDist) / frontW);
        front = pow(front, 2.0);
        float biteN = tex2D(noiseTex, frac(float2(angle * 20.0 / PI + seed, st * 0.2))).b;
        float bite = step(0.45, biteN);
        front *= 0.4 + 0.8 * bite;
        float fade = exp(-st * 1.3) + max(0, 1.0 - st * 0.55) * 0.3;
        float3 c = float3(1.5, 0.45, 0.15) * front * fade;
        shockAll += c * 2.2;
        shockPush += front * exp(-st * 4.0) * 0.02;
    }

    if (shockPush > 0.0001)
    {
        float2 pushOff = outDir * shockPush * worldViewSize / max(worldViewSize.x, 1.0);
        float2 pushedCoords = clamp(coords + pushOff, 0.002, 0.998);
        float4 pushed = tex2D(uImage0, pushedCoords);
        original.rgb = lerp(original.rgb, pushed.rgb, saturate(shockPush * 45.0));
    }

    //=== 8. 抛出白热闪光 ===
    //瞬时以 portalCenter 为源的径向白热球体 + 全屏淡白
    float3 ejectColor = float3(0, 0, 0);
    if (ejectBurst > 0.001)
    {
        float eb = saturate(ejectBurst);
        //中心球体
        float sphereR = radius * (0.4 + 1.8 * (1.0 - eb));//随 eb 衰减而扩大
        float sphere = exp(-pow(dist / max(sphereR, 1.0), 2.2));
        float3 burstCore = float3(1.8, 1.3, 0.85) * sphere * eb * 2.5;

        //径向光刃
        float bladeAng = angle * (12.0 + 6.0 * seed);
        float bladeN = tex2D(noiseTex, frac(float2(bladeAng * 0.08, seed + eb))).r;
        float blade = pow(saturate(bladeN), 4.0);
        float bladeRange = exp(-dist / max(radius * 2.5, 1.0));
        float3 bladeCol = float3(1.5, 0.7, 0.25) * blade * bladeRange * eb * 1.6;

        //全屏淡白
        float3 veil = float3(1.0, 0.85, 0.7) * eb * eb * 0.55;

        ejectColor = burstCore + bladeCol + veil;
    }

    //=== 合成 ===
    float3 additive = ringEnergy + spikeColor + shockAll + ejectColor;
    float3 finalColor = original.rgb + additive;

    return float4(finalColor, original.a);
}

technique Technique1
{
    pass VoidArrivalPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
