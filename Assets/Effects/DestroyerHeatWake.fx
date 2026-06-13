// ============================================================================
// DestroyerHeatWake.fx 毁灭者高速热浪尾流位移图
// uv.x 1前端 0尾端；输出 R方向 G强度 A混合，供 WarpShader
// ps_3_0
// ============================================================================

float uTime;
float uIntensity;   //总强度 0~1(按速度驱动)
float uProgress;    //生命淡出 0~1
float uRotation;    //尾流轴世界角(=头部速度方向)

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
    for (int i = 0; i < 4; i++)
    {
        v += valueNoise(p) * amp;
        p = p * 2.17 + shift;
        amp *= 0.5;
    }
    return v;
}

float4 HeatWakePS(float2 uv : TEXCOORD0) : COLOR0
{
    float axial = 1.0 - uv.x;             //0=前端(贴着头部) → 1=尾端
    float lat = (uv.y - 0.5) * 2.0;       //-1..1 横向

    // 横向收束，前端窄尾端宽
    float halfWidth = lerp(0.34, 1.0, axial);
    float lateralFall = exp(-pow(lat / halfWidth, 2.0) * 3.2);

    // 轴向包络
    float axialFall = smoothstep(0.0, 0.10, axial) * pow(saturate(1.0 - axial), 1.55);

    // 沿轴回卷 fbm 湍流
    float n = fbm2(float2(axial * 5.5 + uTime * 2.6, lat * 2.4 - uTime * 0.8));

    // 热涟漪主波
    float ripple = sin(axial * 34.0 + uTime * 13.0 + n * 6.3);

    // 低频起伏
    float swell = 0.6 + 0.4 * sin(axial * 9.0 + uTime * 5.0 + n * 2.0);

    // 位移方向：横向摆动+向后拖拽
    float2 pushLocal = float2(-0.38, ripple * 1.0);
    float worldAngle = atan2(pushLocal.y, pushLocal.x) + uRotation;
    float direction = frac(worldAngle / TAU + 0.5);

    // 位移强度
    float magnitude = abs(ripple) * swell * lateralFall * axialFall;
    magnitude *= uIntensity * uProgress;
    magnitude = saturate(magnitude);

    float alpha = lateralFall * axialFall * uProgress;

    return float4(direction, magnitude, 0, saturate(alpha));
}

technique HeatWake
{
    pass P0
    {
        PixelShader = compile ps_3_0 HeatWakePS();
    }
}
