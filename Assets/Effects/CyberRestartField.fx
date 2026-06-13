// ============================================================================
// CyberRestartField.fx 赛博领域系统重启演出
// 撕裂→收缩→奇点→炸裂四阶段；s0+s1 噪声；Additive 四边形
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;          //累计动画时间
float tearK;          //撕裂阶段权重 0..1
float collapseK;      //收缩阶段权重 0..1
float singularityK;   //奇点阶段权重 0..1
float burstK;         //炸裂阶段权重 0..1
float crackSeed;      //本次重启随机种子
float globalAlpha;    //整体淡入淡出

static const float PI = 3.14159265;
static const float TAU = 6.28318530;

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

//沿径向叠加多频脊线噪声，得到流动感纹理
float ridge(float2 uv)
{
    float a = tex2D(noiseTex, uv).r;
    return 1.0 - abs(a * 2.0 - 1.0);
}

//黑墙裂缝（极坐标角度切槽 + 噪声扰动 + 径向衰减）
//黑墙裂缝：用平滑随机角度替代等分槽，得到非周期、纤细如剃刀的裂缝线
//返回 (核心红芯, 黑墙轮廓, 末端高光)
float3 cracks(float2 centered, float dist, float angle)
{
    const float CrackCount = 5.0;
    float ang01 = angle / TAU + 0.5;
    //slot 仍按等分查找最近的两条裂缝候选，但每条裂缝有独立角度偏移，避免硬扇形周期感
    float slot = ang01 * CrackCount + crackSeed * 11.0;
    float idx = floor(slot);

    //叠加多条候选裂缝（当前槽 + 左右相邻槽），加权后取最近，抹平槽边界
    float coreSum = 0.0;
    float wallSum = 0.0;
    float tipSum = 0.0;

    [unroll] for (int k = -1; k <= 1; k++)
    {
        float si = idx + (float)k;
        //hash 输入按 CrackCount 取模，让左右接缝处取到同一条裂缝种子
        float siMod = si - CrackCount * floor(si / CrackCount);
        float seed = hash11(siMod * 1.37 + crackSeed * 17.0);
        //每条裂缝在槽内有 ±0.45 的角度漂移，避免均匀分布
        float drift = (seed - 0.5) * 0.9;
        //裂缝在该槽内的中线位置
        float center = (si + 0.5 + drift) / CrackCount;//[0,1]
        float dAng = ang01 - center;
        //角向距离折叠到 [-0.5, 0.5]
        dAng = dAng - floor(dAng + 0.5);
        //仅相邻槽内才考虑（简单距离剔除）
        if (abs(dAng) > 1.0 / CrackCount) continue;

        //每条裂缝的生长延迟与进度
        float delay = seed * 0.55;
        float local = saturate((tearK - delay) / max(0.0001, 1.0 - delay));
        float grow = 1.0 - pow(1.0 - local, 2.6);
        //收缩阶段被吸回中心，使用更线性的回拉曲线避免突然消失
        float retract = saturate(1.0 - pow(collapseK, 0.7));
        float reach = grow * retract;
        if (reach <= 0.0001) continue;

        //裂缝径向折线扰动：低频 + 高频混合
        float jLow = tex2D(noiseTex, float2(dist * 1.4 - uTime * 0.25, seed * 1.3)).g;
        float jHi = tex2D(noiseTex, float2(dist * 4.8 + uTime * 0.5, seed * 2.7 + 0.5)).b;
        float curve = (jLow - 0.5) * 0.045 + (jHi - 0.5) * 0.020 * sin(dist * 9.0 + seed * 7.0);
        //角向距离换算为线性偏移，单位与 dAng 相同
        float lateral = dAng - curve;

        //径向半宽：保持纤细，仅在中段轻微鼓起，端点收针
        float radK = saturate(dist / max(reach, 0.001));
        float taper = sin(radK * PI);
        //核心宽度小于槽宽 4%，黑墙宽度仅 12%，杜绝扇形切割观感
        float halfCore = (0.0035 + 0.012 * taper) * (0.7 + 0.3 * grow);
        float halfWall = halfCore * 2.6;

        //长度限制
        float withinLen = smoothstep(reach + 0.02, reach - 0.02, dist);
        float aLat = abs(lateral);
        float coreMask = smoothstep(halfCore, 0.0, aLat) * withinLen;
        float wallMask = smoothstep(halfWall, halfCore * 0.6, aLat) * withinLen;

        //末端高光
        float tipBand = smoothstep(reach * 0.88, reach, dist) * (1.0 - smoothstep(reach, reach + 0.04, dist));
        float tipPulse = 0.5 + 0.5 * sin(uTime * 9.0 + seed * 13.0);
        float tipMask = tipBand * coreMask * (0.4 + 0.6 * tipPulse);

        coreSum = max(coreSum, coreMask);
        wallSum = max(wallSum, wallMask);
        tipSum = max(tipSum, tipMask);
    }

    //外缘 0.95-1.05 软衰减
    float outerFade = 1.0 - smoothstep(0.92, 1.05, dist);
    return float3(coreSum, wallSum, tipSum) * outerFade;
}

