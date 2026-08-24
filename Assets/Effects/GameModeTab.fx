// ============================================================================
//GameModeTab.fx 游戏模式标签（残酷/修罗）
//SDF 旗标：暗漆底 + 模式纹样（爪痕/修罗火环）+ 点亮呼吸 + 切换爆发环
//AlphaBlend 预乘；ps_3_0
//uMode 0=残酷（三道爪痕） 1=修罗（环+三棱+芯）
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution; //quad 尺寸（px），只用于长宽比
float uMode;        //0 残酷 / 1 修罗
float uLit;         //0..1 点亮程度（CPU 缓动）
float uHover;       //0..1 悬停
float uBurst;       //0..1 切换爆发进度（1=无）
float uBurstOn;     //1=本次爆发是"开启"（纹样随爆发逐道亮），0=关闭爆发
float uDisabled;    //0..1 Boss 锁定置灰
float3 uAccent;     //模式主色
float3 uEmber;      //模式余烬色（残酷=橙，修罗=金）

float sdRoundBox(float2 p, float2 b, float r)
{
    float2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

//带锥度与撕口的爪痕笔画：返回 0..1 覆盖
float clawStroke(float2 p, float2 a, float2 b, float w0, float w1, float seed)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / dot(ba, ba));
    float d = length(pa - ba * h);
    float w = lerp(w0, w1, h);
    w *= 1.0 + 0.30 * sin(h * 26.0 + seed); //锯齿撕痕
    return smoothstep(w, w * 0.40, d);
}

//修罗棱：自环缘向外的锥形短刃
float prong(float2 p, float ang)
{
    float s = sin(ang);
    float c = cos(ang);
    float2 q = float2(c * p.x + s * p.y, -s * p.x + c * p.y);
    float h = saturate((q.x - 0.20) / 0.20);
    float d = length(float2(q.x - clamp(q.x, 0.20, 0.40), q.y));
    float w = lerp(0.040, 0.006, h);
    return smoothstep(w, w * 0.40, d);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //长宽比校正：y 半幅 0.5
    float aspect = uResolution.x / max(uResolution.y, 1.0);
    float2 p = (coords - 0.5) * float2(aspect, 1.0);

    //——旗身：圆角矩形 + 底缘燕尾切口——
    float d = sdRoundBox(p, float2(aspect * 0.86 * 0.5, 0.44), 0.055);
    float notchIn = step(0.26, p.y) * (1.0 - smoothstep(0.0, 0.02, abs(p.x) - (p.y - 0.26) * 0.55));
    float body = smoothstep(0.012, -0.006, d) * (1.0 - notchIn);
    float r = length(p);

    //漆底 + 纵向拉丝
    float grain = sin(p.x * 42.0 + sin(p.y * 9.0)) * 0.5 + 0.5;
    float3 col = float3(0.055, 0.040, 0.048) * (0.85 + grain * 0.16);
    //底部沉一点，顶缘受光
    col *= 1.0 - (p.y + 0.5) * 0.22;

    //点亮时旗内自下而上的一层暗火衬
    float innerFire = saturate(0.5 - p.y) * uLit * 0.20;
    col += uAccent * innerFire * (0.8 + 0.2 * sin(uTime * 1.7));

    //——模式纹样——
    //注意：不要在 uniform 上写 if 分支（MojoShader 会把常量布局搅乱，实测一个作业分支取反、
    //另一个作业整片 NaN 条带），两套纹样都算完用 uMode lerp 混合
    float breath = 0.86 + 0.14 * sin(uTime * 2.4 + uMode * 1.7);
    //开启爆发时纹样逐段亮：门值随爆发推进；平时全亮
    float gate = lerp(1.0, uBurst, uBurstOn * step(uBurst, 0.999));
    float g0 = smoothstep(0.10, 0.28, gate);
    float g1 = smoothstep(0.36, 0.54, gate);
    float g2 = smoothstep(0.62, 0.80, gate);

    //残酷：三道爪痕，右上向左下撕开
    float2 dir = normalize(float2(-0.52, 1.0));
    float2 perp = float2(-dir.y, dir.x);
    float claw = 0.0;
    claw += clawStroke(p, -dir * 0.30 + perp * 0.15 + float2(0.02, -0.03),
                          dir * 0.26 + perp * 0.15, 0.052, 0.008, 1.3) * g0;
    claw += clawStroke(p, -dir * 0.34 + float2(-0.01, 0.0),
                          dir * 0.30, 0.060, 0.010, 4.1) * g1;
    claw += clawStroke(p, -dir * 0.27 - perp * 0.15 + float2(0.01, 0.03),
                          dir * 0.24 - perp * 0.15, 0.048, 0.007, 7.9) * g2;

    //修罗：火环 + 三向棱 + 芯
    float ringR = 0.235 + 0.012 * sin(uTime * 2.1) * uLit;
    float asura = smoothstep(0.034, 0.012, abs(r - ringR)) * g0;
    asura += (prong(p, -1.5708) + prong(p, 2.618) + prong(p, 0.5236)) * g1;
    asura += smoothstep(0.055, 0.018, r) * g2;

    float icon = saturate(lerp(claw, asura, saturate(uMode)));

    //未点亮=石上刻痕，点亮=主色+余烬芯
    float hot = icon * icon * icon;
    float3 litCol = lerp(uAccent * 0.55, uAccent * 1.15, icon);
    litCol = lerp(litCol, uEmber, hot * 0.85);
    float3 dimCol = float3(0.30, 0.27, 0.26) * (0.45 + 0.55 * icon);
    float iconEnergy = uLit * breath + uHover * 0.10;
    col = lerp(col, lerp(dimCol, litCol, saturate(uLit * 1.2)), icon * saturate(0.55 + iconEnergy * 0.6));
    //点亮泛光
    col += uAccent * icon * uLit * breath * 0.30;

    //——顶缘受光线 + 悬停缘光——
    float edge = smoothstep(0.016, 0.002, abs(d + 0.006));
    float topLight = saturate(0.5 - (p.y + 0.5)) + 0.25;
    float3 rimCol = lerp(float3(0.42, 0.38, 0.36), lerp(uAccent, uEmber, 0.35), uLit);
    col += rimCol * edge * topLight * (0.55 + uHover * 0.55 + uLit * 0.25);

    //——切换爆发：扩张环闪——
    float shock = smoothstep(0.10, 0.0, abs(r - uBurst * 0.62)) * (1.0 - uBurst) * step(uBurst, 0.999);
    col += (uEmber * 0.8 + 0.45) * shock * (0.55 + 0.45 * uBurstOn);

    //——Boss 锁定：压灰——
    float gray = dot(col, float3(0.30, 0.50, 0.20));
    col = lerp(col, float3(gray, gray, gray) * 0.55, uDisabled * 0.85);

    //——合成：旗身 + 悬停外晕——
    float halo = smoothstep(0.10, 0.0, d) * (1.0 - body) * (uHover * 0.35 + shock * 0.5);
    float a = saturate(body + halo) * uAlpha;
    float3 final = col * body + lerp(uAccent, uEmber, 0.5) * halo;

    return float4(final * a, a);
}

technique Technique1
{
    pass GameModeTabPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
