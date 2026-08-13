// ============================================================================
//Blackwall.fx 旧网黑墙
//红黑翻涌的代码之墙：墙体噪声流 + 边缘热脊 + 墙外红晕外溢
//全程序化零采样器；AlphaBlend 预乘 alpha；噪声栈克制（两层 vnoise）
// ============================================================================

float uTime;
float uIntensity;      //整体强度 0-1
float2 uScreenSize;    //视口像素
float uWallScreenX;    //墙右缘的屏幕x（像素，含缩放）

#define TAU 6.28318530

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 PSBlackwall(float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = uv * uScreenSize;
    float t = uTime;
    //d>0 在墙体内，d<0 在墙外
    float d = uWallScreenX - px.x;

    float3 col = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    //墙体：向上翻涌的红黑数据流
    if (d > 0.0)
    {
        //两层竖向流动噪声，慢底流+快面流
        float n1 = vnoise(px * float2(0.0045, 0.0028) + float2(0.0, -t * 0.22));
        float n2 = vnoise(px * float2(0.013, 0.011) + float2(37.0, -t * 0.85));
        float band = n1 * 0.62 + n2 * 0.38;

        //代码列明暗：宽列的静态差异 + 低频重掷
        float colId = floor(px.x / 18.0);
        float colFlick = hash21(float2(colId, floor(t * 1.5)));
        band += (colFlick - 0.5) * 0.10;

        float3 body = lerp(float3(0.015, 0.001, 0.004),
                           float3(0.34, 0.020, 0.050), saturate(band));

        //高位亮缝：数据束
        float seam = smoothstep(0.74, 0.96, band);
        body += float3(0.85, 0.10, 0.09) * seam * 0.75;

        //缓慢上行的整体脉冲波
        float wave = sin(px.y * 0.010 - t * 1.35);
        wave = smoothstep(0.90, 1.0, wave);
        body += float3(0.55, 0.06, 0.05) * wave * 0.30;

        //越深入墙体越暗，墙不是一张亮片而是一堵有厚度的黑
        body *= lerp(1.0, 0.30, saturate(d / 520.0));

        col = body;
        alpha = 1.0;
    }

    //边缘热脊：墙面与旧网交界处的窄条白热
    float ridge = exp(-abs(d) / 9.0);
    col += float3(1.05, 0.22, 0.13) * ridge * (0.85 + 0.15 * sin(t * 2.3 + px.y * 0.02));
    alpha = max(alpha, ridge);

    //墙外红晕外溢：向旧网一侧衰减的软光
    if (d < 0.0)
    {
        float spill = exp(d / 60.0); //d为负，越远越小
        float flick = 0.85 + 0.15 * vnoise(float2(px.y * 0.02, t * 0.9));
        col += float3(0.42, 0.045, 0.045) * spill * flick;
        alpha = max(alpha, spill * 0.55);
    }

    col = saturate(col) * uIntensity;
    alpha = saturate(alpha) * uIntensity;
    //预乘 alpha 输出
    return float4(col * alpha, alpha);
}

technique Blackwall
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSBlackwall();
    }
}
