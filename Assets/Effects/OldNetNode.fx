// ============================================================================
//OldNetNode.fx 旧网数据节点三态
//材质：还在通电的数据晶体，双壳错相旋转（刚体旋转坐标，零极角）、
//体内存储行逐行明灭 + 读写扫描行、引导期自内向外的重写波
//TechData=冷青普通 / TechEncrypt=琥珀红加密（锁壳+重写波）/ TechEvent=警戒闸杆
//全程序化零采样器；直线算术无动态分支；AlphaBlend 预乘输出
//画布契约：48x48 正方 quad，内容在 |p|<=0.92 内自然归零 + guard 保险
// ============================================================================

float uTime;
float uSeed;
//加密节点引导进度 0..1（其余技法传 0）
float uProgress;
//整体透明度
float uAlpha;

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//刚体旋转（连续，无接缝）
float2 rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

//L1 菱形距离（abs 折叠，无 atan2）
float diamond(float2 p)
{
    return abs(p.x) + abs(p.y);
}

//体内存储行：行 id 明灭（内存条在读写）+ 下行扫描行
float dataRows(float2 p, float body, float t, float seed, out float scan)
{
    float rowK = 8.0;
    float rowCoord = p.y * rowK + seed * 7.0;
    float rowId = floor(rowCoord);
    float rowFlick = step(0.52, hash21(float2(rowId, floor(t * 1.8 + seed))));
    float rowLine = smoothstep(0.34, 0.05, abs(frac(rowCoord) - 0.5));
    float scanPos = frac(t * 0.4 + seed) * 1.6 - 0.8;
    scan = smoothstep(0.14, 0.0, abs(p.y - scanPos)) * body;
    return rowLine * rowFlick * body;
}

// ──────────── 普通数据节点：冷青单壳晶体 ────────────
float4 PSData(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;
    float t = uTime;
    float breath = 1.0 + 0.05 * sin(t * 2.4 + uSeed);
    p /= breath;

    float dOut = diamond(rot(p, t * 0.5 + uSeed));
    float dIn = diamond(rot(p, -t * 0.9 + uSeed));

    float3 cold = float3(0.0, 0.86, 1.0);
    float3 deep = float3(0.015, 0.16, 0.20);

    //壳线双层 + 体积
    float shell = smoothstep(0.06, 0.0, abs(dOut - 0.72)) * 0.95
                + smoothstep(0.04, 0.0, abs(dOut - 0.58)) * 0.4;
    float body = smoothstep(0.72, 0.18, dOut);

    float scan;
    float rows = dataRows(p, body, t, uSeed, scan);

    float core = smoothstep(0.28, 0.04, dIn);

    float3 col = deep * body
        + cold * rows * 0.5
        + cold * shell
        + cold * scan * 0.45
        + lerp(cold, float3(1.0, 1.0, 1.0), 0.72) * core;
    float alpha = saturate(body * 0.5 + shell + core + rows * 0.35 + scan * 0.3);

    float guard = smoothstep(1.0, 0.92, max(abs(p.x), abs(p.y)));
    col = saturate(col) * guard * uAlpha;
    alpha = saturate(alpha) * guard * uAlpha;
    return float4(col * alpha, alpha);
}

