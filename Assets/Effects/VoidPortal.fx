// ============================================================================
//VoidPortal.fx 虚空裂隙全屏后处理
//径向扭曲+色差+压暗+涡流边缘；s0 场景 s1 噪声
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float intensity;
float expandProgress;
float riftHalfHeight;
float riftMaxWidth;
float dimStrength;
float energyPower;
float crackSeed;
float shockwaveTime;//从最近一次冲击波触发起的秒数，<0表示未激活

float2 riftCenter;
float2 screenPosition;
float2 worldViewSize;

//---- 工具 ----

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float sampleNoise(float2 uv)
{
    float3 n = tex2D(noiseTex, frac(uv)).rgb;
    return n.r * 0.5 + n.g * 0.35 + n.b * 0.15;
}

float layeredNoise(float2 uv, float timeOff)
{
    float n1 = tex2D(noiseTex, frac(uv * 0.37 + float2(timeOff * 0.013, timeOff * 0.009))).r;
    float n2 = tex2D(noiseTex, frac(uv * 1.13 + float2(timeOff * -0.017, timeOff * 0.021))).g;
    float n3 = tex2D(noiseTex, frac(uv * 2.71 + float2(timeOff * 0.031, timeOff * -0.011))).b;
    return n1 * 0.5 + n2 * 0.35 + n3 * 0.15;
}

//裂隙SDF
float riftSDF(float2 relPos, out float centerLineX, out float yNorm, out float envelope)
{
    float wProg = pow(saturate(expandProgress), 1.4);
    float hProg = pow(saturate(expandProgress), 0.55);

    float halfH = riftHalfHeight * hProg;
    float maxW = riftMaxWidth * wProg;

    yNorm = clamp(relPos.y / max(halfH, 0.1), -1.0, 1.0);
    float tipDist = max(abs(relPos.y) - halfH, 0.0);

    //中心线蛇形偏移
    float pathNoise = tex2D(noiseTex, frac(float2(
        relPos.y * 0.0022 + crackSeed * 0.1,
        uTime * 0.045 + crackSeed * 0.3))).r;
    centerLineX = (pathNoise - 0.5) * maxW * 0.4;

    envelope = 1.0 - pow(abs(yNorm), 1.6);

    float widthNoise = tex2D(noiseTex, frac(float2(
        relPos.y * 0.005 + uTime * 0.02 + crackSeed * 0.7,
        uTime * 0.015 + crackSeed * 0.2))).g;
    float localWidth = maxW * envelope * (0.45 + 0.55 * widthNoise);

    float tearNoise = tex2D(noiseTex, frac(float2(
        relPos.y * 0.018 + uTime * 0.012 + crackSeed,
        uTime * 0.008))).b;
    localWidth *= (0.72 + 0.28 * step(0.32, tearNoise));

    float xDist = abs(relPos.x - centerLineX) - localWidth;
    return max(xDist, tipDist * 0.6);
}

