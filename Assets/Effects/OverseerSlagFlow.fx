// ============================================================================
//OverseerSlagFlow.fx 铸造监工的熔渣双形态（EowAcid/SHPCModMagma 血统换铸造渣材质）
//熔渣签名=黑壳浮板骑在炽亮体上、噪声滚速压慢显粘度、白热只在头部小区。
//TechGlob 空中渣团：双频揉形轮廓 + 头部热芯（顺飞行向 +x）+ 尾部颈缩滴串
//（uStretch 驱动）+ 黑壳浮板自尾向头生长 + uCool 冷却史（炉橙→暗红→铁壳）。
//TechPool 贴地余渣斑：扁透镜液面 + 端部弯月挂边 + 窄反射带 +
//uDry 结皮渐干（黑壳自缘向心长、亮缝网萎缩）+ 早期泡爆点。
//极角审计：全笛卡尔，噪声输入=刚体旋转坐标+时间，无 atan2。
//预乘输出，AlphaBlend 批（黑壳要真遮挡）。s1=PerlinNoise（值域 0.22~0.776）
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;      //秒
float uSeed;      //实例相位
float uStretch;   //速度拉伸 0..1（飞行相：头 +x 尾 -x）
float uCool;      //冷却史 0=刚出包 1=冷透
float uDry;       //余渣斑干涸 0..1（TechPool 用）

static const float3 SLAG_CORE  = float3(1.000, 0.760, 0.360);  //熔金热芯
static const float3 SLAG_HOT   = float3(0.960, 0.430, 0.120);  //炽渣橙
static const float3 SLAG_RED   = float3(0.540, 0.130, 0.050);  //暗红渣体
static const float3 CRUST_DARK = float3(0.150, 0.085, 0.060);  //黑壳
static const float3 IRON_COLD  = float3(0.200, 0.180, 0.185);  //冷透铁壳

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//两频揉形：慢速粘度签名（滚速压慢）
float wobble(float2 p, float seed) {
    float n1 = noiseTex(p * 0.55 + float2(seed, uTime * 0.055));
    float n2 = noiseTex(p * 1.30 + float2(uTime * 0.04, seed * 2.1));
    return (n1 - 0.5) * 0.30 + (n2 - 0.5) * 0.14;
}

