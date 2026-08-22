// ============================================================================
//Blackwall.fx 旧网黑墙
//红黑翻涌的代码之墙：墙体噪声流 + 纵向楼层缝 + 边缘热脊 + 墙外红晕外溢
//uSurge 涌动脉冲（OldNetSkyEvents 驱动）：行波加密增辉、热脊与红晕同步爆发
//全程序化零采样器；AlphaBlend 预乘 alpha；噪声栈克制（两层 vnoise）
// ============================================================================

float uTime;
float uIntensity;      //整体强度 0-1
float2 uScreenSize;    //视口像素
float uWallScreenX;    //墙右缘的屏幕x（像素，含缩放）
float uSurge;          //0~1 涌动脉冲

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

    //三套全算、step 门控乘混合，全屏 ps_3_0 禁动态分支
    //（FNA3D 对 fx_2_0 内流程控制的翻译不可信，OniWorldGrade 事故同款处方）
    float inWall = step(0.0, d);
    float outWall = 1.0 - inWall;

    //墙体：向上翻涌的红黑数据流（两层竖向流动噪声，慢底流+快面流）
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

    //纵向楼层缝：墙是建起来的巨构不是一张纹理
    //每 ~130px 一道横向结构缝，暗缝为主，少数缝亮着（还通电的层）
    float strataId = floor(px.y / 130.0);
    float fy = frac(px.y / 130.0);
    float dLine = min(fy, 1.0 - fy) * 130.0;
    float strataLine = 1.0 - smoothstep(0.5, 2.6, dLine);
    float strataRand = hash21(float2(strataId, 3.7));
    body *= 1.0 - strataLine * 0.35;
    body += float3(0.50, 0.07, 0.05) * strataLine * step(0.80, strataRand) * 0.45;

    //缓慢上行的整体脉冲波：涌动期加密增辉
    float wave = sin(px.y * 0.010 - t * (1.35 + uSurge * 2.2));
    wave = smoothstep(0.90 - uSurge * 0.22, 1.0, wave);
    body += float3(0.55, 0.06, 0.05) * wave * (0.30 + uSurge * 0.55);

    //越深入墙体越暗，墙不是一张亮片而是一堵有厚度的黑
    body *= lerp(1.0, 0.30, saturate(d / 520.0));

    float3 col = body * inWall;
    float alpha = inWall;

    //边缘热脊：墙面与旧网交界处的窄条白热；涌动期爆发增辉
    float ridge = exp(-abs(d) / 9.0);
    col += float3(1.05, 0.22, 0.13) * ridge
        * (0.85 + 0.15 * sin(t * 2.3 + px.y * 0.02)) * (1.0 + uSurge * 1.2);
    alpha = max(alpha, ridge);

    //墙外红晕外溢：向旧网一侧衰减的软光；涌动期外溢更远更亮
    //exp 输入钳到 ≤0：门控乘替代分支后，墙内像素的 exp(d/60) 会溢出，必须夹住
    float spill = exp(min(d, 0.0) / (60.0 + uSurge * 70.0)) * outWall;
    float flick = 0.85 + 0.15 * vnoise(float2(px.y * 0.02, t * 0.9));
    col += float3(0.42, 0.045, 0.045) * spill * flick * (1.0 + uSurge * 1.5);
    alpha = max(alpha, spill * 0.55);

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
