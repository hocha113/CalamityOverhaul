// ============================================================================
//ProofingHallMelt.fx 验收堂浇注坑的常驻熔浴（侧视，坑内立面）
//与 A3 OverseerSlagFlow 分家：那边是飞行渣团+会干涸的余渣斑（uCool/uDry 生命周期），
//这边是房间固定设施=永远在坑里的熔浴，材质签名走「对流」不走「干涸」：
//双噪声等值线对流热缝在暗液面上游走、缘部壳板贴坑壁生长、液面一线亮带、
//面上热辉呼吸 + 高热期格哈希泡爆闪点。uHeat 由房态驱动（冷炉待机→战斗沸腾）。
//quad 映射：uv.y=0 在液面上方辉光带顶，液面在 v≈0.42，v=1 坑底。
//极角审计：全笛卡尔，噪声输入=平移坐标+时间，无 atan2。
//预乘输出，AlphaBlend 批。s1=PerlinNoise（值域 0.22~0.776）
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;   //秒
float uSeed;   //坑位相位（三坑错拍）
float uHeat;   //房态热度 0=冷炉结壳 1=战斗沸腾

static const float3 MELT_CORE = float3(1.000, 0.780, 0.400);  //熔金
static const float3 MELT_HOT  = float3(0.980, 0.470, 0.130);  //炉橙
static const float3 MELT_RED  = float3(0.560, 0.140, 0.055);  //暗红熔体
static const float3 CRUST     = float3(0.135, 0.080, 0.058);  //黑壳
static const float3 IRON      = float3(0.205, 0.185, 0.190);  //冷铁

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float4 PSBath(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float px = (uv.x - 0.5) * 2.0;   //[-1,1] 横向，坑壁在 ±1
    float heat = saturate(uHeat);

    //液面起伏：对流顶起的低幅波，热度越高越活
    float surf = 0.42 + (noiseTex(float2(px * 0.6 + uTime * 0.05, uSeed)) - 0.5)
        * 0.05 * (0.4 + heat * 0.8);
    float below = uv.y - surf;       //>0 液内

    //====== 对流热缝：双噪声场等值线（缝=两场相等处），慢滚显粘度 ======
    float2 cp = float2(px * 1.35 + uSeed * 7.0, uv.y * 2.1 + uSeed * 3.0);
    float n1 = noiseTex(cp * 0.50 + float2(uTime * 0.045, 0.0));
    float n2 = noiseTex(cp * 1.10 - float2(uTime * 0.028, uTime * 0.012));
    float vein = 1.0 - smoothstep(0.035, 0.15, abs(n1 - n2));

    //====== 缘部壳板：贴坑壁 + 低热期向心生长（战斗沸腾时缩回缘部）======
    float edgeK = abs(px);
    float cn = noiseTex(float2(px * 0.85 + uSeed * 3.1, uv.y * 1.4 + uSeed));
    float crustGate = saturate((1.0 - heat) * 1.05 + smoothstep(0.58, 0.97, edgeK) * 0.85 - 0.18);
    float crust = smoothstep(0.62 - crustGate * 0.30, 0.74 - crustGate * 0.16, cn)
        * saturate(crustGate * 1.5);

    //====== 液内体色：深度降温 + 对流缝提亮（壳区不亮）======
    float depthK = saturate(below / 0.58);
    float bodyHeat = saturate(heat * (1.0 - depthK * 0.55) + 0.08);
    float3 melt = lerp(MELT_RED, MELT_HOT, saturate(bodyHeat * 1.55));
    //熔金只许顺对流缝与液面亮带出场：体色漫金=能量池不是铁水，压半
    melt = lerp(melt, MELT_CORE, saturate(bodyHeat * 1.25 - 0.62) * 0.45);
    melt += MELT_CORE * vein * bodyHeat * (0.45 + heat * 0.55) * (1.0 - crust);
    float3 crustCol = lerp(CRUST, IRON, (1.0 - heat) * 0.75);
    //壳板缝里漏光（热度越低漏得越暗）
    melt = lerp(melt, crustCol + MELT_HOT * vein * heat * 0.30, crust);

    //====== 液面一线亮带（壳区断开；圆高光=塑料的反义，走窄带）======
    float band = exp2(-below * below * 950.0) * (1.0 - crust)
        * (0.45 + 0.55 * noiseTex(float2(px * 1.7 - uTime * 0.20, uSeed * 5.0)));
    melt += MELT_CORE * band * (0.35 + heat * 0.75);

    //====== 高热泡爆闪点：格哈希相位，液面附近瞬时亮鼓 ======
    float cellId = floor(px * 3.5 + uSeed * 11.0);
    float lx = frac(px * 3.5 + uSeed * 11.0) - 0.5;
    float bh = frac(sin(cellId * 127.1 + uSeed * 311.7) * 43758.55);
    float bubbleT = frac(uTime * (0.5 + bh * 0.7) + bh);
    float bubbleGate = smoothstep(0.09, 0.02, abs(bubbleT - 0.5)) * step(0.4, bh)
        * saturate(heat * 1.6 - 0.35);
    float bubbleD = lx * lx * 80.0 + (below - 0.04) * (below - 0.04) * 320.0;
    melt += float3(1.0, 0.92, 0.75) * exp2(-bubbleD) * bubbleGate * (1.0 - crust);

    //====== 面上热辉：指数衰减呼吸柱（liquid 外唯一出面的部分）======
    float aboveH = saturate(-below / 0.42);
    float shim = noiseTex(float2(px * 0.9 + uSeed, -below * 1.6 - uTime * 0.12));
    float glow = exp(-aboveH * 4.2) * (0.30 + 0.70 * shim)
        * (0.18 + heat * 0.82) * (1.0 - smoothstep(0.85, 1.0, edgeK));

    //====== 合成（预乘）：液内近不透明；面上辉光走近加法（alpha 压三成，色全强度）======
    if (below < 0.0) {
        float ga = vc.a * glow;
        float3 gcol = lerp(MELT_HOT, MELT_CORE, heat * 0.6);
        return float4(gcol * vc.rgb * ga, ga * 0.3);
    }
    float inPool = smoothstep(0.0, 0.03, below);
    float a = vc.a * (0.88 + 0.12 * heat) * inPool;
    return float4(melt * vc.rgb * a, a);
}

technique TechBath {
    pass P0 {
        PixelShader = compile ps_3_0 PSBath();
    }
}
