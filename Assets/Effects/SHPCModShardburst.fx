// ============================================================================
//SHPCModShardburst.fx 霰射枪管碎光爆闪
//UV 0~1→-1~1；+X 为碎裂方向（quad 旋转对齐）
//接缝审计：角向仅经 cos(angle) 与 sin(k*angle) k∈整数 消费，噪声输入全为笛卡尔
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float progress;         //0~1 碎裂扩张进度
float fadeAlpha;        //整体透明度 0~1
float burstSeed;        //每次碎裂的随机种子 0~1，所有者端 roll 后经 ai 传入
float3 coreColor;       //白金核心
float3 glowColor;       //琥珀辉光

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);

    //前向锥权重：+X 最强、后方渐弱（cos 连续无接缝）
    float coneW = smoothstep(-0.35, 0.75, cos(angle));

    //=
    //A. 放射状玻璃裂纹 ： 两组整数频率细线，前向锥内更长更亮
    //=
    float seedPhase = burstSeed * 6.28318;
    float rays1 = pow(abs(sin(angle * 7.0 + seedPhase)), 48.0);
    float rays2 = pow(abs(sin(angle * 11.0 - seedPhase)), 64.0);
    float crackReach = progress * (0.55 + 0.45 * coneW);
    float rayMask = smoothstep(crackReach * 1.05, crackReach * 0.45, dist) * smoothstep(0.02, 0.1, dist);
    float cracks = (rays1 * 0.9 + rays2 * 0.7) * rayMask * (0.35 + 0.65 * coneW);

    //=
    //B. 碎裂波前环 ： RGB 三色微错位形成玻璃色散边缘
    //=
    float ringPos = progress * 0.82;
    float ringTh = 0.045 + (1.0 - progress) * 0.03;
    //噪声碎口：笛卡尔采样（连续），让环残缺如碎玻璃截面
    float2 nUV = centered * 0.9 + float2(burstSeed * 3.7, uTime * 0.35);
    float nRough = tex2D(noiseSamp, nUV).r;
    float chroma = 0.016 + 0.02 * progress;
    float ringR = 1.0 - smoothstep(0.0, ringTh, abs(dist - (ringPos + chroma)));
    float ringG = 1.0 - smoothstep(0.0, ringTh, abs(dist - ringPos));
    float ringB = 1.0 - smoothstep(0.0, ringTh, abs(dist - (ringPos - chroma)));
    float ringGate = 0.55 + 0.45 * nRough;

    //=
    //C. 锥内碎屑闪点 ： Voronoi 噪声阈值化的散落玻璃亮斑
    //=
    float2 gUV = centered * 1.6 + float2(-uTime * 0.15, burstSeed * 7.3);
    float nGlint = tex2D(noiseSamp, gUV).g;
    float glintZone = smoothstep(progress, progress * 0.35, dist) * coneW;
    float glints = smoothstep(0.72, 0.95, nGlint) * glintZone;

    //=
    //D. 中心破碎白闪 ： 仅在开局迸发
    //=
    float flash = pow(saturate(1.0 - dist / 0.3), 2.2) * pow(saturate(1.0 - progress / 0.4), 1.8);

    //=
    //颜色合成
    //=
    float3 cWhite = float3(1.0, 0.99, 0.94);
    float3 color = float3(0.0, 0.0, 0.0);
    color += lerp(glowColor, cWhite, 0.55) * cracks;
    color += (glowColor * float3(ringR, ringG, ringB) + cWhite * ringG * 0.25) * ringGate * 0.85;
    color += coreColor * glints * 0.9;
    color += cWhite * flash;

    float alpha = saturate(cracks + (ringR + ringG + ringB) * 0.28 * ringGate + glints * 0.8 + flash);
    alpha *= fadeAlpha;

    return float4(color * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModShardburstPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
