// ============================================================================
//OldNetWarden.fx 回收官旗舰双技法（消费端 Renders/OldNetWardenRender.cs）
//TechSealRing = 旋转字形纹章环：极坐标字形槽位量化 + 径向扫描流 + 外缘故障撕裂
//TechCoreEye  = 核心独目：同心量化环 + 整数次谐波辐条 + 失血比驱动的块状侵蚀降解
//全程序化零采样器；直线算术无动态分支（step 门控），防 FNA3D 无日志崩溃
//极角纪律：atan2 只经"整数槽位 floor（字形在槽界归零）"与"整数 k 的 sin(k*ang)"
//消费，2π 跳变恰落在槽位边界/整周期上，无接缝（VFX.md Polar shader seams 协议）
//画布契约：正方 quad，内容在 |p|<=0.92 内自然归零 + guard 保险；AlphaBlend 预乘输出
// ============================================================================

float uTime;
float uSeed;
//失血比 0..1：降解侵蚀强度
float uDecay;
//招式充能 0..1：白热提亮与瞳孔收缩
float uCharge;
//纹章环累计相位（C# 每帧递增传入，充能期加速）
float uSpin;
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

// ──────────── 纹章环：字形槽位 + 扫描流 + 故障撕裂 ────────────
float4 PSSealRing(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;
    float t = uTime;
    //环的旋转 = 刚体旋转坐标（先转再取角，槽位随环走）
    float2 q = rot(p, uSpin);
    float r = length(q);
    //0..1 角坐标：2π 跳变恰在 15|0 槽界上
    float a01 = atan2(q.y, q.x) / 6.2831853 + 0.5;

    float3 ember = float3(0.92, 0.25, 0.17);
    float3 amber = float3(1.0, 0.59, 0.20);
    float3 deep = float3(0.14, 0.045, 0.035);

    //环带 [0.60, 0.86] 与内外缘线
    float band = smoothstep(0.58, 0.63, r) * smoothstep(0.88, 0.83, r);
    float edgeIn = smoothstep(0.035, 0.0, abs(r - 0.60));
    float edgeOut = smoothstep(0.035, 0.0, abs(r - 0.86));

    //字形槽位：16 槽 × 3 行网格；字形条在槽界/行界自然归零（无缝保证）
    float slotF = a01 * 16.0;
    float slotId = floor(slotF);
    float slotLocal = frac(slotF);
    float rowF = (r - 0.60) / 0.26 * 3.0;
    float rowId = floor(rowF);
    float rowLocal = frac(rowF);
    //低频重掷 = 字形重写明灭
    float rewrite = floor(t * 1.6 + uSeed);
    float glyphOn = step(0.42, hash21(float2(slotId * 7.13 + uSeed, rowId * 3.71 + rewrite)));
    float bar = smoothstep(0.42, 0.30, abs(slotLocal - 0.5))
              * smoothstep(0.44, 0.28, abs(rowLocal - 0.5));
    //失血侵蚀：字形块随 uDecay 熄灭（甲片剥落的环上镜像）
    float erode = step(hash21(float2(slotId * 1.93, rowId * 5.17 + uSeed)), uDecay * 0.85);
    float glyph = bar * glyphOn * band * (1.0 - erode * 0.9);

    //径向扫描流：亮带自内向外行进（纯径向，无极角参与）
    float scanR = 0.60 + frac(t * 0.45 + uSeed) * 0.26;
    float scan = smoothstep(0.05, 0.0, abs(r - scanR)) * band;

    //外缘故障撕裂：8 槽量化径向抖刺，失血越深撕得越凶
    float slot8 = floor(a01 * 8.0);
    float jag = hash21(float2(slot8 * 2.31 + uSeed, floor(t * 7.0)));
    float tear = step(0.86, r) * step(r, 0.86 + jag * 0.10) * (0.4 + uDecay * 0.6);

    //充能白热
    float3 hot = lerp(ember, float3(1.0, 0.97, 0.9), uCharge * 0.8);

    float3 col = deep * band
        + hot * glyph * (0.85 + uCharge * 0.5)
        + amber * scan * 0.5
        + hot * (edgeIn + edgeOut) * (0.6 + uCharge * 0.5)
        + ember * tear;
    float alpha = saturate(band * 0.22 + glyph * 0.85 + scan * 0.4
        + (edgeIn + edgeOut) * 0.7 + tear * 0.8);

    float guard = smoothstep(1.0, 0.92, max(abs(p.x), abs(p.y)));
    col = saturate(col) * guard * uAlpha;
    alpha = saturate(alpha) * guard * uAlpha;
    return float4(col * alpha, alpha);
}

// ──────────── 核心独目：同心量化环 + 辐条 + 块状降解 ────────────
float4 PSCoreEye(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;
    float t = uTime;
    float r = length(p);
    float ang = atan2(p.y, p.x);

    float3 ember = float3(0.92, 0.25, 0.17);
    float3 amber = float3(1.0, 0.59, 0.20);
    float3 deep = float3(0.12, 0.04, 0.03);

    //盘体
    float disc = smoothstep(0.85, 0.75, r);

    //同心量化环：环线 + 每圈独立明灭（纯径向量化）
    float ringF = r * 6.0;
    float ringId = floor(ringF);
    float ringLine = smoothstep(0.5, 0.38, abs(frac(ringF) - 0.5));
    float ringFlick = 0.5 + 0.5 * step(0.35, hash21(float2(ringId, floor(t * 2.2 + uSeed))));

    //数据流辐条：sin(12*ang) 整数次谐波，跨 2π 连续
    float spokes = max(sin(ang * 12.0 + t * 3.0 + uSpin * 2.0), 0.0);
    spokes = pow(spokes, 6.0) * smoothstep(0.2, 0.45, r) * disc;

    //瞳孔核心：充能收缩 = 收束读秒
    float coreR = 0.20 - uCharge * 0.06;
    float core = smoothstep(coreR, coreR * 0.25, r);
    float iris = smoothstep(0.04, 0.0, abs(r - (coreR + 0.06)));

    //失血侵蚀：直角网格块蚀灭（笛卡尔哈希不含极角，天然无缝）；核心最后熄灭
    float2 cell = floor(p * 7.0 + uSeed);
    float dead = step(hash21(cell), uDecay * 0.9) * step(0.30, r);
    float alive = 1.0 - dead * 0.92;

    float3 hot = lerp(ember, float3(1.0, 0.95, 0.88), uCharge);

    float3 col = (deep * disc
        + ember * ringLine * ringFlick * disc * 0.7
        + amber * spokes * 0.8
        + hot * iris * 0.9
        + lerp(hot, float3(1.0, 1.0, 1.0), 0.6) * core) * alive;
    float alpha = saturate(disc * 0.35 + ringLine * disc * 0.5 + spokes * 0.6
        + core + iris * 0.8) * alive;

    float guard = smoothstep(1.0, 0.92, max(abs(p.x), abs(p.y)));
    col = saturate(col) * guard * uAlpha;
    alpha = saturate(alpha) * guard * uAlpha;
    return float4(col * alpha, alpha);
}

technique TechSealRing
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSSealRing();
    }
}

technique TechCoreEye
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCoreEye();
    }
}
