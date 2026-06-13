// ============================================================================
// SignalTowerLightning.fx 信号塔红色闪电
// 程序生成，塔顶在画布底部中点；Additive 叠加
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float lifeProgress;  //生命 0~1，0刚生 1消亡
float intensity;     //总体亮度，脉冲/爆闪
float seed;          //实例随机种子
float2 texelSize;    //1/宽 1/高
float aspect;        //宽/高，冲击点保持正圆

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
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

//返回主干闪电在纵向比例v处的水平偏移，值域约-1~1
//两端通过smoothstep压回0，保证闪电源点集中在天顶正上方、终点精准对准塔顶
float pathOffset(float v, float s)
{
    float o = 0.0;
    o += (valueNoise(float2(v * 4.0, s * 1.7)) - 0.5) * 2.0 * 0.55;
    o += (valueNoise(float2(v * 10.0 + s * 3.3, s)) - 0.5) * 2.0 * 0.30;
    o += (valueNoise(float2(v * 26.0, s * 5.1)) - 0.5) * 2.0 * 0.12;
    o *= smoothstep(0.0, 0.18, v);
    o *= smoothstep(1.0, 0.85, v);
    return o;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float u = coords.x;
    float v = coords.y;

    //---- 主干 ----
    float mainOff = pathOffset(v, seed);
    float mainPath = 0.5 + mainOff * 0.32;

    //高频抖动：每帧生成毛刺，制造瞬态跳动感
    float jitterTime = floor(uTime * 50.0);
    float jitter = (hash21(float2(v * 220.0, jitterTime + seed * 7.1)) - 0.5) * 0.014;
    mainPath += jitter;

    float mainDist = abs(u - mainPath);

    float coreR = 0.0032;
    float core = 1.0 - smoothstep(coreR * 0.6, coreR * 2.0, mainDist);

    float glowR = 0.04;
    float glow = 1.0 - smoothstep(0.0, glowR, mainDist);
    glow = pow(saturate(glow), 1.8);

    //适度的主干弥散，不再让halo扩到接近画布边缘
    float wideR = 0.10;
    float wide = 1.0 - smoothstep(0.0, wideR, mainDist);
    wide = pow(saturate(wide), 3.0) * 0.45;

    //---- 支路1：向右分叉，自v=0.30起 ----
    float b1v = saturate((v - 0.30) / 0.60);
    float b1Off = pathOffset(v * 0.8 + 3.1, seed * 3.7 + 1.3) + b1v * 0.85;
    float b1Path = mainPath + b1Off * 0.18;
    float b1Dist = abs(u - b1Path);
    float b1Mask = step(0.30, v) * (1.0 - b1v * 0.75);
    float b1Core = (1.0 - smoothstep(coreR * 0.6, coreR * 2.5, b1Dist)) * b1Mask;
    float b1Glow = (1.0 - smoothstep(0.0, 0.04, b1Dist)) * b1Mask * 0.9;

    //---- 支路2：向左分叉，自v=0.52起 ----
    float b2v = saturate((v - 0.52) / 0.45);
    float b2Off = pathOffset(v * 1.1 - 5.7, seed * 7.9 + 2.8) - b2v * 0.95;
    float b2Path = mainPath + b2Off * 0.16;
    float b2Dist = abs(u - b2Path);
    float b2Mask = step(0.52, v) * (1.0 - b2v * 0.8);
    float b2Core = (1.0 - smoothstep(coreR * 0.6, coreR * 2.3, b2Dist)) * b2Mask;
    float b2Glow = (1.0 - smoothstep(0.0, 0.035, b2Dist)) * b2Mask * 0.85;

    //---- 支路3：顶段散射短分叉，自v=0.12起、到v=0.55止 ----
    float b3v = saturate((v - 0.12) / 0.30);
    float b3Off = pathOffset(v * 1.6 + 9.3, seed * 2.1 + 0.7) + b3v * 0.45;
    float b3Path = mainPath + b3Off * 0.12;
    float b3Dist = abs(u - b3Path);
    float b3Mask = step(0.12, v) * step(v, 0.55) * (1.0 - b3v * 0.9);
    float b3Glow = (1.0 - smoothstep(0.0, 0.03, b3Dist)) * b3Mask * 0.7;

    //---- 打击点爆闪：画布底部中点 ----
    //x方向按aspect矫正成像素均匀的圆
    float2 impactD = float2((u - 0.5) * aspect, v - 1.0);
    float impactLen = length(impactD);
    float impactBurst = 1.0 - smoothstep(0.0, 0.28, impactLen);
    impactBurst = pow(saturate(impactBurst), 2.6);

    //径向星芒：围绕打击点的六叶扇形
    float angle = atan2(impactD.y, impactD.x);
    float rays = pow(abs(sin(angle * 3.0 + uTime * 1.5 + seed * 3.0)), 10.0);
    float rayMask = 1.0 - smoothstep(0.0, 0.32, impactLen);
    float starRays = rays * rayMask * 0.55;

    //---- 顶端源点辉光：画布顶部中点 ----
    float2 skyD = float2((u - 0.5) * aspect, v);
    float skyLen = length(skyD);
    float skyGlow = (1.0 - smoothstep(0.0, 0.14, skyLen)) * 0.28;

    //---- 生命周期曲线 ----
    //0~0.08 延伸：头端沿v由顶向下展开
    //0.08~0.35 停留：全亮爆闪
    //0.35~1.0 淡出：尾端从顶向下褪去且加剧闪烁
    float life = saturate(lifeProgress);

    float extendMask = 1.0;
    if (life < 0.08) {
        float extendFront = life / 0.08;
        extendMask = smoothstep(extendFront + 0.06, extendFront - 0.02, v);
    }

    float fadeTailMask = 1.0;
    if (life > 0.35) {
        //从顶端开始褪色，模拟闪电向下回撤
        float fadeFront = (life - 0.35) / 0.65;
        fadeTailMask = smoothstep(fadeFront - 0.05, fadeFront + 0.15, v);
    }

    float fade = 1.0;
    if (life > 0.35) {
        fade = 1.0 - smoothstep(0.35, 1.0, life);
    }

    //强度闪烁：前段稳定脉动，尾段加剧+随机熄灭帧
    float flicker = 0.82 + 0.18 * sin(uTime * 58.0 + seed * 17.0);
    if (life > 0.35) {
        flicker = 0.55 + 0.45 * sin(uTime * 95.0 + seed * 11.0);
        float blackoutTime = floor(uTime * 32.0);
        if (hash11(blackoutTime + seed * 7.0) > 0.72) flicker *= 0.25;
    }

    //---- 颜色合成：白热核心 + 亮红halo + 暗红弥漫 ----
    float totalCore = saturate(core + b1Core + b2Core + core * impactBurst * 1.6);
    float totalGlow = saturate(glow + b1Glow + b2Glow + b3Glow + wide * 0.4);
    float totalWide = saturate(wide + impactBurst * 0.85 + starRays + skyGlow);

    //核心色：微暖偏白，模拟电弧瞬态高温
    float3 colorCore = float3(1.6, 1.42, 1.15) * totalCore;
    //亮红halo：主体颜色
    float3 colorGlow = float3(1.35, 0.22, 0.18) * totalGlow * 0.95;
    //暗红弥漫+冲击爆闪
    float3 colorWide = float3(1.0, 0.08, 0.05) * totalWide;

    float3 col = colorCore + colorGlow + colorWide;
    col *= extendMask * fadeTailMask * fade * flicker * intensity;

    //边缘羽化：对画布矩形做强衰减，所有halo从距边缘0.12开始快速归零
    //避免任何情况下贴图边界直接暴露
    float edgeMaskX = smoothstep(0.0, 0.12, u) * smoothstep(1.0, 0.88, u);
    float edgeMaskY = smoothstep(0.0, 0.03, v) * smoothstep(1.0, 0.97, v);
    col *= edgeMaskX * edgeMaskY;

    //alpha基于亮度（Additive混合下alpha仅用于反裁剪）
    float alpha = saturate(max(max(col.r, col.g), col.b));
    return float4(col, alpha);
}

technique Tech
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
