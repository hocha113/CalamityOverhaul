// ============================================================================
//CyberDomainSky.fx 赛博领域 L3 专属天空(红黑数据深渊)
//全覆盖预乘天幕，s1 绑 PerlinNoise(LinearWrap)；直线算术 + 普通 tex2D
//
//元素：深渊竖向渐变 + 噪声云翳 / 透视数据地平网格(极慢滚动) /
//悬浮巨构剪影列(uCamX 视差 + 稀疏亮窗) / 高空系统核心巨环(双轨弧慢转) /
//上半天稀疏数据星尘(静态 hash 位置 + 2 分钟级去同相明灭) / 地平辉光线
//
//常驻舒适约定：无快速脉动；唯一 sin 是星尘 ≥2min 周期去同相明灭。
//极角约束：唯一 atan2 在系统核心，消费者 frac((θ+t)·2/2π)——2∈ℤ 跨 ±π 连续。
//色板红黑系，与 CybCourseSky(深蓝 Draedon 系)刻意错开。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;
float uPresence;        //0~1 天空在场强度
float2 uScreenSize;     //视口像素尺寸
float uCamX;            //真实相机世界X，远景视差用

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float aspect = uScreenSize.x / max(uScreenSize.y, 1.0);
    float horizon = 0.62;

    //=
    //深渊底色：顶部近黑微红 → 地平暗红，地平以下再沉
    //=
    float3 cTop     = float3(0.012, 0.003, 0.006);
    float3 cHorizon = float3(0.115, 0.022, 0.030);
    float3 cBelow   = float3(0.045, 0.010, 0.014);
    float upT = saturate(coords.y / horizon);
    float3 col = lerp(cTop, cHorizon, upT * upT);
    float belowT = saturate((coords.y - horizon) / (1.0 - horizon));
    col = lerp(col, cBelow, smoothstep(0.0, 0.55, belowT));

    //噪声云翳：大尺度极慢漂移的暗红斑驳
    float neb = tex2D(noiseTex, frac(coords * float2(1.3, 0.9)
        + float2(uTime * 0.0012, uTime * 0.0007))).r;
    col *= 0.82 + 0.36 * neb;

    //=
    //透视数据地平网格(地平以下)
    //=
    float py = coords.y - horizon;
    float floorMask = smoothstep(0.006, 0.05, py);
    float persp = 1.0 / max(py, 0.004);
    //纵向汇聚线(随相机极缓视差)
    float vx = (coords.x - 0.5) * persp * 0.75 + uCamX * 0.00004;
    float vd = abs(frac(vx) - 0.5);
    float vLine = smoothstep(0.455, 0.5, vd);
    //横向线：向观察者极慢推进
    float hz = frac(persp * 0.42 + uTime * 0.022);
    float hLine = smoothstep(0.88, 1.0, hz);
    float gridGlow = (vLine * 0.55 + hLine * 0.45) * floorMask;
    col += float3(0.30, 0.05, 0.05) * gridGlow * 0.32;

    //地平辉光线
    float hgl = 1.0 - smoothstep(0.0, 0.045, abs(py));
    col += float3(0.35, 0.07, 0.06) * hgl * 0.40;

    //=
    //悬浮巨构剪影列(仅地平以上，暗于天空 + 稀疏亮窗)
    //=
    float mx = coords.x + uCamX * 0.000016;
    float mCol = floor(mx * 7.0);
    float colLocal = frac(mx * 7.0);
    float mH = hash21(float2(mCol, 3.1));
    float mActive = step(0.52, mH);
    float mTop = 0.14 + mH * 0.20;
    float mBot = mTop + 0.16 + hash21(float2(mCol, 7.7)) * 0.20;
    float wHalf = 0.11 + hash21(float2(mCol, 5.5)) * 0.09;
    float mono = mActive
        * step(mTop, coords.y) * step(coords.y, min(mBot, horizon))
        * step(abs(colLocal - 0.5), wHalf);
    //剪影压暗
    col = lerp(col, float3(0.010, 0.003, 0.005), mono * 0.80);
    //亮窗：静态 hash 点阵
    float2 winId = floor(float2(colLocal * 30.0, coords.y * 64.0));
    float win = step(0.966, hash21(winId + mCol * 13.7));
    col += float3(0.55, 0.10, 0.08) * win * mono * 0.55;

    //=
    //系统核心巨环(高空，双轨弧慢转)
    //=
    float2 cd = (coords - float2(0.70, 0.22)) * float2(aspect, 1.0);
    float cr = length(cd);
    float coreGlow = exp(-cr * cr * 90.0);
    float ringMain = 1.0 - smoothstep(0.011, 0.018, abs(cr - 0.085));
    float coreDot = 1.0 - smoothstep(0.016, 0.032, cr);
    float thetaC = atan2(cd.y, cd.x);
    //双段轨道弧：2 整数倍角，约 60s 一圈
    float orbPhase = frac((thetaC + uTime * 0.105) * 0.3183099);
    float orb = smoothstep(0.07, 0.15, orbPhase) * smoothstep(0.93, 0.85, orbPhase);
    float orbRing = (1.0 - smoothstep(0.005, 0.011, abs(cr - 0.118))) * orb;
    col += float3(0.30, 0.05, 0.05) * coreGlow * 0.55;
    col += float3(0.85, 0.18, 0.10) * ringMain * 0.85;
    col += float3(1.0, 0.38, 0.18) * coreDot * 0.70;
    col += float3(0.95, 0.30, 0.14) * orbRing * 0.60;

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
    star *= twinkle * (1.0 - smoothstep(0.40, 0.60, coords.y));
    col += float3(0.70, 0.16, 0.10) * star * 0.55;

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