//------------------------------------------------------------------
//空中渣团
//------------------------------------------------------------------
float4 PSGlob(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float2 p = (uv - 0.5) * 2.0;   //[-1,1]，+x=飞行向（C# 旋转 quad 对齐速度）

    //速度拉伸：头钝尾长（尾部向 -x 抻出）
    float tailK = saturate(-p.x) * uStretch;
    float2 q = p;
    q.x /= 1.0 + uStretch * 0.55;          //整体沿飞行向拉长
    float r = length(q * float2(1.0, 1.35));

    //尾部颈缩：Plateau–Rayleigh 断滴（尾段半径被正弦颈缩咬细）
    float neck = 1.0 - 0.42 * tailK * (0.5 + 0.5 * sin(p.x * 9.0 + uSeed * 6.28));
    r /= max(neck, 0.3);

    float w = wobble(q, uSeed);
    float body = 1.0 - smoothstep(0.66 + w, 0.80 + w, r);
    if (body <= 0.003) {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    //====== 体色：头热尾冷的沿轴梯度 × uCool 冷却史 ======
    float headK = saturate(p.x * 0.9 + 0.5);           //+x 头部更热
    float heat = saturate(headK * (1.0 - uCool * 0.85) + 0.10);
    float3 col = lerp(SLAG_RED, SLAG_HOT, saturate(heat * 1.6));
    //熔金只在头端小区（白热大面积=能量球，不是渣）
    col = lerp(col, SLAG_CORE, saturate(heat * heat * 1.5 - 0.52));

    //====== 黑壳浮板：低频大块暗壳骑在亮体上，自尾向头生长，随冷却铺满 ======
    float cn = noiseTex(q * 0.8 + float2(uSeed * 1.7, uTime * 0.03));
    float crustGate = saturate(uCool * 0.9 + tailK * 0.55 + 0.10);
    float crust = smoothstep(0.64 - crustGate * 0.26, 0.72 - crustGate * 0.18, cn);
    float3 crustCol = lerp(CRUST_DARK, IRON_COLD, uCool);
    col = lerp(col, crustCol, crust * (0.60 + uCool * 0.38));

    //====== 张力亮缘：轮廓一线更亮（壳区不亮）======
    float rimD = abs(r - (0.72 + w));
    float rim = exp2(-rimD * rimD * 420.0) * (1.0 - crust) * heat;
    col += SLAG_CORE * rim * 0.8;

    //====== 头部白热小芯（只许小区，≤ 体面积一成）======
    float2 hp = q - float2(0.34, 0.0);
    float hotCore = exp(-dot(hp, hp) * 26.0) * (1.0 - uCool) * (1.0 - crust);
    col += float3(1.0, 0.93, 0.78) * hotCore * 0.9;

    //预乘输出
    float a = body * vc.a;
    return float4(col * vc.rgb * a, a);
}

//------------------------------------------------------------------
//贴地余渣斑
//------------------------------------------------------------------
float4 PSPool(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float2 p = (uv - 0.5) * 2.0;   //x=[-1,1] 横展，y 竖窄

    //扁透镜：液面在 y≈-0.15，池体向下鼓
    float surfY = -0.15;
    float w = wobble(float2(p.x * 0.9, 0.3), uSeed) * 0.5;
    float halfW = 0.80 + w * 0.3;
    //横向端部收圆（弯月挂边的形）
    float xNorm = p.x / halfW;
    float depth = sqrt(saturate(1.0 - xNorm * xNorm));    //端部收零的池深
    float bottom = surfY + depth * (0.72 - uDry * 0.18);  //干涸变薄
    float inPool = step(abs(xNorm), 1.0)
                 * smoothstep(surfY - 0.06, surfY + 0.02, p.y)
                 * smoothstep(bottom + 0.08, bottom - 0.04, p.y);
    if (inPool <= 0.003) {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    //====== 体色：中心近熔金、缘先冷 ======
    float edgeK = abs(xNorm);
    float heat = saturate((1.0 - edgeK * 0.75) * (1.0 - uDry));
    float3 col = lerp(SLAG_RED, SLAG_HOT, saturate(heat * 1.5));
    col = lerp(col, SLAG_CORE, saturate(heat * 1.3 - 0.55));

    //====== 结皮渐干：黑壳自缘向心长（新鲜态中心必须是净液面）======
    float cn = noiseTex(float2(p.x * 0.9 + uSeed * 3.0, p.y * 1.3 + uSeed));
    float crustLine = uDry * 1.05 - 0.02;
    float crust = smoothstep(crustLine + 0.42, crustLine + 0.14, 1.0 - edgeK + (cn - 0.5) * 0.30);
    crust = max(crust, smoothstep(0.55, 0.92, uDry));     //末段整体结皮
    float3 crustCol = lerp(CRUST_DARK, IRON_COLD, uDry * 0.7);
    //缝网：壳区里的亮缝（壳越干缝越窄越暗）
    float crack = 1.0 - smoothstep(0.03, 0.09, abs(cn - 0.5));
    float3 crackGlow = SLAG_HOT * crack * (1.0 - uDry) * 0.8;
    col = lerp(col, crustCol + crackGlow, crust);

    //====== 液面窄反射带：表面一线各向异性亮（圆高光=塑料的反义）======
    float bandD = abs(p.y - surfY - 0.06);
    float bn = noiseTex(float2(p.x * 1.8 - uTime * 0.22, uSeed * 5.0));
    float band = exp2(-bandD * bandD * 700.0) * smoothstep(0.34, 0.62, bn) * (1.0 - crust) * heat;
    col += SLAG_CORE * band * 0.9;

    //====== 弯月挂边：两端贴缘更暗更饱和 ======
    float meniscus = smoothstep(0.86, 1.0, edgeK);
    col = lerp(col, SLAG_RED * 0.55, meniscus * (1.0 - crust) * 0.6);

    //====== 早期泡爆点：格哈希相位的瞬时鼓包亮点（干后消失）======
    float cellId = floor(p.x * 3.0 + uSeed * 13.0);
    float lx = frac(p.x * 3.0 + uSeed * 13.0) - 0.5;      //格内局部坐标
    float bh = frac(sin(cellId * 127.1 + uSeed * 311.7) * 43758.55);
    float bubbleT = frac(uTime * (0.55 + bh * 0.65) + bh);
    float bubbleGate = smoothstep(0.10, 0.02, abs(bubbleT - 0.5)) * step(0.35, bh) * (1.0 - uDry);
    float bubbleD = lx * lx * 90.0 + (p.y - surfY - 0.10) * (p.y - surfY - 0.10) * 40.0;
    float bubble = exp2(-bubbleD) * bubbleGate * (1.0 - crust);
    col += float3(1.0, 0.9, 0.7) * bubble;

    //预乘输出
    float a = inPool * vc.a * (1.0 - smoothstep(0.75, 1.0, uDry) * 0.35);
    return float4(col * vc.rgb * a, a);
}

technique TechGlob {
    pass P0 {
        PixelShader = compile ps_3_0 PSGlob();
    }
}

technique TechPool {
    pass P0 {
        PixelShader = compile ps_3_0 PSPool();
    }
}
