// ============================================================================
//OldNetSky.fx 旧网天幕 v2：墙外即墙海（黑墙化重做）
//构图承 CyberDomainSky 的黑墙语法：黑是主体（深渊黑底占七成）、红只在结构上烧、
//差速视差（三层幕帘 6%/16%/36% + 纵向视差 clamp）+ 空气透视给纵深。
//身份差异（旧网≠领域）：
//  ①地平线本体是"墙海"幕帘而非领域核心环，墙外的地平线四面都是数据风暴墙
//  ②濒死服务器余烬（uSeed 决定论，同一存档阵列不变）：红烬为主、
//    ~2% 冷青幸存者是考古残光；分钟级去同相明灭
//  ③uWallScreenX 西缘余晖锚 + uSurge 黑墙涌动脉冲（事件驱动）
//  ④uCorrupt 带内腐化：墙脚带天最静，衰减区幕帘湍流加剧+列闪变（疯域的天）
//  ⑤uGiant 巨物剪影槽：远幕之后掠过的深旧网巨物，比黑底更黑的吸光体+暗红缘光，
//    中/近幕后画自然形成遮挡（"墙后有东西"）
//s1 绑 PerlinNoise(LinearWrap)，实测值域 0.22~0.776，阈值均按此定；
//直线算术无动态分支；AlphaBlend 预乘输出（整屏实底天幕）。
//常驻舒适：无全局同相呼吸，余烬明灭 per-cell 去同相，列闪变只随 uCorrupt² 上量
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float uIntensity;
float2 uScreenSize;
float uCamX;          //真实相机世界X（周期图案吃绝对值）
float uCamY;          //相机中心相对世界地表的Y偏移（世界像素）
float uSeed;          //宏观种子
float uWallScreenX;   //黑墙右缘屏幕x（远离墙时大负值，余晖自然归零）
float uCorrupt;       //0~1 带内腐化（墙脚0→衰减区1）
float uSurge;         //0~1 黑墙涌动脉冲
float4 uGiant;        //xy=剪影中心uv z=尺度 w=在场强度

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PSSky(float2 coords : TEXCOORD0) : COLOR0
{
    float aspect = uScreenSize.x / max(uScreenSize.y, 1.0);
    float horizon = 0.62;
    float t = uTime;

    //═══ 深渊黑底：近黑微红，黑到位红才烫 ═══
    float3 cTop     = float3(0.003, 0.001, 0.002);
    float3 cHorizon = float3(0.020, 0.004, 0.006);
    float3 cBelow   = float3(0.006, 0.002, 0.003);
    float upT = saturate(coords.y / horizon);
    float3 col = lerp(cTop, cHorizon, upT * upT);
    float belowT = saturate((coords.y - horizon) / (1.0 - horizon));
    col = lerp(col, cBelow, smoothstep(0.0, 0.55, belowT));

    //纵向视差位移：相对地表偏移，clamp 防高空带/深层把墙推出画
    float camYFar  = clamp(uCamY * 0.000015, -0.10, 0.10);
    float camYMid  = clamp(uCamY * 0.00004,  -0.14, 0.14);
    float camYNear = clamp(uCamY * 0.00009,  -0.18, 0.18);

    //腐化驱动：湍流摆幅与上升流速随腐化抬升
    float turb = 1.0 + uCorrupt * 1.5;
    float rise = 1.0 + uCorrupt * 0.9;

    //═══ 墙海幕帘 ×3：纵向丝流 × 湍流上缘（先算遮罩，巨物要被中/近幕压住） ═══

    //--- 远幕：暗、矮、糊 ---
    float xF = coords.x + uCamX * 0.00003;
    float yF = coords.y + camYFar;
    float streakF = tex2D(noiseTex, frac(float2(xF * 3.2, yF * 0.45 - t * 0.0045 * rise) + uSeed * 0.37 + 0.13)).r;
    float edgeF = tex2D(noiseTex, frac(float2(xF * 1.1 + 0.71, t * 0.0022 * turb))).g;
    float topF = 0.36 - edgeF * 0.10 * turb;
    float bodyF = smoothstep(topF - 0.05, topF + 0.14, yF)
        * (1.0 - smoothstep(horizon, horizon + 0.06, coords.y));
    float veilF = bodyF * (0.30 + 0.70 * smoothstep(0.36, 0.72, streakF));

    //--- 中幕 ---
    float xM = coords.x + uCamX * 0.00008;
    float yM = coords.y + camYMid;
    float streakM = tex2D(noiseTex, frac(float2(xM * 4.6, yM * 0.55 - t * 0.0075 * rise) + 0.47)).r;
    float edgeM = tex2D(noiseTex, frac(float2(xM * 1.6 + 0.29, t * 0.0031 * turb))).g;
    float topM = 0.27 - edgeM * 0.13 * turb;
    float bodyM = smoothstep(topM - 0.04, topM + 0.12, yM)
        * (1.0 - smoothstep(horizon, horizon + 0.05, coords.y));
    float veilM = bodyM * (0.26 + 0.74 * smoothstep(0.38, 0.74, streakM));

    //--- 近幕：亮、高、锐；腐化列闪变只作用在这一层 ---
    float xN = coords.x + uCamX * 0.00018;
    float yN = coords.y + camYNear;
    float streakN = tex2D(noiseTex, frac(float2(xN * 6.4, yN * 0.62 - t * 0.0110 * rise) + 0.83)).r;
    float edgeN = tex2D(noiseTex, frac(float2(xN * 2.3 + 0.55, t * 0.0042 * turb))).g;
    //列闪变：26px 宽列、~2.5Hz 重掷，幅度随 uCorrupt² 上量（墙脚带恒零）
    float colId = floor(coords.x * uScreenSize.x / 26.0);
    float glitch = (hash21(float2(colId, floor(t * 2.5))) - 0.5) * uCorrupt * uCorrupt;
    float topN = 0.16 - edgeN * 0.16 * turb + glitch * 0.04;
    float bodyN = smoothstep(topN - 0.03, topN + 0.10, yN)
        * (1.0 - smoothstep(horizon, horizon + 0.04, coords.y));
    float veilN = bodyN * (0.22 + 0.78 * smoothstep(0.42, 0.76, streakN)) * (1.0 + glitch * 0.55);

    //═══ 远幕着色 → 巨物剪影 → 中/近幕着色（加色顺序即遮挡关系） ═══
    col += float3(0.24, 0.028, 0.032) * veilF * 0.55;

    //巨物：吸光体把底色压向更黑 + 暗红缘光；w=0 时恒零
    float2 gd = (coords - uGiant.xy) * float2(aspect, 1.0) / max(uGiant.z, 0.001);
    float gEdge = tex2D(noiseTex, frac(gd * float2(0.22, 0.50) + float2(t * 0.003, uSeed * 0.61))).r;
    float gShape = length(gd * float2(1.0, 3.0));
    float gBody = (1.0 - smoothstep(0.62 + gEdge * 0.30, 0.98 + gEdge * 0.30, gShape)) * uGiant.w;
    float gRim = saturate(smoothstep(0.52 + gEdge * 0.30, 0.82 + gEdge * 0.30, gShape)
        - smoothstep(0.82 + gEdge * 0.30, 1.16 + gEdge * 0.30, gShape)) * uGiant.w;
    col = lerp(col, float3(0.0006, 0.0002, 0.0004), gBody * 0.85);
    col += float3(0.22, 0.035, 0.030) * gRim * 0.55;

    col += float3(0.48, 0.055, 0.050) * veilM * 0.80;
    col += float3(0.78, 0.115, 0.075) * veilN * 1.00;

    //顶缘热线：各层湍流上缘一条烫边；涌动期整体增辉
    float hotBoost = 1.0 + uSurge * 1.5;
    float hotF = (1.0 - smoothstep(0.0, 0.016, abs(yF - topF))) * bodyF;
    float hotM = (1.0 - smoothstep(0.0, 0.014, abs(yM - topM))) * bodyM;
    float hotN = (1.0 - smoothstep(0.0, 0.012, abs(yN - topN))) * bodyN;
    col += float3(0.55, 0.11, 0.06) * hotF * 0.35 * hotBoost;
    col += float3(0.80, 0.19, 0.09) * hotM * 0.50 * hotBoost;
    col += float3(1.0, 0.30, 0.13) * hotN * 0.65 * hotBoost;

    //═══ 死网线框残骸 ×2：per-cell 静态刚体旋转矩形，极慢漂移 ═══
    //--- 远层 ---
    float2 duvF = float2(coords.x * aspect + uCamX * 0.00005 + t * 0.0008,
        coords.y + camYFar * 0.8);
    float2 dCellF = duvF * 3.0;
    float2 dIdF = floor(dCellF);
    float dHF = hash21(dIdF + 5.31 + uSeed * 0.11);
    float dActiveF = step(0.66, dHF);
    float2 dLocF = frac(dCellF) - 0.5;
    float angF = dHF * 6.28318;
    float caF = cos(angF);
    float saF = sin(angF);
    float2 rpF = float2(dLocF.x * caF - dLocF.y * saF, dLocF.x * saF + dLocF.y * caF);
    float2 halfF = float2(0.09 + hash21(dIdF + 3.7) * 0.12, 0.045 + hash21(dIdF + 9.2) * 0.07);
    float2 adF = abs(rpF) - halfF;
    float outlineF = 1.0 - smoothstep(0.004, 0.011, abs(max(adF.x, adF.y)));
    float zoneF = 1.0 - smoothstep(horizon - 0.16, horizon, coords.y);
    col += float3(0.26, 0.045, 0.045) * outlineF * dActiveF * zoneF * 0.28;

    //--- 近层：更大更亮，带内残骸密度随腐化微升 ---
    float2 duvN = float2(coords.x * aspect + uCamX * 0.00022 - t * 0.0013,
        coords.y + camYNear * 0.8);
    float2 dCellN = duvN * 1.7;
    float2 dIdN = floor(dCellN);
    float dHN = hash21(dIdN + 11.57 + uSeed * 0.23);
    float dActiveN = step(0.72 - uCorrupt * 0.08, dHN);
    float2 dLocN = frac(dCellN) - 0.5;
    float angN = dHN * 6.28318;
    float caN = cos(angN);
    float saN = sin(angN);
    float2 rpN = float2(dLocN.x * caN - dLocN.y * saN, dLocN.x * saN + dLocN.y * caN);
    float2 halfN = float2(0.11 + hash21(dIdN + 2.9) * 0.15, 0.05 + hash21(dIdN + 7.3) * 0.09);
    float2 adN = abs(rpN) - halfN;
    float outlineN = 1.0 - smoothstep(0.003, 0.008, abs(max(adN.x, adN.y)));
    float diagN = (1.0 - smoothstep(0.004, 0.010, abs(rpN.x - rpN.y)))
        * step(max(adN.x, adN.y), -0.01);
    float zoneN = 1.0 - smoothstep(horizon - 0.10, horizon, coords.y);
    col += float3(0.52, 0.09, 0.07) * (outlineN + diagN * 0.4) * dActiveN * zoneN * 0.46;

    //═══ 濒死服务器余烬（上半天空，静态网格，分钟级去同相明灭） ═══
    float2 sUV = coords * float2(aspect, 1.0) * 5.0 + float2(uCamX * 0.00001, camYFar * 0.5);
    float2 sId = floor(sUV);
    float sH = hash21(sId + uSeed * 0.013);
    float2 sOff = float2(hash21(sId + 1.3), hash21(sId + 2.6)) - 0.5;
    float sDist = length(frac(sUV) - 0.5 - sOff * 0.55);
    float star = (1.0 - smoothstep(0.010, 0.042, sDist)) * step(0.78, sH);
    float phase = sH * 6.28318;
    //常燃余烬慢呼吸 vs 濒死深谷明灭（sin*sin 低谷近熄）
    float breath = 0.55 + 0.45 * sin(t * 0.045 + phase * 7.0);
    float slowDie = 0.5 + 0.5 * sin(t * 0.03 + phase * 3.0);
    float dying = step(0.92, sH);
    float amp = lerp(breath, slowDie * slowDie, dying);
    float3 emberCol = lerp(float3(0.50, 0.11, 0.07), float3(0.72, 0.16, 0.09), frac(sH * 5.3));
    //幸存者冷青（~2%）：还亮着的老服务器，考古残光
    float survivor = step(0.985, hash21(sId + 4.2));
    emberCol = lerp(emberCol, float3(0.22, 0.50, 0.55), survivor);
    float skyZone = 1.0 - smoothstep(0.40, 0.60, coords.y);
    col += emberCol * star * amp * skyZone * 0.55;

    //═══ 地平辉光（窄） + 西缘余晖锚（uSurge 涌动增幅） ═══
    float hgl = 1.0 - smoothstep(0.0, 0.030, abs(coords.y - horizon));
    col += float3(0.20, 0.045, 0.040) * hgl * 0.22;

    float pxX = coords.x * uScreenSize.x;
    float dWall = pxX - uWallScreenX;
    float spill = exp(-max(dWall, 0.0) / 900.0);
    float sFlick = 0.85 + 0.15 * tex2D(noiseTex, frac(float2(coords.y * 2.0, t * 0.05))).g;
    float horizonBoost = 0.4 + smoothstep(0.3, 0.95, coords.y) * 0.6;
    col += float3(0.34, 0.040, 0.034) * spill * sFlick * horizonBoost * (0.55 + uSurge * 1.6);

    //预乘输出
    col = saturate(col);
    float a = saturate(uIntensity);
    return float4(col * a, a);
}

technique TechSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSSky();
    }
}