//大尺度湍流黑墙：跨越整个领域的不规则黑色团块，强化"被撕开的黑墙数据" 氛围
//返回 (黑墙浓度, 暗红高光)
float2 darkWallTurbulence(float2 centered, float dist, float angle)
{
    //演出权重：撕裂段缓起，收缩段达到峰值，奇点/炸裂衰退
    float w = saturate(tearK * 0.65 + collapseK * 0.85 - singularityK * 0.4 - burstK * 0.85);
    if (w <= 0.0) return 0.0;

    //极坐标采样：低频湍流（角向 + 径向缓慢推进）
    //角向乘以整数倍频，确保 ang01=0 与 ang01=1 在接缝处采样一致，避免左侧水平线像素跳跃
    float ang01 = angle / TAU + 0.5;
    float2 uv0 = float2(ang01 * 2.0 + uTime * 0.04, dist * 0.55 - uTime * 0.07);
    float2 uv1 = float2(ang01 * 3.0 - uTime * 0.06, dist * 1.1 + uTime * 0.05);
    float n0 = tex2D(noiseTex, uv0 + crackSeed * 0.3).r;
    float n1 = tex2D(noiseTex, uv1 + crackSeed * 0.7).g;
    float field = n0 * 0.65 + n1 * 0.45;

    //阈值化得到大块"黑墙"，边缘羽化避免硬切
    float wallSoft = smoothstep(0.55, 0.85, field);
    //局部脊线给一些暗红边光
    float ridgeN = 1.0 - abs(field * 2.0 - 1.0);
    float ember = smoothstep(0.78, 1.0, ridgeN);

    //径向遮罩：内 0.10 留白避开核心，外 0.95 软淡出
    float radialMask = smoothstep(0.10, 0.25, dist) * (1.0 - smoothstep(0.92, 1.05, dist));
    return float2(wallSoft, ember) * radialMask * w;
}

