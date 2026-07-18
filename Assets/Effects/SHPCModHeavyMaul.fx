// ============================================================================
//SHPCModHeavyMaul.fx 重型枪管·贯体重锤冲击波
//UV 0~1→-1~1；+X 为贯穿（凿击来向）方向
//极坐标接缝审计：angle 仅进入 ① tex2D(wrap) 且 normAngle 乘整数、
//② sin(整数*angle+连续相位)，其余全部走 dist / 笛卡尔，无缝
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float progress;         //0~1 冲击波扩张进度
float fadeAlpha;        //整体透明度 0~1
float3 coreColor;       //白热核心色
float3 ringColor;       //灼铁橙主色
float3 ironColor;       //暗铁烟色

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;

    //贯体各向异性：沿贯穿方向（x）轻微压扁，环面像被锤头挤出的钣金波
    float2 squashed = float2(centered.x * 1.25, centered.y);
    float dist = length(squashed);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    //环边噪声扰动：normAngle 乘整数 6，wrap 采样过缝连续
    float n1 = tex2D(noiseSamp, float2(normAngle * 6.0 - uTime * 0.7, dist * 1.5 - progress)).r;
    float adjDist = dist + (n1 - 0.5) * 0.06;

    //=
    //A. 主冲击环 ： 厚重的锻压波前，外缘锐利、内侧拖暗铁烟
    //=
    float thickness = 0.055 + (1.0 - progress) * 0.05;
    float ringD = adjDist - progress * 0.82;
    float mainRing = 1.0 - smoothstep(0.0, thickness, abs(ringD));
    //外缘白热层（更薄更亮，金属受锻的炽线）
    float rimLine = 1.0 - smoothstep(0.0, thickness * 0.35, abs(ringD - thickness * 0.4));
    //内侧烟拖尾：环身后方 0.35~0.62 进度带内的暗铁余温
    float innerSmoke = smoothstep(progress * 0.30, progress * 0.52, adjDist)
        * (1.0 - smoothstep(progress * 0.62, progress * 0.80, adjDist));
    float smokeNoise = tex2D(noiseSamp, float2(centered.x * 0.9 + uTime * 0.3, centered.y * 0.9 - uTime * 0.2)).g;
    innerSmoke *= (0.35 + smokeNoise * 0.65) * (1.0 - progress * 0.6);

    //=
    //B. 金属光泽扇纹 ： 环面上的锻面反光条带（整数 12 谐波，过缝安全）
    //=
    float gloss = pow(abs(sin(angle * 12.0 - uTime * 2.0)), 6.0);
    mainRing *= 0.72 + gloss * 0.45;

    //=
    //C. 滞后回锤环 ： 第二记闷响，滞后主环 62%
    //=
    float lagPos = progress * 0.5;
    float lagRing = (1.0 - smoothstep(0.0, 0.035, abs(adjDist - lagPos))) * 0.45;
    lagRing *= smoothstep(0.12, 0.35, progress);

    //=
    //D. 径向锻裂纹 ： 8 条整数谐波裂线，只咬在波前外侧
    //=
    float cracks = pow(abs(sin(angle * 8.0 + n1 * 2.4)), 28.0);
    float crackZone = smoothstep(progress * 0.55, progress * 0.9, dist)
        * (1.0 - smoothstep(progress * 0.95, progress * 1.35, dist));
    cracks *= crackZone * (1.0 - progress) * 1.1;

    //=
    //E. 贯体活塞闪光 ： 沿 ±X 的压缩光矛，贯穿来向更亮（纯笛卡尔）
    //=
    float lanceCore = exp(-abs(centered.y) * 16.0);
    float lanceLen = 1.0 - smoothstep(0.15, 0.95, abs(centered.x));
    float forwardBias = 0.65 + 0.35 * smoothstep(-0.6, 0.6, centered.x);
    float lance = lanceCore * lanceLen * forwardBias * pow(saturate(1.0 - progress / 0.45), 1.6);

    //=
    //F. 中心锻锤白闪 ： 落锤瞬间过曝，随后迅速塌缩
    //=
    float flash = pow(saturate(1.0 - dist / 0.24), 2.2) * pow(saturate(1.0 - progress / 0.32), 2.0);

    //=
    //颜色合成：白热锻线 → 灼铁橙环身 → 暗铁烟
    //=
    float3 cWhite = float3(1.0, 0.97, 0.9);
    float3 ringMix = lerp(ringColor, cWhite, saturate(rimLine * 0.85));

    float3 color = float3(0.0, 0.0, 0.0);
    color += ringMix * mainRing * (0.85 + rimLine * 0.7);
    color += ringColor * lagRing;
    color += ironColor * innerSmoke * 0.8;
    color += cWhite * cracks * 0.9;
    color += coreColor * lance * 0.85;
    color += cWhite * flash;

    float alpha = saturate(mainRing + lagRing * 0.6 + innerSmoke * 0.45 + cracks * 0.7 + lance * 0.8 + flash);
    alpha *= fadeAlpha;

    return float4(color * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModHeavyMaulPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
