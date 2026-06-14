// ============================================================================
//SignalTowerVirusBroadcast.fx
//信号塔病毒广播电磁波
//核心：一道高清晰度的主冲击环 + 内缩谐波次环 + 径向闪电分支 + 六边形电磁网格
//程序化生成，搭配Immediate+Additive绘制到大型四边形，不做纹理采样
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float waveProgress;     //0~1，主环归一化半径
float waveThickness;    //主环厚度（归一化）
float fadeAlpha;        //整体淡出 0~1
float seed;             //随机扰动种子

//程序化哈希：使用2D向量输入以消除atan2在左侧x轴的奇点跳跃
float hash21(float2 p) {
    p = frac(p * float2(443.8975, 397.2973) + seed * 17.13);
    p += dot(p, p + 19.19);
    return frac(p.x * p.y);
}

//以2D方向向量采样的值噪声：无缝环绕
float dirValueNoise(float2 dir, float freq, float phase) {
    //将方向分量投到高频格点，dir本身连续 → 左侧不再跳跃
    float2 uv = dir * freq + float2(phase, phase * 0.37);
    float2 i = floor(uv);
    float2 f = frac(uv);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

//分形叠加闪电纹路：多层低频偏移 → 天然锯齿感
float lightningNoise(float2 dir, float phase) {
    float n = 0.0;
    float amp = 0.5;
    float freq = 6.0;
    for (int i = 0; i < 4; i++) {
        n += dirValueNoise(dir, freq, phase + i * 7.31) * amp;
        amp *= 0.55;
        freq *= 2.0;
    }
    return n;
}

//六边形距离场：让内部呈现蜂巢能量网格
float hexGrid(float2 p) {
    //六边形瓷砖的经典技巧：坐标变换后取距最近格心的距离
    p *= float2(1.0, 1.15470054);//1/sin60
    float2 h = float2(0.5, 0.5);
    float2 a = fmod(p, 1.0) - h;
    float2 b = fmod(p + h, 1.0) - h;
    float2 nearest = dot(a, a) < dot(b, b) ? a : b;
    return length(nearest);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    //方向向量（单位化），连续且无跳跃
    float2 dir = dist > 1e-4 ? centered / dist : float2(1.0, 0.0);

    //主环扰动：保持波前呈能量脉动，但不再引入atan2奇点
    float edgeNoise1 = dirValueNoise(dir, 7.0, uTime * 1.8);
    float edgeNoise2 = dirValueNoise(dir, 18.0, uTime * 3.1 + 4.7);
    float noiseDisp = ((edgeNoise1 * 0.65 + edgeNoise2 * 0.35) - 0.5) * 0.035;
    float adjDist = dist + noiseDisp;

    //主冲击环：窄而锋利
    float mainRingDist = abs(adjDist - waveProgress);
    float mainCore = 1.0 - smoothstep(0.0, waveThickness * 0.35, mainRingDist);
    float mainSoft = 1.0 - smoothstep(0.0, waveThickness, mainRingDist);
    //近场瞬爆：给主环前缘一道极亮的核心线
    float mainPulse = exp(-mainRingDist * 90.0);

    //内缩谐波次环：三道间距逐渐收窄的子环，强化电磁波扩散感
    float harmonics = 0.0;
    [unroll]
    for (int k = 1; k <= 3; k++) {
        float subR = waveProgress - waveThickness * (1.6 * k);
        if (subR > 0.02) {
            float sd = abs(adjDist - subR);
            float weight = 1.0 / (k + 1.0);
            harmonics += (1.0 - smoothstep(0.0, waveThickness * 0.28 / k, sd)) * weight;
        }
    }

    //径向闪电分支：基于方向向量的分形噪声切出若干电弧
    float bolt = lightningNoise(dir * 2.3, uTime * 1.6);
    float boltCore = smoothstep(0.78, 0.96, bolt);
    //二次扰动让主干上再长出枝杈
    float boltFork = smoothstep(0.68, 0.82, bolt) * dirValueNoise(dir, 30.0, uTime * 4.0);
    //闪电只在主环内侧、尚未离开的活跃区域生效
    float innerActive = step(adjDist, waveProgress + waveThickness * 0.3)
                      * step(waveProgress * 0.18, adjDist);
    //随径向距离做闪烁淡出，避免充满整个区域
    float boltRadialFade = smoothstep(0.0, waveProgress, adjDist) *
                           (1.0 - smoothstep(waveProgress - waveThickness * 0.5, waveProgress + waveThickness, adjDist));
    float lightning = (boltCore + boltFork * 0.55) * innerActive * boltRadialFade;

    //电磁六边形网格：微弱底色，提供高科技质感又不遮挡外环
    float2 gridUV = float2(dir.x, dir.y) * dist * 14.0 + float2(0.0, uTime * 0.4);
    float hexD = hexGrid(gridUV);
    float hexLine = smoothstep(0.03, 0.00, hexD - 0.43);
    //网格仅在主环内部、且半径小于波前的区域显示
    float hexMask = step(adjDist, waveProgress - waveThickness * 0.2)
                  * smoothstep(0.0, waveProgress * 0.6, adjDist);
    float hexGlow = hexLine * hexMask * 0.45;

    //外围破碎拖尾：主环之外的电离残留
    float trailDist = adjDist - waveProgress;
    float trail = smoothstep(waveThickness * 2.2, 0.0, max(trailDist, 0.0));
    float trailNoise = dirValueNoise(dir, 34.0, uTime * 2.4);
    trail *= step(0.55, trailNoise) * 0.6;

    //最内侧中心清空：避免整体糊成一坨
    float centerClear = smoothstep(0.0, 0.08, adjDist);
    harmonics *= centerClear;
    lightning *= centerClear;
    hexGlow *= centerClear;

    //配色：冷白电弧核心 + 紫蓝中段 + 洋红外缘
    float3 hotCore  = float3(0.98, 0.95, 1.00);
    float3 coolArc  = float3(0.55, 0.80, 1.00);
    float3 violet   = float3(0.55, 0.28, 1.00);
    float3 magenta  = float3(1.00, 0.45, 0.95);
    float3 hexTint  = float3(0.35, 0.55, 1.00);

    //主环颜色：核心白 → 中段紫 → 外缘洋红
    float3 ringCol = lerp(violet, hotCore, mainCore) * (mainSoft * 0.6 + mainCore * 1.4);
    ringCol += magenta * mainPulse * 0.9;
    ringCol += coolArc * mainPulse * 0.6;

    //谐波子环：紫蓝色
    float3 harmCol = lerp(violet, coolArc, 0.35) * harmonics * 0.85;

    //闪电分支：核心白+蓝边
    float3 lightCol = hotCore * lightning * 1.2 + coolArc * lightning * 0.7;

    //蜂巢网格：低饱和
    float3 hexCol = hexTint * hexGlow;

    //拖尾：洋红
    float3 trailCol = magenta * trail * 0.4;

    float3 finalColor = (ringCol + harmCol + lightCol + hexCol + trailCol) * fadeAlpha;

    //可读性控制：alpha由可视要素合成，整体做一次soft-clip避免过曝
    float alpha = saturate(
        mainSoft * 0.95
      + mainPulse * 1.0
      + harmonics * 0.65
      + lightning * 0.9
      + hexGlow * 0.5
      + trail * 0.45
    ) * fadeAlpha;

    //Additive混合：预乘alpha以保持亮度稳定
    return float4(finalColor * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SignalTowerVirusBroadcastPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
