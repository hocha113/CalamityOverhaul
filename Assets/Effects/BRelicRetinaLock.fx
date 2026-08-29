// ============================================================================
//BRelicRetinaLock.fx 饕餮之喉·视网膜锁定(方形面片，SpriteBatch Immediate)
//uMode 0=处刑窗口常驻准星：呼吸虹膜环+旋转刻度+十字发丝线+瞳孔微辉，窗口将尽转急闪
//uMode 1=处刑命中爆闪：收缩环→横向扫描线(隔行残影)→竖瞳裂隙闪→整体衰减
//极角审计：theta 仅进 sin(3θ+φ) 整数倍角，连续；噪声一律刚体旋转笛卡尔坐标
//预乘输出 AlphaBlend；所有成分在 r=1 前自然归零(画布契约，无边缘平切)
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //PerlinNoise 512

float uTime;
float uMode;        //0 常驻准星 / 1 爆闪
float uProgress;    //常驻:窗口已耗比例 0~1 / 爆闪:演出进度 0~1
float uIntensity;
float seed;
float uSeal;        //月噬封禁 0~1：常驻准星褪色转灰(吸血被封的可读提示)

static const float PI = 3.14159265;

//刚体旋转(无接缝)
float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0)
    {
        return float4(0, 0, 0, 0);
    }
    float theta = atan2(p.y, p.x);
    float edge = smoothstep(1.0, 0.82, r);

    //血噪肌理：旋转笛卡尔采样
    float grain = tex2D(uImage1, Rot(p, uTime * 0.4) * 0.9 + seed).r * 0.30 + 0.85;

    float3 cRed  = float3(0.92, 0.10, 0.08);
    float3 cDeep = float3(0.48, 0.03, 0.05);
    float3 cCore = float3(1.00, 0.55, 0.48);

    float3 col = float3(0, 0, 0);
    float alpha = 0.0;

    if (uMode < 0.5)
    {
        //=== 常驻准星 ===
        //窗口将尽(耗时>75%)转急促闪烁提醒
        float blink = uProgress > 0.75 ? (0.55 + 0.45 * sin(uTime * 26.0)) : 1.0;
        //呼吸虹膜环
        float r0 = 0.52 + 0.030 * sin(uTime * 2.4 + seed * 7.0);
        float ring = exp(-pow((r - r0) * 22.0, 2.0));
        //旋转刻度：3θ 整数倍角，贴环带
        float ringZone = exp(-pow((r - r0) * 8.0, 2.0));
        float ticks = pow(saturate(sin(3.0 * theta + uTime * 1.8 + seed * 11.0) * 0.5 + 0.5), 14.0) * ringZone;
        //十字发丝线：环外侧渐远渐隐
        float hairZone = smoothstep(r0 + 0.06, r0 + 0.26, r);
        float hair = (exp(-pow(p.x * 42.0, 2.0)) + exp(-pow(p.y * 42.0, 2.0))) * hairZone * 0.6;
        //瞳孔微辉
        float pupil = exp(-r * r * 14.0) * (0.30 + 0.18 * sin(uTime * 3.1 + seed * 5.0));

        col = cDeep * ring * 0.8 + cRed * (ring * 0.55 + ticks * 0.9 + hair) + cCore * pupil * 0.6;
        col *= grain;
        alpha = saturate(ring * 0.60 + ticks * 0.55 + hair * 0.45 + pupil * 0.35)
              * 0.62 * blink;

        //月噬封禁：血红准星褪成死灰并压暗(仅常驻准星，爆闪不受封禁影响)
        float gray = dot(col, float3(0.30, 0.55, 0.15));
        col = lerp(col, float3(gray, gray, gray) * 0.85, uSeal);
        alpha *= lerp(1.0, 0.55, uSeal);
    }
    else
    {
        //=== 处刑爆闪 ===
        float pr = uProgress;
        float decay = 1.0 - smoothstep(0.55, 1.0, pr);
        //收缩锁定环(前 35%)
        float lockT = saturate(pr / 0.35);
        float r0 = lerp(0.95, 0.40, lockT);
        float ringW = lerp(18.0, 30.0, lockT);
        float ring = exp(-pow((r - r0) * ringW, 2.0)) * (0.6 + 0.8 * lockT);
        //横向扫描带(10%~55%)：自上而下一次掠过 + 隔行残影
        float scanT = saturate((pr - 0.10) / 0.45);
        float scanWindow = step(0.10, pr) * step(pr, 0.60);
        float scanY = lerp(-1.15, 1.15, scanT);
        float scan = exp(-pow((p.y - scanY) * 9.0, 2.0)) * 1.3 * scanWindow;
        float interlace = pow(saturate(sin(p.y * 90.0 + uTime * 30.0) * 0.5 + 0.5), 3.0)
                        * 0.30 * scan;
        //竖瞳裂隙闪(25%~65%)
        float slitEnv = smoothstep(0.25, 0.38, pr) * (1.0 - smoothstep(0.50, 0.65, pr));
        float slit = exp(-pow(p.x * 24.0, 2.0)) * exp(-pow(p.y * 3.0, 2.0)) * slitEnv * 1.5;
        //锁定刻度(整数倍角)
        float ticks = pow(saturate(sin(3.0 * theta - uTime * 4.0) * 0.5 + 0.5), 10.0)
                    * exp(-pow((r - r0) * 9.0, 2.0)) * lockT;

        col = cDeep * ring * 0.7
            + cRed * (ring * 0.8 + scan + ticks * 1.1)
            + cCore * (slit + scan * 0.35 + interlace);
        col *= grain;
        alpha = saturate(ring * 0.65 + scan * 0.55 + slit * 0.7 + ticks * 0.5 + interlace * 0.4)
              * decay * 0.92;
    }

    alpha *= edge * uIntensity;
    return float4(col * alpha, alpha) * vertexColor.a;
}

technique Technique1
{
    pass BRelicRetinaLockPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
