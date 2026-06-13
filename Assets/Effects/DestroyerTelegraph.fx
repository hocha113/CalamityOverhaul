// ============================================================================
// DestroyerTelegraph.fx 毁灭者预警线能量流动
// uv.x 沿线 0起点→1末端，uv.y 横向；Additive 白色四边形
// ps_3_0
// ============================================================================

float uTime;
float uIntensity;     //整体亮度(含淡入)
float uLockProgress;  //0追踪 0~1锁定白闪推进
float uAspect;        //长宽比，保持噪声各向同性
float3 uColor;        //主色(追踪期暗红)

#define PI  3.14159265
#define TAU 6.28318530

// 哈希
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// 值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 分形噪声
float fbm2(float2 p)
{
    float v = 0.0;
    float amp = 0.5;
    float2 shift = float2(17.3, 31.7);
    for (int i = 0; i < 3; i++)
    {
        v += valueNoise(p) * amp;
        p = p * 2.17 + shift;
        amp *= 0.5;
    }
    return v;
}

float4 TelegraphPS(float2 uv : TEXCOORD0) : COLOR0
{
    float lat = (uv.y - 0.5) * 2.0;   //-1..1 横向
    float x = uv.x;                    //沿线 0..1

    // 端点羽化
    float endFade = smoothstep(0.0, 0.05, x) * smoothstep(1.0, 0.985, x);

    // 横向核心+光晕，锁定时核心加粗
    float coreSharp = lerp(48.0, 16.0, uLockProgress);
    float core = exp(-lat * lat * coreSharp);
    float glow = exp(-lat * lat * 5.5) * 0.42;

    // 沿线能量流
    float flowSpeed = 2.4 + uLockProgress * 3.0;
    float n = fbm2(float2(x * uAspect * 0.55 - uTime * flowSpeed, lat * 1.8));

    // 朝打击方向脉冲段
    float pulse = 0.55 + 0.45 * sin(x * uAspect * 1.45 - uTime * (8.0 + uLockProgress * 14.0) + n * 5.0);
    pulse = pow(pulse, 1.6);

    // 锁定白闪振荡
    float flash = 1.0 + uLockProgress * 0.45 * sin(uTime * 46.0);

    // 亮度合成
    float lum = (core * (1.0 + uLockProgress * 1.4) + glow * (0.75 + n * 0.55)) * pulse * endFade * flash;

    // 锁定期核心白热
    float3 col = uColor * lum;
    col += float3(1.0, 0.93, 0.82) * core * uLockProgress * lum * 1.15;

    return float4(col * uIntensity, 1.0);
}

technique Telegraph
{
    pass P0
    {
        PixelShader = compile ps_3_0 TelegraphPS();
    }
}
