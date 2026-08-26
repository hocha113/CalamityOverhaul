// ============================================================================
//BRelicWaxShell.fx 蜂涡信标·蜜蜡甲体表结晶膜
//placeholder2 方块quad，SpriteBatch Immediate + AlphaBlend(预乘输出)
//六角蜡室双lattice无分支SDF逐格结晶(uGrow自下而上+逐格哈希错序)，
//蜡材质三律：张力挂边(外缘沉暗饱和)、高光只走各向异性窄反射带、
//白只在uCrack/uFormPulse瞬间；纯哈希程序化零采样器，无atan2无uniform分支
//ps_3_0
// ============================================================================

float uTime;
float uGrow;       //0~1 结晶进度(融蜡时回落，蜡室按哈希反序退场)
float uCrack;      //吸收/碎裂闪 0~1
float uFormPulse;  //结晶完成扩张环 0~1
float uSeed;       //逐玩家错种
float3 uColWax;    //蜜蜡浅
float3 uColAmber;  //琥珀深

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//正周期取模(负坐标安全)
float2 mod2(float2 p, float2 m)
{
    return p - m * floor(p / m);
}

float4 WaxShellPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;

    //人形椭圆壳：横向收窄
    float es = length(float2(centered.x / 0.72, centered.y));

    //膜带：主体环带 + 体表淡罩
    float shellBand = smoothstep(0.98, 0.86, es) * smoothstep(0.5, 0.66, es);
    float frontFill = smoothstep(0.66, 0.28, es) * 0.16;

    //---- 六角蜡室(双lattice，step选择无分支) ----
    float2 p = centered * 3.4 + uSeed * 17.0;
    float2 rr = float2(1.0, 1.7320508);
    float2 h = rr * 0.5;
    float2 a = mod2(p, rr) - h;
    float2 b = mod2(p - h, rr) - h;
    float sel = step(dot(b, b), dot(a, a));
    float2 g = lerp(a, b, sel);
    float2 cellId = p - g;

    float2 q = abs(g);
    //平顶六角距离场：格心0→格缘0.5
    float hexd = max(q.x * 0.866025 + q.y * 0.5, q.y);
    float border = smoothstep(0.36, 0.5, hexd);

    //---- 逐格结晶：自下而上 + 哈希错序 ----
    float cellHash = hash21(cellId + uSeed * 31.0);
    float heightBias = 1.0 - (centered.y * 0.5 + 0.5);   //底部先结
    float thr = cellHash * 0.58 + heightBias * 0.42;
    float on = saturate((uGrow * 1.05 - thr) / 0.09);
    //结晶前沿：正在成形的格短暂发亮
    float frontGlow = on * (1.0 - on) * 4.0;

    //---- 蜡体配色 ----
    //格内明暗错落 + 上方受光
    float3 body = lerp(uColAmber, uColWax, cellHash * 0.45 + 0.25 - centered.y * 0.18);
    //张力挂边：外缘沉暗更饱和
    float rimDark = smoothstep(0.7, 0.96, es);
    body *= 1.0 - rimDark * 0.34;
    //格缘沟线沉一点
    body *= 1.0 - border * 0.22;

    //各向异性窄反射带：缓移横带，逐格哈希闪烁(圆形高光=塑料，禁)
    float bandY = sin(uTime * 0.6 + uSeed * 5.0) * 0.34;
    float glintBand = exp(-pow((centered.y - bandY) * 6.5, 2.0));
    float glintMask = 0.45 + 0.55 * sin(uTime * 0.9 + cellHash * 6.2831);
    body += uColWax * glintBand * glintMask * 0.28;

    //结晶前沿亮线
    body += uColWax * frontGlow * 0.5;

    //碎裂/吸收闪：格缘网发白(≤数帧由消费端包络控制)
    body += float3(1.0, 0.96, 0.8) * border * on * uCrack * 0.85;
    body += float3(1.0, 0.9, 0.6) * uCrack * 0.2;

    //结晶完成扩张环
    float pulseR = 1.25 - uFormPulse * 0.55;
    float ringPulse = exp(-pow((es - pulseR) * 8.0, 2.0)) * uFormPulse;
    body += uColWax * ringPulse * 0.9;

    //---- 合成(预乘) ----
    float alpha = (shellBand * (0.4 + cellHash * 0.16) + frontFill) * on;
    alpha += ringPulse * 0.35;
    alpha = saturate(alpha) * vertexColor.a;

    return float4(body * alpha, alpha);
}

technique WaxShell
{
    pass P0
    {
        PixelShader = compile ps_3_0 WaxShellPS();
    }
}