// ──────────── 加密节点：琥珀红双锁壳 + 引导重写波 ────────────
float4 PSEncrypt(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;
    float t = uTime;
    float breath = 1.0 + 0.04 * sin(t * 3.1 + uSeed);
    p /= breath;

    //双锁壳反向旋转：读作"上锁"
    float dLockA = diamond(rot(p, t * 0.55 + uSeed));
    float dLockB = diamond(rot(p, -t * 0.75 + uSeed + 0.6));
    float dIn = diamond(rot(p, t * 1.3 + uSeed));

    float3 amber = float3(1.0, 0.59, 0.20);
    float3 ember = float3(0.92, 0.25, 0.17);
    float3 deep = float3(0.20, 0.07, 0.02);

    float shellA = smoothstep(0.055, 0.0, abs(dLockA - 0.76)) * 0.95;
    float shellB = smoothstep(0.05, 0.0, abs(dLockB - 0.62)) * 0.8;
    float body = smoothstep(0.74, 0.16, min(dLockA, dLockB));

    float scan;
    float rows = dataRows(p, body, t, uSeed + 3.7, scan);

    //引导重写波：自内向外的白热前沿 + 已重写区提亮
    float rewriteR = uProgress * 0.85;
    float active = step(0.001, uProgress);
    float front = smoothstep(0.10, 0.0, abs(dLockA - rewriteR)) * active;
    float rewritten = smoothstep(rewriteR, rewriteR - 0.2, dLockA) * active;

    float core = smoothstep(0.26, 0.04, dIn);

    float3 col = deep * body
        + ember * rows * 0.5
        + amber * shellA
        + ember * shellB
        + amber * scan * 0.4
        + lerp(amber, float3(1.0, 1.0, 1.0), 0.6) * core
        + float3(1.0, 0.95, 0.85) * front * 0.9
        + amber * rewritten * 0.35;
    float alpha = saturate(body * 0.55 + shellA + shellB + core
        + rows * 0.35 + scan * 0.25 + front * 0.8 + rewritten * 0.3);

    float guard = smoothstep(1.0, 0.92, max(abs(p.x), abs(p.y)));
    col = saturate(col) * guard * uAlpha;
    alpha = saturate(alpha) * guard * uAlpha;
    return float4(col * alpha, alpha);
}

// ──────────── 事件节点：待扳的警戒闸杆 ────────────
float4 PSEvent(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;
    float t = uTime;

    float3 warn = float3(0.92, 0.25, 0.17);
    float3 amber = float3(1.0, 0.67, 0.24);
    float3 dark = float3(0.10, 0.05, 0.045);

    //慢闪信标节律（亮-灭，区别于常亮节点）
    float blink = lerp(0.3, 1.0, step(0.3, sin(t * 4.0 + uSeed)));

    //底座横杠
    float2 pb = p - float2(0.0, 0.62);
    float basePlate = smoothstep(0.03, 0.0, max(abs(pb.x) - 0.5, abs(pb.y) - 0.09));

    //斜置闸杆（旋转矩形 SDF）
    float2 pl = rot(p - float2(0.0, 0.12), 0.5);
    float lever = smoothstep(0.03, 0.0, max(abs(pl.x) - 0.09, abs(pl.y) - 0.46));

    //杆头信标：菱形芯（杆顶端在旋转前 (0,-0.46)，转回世界≈(0.22,-0.28)）
    float2 ph = p - float2(0.22, -0.30);
    float head = smoothstep(0.20, 0.03, diamond(ph));
    float headCore = smoothstep(0.08, 0.01, diamond(ph));

    //警戒扩散环：向外行进的菱形描边
    float ringPhase = frac(t * 0.7 + uSeed * 0.31);
    float ringR = 0.25 + ringPhase * 0.62;
    float ring = smoothstep(0.045, 0.0, abs(diamond(p) - ringR)) * (1.0 - ringPhase) * blink;

    float3 col = dark * (basePlate + lever)
        + amber * lever * 0.55
        + warn * head * blink
        + float3(1.0, 1.0, 1.0) * headCore * 0.7 * blink
        + warn * ring * 0.5
        + amber * basePlate * 0.35;
    float alpha = saturate(basePlate + lever + head * blink + ring * 0.5);

    float guard = smoothstep(1.0, 0.92, max(abs(p.x), abs(p.y)));
    col = saturate(col) * guard * uAlpha;
    alpha = saturate(alpha) * guard * uAlpha;
    return float4(col * alpha, alpha);
}

technique TechData
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSData();
    }
}

technique TechEncrypt
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSEncrypt();
    }
}

technique TechEvent
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSEvent();
    }
}