//主着色器
float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);

    if (intensity < 0.001 || expandProgress < 0.001)
        return original;

    float2 worldPos = screenPosition + worldViewSize * coords;
    float2 screenUV = worldViewSize * coords;
    float2 relPos = worldPos - riftCenter;
    float worldDist = length(relPos);

    float effectRadius = (riftHalfHeight + riftMaxWidth) * saturate(expandProgress) * 1.85;
    float normDist = saturate(worldDist / max(effectRadius, 1.0));

    float centerLineX, yNorm, envelope;
    float sd = riftSDF(relPos, centerLineX, yNorm, envelope);
    float2 voidLocal = relPos - float2(centerLineX, 0.0);

    //=== 第一层：UV扭曲 + 边缘热浪 ===
    float2 distUV1 = frac(screenUV * 0.0004 + float2(uTime * 0.018, uTime * 0.013));
    float2 warpDisp = tex2D(noiseTex, distUV1).rg * 2.0 - 1.0;

    float edgeBand = riftMaxWidth * expandProgress * 0.5;
    float edgeFactor = saturate(1.0 - max(sd, 0.0) / max(edgeBand, 1.0));

    float heatHaze = sampleNoise(relPos * 0.012 + float2(0.0, uTime * 0.4)) - 0.5;
    float warpStr = intensity * 0.0045 * (0.3 + edgeFactor * 1.6) * saturate(expandProgress);
    warpStr += edgeFactor * heatHaze * 0.006 * intensity;

    float2 pullDir = -relPos / max(worldDist, 0.1);
    float pullStr = intensity * 0.0026 * saturate(1.0 - normDist) * saturate(expandProgress);
    pullStr *= pullStr;

    float2 warpOffset = (warpDisp * warpStr + pullDir * pullStr) * worldViewSize / max(worldViewSize.x, 1.0);
    float2 warpedCoords = clamp(coords + warpOffset, 0.002, 0.998);
    original = tex2D(uImage0, warpedCoords);

    //=== 第二层：边缘色差 ===
    float2 edgeDir = relPos / max(worldDist, 0.1);
    float caWorldPx = edgeFactor * 4.5 * intensity;
    float2 caOffset = edgeDir * caWorldPx / worldViewSize;
    original.r = tex2D(uImage0, warpedCoords + caOffset).r;
    original.b = tex2D(uImage0, warpedCoords - caOffset * 0.65).b;

    //=== 第三层：背景压暗与红色映射 ===
    float dimRange = effectRadius * 2.5;
    float dimFactor = saturate(1.0 - worldDist / max(dimRange, 1.0));
    dimFactor = dimFactor * dimFactor;
    float dimPulse = 0.86 + 0.14 * sin(uTime * 1.6);
    float targetDim = lerp(0.45, 0.13, dimFactor * 0.5);
    float actualDim = lerp(1.0, targetDim, intensity * dimStrength * dimPulse);
    original.rgb *= actualDim;

    float lum = dot(original.rgb, float3(0.299, 0.587, 0.114));
    float3 gray = float3(lum, lum, lum);
    float desatAmount = 0.45 * intensity * dimFactor;
    original.rgb = lerp(original.rgb, gray, desatAmount);

    float3 shadowVoid = float3(0.05, 0.004, 0.012);
    float3 midCrimson = float3(0.34, 0.022, 0.038);
    float3 highEmber  = float3(0.66, 0.13, 0.07);
    float3 redMap;
    if (lum < 0.25)
        redMap = lerp(shadowVoid, midCrimson, lum / 0.25);
    else
        redMap = lerp(midCrimson, highEmber, saturate((lum - 0.25) / 0.75));
    float redTintStr = 0.32 * intensity * dimFactor;
    original.rgb = lerp(original.rgb, redMap * (lum * 0.6 + 0.4), redTintStr);

    //=== 第四层：虚空内部体积感（域扭曲 + 视差） ===
    float edgeThick = riftMaxWidth * saturate(expandProgress) * 0.16;
    float insideFactor = saturate(-sd / max(edgeThick * 5.0, 1.0));
    //深度因子：靠近裂隙中心轴 → 越深
    float depth = saturate(1.0 - abs(voidLocal.x) / max(riftMaxWidth * expandProgress * 0.85, 1.0));

    //域扭曲
    float2 warpA = float2(
        sampleNoise(voidLocal * 0.0035 + float2(uTime * 0.04, -uTime * 0.025)),
        sampleNoise(voidLocal * 0.0035 + float2(11.3 + uTime * -0.03, 7.7 + uTime * 0.045))) * 2.0 - 1.0;
    float2 warpedVoid = voidLocal + warpA * 280.0 * (0.5 + depth * 0.6);

    //三层视差
    float layerFar = sampleNoise(warpedVoid * 0.0018 + float2(uTime * 0.012, -uTime * 0.008));
    float layerMid = sampleNoise(warpedVoid * 0.0048 + float2(-uTime * 0.022, uTime * 0.016));
    float layerNear = sampleNoise(warpedVoid * 0.011 + float2(uTime * 0.05, uTime * 0.038));
    float voidVolume = layerFar * 0.5 + layerMid * 0.32 + layerNear * 0.18;

    //深处偶尔的红色裂痕泄露
    float deepCrack = pow(saturate(layerMid * layerNear * 1.6), 5.0);

    float3 voidBase = float3(0.012, 0.003, 0.006) * voidVolume * (1.0 - depth * 0.6);
    float3 voidGlimmer = float3(0.55, 0.05, 0.025) * deepCrack * (0.4 + depth);
    float3 voidColor = voidBase + voidGlimmer;

    //=== 第五层：双向反向涡流 ===
    float vortexAngle = atan2(voidLocal.y, voidLocal.x + 0.001);
    float vortexR = length(voidLocal);
    float angCW = vortexAngle + uTime * 0.55 - vortexR * 0.0026;
    float angCCW = vortexAngle - uTime * 0.85 + vortexR * 0.0042;
    float swirl1 = tex2D(noiseTex, frac(float2(angCW * 0.16, vortexR * 0.0018 + crackSeed * 0.15))).r;
    float swirl2 = tex2D(noiseTex, frac(float2(angCCW * 0.22, vortexR * 0.0030 + crackSeed * 0.4))).g;
    float swirl = pow(saturate(swirl1 * 0.6 + swirl2 * 0.4), 1.4);

    float3 swirlColor = float3(0.18, 0.012, 0.008) + float3(0.55, 0.05, 0.03) * pow(depth, 1.8);
    voidColor += swirlColor * swirl * (0.5 + depth);

    //=== 第六层：深处浮游微光（伪3D星点） ===
    float stars = 0.0;
    float2 sUV1 = frac(voidLocal * 0.014 + float2(uTime * 0.018, -uTime * 0.014));
    float sN1 = tex2D(noiseTex, sUV1).r;
    stars += smoothstep(0.93, 0.99, sN1) * 0.6;
    float2 sUV2 = frac(voidLocal * 0.026 + float2(-uTime * 0.034, uTime * 0.022));
    float sN2 = tex2D(noiseTex, sUV2).g;
    stars += smoothstep(0.92, 0.98, sN2) * 0.85;
    float2 sUV3 = frac(voidLocal * 0.052 + float2(uTime * 0.06, uTime * 0.045));
    float sN3 = tex2D(noiseTex, sUV3).b;
    stars += smoothstep(0.94, 0.99, sN3);

    float starTwinkle = 0.5 + 0.5 * sin(uTime * 3.7 + (sN1 + sN2 + sN3) * 6.28);
    voidColor += float3(1.0, 0.45, 0.22) * stars * starTwinkle * (0.35 + depth * 0.7);

    original.rgb = lerp(original.rgb, voidColor, insideFactor);

    //=== 第七层：多阶能量边缘 ===
    float absSD = max(abs(sd), 0.0);
    float innerEdge = exp(-absSD / max(edgeThick * 0.13, 0.1));
    float midGlow = exp(-absSD / max(edgeThick * 1.5, 0.1));
    float outerGlow = exp(-absSD / max(edgeThick * 5.5, 0.1));

    float flickNoise = tex2D(noiseTex, frac(float2(relPos.y * 0.008 + uTime * 0.5, relPos.x * 0.005 + uTime * 0.2))).r;
    float flicker1 = 0.55 + 0.45 * flickNoise;
    float flickNoise2 = tex2D(noiseTex, frac(float2(relPos.x * 0.004 + uTime * 0.12, relPos.y * 0.003))).g;
    float flicker2 = 0.7 + 0.3 * flickNoise2;

    float eNoise = layeredNoise(relPos * 0.004, uTime);
    float domainBreathe = 0.9 + 0.1 * sin(uTime * 0.7);

    float3 hotCore = float3(1.0, 0.6, 0.28) * innerEdge * energyPower * flicker1 * 2.4;
    float3 redGlow = float3(0.92, 0.08, 0.035) * midGlow * energyPower * eNoise * flicker2 * 1.05;
    float3 deepRedGlow = float3(0.34, 0.014, 0.008) * outerGlow * energyPower * 0.55;
    float3 edgeEnergy = (hotCore + redGlow + deepRedGlow) * saturate(expandProgress) * domainBreathe;

    //=== 第八层：辐射裂纹 ===
    float angle = atan2(relPos.y, relPos.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    float crackNoise = tex2D(noiseTex, frac(float2(
        normAngle * 3.0 + uTime * 0.03 + crackSeed * 0.5,
        worldDist * 0.002 - uTime * 0.02))).r;
    float crack = smoothstep(0.40, 0.48, crackNoise) * smoothstep(0.62, 0.52, crackNoise);
    float crackRange = saturate(1.0 - normDist * 1.3);
    crackRange *= crackRange;
    float crackPulse = 0.5 + 0.5 * sin(uTime * 1.3 + hash21(floor(frac(float2(normAngle, worldDist * 0.001)) * 10.0)) * 6.28318);
    float crackTotal = crack * crackRange * crackPulse * 1.6;
    float3 crackColor = float3(0.95, 0.18, 0.08);

    //=== 第九层：撕裂冲击波 ===
    //不规则狰狞撕裂环：前0.15s猛烈爆发后保持可见时长，缓慢扩散直至消散
    float3 shockColor = float3(0, 0, 0);
    float shockPushStr = 0.0;
    if (shockwaveTime >= 0.0 && shockwaveTime < 2.4)
    {
        float st = shockwaveTime;

        //半径推进：先快速冲出（0~0.3s），之后保持较慢匀速向外推进，给观众足够观察时间
        float burstR = (riftHalfHeight + riftMaxWidth) * 0.55 * (1.0 - exp(-st * 9.0));
        float cruiseR = (riftHalfHeight + riftMaxWidth) * 0.7 * max(st - 0.15, 0.0);
        float shockR = burstR + cruiseR;

        //角度噪声扭曲：让圆环参差不齐
        float aWrap = (angle + 3.14159) / 6.28318;
        float angNoise1 = tex2D(noiseTex, frac(float2(aWrap * 4.0 + crackSeed * 0.3, st * 0.08))).r;
        float angNoise2 = tex2D(noiseTex, frac(float2(aWrap * 11.0 - crackSeed * 0.7, st * 0.04))).g;
        float angDistort = (angNoise1 - 0.5) * 320.0 + (angNoise2 - 0.5) * 140.0;
        float distortedR = shockR + angDistort;

        //尖刺喷射
        float spike = pow(saturate(angNoise1 * angNoise2 * 2.2), 3.5);
        distortedR += spike * (shockR + 100.0) * 0.55;

        //波前宽度：前期窄锐，后期适度变厚保持可见性
        float radial = worldDist - distortedR;
        float frontWidth = 24.0 + 80.0 * saturate(st * 0.8);
        float tailWidth = 160.0 + 280.0 * saturate(st * 0.6);

        float front = saturate(1.0 - abs(radial) / frontWidth);
        front = pow(front, 1.6);
        float tail = saturate(1.0 - max(-radial, 0.0) / tailWidth);
        tail = pow(tail, 2.4) * 0.55;

        //波前用高频噪声咬出毛刺
        float biteN = tex2D(noiseTex, frac(float2(aWrap * 28.0 + crackSeed, worldDist * 0.005 + st * 0.15))).b;
        float bite = step(0.42, biteN);
        front *= 0.35 + 0.85 * bite;

        //时间包络：t=0爆发峰值，0.15s急剧下降到中等强度，之后线性慢衰到2.4s消失
        float envBurst = exp(-st * 8.0);
        float envSustain = saturate(1.0 - (st - 0.15) / 2.25);
        envSustain = envSustain * envSustain;
        float fade = envBurst * 0.7 + envSustain * 0.55;

        float dirBias = 0.7 + 0.3 * abs(cos(angle));

        float3 coreCol = float3(1.5, 0.9, 0.55);
        float3 emberCol = float3(0.9, 0.10, 0.04);
        float3 deepCol = float3(0.45, 0.02, 0.01);

        shockColor = (coreCol * front * 1.4 + emberCol * (front * 0.4 + tail) + deepCol * tail * 0.5)
                     * fade * dirBias * 1.8;

        //背景UV推挤：仅在前沿冲击瞬间生效
        shockPushStr = front * envBurst * dirBias * 0.012;
    }
    if (shockPushStr > 0.0001)
    {
        float2 outDir = relPos / max(worldDist, 0.1);
        float2 pushOff = outDir * shockPushStr * worldViewSize / max(worldViewSize.x, 1.0);
        float2 pushedCoords = clamp(coords + pushOff, 0.002, 0.998);
        float4 pushed = tex2D(uImage0, pushedCoords);
        original.rgb = lerp(original.rgb, pushed.rgb, saturate(shockPushStr * 60.0));
    }

    //=== 合成 ===
    float3 additive = edgeEnergy + crackColor * crackTotal + shockColor;
    float3 finalColor = original.rgb + additive * intensity * domainBreathe;

    return float4(finalColor, original.a);
}

technique Technique1
{
    pass VoidPortalPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
