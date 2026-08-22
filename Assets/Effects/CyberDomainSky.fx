// ============================================================================
//CyberDomainSky.fx 赛博领域 L3 专属天空(2077 黑墙风格)
//全覆盖预乘天幕，s1 绑 PerlinNoise(LinearWrap)；直线算术 + 普通 tex2D
//
//构图原则：黑是主体，红只在墙体与残骸上燃烧，深渊黑底(≤0.015)占七成，
//亮度全部集中在少数元素上，靠差速视差与空气透视给纵深：
//  黑墙幕帘 ×3 层(纵向丝流 + 湍流上缘 + 顶缘热线)：视差约 6%/16%/36%
//    (uv偏移系数 0.00003/0.00008/0.00018，世界锚定≈0.0005)，纵向视差取横向一半，
//    远层暗矮、近层亮高(空气透视)
//  死网线框残骸 ×2 层(per-cell 静态刚体旋转矩形线框，极慢漂移)
//  系统核心巨环：沉入墙后，被中/近幕帘遮挡("墙后有东西"的纵深暗示)
//  地板降级为黑虚空里的残缺网格补丁；地平辉光收窄
//
//uCamY 为相机中心相对世界地表的偏移(C#端换算)，非周期垂直包络的位移
//须 clamp 防太空/地狱极端坐标把墙推出画面。
//常驻舒适约定：无快速脉动；星尘 ≥2min 去同相明灭；幕帘上升流 ≤15px/s 观感。
//极角约束：唯一 atan2 在系统核心，消费者 frac((θ+t)·2/2π)，2∈ℤ 跨 ±π 连续；
//残骸旋转是 per-cell 静态刚体旋转(hash 角)，无接缝风险。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float uPresence;        //0~1 天空在场强度
float2 uScreenSize;     //视口像素尺寸
float uCamX;            //真实相机世界X，视差用(周期图案吃绝对值)
float uCamY;            //相机中心相对世界地表的Y偏移(世界像素)

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float aspect = uScreenSize.x / max(uScreenSize.y, 1.0);
    float horizon = 0.60;

    //=
    //深渊黑底：近黑微红，黑到位红才烫
    //=
    float3 cTop     = float3(0.004, 0.001, 0.002);
    float3 cHorizon = float3(0.018, 0.004, 0.006);
    float3 cBelow   = float3(0.007, 0.002, 0.003);
    float upT = saturate(coords.y / horizon);
    float3 col = lerp(cTop, cHorizon, upT * upT);
    float belowT = saturate((coords.y - horizon) / (1.0 - horizon));
    col = lerp(col, cBelow, smoothstep(0.0, 0.55, belowT));

    //=
    //黑墙幕帘：三层视差，先算遮罩(核心环要被中/近层遮挡)
    //每层 = 纵向丝流(噪声x高频/y拉长上升流) × 垂直包络(湍流上缘)
    //=
    //纵向视差位移：相对地表偏移，clamp 防极端坐标推墙出画
    float camYFar  = clamp(uCamY * 0.000015, -0.10, 0.10);
    float camYMid  = clamp(uCamY * 0.00004,  -0.14, 0.14);
    float camYNear = clamp(uCamY * 0.00009,  -0.18, 0.18);

    //--- 远层：暗、矮、糊 ---
    float xF = coords.x + uCamX * 0.00003;
    float yF = coords.y + camYFar;
    float streakF = tex2D(noiseTex, frac(float2(xF * 3.2, yF * 0.45 - uTime * 0.0045) + 0.13)).r;
    float edgeF = tex2D(noiseTex, frac(float2(xF * 1.1 + 0.71, uTime * 0.0022))).g;
    float topF = 0.34 - edgeF * 0.10;
    float bodyF = smoothstep(topF - 0.05, topF + 0.14, yF)
        * (1.0 - smoothstep(horizon, horizon + 0.06, coords.y));
    float veilF = bodyF * (0.30 + 0.70 * smoothstep(0.36, 0.86, streakF));

    //--- 中层 ---
    float xM = coords.x + uCamX * 0.00008;
    float yM = coords.y + camYMid;
    float streakM = tex2D(noiseTex, frac(float2(xM * 4.6, yM * 0.55 - uTime * 0.0075) + 0.47)).r;
    float edgeM = tex2D(noiseTex, frac(float2(xM * 1.6 + 0.29, uTime * 0.0031))).g;
    float topM = 0.26 - edgeM * 0.13;
    float bodyM = smoothstep(topM - 0.04, topM + 0.12, yM)
        * (1.0 - smoothstep(horizon, horizon + 0.05, coords.y));
    float veilM = bodyM * (0.26 + 0.74 * smoothstep(0.38, 0.88, streakM));

    //--- 近层：亮、高、锐 ---
    float xN = coords.x + uCamX * 0.00018;
    float yN = coords.y + camYNear;
    float streakN = tex2D(noiseTex, frac(float2(xN * 6.4, yN * 0.62 - uTime * 0.0110) + 0.83)).r;
    float edgeN = tex2D(noiseTex, frac(float2(xN * 2.3 + 0.55, uTime * 0.0042))).g;
    float topN = 0.15 - edgeN * 0.16;
    float bodyN = smoothstep(topN - 0.03, topN + 0.10, yN)
        * (1.0 - smoothstep(horizon, horizon + 0.04, coords.y));
    float veilN = bodyN * (0.22 + 0.78 * smoothstep(0.42, 0.90, streakN));

    //=
    //系统核心巨环：沉入墙后(y=0.30)，被中/近幕帘遮挡
    //=
    float2 cd = (coords - float2(0.66, 0.30)) * float2(aspect, 1.0);
    float cr = length(cd);
    float coreGlow = exp(-cr * cr * 90.0);
    float ringMain = 1.0 - smoothstep(0.011, 0.018, abs(cr - 0.085));
    float coreDot = 1.0 - smoothstep(0.016, 0.032, cr);
    float thetaC = atan2(cd.y, cd.x);
    //双段轨道弧：2 整数倍角，约 60s 一圈
    float orbPhase = frac((thetaC + uTime * 0.105) * 0.3183099);
    float orb = smoothstep(0.07, 0.15, orbPhase) * smoothstep(0.93, 0.85, orbPhase);
    float orbRing = (1.0 - smoothstep(0.005, 0.011, abs(cr - 0.118))) * orb;
    //幕帘透过率：墙越厚核心越暗
    float occlude = 1.0 - saturate(veilM * 0.55 + veilN * 0.75);
    col += (float3(0.30, 0.05, 0.05) * coreGlow * 0.50
        + float3(0.85, 0.18, 0.10) * ringMain * 0.80
        + float3(1.0, 0.38, 0.18) * coreDot * 0.65
        + float3(0.95, 0.30, 0.14) * orbRing * 0.55) * occlude;

    //=
    //幕帘着色：远→近 空气透视递增，顶缘热线
    //=
    col += float3(0.26, 0.030, 0.035) * veilF * 0.55;
    col += float3(0.50, 0.060, 0.055) * veilM * 0.80;
    col += float3(0.80, 0.120, 0.080) * veilN * 1.00;
    //顶缘热线：各层湍流上缘一条烫边
    float hotF = (1.0 - smoothstep(0.0, 0.016, abs(yF - topF))) * bodyF;
    float hotM = (1.0 - smoothstep(0.0, 0.014, abs(yM - topM))) * bodyM;
    float hotN = (1.0 - smoothstep(0.0, 0.012, abs(yN - topN))) * bodyN;
    col += float3(0.60, 0.12, 0.07) * hotF * 0.35;
    col += float3(0.85, 0.20, 0.10) * hotM * 0.50;
    col += float3(1.0, 0.32, 0.14) * hotN * 0.65;

    //=
    //死网线框残骸 ×2 层：per-cell 静态刚体旋转矩形线框，极慢漂移
    //=
    //--- 远层残骸 ---
    float2 duvF = float2(coords.x * aspect + uCamX * 0.00005 + uTime * 0.0008,
        coords.y + camYFar * 0.8);
    float2 dCellF = duvF * 3.0;
    float2 dIdF = floor(dCellF);
    float dHF = hash21(dIdF + 5.31);
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
    col += float3(0.30, 0.05, 0.05) * outlineF * dActiveF * zoneF * 0.30;

    //--- 近层残骸：更大更亮 ---
    float2 duvN = float2(coords.x * aspect + uCamX * 0.00022 - uTime * 0.0013,
        coords.y + camYNear * 0.8);
    float2 dCellN = duvN * 1.7;
    float2 dIdN = floor(dCellN);
    float dHN = hash21(dIdN + 11.57);
    float dActiveN = step(0.72, dHN);
    float2 dLocN = frac(dCellN) - 0.5;
    float angN = dHN * 6.28318;
    float caN = cos(angN);
    float saN = sin(angN);
    float2 rpN = float2(dLocN.x * caN - dLocN.y * saN, dLocN.x * saN + dLocN.y * caN);
    float2 halfN = float2(0.11 + hash21(dIdN + 2.9) * 0.15, 0.05 + hash21(dIdN + 7.3) * 0.09);
    float2 adN = abs(rpN) - halfN;
    float outlineN = 1.0 - smoothstep(0.003, 0.008, abs(max(adN.x, adN.y)));
    //内部对角短线：残骸不是空框
    float diagN = (1.0 - smoothstep(0.004, 0.010, abs(rpN.x - rpN.y)))
        * step(max(adN.x, adN.y), -0.01);
    float zoneN = 1.0 - smoothstep(horizon - 0.10, horizon, coords.y);
    col += float3(0.58, 0.10, 0.08) * (outlineN + diagN * 0.4) * dActiveN * zoneN * 0.50;

    //=
    //地板：黑虚空里的残缺网格补丁(降级)，地平辉光收窄
    //=
    float py = coords.y - horizon;
    float floorMask = smoothstep(0.006, 0.05, py);
    float persp = 1.0 / max(py, 0.004);
    float vx = (coords.x - 0.5) * persp * 0.75 + uCamX * 0.00012;
    float vd = abs(frac(vx) - 0.5);
    float vLine = smoothstep(0.455, 0.5, vd);
    float hz = frac(persp * 0.42 + uTime * 0.018);
    float hLine = smoothstep(0.88, 1.0, hz);
    //补丁遮罩：大部分地板沉入黑暗
    float2 patchId = floor(float2(vx * 0.30, persp * 0.13));
    float patch = step(0.58, hash21(patchId + 4.9));
    float gridGlow = (vLine * 0.55 + hLine * 0.45) * floorMask * patch;
    col += float3(0.26, 0.045, 0.045) * gridGlow * 0.20;

    float hgl = 1.0 - smoothstep(0.0, 0.028, abs(py));
    col += float3(0.32, 0.065, 0.055) * hgl * 0.30;

    //=
    //数据星尘(上半天空，静态位置，≥2min 去同相明灭)
    //=
    float2 sUV = coords * float2(aspect, 1.0) * 6.0 + float2(uCamX * 0.00001, 0.0);
    float2 sId = floor(sUV);
    float sH = hash21(sId);
    float2 sOff = float2(hash21(sId + 1.3), hash21(sId + 2.6)) - 0.5;
    float sDist = length(frac(sUV) - 0.5 - sOff * 0.55);
    float star = (1.0 - smoothstep(0.015, 0.045, sDist)) * step(0.80, sH);
    float twinkle = 0.70 + 0.30 * sin(sH * 6.28318 + uTime * 0.05);
    star *= twinkle * (1.0 - smoothstep(0.35, 0.55, coords.y));
    col += float3(0.60, 0.13, 0.09) * star * 0.40;

    //预乘输出
    float a = saturate(uPresence);
    return float4(col * a, a);
}

technique Technique1
{
    pass CyberDomainSkyPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
