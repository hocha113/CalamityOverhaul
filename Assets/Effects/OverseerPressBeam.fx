// ============================================================================
//OverseerPressBeam.fx 压印锤行程标线（光柱预告）
//材质=机加工准直标线，与能量激光划清：两缘硬轨线 + 柱内下行刻度杠
//（行程读数在倒数）+ 落点脚线 + 自外向锤宽收拢的瞄准角标。
//端点契约：顶端=锤底发射面亮线（光从锤头投下），底端=脚线+角标框（不平切）。
//宽度生命周期：uCharge 前 15% 自中线展开，锁定拍 uLock 白闪 ≤2f。
//画布契约（C# 折算同步改）：quad 宽 = HammerWidth*2（束体半宽占 xc 0.5），
//quad 高 = 柱长，v=0 锤底 / v=1 地板。
//只进 Additive 批：rgb 不预乘、a 携带包络（加色批源因子是 SrcAlpha）。
//s1=PerlinNoise（实测值域 0.22~0.776）
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;      //秒
float uCharge;    //预告进度 0..1
float uLock;      //锁定白闪 0..1（C# 在预告末 2f 打 1 再衰减）
float uSeed;      //实例相位
float uLenPx;     //柱长（世界 px），刻度间距换算用

//束体半宽在 xc 空间的位置（quad 宽=束宽×2 的折算结果，C# 侧同源）
static const float BEAM_HALF = 0.5;

static const float3 RAIL_ORANGE = float3(1.00, 0.56, 0.18);  //轨线炉橙
static const float3 TICK_AMBER  = float3(0.95, 0.66, 0.30);  //刻度琥珀
static const float3 FOOT_HOT    = float3(1.00, 0.78, 0.42);  //脚线热金

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float4 PSBeam(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float xc = (uv.x - 0.5) * 2.0;   //[-1,1]
    float v = uv.y;                  //0 锤底 → 1 地板
    float ax = abs(xc);

    //====== 展开包络：自中线向两侧展开（前 15%），锁定后维持 ======
    float grow = smoothstep(0.0, 0.15, uCharge);
    float halfNow = BEAM_HALF * grow;
    float inBeam = 1.0 - smoothstep(halfNow, halfNow + 0.04, ax);

    //====== 两缘硬轨线：准直双线（机加工的答案）======
    float railD = abs(ax - halfNow);
    float rail = exp2(-railD * railD * 2600.0) * grow;

    //====== 柱内下行刻度杠：行程读数在倒数，速度随 charge 加快 ======
    float tickRows = max(uLenPx / 26.0, 4.0);
    float tickPhase = frac(v * tickRows - uTime * (2.2 + uCharge * 4.0) - uSeed);
    float tick = smoothstep(0.06, 0.16, tickPhase) * smoothstep(0.40, 0.30, tickPhase);
    //刻度只在束体中带，且随 charge 提亮
    float tickMask = 1.0 - smoothstep(halfNow * 0.72, halfNow * 0.86, ax);
    tick *= tickMask * (0.22 + 0.55 * uCharge);
    //噪声撕一点参差，不许是干净印刷线
    float tn = noiseTex(float2(xc * 0.8 + uSeed, v * 3.0 - uTime * 0.3));
    tick *= 0.65 + 0.5 * tn;

    //====== 中带底光：束体内微光（暗于轨线，标线不是光束）======
    float fill = inBeam * (0.045 + 0.10 * uCharge);
    //纵向不均匀：噪声亮带缓慢下行
    float bandN = noiseTex(float2(uSeed * 3.1, v * 1.6 - uTime * 0.55));
    fill *= 0.7 + 0.6 * smoothstep(0.38, 0.68, bandN);

    //====== 顶端发射面：锤底一线亮（光从锤头投下）======
    float emitter = exp2(-v * v * 1800.0) * inBeam * 1.6;

    //====== 落点脚线：地板处横亮线 ======
    float footD = abs(v - 0.985);
    float foot = exp2(-footD * footD * 9000.0) * inBeam * (0.5 + 0.8 * uCharge);

    //====== 瞄准角标：两侧短杠自外向束宽收拢（charge 驱动的收敛承诺）======
    float bracketX = lerp(0.96, halfNow + 0.06, smoothstep(0.05, 0.85, uCharge));
    float bkD = abs(ax - bracketX);
    float bkBand = smoothstep(0.945, 0.955, v) * smoothstep(0.995, 0.985, v);
    float bracket = exp2(-bkD * bkD * 900.0) * bkBand * grow * 1.4;

    //====== 合成 ======
    float railLum = rail * (0.55 + 0.45 * uCharge);
    float3 col = RAIL_ORANGE * railLum
               + TICK_AMBER * tick
               + RAIL_ORANGE * fill
               + FOOT_HOT * (emitter + foot)
               + FOOT_HOT * bracket;

    //锁定白闪：全柱一拍过曝
    col += float3(1.0, 0.97, 0.9) * uLock * inBeam * 0.8;

    float alpha = saturate(railLum + tick + fill + emitter + foot + bracket + uLock * inBeam * 0.8);
    //Additive 契约：rgb 不预乘，a 携带包络
    return float4(col * vc.rgb, alpha * vc.a);
}

technique TechBeam {
    pass P0 {
        PixelShader = compile ps_3_0 PSBeam();
    }
}