//收缩阶段：径向向内的数据流细带 + 螺旋渐暗涡
float3 collapseStreams(float dist, float angle)
{
    float w = collapseK;
    if (w <= 0.0) return 0.0;

    //螺旋角：让径向条带跟随角度产生螺线感，同时随时间向内推进
    //以 ang01×3 作为角向采样频率，保证接缝处（ang01=0 与 1）在采样空间为整数倍距离，水平接缝消失
    float ang01s = angle / TAU + 0.5;
    float spin = ang01s * 3.0 + uTime * 0.25 + crackSeed * 0.8;
    //径向相位：dist 越大相位越落后，模拟"外缘起步、向内追赶"
    float radPhase = dist * 14.0 - uTime * 6.0 * (0.4 + 0.8 * w);
    float ribbon = ridge(float2(spin, radPhase * 0.05));
    ribbon = pow(saturate(ribbon), 2.4);

    //径向条带衰减：center 处归零（已被奇点吃掉）、外缘随阶段进度回收
    float outerLimit = lerp(1.05, 0.05, pow(w, 1.5));//收缩末段把外径压到接近核心
    float radial = smoothstep(0.02, 0.10, dist) * (1.0 - smoothstep(outerLimit - 0.10, outerLimit, dist));

    //角向高频细栅
    float fine = tex2D(noiseTex, float2(spin * 1.6, dist * 2.6 + uTime * 0.3)).b;
    fine = smoothstep(0.55, 0.95, fine);

    float streams = ribbon * radial * (0.35 + 0.55 * fine) * w * 0.7;

    //引力涡环：用更柔的环带替代之前的明显黑环
    float ringR = lerp(0.92, 0.05, pow(w, 1.3));
    float ringDist = abs(dist - ringR);
    float ringMask = smoothstep(0.06, 0.0, ringDist) * w * 0.6;

    return float3(streams, ringMask, 0.0);
}

//奇点核心：竖直窄裂口 + 内部抖动 + 引力透镜暗环
float3 singularityCore(float2 centered, float dist)
{
    float w = singularityK;
    if (w <= 0.0) return 0.0;

    //核心整体尺寸：随 w 起伏、随 burstK 急速塌缩；放大约一倍并加入低频呼吸
    float pulse = 0.85 + 0.15 * sin(uTime * 12.0 + crackSeed * 9.0);
    float breath = 0.92 + 0.08 * sin(uTime * 3.5 + crackSeed * 5.0);
    float coreSize = lerp(0.17, 0.33, w) * pulse * breath;
    float burstShrink = 1.0 - burstK;
    coreSize *= burstShrink;

    //垂直窄裂口的有向距离场：x 越小越靠裂缝中线、y 决定裂口长度
    float halfH = coreSize * 1.4;
    float halfW = coreSize * 0.18;
    //裂口轻微抖动
    float jx = (tex2D(noiseTex, float2(centered.y * 4.0 + uTime * 0.7, crackSeed)).r - 0.5) * 0.012;
    float dx = abs(centered.x - jx);
    float dy = abs(centered.y);
    float slitX = smoothstep(halfW, 0.0, dx);
    float slitY = smoothstep(halfH, halfH * 0.85, dy);//端点更柔
    float slit = slitX * (1.0 - smoothstep(halfH * 1.0, halfH * 1.15, dy));

    //核心黑色椭圆基底（软）
    float2 ellip = centered / float2(coreSize * 0.55, coreSize * 1.4);
    float core = 1.0 - smoothstep(0.85, 1.05, length(ellip));

    //外缘引力透镜暗环
    float lensR = coreSize * 2.6;
    float lensDist = abs(dist - lensR);
    float lens = smoothstep(coreSize * 1.0, 0.0, lensDist) * w;

    //横向短闪偶发
    float jitter = sin(uTime * 18.0 + crackSeed * 23.0);
    float crossBand = step(0.55, jitter) * smoothstep(coreSize * 4.0, coreSize * 1.0, dist) * smoothstep(0.012, 0.0, abs(centered.y));

    //x: 黑底 y: 红裂口 z: 透镜环
    return float3(core * w, slit * w + crossBand * 0.6, lens);
}

