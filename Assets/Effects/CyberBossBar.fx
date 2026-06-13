// ============================================================================
// CyberBossBar.fx 赛博朋克2077风格敌人血条 HUD
// 不采样贴图，uv 程序化生成；AlphaBlend 预乘 alpha
// ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //quad 像素尺寸
float uLifeRatio;    //平滑当前血量 0~1
float uTrailRatio;   //延迟血量，受击残影
float uHitFlash;     //受击白闪 0~1
float uSegments;     //分段数量

//================== 工具函数 ==================

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//================== 主像素着色 ==================

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vcol : COLOR0) : COLOR0
{
    float2 ipx = 1.0 / uResolution;
    float2 p = uv * uResolution;

    //—— 平行四边形剪切：上边右移、下边左移，制造倾斜的电子读数感 ——
    float skew = 0.08;
    float leftEdge = skew * (1.0 - uv.y);
    float rightEdge = 1.0 - skew * uv.y;
    float aaX = 1.5 * ipx.x;
    float insideX = smoothstep(leftEdge - aaX, leftEdge + aaX, uv.x)
                  * (1.0 - smoothstep(rightEdge - aaX, rightEdge + aaX, uv.x));
    if (insideX <= 0.0) {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    //沿条方向的归一化位置
    float t = (uv.x - leftEdge) / max(rightEdge - leftEdge, 1e-4);

    //跨条厚度方向：nrm 顶=-1 底=+1，vEdge 让上下边缘柔化（不像硬矩形）
    float dy = uv.y - 0.5;
    float nrm = clamp(dy * 2.0, -1.0, 1.0);
    float vEdge = 1.0 - smoothstep(0.5 - 3.0 * ipx.y, 0.5, abs(dy));

    //—— 威胁配色：满血偏珊瑚红，残血偏猩红（始终红色系，不用黄）——
    float threat = saturate(uLifeRatio);
    float3 cFull = float3(1.00, 0.45, 0.34);
    float3 cLow  = float3(1.00, 0.17, 0.19);
    float3 fillBase = lerp(cLow, cFull, smoothstep(0.18, 0.72, threat));
    float3 hot  = float3(1.00, 0.78, 0.62);
    float3 deep = float3(0.30, 0.035, 0.05);
    float3 slot = float3(0.06, 0.018, 0.022);

    //—— 分段：每段尾部留出间隙，形成断续信号槽 ——
    float segN = max(uSegments, 1.0);
    float ti = t * segN;
    float segLocal = frac(ti);
    float gapFrac = 0.06;
    //在段尾 gapFrac 区间内形成断口，边界留极窄 AA 过渡
    float inGap = smoothstep(1.0 - gapFrac, 1.0 - gapFrac + 0.012, segLocal);

    //—— 状态：已填充 / 残影(掉血缓降) ——
    float filled = step(t, uLifeRatio);
    float trail = step(t, uTrailRatio) * (1.0 - filled);

    //管状光泽：中心亮、上缘补光、下缘压暗
    float vy = saturate(1.0 - nrm * nrm);
    float tube = 0.5 + 0.5 * vy;
    float rimTop = smoothstep(0.55, 1.0, -nrm);
    float shadeBot = smoothstep(0.5, 1.0, nrm);

    //沿段流动的能量脉冲
    float flow = sin(t * 46.0 - uTime * 3.2) * 0.5 + 0.5;

    float3 fillCol = fillBase * tube;
    fillCol = lerp(fillCol, hot, flow * 0.10);
    fillCol += hot * rimTop * 0.35;
    fillCol *= 1.0 - shadeBot * 0.35;

    float3 trailCol = deep * (0.7 + 0.3 * vy);

    //—— 合成底色：空槽 → 残影 → 填充 ——
    float3 col = slot;
    float a = 0.52;
    col = lerp(col, trailCol, trail);
    a = lerp(a, 0.60, trail);
    col = lerp(col, fillCol, filled);
    a = lerp(a, 0.94, filled);

    //段分隔细亮线（仅填充段）
    float sep = smoothstep(0.018, 0.0, abs(segLocal - (1.0 - gapFrac)));
    col += fillBase * sep * filled * 0.5;

    //—— 填充前沿高光 + 色散 ——
    float leadDist = abs(t - uLifeRatio);
    float lead = smoothstep(0.016, 0.0, leadDist)
               * step(0.001, uLifeRatio) * (1.0 - step(0.999, uLifeRatio));
    col += hot * lead * 1.9;
    a = max(a, lead * 0.95);
    col.r += smoothstep(0.03, 0.0, abs((t - uLifeRatio) - 0.012)) * 0.45;
    col.b += smoothstep(0.03, 0.0, abs((t - uLifeRatio) + 0.012)) * 0.28;

    //残影前沿（暗红微亮）
    float trailLead = smoothstep(0.012, 0.0, abs(t - uTrailRatio))
                    * step(uLifeRatio, t) * step(t, uTrailRatio + 0.02);
    col += deep * 3.0 * trailLead;

    //扫描线（横向像素行）
    float scan = 0.93 + 0.07 * step(0.5, frac(p.y * 0.7 + uTime * 0.3));
    col *= scan;

    //表面颗粒噪声
    float grain = valueNoise(p * 0.8 + float2(0.0, uTime * 0.7));
    col += (grain - 0.5) * 0.05 * (filled + 0.3);

    //间隙：压暗并削弱 alpha，形成断口
    col = lerp(col, slot * 0.3, inGap);
    a *= 1.0 - inGap * 0.82;

    //受击白闪
    col = lerp(col, float3(1.0, 0.93, 0.86), uHitFlash * 0.5 * (filled + 0.15));
    a = max(a, uHitFlash * 0.25 * filled);

    //—— 残血故障：高频闪烁 + 横向错位条带 + 猩红染色 ——
    float danger = 1.0 - smoothstep(0.0, 0.26, threat);
    if (danger > 0.001) {
        float flick = step(0.55, frac(uTime * 9.0));
        col *= 1.0 + danger * flick * 0.22;
        float band = frac(p.y * 0.09 - uTime * 1.5);
        float bandLine = smoothstep(0.46, 0.5, band) * (1.0 - smoothstep(0.5, 0.54, band));
        col += cLow * bandLine * danger * 0.55 * filled;
    }

    col = max(col, 0.0);

    //预乘 alpha 输出（匹配 BlendState.AlphaBlend）
    float finalA = saturate(a * vEdge * insideX * uAlpha);
    return float4(col * finalA, finalA);
}

technique Technique1
{
    pass CyberBossBarPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
