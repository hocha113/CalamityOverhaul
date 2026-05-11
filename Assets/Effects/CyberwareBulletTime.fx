// ============================================================================
// CyberwareBulletTime.fx —— 义体雷达子弹时间专属全屏滤镜
// 设计目标：
//   - 替代旧的"纯色 + 横条 + 扫描线"CPU 堆叠版滤镜，改用程序生成的高级合成
//   - 视觉语言：冷色 HUD 凝固感，让"世界被钉住"的感官立刻成立
//   - 视线引导：以雷达锚点（屏幕中央偏下）为原点，向四周扩散的低频时间波 + 径向暗角
//   - 体感：电影化的 letterbox 上下软幅、低强度胶片颗粒、淡入的画面四角 HUD 括号
//
// 参数说明：
//   uResolution     屏幕像素尺寸
//   uCenter         雷达锚点在屏幕上的像素坐标（时间波的原点 / 视线引导中心）
//   uTime           累计真实时间（秒），冻结期间由本机控制器自己推进
//   uAlpha          全局不透明度（0~1），由雷达 OpenProgress 驱动
//   uOpenProgress   入场进度（0~1），用于让"波纹 / 括号 / 条带"分级淡入
//   uHudColor       全局 HUD 蓝青色调；与雷达背板配色保持同一色系，避免冲突
//
// 渲染方式：sb.Begin(Immediate, AlphaBlend, ..., effect)；
//   一张 1x1 占位贴图缩放到全屏即可，无需采样 backbuffer
// 输出模式：预乘 alpha（与项目内 HackRamArc / CyberDomainPanel 一致）
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uOpenProgress;
float2 uResolution;
float2 uCenter;
float3 uHudColor;