//炸裂阶段：放射激波 + 同心冲击环
float3 burstField(float dist, float angle)
{
    float w = burstK;
    if (w <= 0.0) return 0.0;

    //放射纹：高频角向脊线乘径向能量曲线
    //角向采样使用 ang01 = angle/TAU + 0.5（[0,1]），并乘以整数倍数 2.0，确保接缝处（ang01=0 与 1）采样一致，消除水平白带
    float ang01 = angle / TAU + 0.5;
    float rays = ridge(float2(ang01 * 2.0 + crackSeed, dist * 0.25 - uTime * 2.0));
    rays = pow(saturate(rays), 1.8);
    //径向能量在 0.05~1.0 之间从 1 到 0 平滑下降，并随 burstK 平移
    float radial = smoothstep(0.0, 0.15, dist) * (1.0 - smoothstep(0.7 + 0.3 * w, 1.0 + 0.3 * w, dist));
    float rayMask = rays * radial * w;

    //冲击环：从 0.1 扩张到 1.0
    float ringR = lerp(0.05, 1.0, smoothstep(0.0, 1.0, w));
    float ringDist = abs(dist - ringR);
    float ringW = 0.04 + 0.05 * (1.0 - w);
    float ring = smoothstep(ringW, 0.0, ringDist) * (1.0 - w * 0.6);

    //中心白热盘：在炸裂前 30% 达到峰值
    float flash = 0.0;
    if (w < 0.35) flash = pow(1.0 - w / 0.35, 1.4);
    float flashMask = flash * (1.0 - smoothstep(0.0, 0.18, dist));

    return float3(rayMask, ring, flashMask);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //把 [0,1] 映射到 [-1,1]，并求极坐标
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    if (dist > 1.05) return 0.0;

    //四阶段累积色
    float3 cracksRGB = cracks(centered, dist, angle);
    float2 wallRGB = darkWallTurbulence(centered, dist, angle);
    float3 collapseRGB = collapseStreams(dist, angle);
    float3 coreRGB = singularityCore(centered, dist);
    float3 burstRGB = burstField(dist, angle);

    //调色：核心暗红 #d8221c，黑墙 #1a0306，热边 #ff7a4f，冷暗 #050103
    float3 hotRed = float3(1.0, 0.40, 0.22);
    float3 coreRed = float3(0.85, 0.10, 0.10);
    float3 wall = float3(0.05, 0.01, 0.02);
    float3 emberRed = float3(0.55, 0.05, 0.06);
    float3 white = float3(1.0, 0.92, 0.78);

    float3 col = 0.0;
    //大尺度黑墙湍流：作为底层氛围色，先于裂缝叠加，避免与裂缝硬切产生周期感
    col += wall * wallRGB.x * 0.55;
    col += emberRed * wallRGB.y * 0.65;

    //裂缝：纤细的黑墙轮廓 + 红芯 + 末端高光，权重压低避免主导画面
    col += wall * cracksRGB.y * 0.9;
    col += coreRed * cracksRGB.x * 0.85;
    col += hotRed * cracksRGB.z * 1.2;

    //收缩：暗红流条 + 柔和黑环
    col += coreRed * collapseRGB.x * 0.7;
    col += hotRed * collapseRGB.x * 0.3;
    col += wall * collapseRGB.y * 1.0;

    //奇点：黑底压底 + 红裂芯 + 透镜暗环
    col -= wall * coreRGB.x * 1.4;//相当于在加法混合下抑制亮度，制造"黑洞感"
    col += coreRed * coreRGB.y * 1.3;
    col += hotRed * coreRGB.y * 0.5;
    col += wall * coreRGB.z * 0.8;

    //炸裂：白热放射 + 暖环 + 中心闪
    col += hotRed * burstRGB.x * 0.85;
    col += white * burstRGB.x * 0.35;
    col += white * burstRGB.y * 0.85;
    col += hotRed * burstRGB.y * 0.55;
    col += white * burstRGB.z * 1.0;

    //领域整体红雾：四阶段权重之和的弱底色，让"领域被点燃"
    float ambient = saturate(tearK * 0.25 + collapseK * 0.4 + singularityK * 0.45 + burstK * 0.35);
    col += coreRed * ambient * 0.10 * (1.0 - dist * 0.8);

    //外缘羽化
    float fade = 1.0 - smoothstep(0.95, 1.05, dist);
    col *= fade * globalAlpha;

    //加法混合下使用 RGB 强度作为 alpha
    float a = saturate(max(max(col.r, col.g), col.b)) * globalAlpha;
    return float4(col, a) * vertexColor;
}

technique Technique1
{
    pass CyberRestartFieldPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
