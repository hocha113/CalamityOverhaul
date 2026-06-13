// ============================================================================
// HackMemoryWipe.fx 骇客时间记忆清除
// 采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;    // 擦除进度 0→1
float intensity;
float2 texelSize;

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 texColor = tex2D(uImage0, coords);
    if (texColor.a < 0.01) return texColor;

    // ==== 数字雨列 ====
    float colScale = 12.0; // 列数
    float colIdx = floor(coords.x * colScale);
    float colRand = hash(colIdx * 127.1 + 7.0);

    // 每列以不同速度下落
    float fallSpeed = 1.5 + colRand * 2.0;
    float fallPhase = frac(coords.y * 3.0 - uTime * fallSpeed + colRand * 10.0);

    // 雨滴亮度（头亮尾暗）
    float dropBright = smoothstep(0.0, 0.02, fallPhase) * smoothstep(0.2, 0.08, fallPhase);
    // 只在部分列激活
    float colActive = step(0.4, hash(colIdx + floor(uTime * 1.5)));
    float rain = dropBright * colActive;

    // ==== 块状擦除遮罩 ====
    float blockSize = 8.0;
    float2 blockId = floor(coords / (texelSize * blockSize));
    float blockRand = hash21(blockId);
    // 擦除阈值随进度推进：blockRand < progress 的块已被擦除
    float eraseThreshold = progress * 1.2; // 略超过1使尾部完全擦除
    float erased = step(blockRand, eraseThreshold);
    // 正在擦除边界的块产生闪烁
    float erasing = step(blockRand, eraseThreshold) * step(eraseThreshold - 0.15, blockRand);
    float erasingFlicker = sin(uTime * 20.0 + blockRand * 50.0) * 0.5 + 0.5;

    // ==== 颜色处理 ====
    // 去饱和（记忆褪色）
    float gray = dot(texColor.rgb, float3(0.299, 0.587, 0.114));
    float desat = intensity * (0.3 + progress * 0.5);
    float3 baseColor = lerp(texColor.rgb, float3(gray, gray, gray), desat);

    // 青绿色调（数字化）
    float3 cyberTint = float3(0.1, 0.9, 0.6);
    baseColor = lerp(baseColor, cyberTint * gray, intensity * 0.15);

    // 数字雨叠加
    baseColor += cyberTint * rain * intensity * 0.4;

    // 擦除区域变暗+静态噪点
    if (erased > 0.5)
    {
        float noise = hash21(blockId + floor(uTime * 10.0));
        float3 staticColor = float3(noise, noise, noise) * 0.15;
        if (erasing > 0.5)
        {
            // 正在擦除：闪烁过渡
            baseColor = lerp(baseColor, staticColor, erasingFlicker * 0.7);
        }
        else
        {
            // 已擦除：暗噪点
            baseColor = staticColor;
        }
    }

    // ==== 边缘青色描线 ====
    float a_r = tex2D(uImage0, coords + float2(texelSize.x * 2.0, 0)).a;
    float a_l = tex2D(uImage0, coords - float2(texelSize.x * 2.0, 0)).a;
    float a_u = tex2D(uImage0, coords + float2(0, texelSize.y * 2.0)).a;
    float a_d = tex2D(uImage0, coords - float2(0, texelSize.y * 2.0)).a;
    float edge = saturate(abs(texColor.a - a_r) + abs(texColor.a - a_l) + abs(texColor.a - a_u) + abs(texColor.a - a_d));
    baseColor += cyberTint * edge * intensity * 0.6 * (1.0 - erased);

    // 已擦除区域降低透明度
    float finalAlpha = texColor.a;
    if (erased > 0.5 && erasing < 0.5)
    {
        finalAlpha *= 0.3 + sin(uTime * 5.0 + blockRand * 20.0) * 0.1;
    }

    return float4(baseColor, finalAlpha);
}

technique Technique1
{
    pass HackMemoryWipePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