//------------------ 工具函数 ------------------

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//------------------ 主像素着色 ------------------

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vcol : COLOR0) : COLOR0
{
    float2 p = uv * uResolution;
    float2 d = p - uCenter;
    float r = length(d);
    float ang = atan2(d.y, d.x);

    //归一化半径：用屏幕对角线作为分母，让 16:9 / 4:3 等不同比例都能保持一致的暗角节奏
    float screenDiag = length(uResolution);
    float rNorm = r / max(screenDiag, 1.0);

    //入场进度的非线性 ease，三次方让前 1/3 阶段几乎不可见，后段快速到位
    float ease = saturate(uOpenProgress);
    ease = 1.0 - pow(1.0 - ease, 3.0);

    //==================================================
    // 1) 冷色底层（极薄，让整屏先笼罩一层"系统介入"的青蓝）
    //==================================================
    float3 baseTint = float3(0.014, 0.040, 0.072);
    float baseAlpha = 0.22 * uAlpha;

    //==================================================
    // 2) 径向暗角：以雷达锚点为原点的渐进压暗
    //    rNorm < 0.05 时几乎透明（让玩家视线落在雷达上），
    //    rNorm > 0.55 后快速变暗形成"聚光灯"效果
    //==================================================
    float vignette = smoothstep(0.06, 0.58, rNorm);
    //四角额外暗角，让屏幕四个角落保持沉浸黑边
    float corner = smoothstep(0.50, 0.95, rNorm);
    float vignetteFinal = saturate(vignette + corner * 0.45);
    float3 vignetteTint = float3(0.002, 0.010, 0.022);
    float vignetteAlpha = vignetteFinal * 0.40 * uAlpha;

    //==================================================
    // 3) 时间波：从雷达原点向外扩散的低频同心环
    //    冻结期间 GameUpdateCount 不推进，本 shader 用上层传入的实时 uTime
    //    所以波纹依旧动起来，传达"时间仍在流逝，只是世界被钉住"的反差
    //==================================================
    float waveSpeed1 = 95.0;
    float waveSpeed2 = 58.0;
    float wave1 = sin((r - uTime * waveSpeed1) * 0.0175) * 0.5 + 0.5;
    float wave2 = sin((r - uTime * waveSpeed2) * 0.0225 + 1.4) * 0.5 + 0.5;
    //叠加两条不同速度的波，避免单一频率显得机械
    float wave = (wave1 * 0.62 + wave2 * 0.38);
    //径向衰减，远离锚点的波纹自然消退
    float waveFalloff = exp(-rNorm * 1.50);
    //入场期间波纹强度跟随 ease 推进，初帧不会突兀
    wave = wave * waveFalloff * ease;
    float3 waveTint = uHudColor * 0.70;
    float waveAlpha = wave * 0.13 * uAlpha;

    //==================================================
    // 4) 径向流光：从锚点向四周发散的"时间被拉伸"暗示线
    //    按角度做哈希采样：稀疏出现 / 缓慢生灭，避免满屏放射状嘈杂感
    //==================================================
    float streakSeed = floor(ang * 14.0);
    float streakRand = hash21(float2(streakSeed, 13.7));
    //出现概率约 18%，保证 80% 角度方向是干净的
    float streakProb = step(0.82, streakRand);
    //各条流光独立的生灭周期
    float streakPulse = 0.5 + 0.5 * sin(uTime * (0.45 + streakRand * 0.6) + streakSeed * 2.7);
    //只在中环（不太靠近锚点、也不到屏幕边缘）出现，避免视线被严重拉走
    float streakNearMask = smoothstep(140.0, 320.0, r);
    float streakFarMask = 1.0 - smoothstep(0.50, 0.85, rNorm);
    float streak = streakProb * streakPulse * streakNearMask * streakFarMask;
    float3 streakTint = uHudColor;
    float streakAlpha = streak * 0.075 * uAlpha * ease;

    //==================================================
    // 5) 顶/底软幅：电影 letterbox 的克制感
    //    使用 smoothstep 让边界平滑过渡，避免硬切的字幕条质感
    //==================================================
    float bandH = 110.0;
    float bandTop = 1.0 - smoothstep(0.0, bandH, p.y);
    float bandBottom = 1.0 - smoothstep(0.0, bandH, uResolution.y - p.y);
    float bandMask = saturate(bandTop + bandBottom);
    float3 bandTint = float3(0.003, 0.012, 0.024);
    float bandAlpha = bandMask * 0.45 * uAlpha;

    //==================================================
    // 6) 四角 HUD 角标：极克制的青色短弧，传递"系统就位"的提示
    //    用 min(x, screenW-x) / min(y, screenH-y) 做角点距离，
    //    再叠加 ang 维度的 fade，仅在四个角落形成 L 型亮带
    //==================================================
    float2 cornerVec = min(p, uResolution - p);
    float cornerNear = exp(-min(cornerVec.x, cornerVec.y) / 28.0);
    //L 型方向偏置：只在水平或垂直接近的方向显现，让 L 形成"括号"质感
    float cornerL = step(8.0, cornerVec.y - cornerVec.x)
                  + step(8.0, cornerVec.x - cornerVec.y);
    cornerL = saturate(cornerL);
    float3 cornerTint = uHudColor;
    float cornerAlpha = cornerNear * cornerL * 0.32 * uAlpha * ease;

    //==================================================
    // 7) 极淡的胶片颗粒：让画面保持"生动"，不至于像静态贴图
    //    噪声以时间做高频抖动，与 uTime 同步
    //==================================================
    float grainRand = hash21(p * 0.5 + frac(uTime * 23.7) * 173.0);
    float grain = (grainRand - 0.5);
    float grainAlpha = grain * 0.045 * uAlpha;

    //==================================================
    // 8) 雷达锚点附近的"开放圈"：明确告诉玩家"操作发生在这里"
    //    一道很轻的内亮外暗的环，半径与雷达活动圈大致吻合（160~220px）
    //==================================================
    float anchorD = abs(r - 190.0);
    float anchorRing = exp(-pow(anchorD / 18.0, 2.0)) * ease;
    float3 anchorTint = uHudColor;
    float anchorAlpha = anchorRing * 0.10 * uAlpha;

    //==================================================
    // 合成（预乘 alpha）
    //   先把每层的"贡献色 = tint * alpha"累加为颜色项；
    //   再把所有层的 alpha 单独累加为 alpha 项，最终保留独立的 alpha 通道
    //==================================================
    float3 col = baseTint * baseAlpha
               + vignetteTint * vignetteAlpha
               + waveTint * waveAlpha
               + streakTint * streakAlpha
               + bandTint * bandAlpha
               + cornerTint * cornerAlpha
               + anchorTint * anchorAlpha;
    float a = baseAlpha + vignetteAlpha + waveAlpha + streakAlpha
            + bandAlpha + cornerAlpha + anchorAlpha + grainAlpha;
    a = saturate(a);

    return float4(col, a) * vcol;
}

technique Technique1
{
    pass CyberwareBulletTimePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
