// ============================================================================
//OldNetVault.fx 主控破译矩阵台体富层
//材质：深网保险库主控台。暗钢台座（拉丝+顶缘受光+琥珀待机灯）之上
//悬浮一枚双层环形锁盘：外环 12 段顺转、内环反转、盘内密文雨字形明灭。
//uOpen=面板开启（提速提亮，上行链路在烧）
//技法：TechVaultRing 单技法。全程序化零采样；直线算术无动态分支
//（FNA3D 保守路线：无循环/无 tex 指令/step 门控乘）；AlphaBlend 预乘输出
//画布契约：quad = 64x72 px 底锚（C# 侧 OldNetVaultRender 折算）
//极角纪律：两环各自先旋转坐标系再 atan2，12 段分界与支割线重合，
//段哈希在割线两侧本就属于不同段，无缝
// ============================================================================

float uTime;
float uSeed;
//面板开启 0/1
float uOpen;
float uAlpha;
//画布像素尺寸（64,72）
float2 uCanvas;

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PSVaultRing(float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = uv * uCanvas;
    float t = uTime;
    const float TAU = 6.28318530;

    float3 coldC = float3(0.55, 0.78, 0.82);
    float3 amberC = float3(1.00, 0.70, 0.31);
    float3 steelC = float3(0.055, 0.075, 0.085);

    float3 col = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    // ──────────── 台座：暗钢双层收分 ────────────
    float cx = uCanvas.x * 0.5;
    //下层 [64,72) 半宽 21 / 上层 [58,64) 半宽 14
    float ax = abs(px.x - cx);
    float tier1 = step(ax, 21.0) * step(64.0, px.y);
    float tier2 = step(ax, 14.0) * step(58.0, px.y) * step(px.y, 64.0);
    float slab = saturate(tier1 + tier2);

    //拉丝丝纹 + 顶缘受光 + 底缘沉暗
    float brush = hash21(float2(floor(px.y), 7.7 + uSeed)) * 0.05;
    float topEdge1 = smoothstep(2.0, 0.0, abs(px.y - 64.0)) * step(ax, 21.0);
    float topEdge2 = smoothstep(2.0, 0.0, abs(px.y - 58.0)) * step(ax, 14.0);
    float3 slabCol = steelC * (0.9 + brush)
        + coldC * (topEdge1 + topEdge2) * 0.22;
    slabCol *= 1.0 - smoothstep(68.0, 72.0, px.y) * 0.5;

    //琥珀待机灯（右肩一粒，开台快闪）
    float2 lampP = float2(cx + 10.0, 61.0);
    float lampD = length(px - lampP);
    float lampPulse = 0.5 + 0.5 * sin(t * (2.0 + uOpen * 6.0) + uSeed);
    float lamp = exp(-lampD * lampD * 0.45) * (0.4 + 0.6 * lampPulse);
    slabCol += amberC * lamp * 1.2;

    col += slabCol * slab;
    alpha += slab * 0.92;

    // ──────────── 链路光柱：台座到锁盘的细供能线 ────────────
    float linkFlick = 0.55 + 0.45 * hash21(float2(floor(px.y * 0.5) + floor(t * 8.0), uSeed));
    float link = smoothstep(1.6, 0.3, ax)
        * smoothstep(58.5, 56.0, px.y) * step(44.0, px.y)
        * linkFlick * (0.35 + uOpen * 0.45);
    col += coldC * link;
    alpha += link * 0.6;

    // ──────────── 环形锁盘（微浮动） ────────────
    float2 ringC = float2(cx, 31.0 + sin(t * 1.3 + uSeed) * 1.5);
    float2 rel = px - ringC;
    float r = length(rel);
    float openSpin = 1.0 + uOpen * 1.6;

    //外环：顺转 12 段。先旋转坐标系再 atan2（割线随段界走，无缝）
    float rotA = t * 0.5 * openSpin + uSeed;
    float ca = cos(rotA);
    float sa = sin(rotA);
    float2 relA = float2(rel.x * ca + rel.y * sa, -rel.x * sa + rel.y * ca);
    float thetaA = atan2(relA.y, relA.x);
    float a12 = (thetaA / TAU + 0.5) * 12.0;
    float segIdx = floor(a12);
    float segFrac = frac(a12);
    float gapA = smoothstep(0.03, 0.14, segFrac) * smoothstep(0.97, 0.86, segFrac);
    float flickA = 0.55 + 0.45 * hash21(float2(segIdx, floor(t * 2.0) + uSeed));
    float bandA = smoothstep(2.6, 1.1, abs(r - 17.0));
    float ringA = bandA * gapA * flickA;
    col += coldC * ringA * (0.85 + uOpen * 0.45);
    alpha += ringA * 0.85;

    //内环：反转 8 段，琥珀，细一号
    float rotB = -t * 0.8 * openSpin + uSeed * 1.7;
    float cb = cos(rotB);
    float sb = sin(rotB);
    float2 relB = float2(rel.x * cb + rel.y * sb, -rel.x * sb + rel.y * cb);
    float thetaB = atan2(relB.y, relB.x);
    float b8 = (thetaB / TAU + 0.5) * 8.0;
    float segIdxB = floor(b8);
    float segFracB = frac(b8);
    float gapB = smoothstep(0.05, 0.18, segFracB) * smoothstep(0.95, 0.82, segFracB);
    float flickB = 0.5 + 0.5 * hash21(float2(segIdxB, floor(t * 3.0) + uSeed * 2.3));
    float bandB = smoothstep(1.8, 0.7, abs(r - 10.5));
    float ringB = bandB * gapB * flickB;
    col += amberC * ringB * (0.7 + uOpen * 0.4);
    alpha += ringB * 0.7;

    // ──────────── 盘内密文雨：字形块明灭 ────────────
    float rain = hash21(float2(floor(rel.x / 2.5),
        floor((rel.y + t * (5.0 + uOpen * 9.0)) / 3.5)) + uSeed);
    float glyph = step(0.80, rain) * smoothstep(8.5, 5.0, r);
    col += coldC * glyph * (0.5 + uOpen * 0.5);
    alpha += glyph * 0.5;

    //盘芯白核（呼吸）
    float corePulse = 0.6 + 0.4 * sin(t * (2.2 + uOpen * 3.0) + uSeed);
    float core = exp(-r * r * 0.22) * corePulse;
    col += lerp(coldC, float3(1.0, 1.0, 1.0), 0.6) * core * 0.9;
    alpha += core * 0.7;

    col = saturate(col) * uAlpha;
    alpha = saturate(alpha) * uAlpha;
    return float4(col * alpha, alpha);
}

technique TechVaultRing
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSVaultRing();
    }
}
