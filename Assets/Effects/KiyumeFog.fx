// ============================================================================
//KiyumeFog.fx 鬼梦湖雾——世界锚定密度场 + 看得见的雾面（单 technique 双 pass）
//FogFilter: Filters.Scene 前景瘴气（拷屏合成，并在这一层把亮点晕开=雾吃光）
//FogOverlay: PostDrawTiles 背景雾（预乘 AlphaBlend）
//密度住在世界坐标: s1 密度窗口纹理(rgb=雾色, a=密度, KiyumeFogSim 每 2 tick 上传)
//与深牢迷雾的分野：那团雾是均质的空气，这层雾是有水位的液体——
//uFogLineY 给出水平面，着色器在面上压一条亮边，从下看是天花板，从上看是海面
//直线算术、无分支、无 atan2、噪声全走绑定贴图（FNA3D 翻译纪律）
//uniform 是设备全局状态：两个调用点各自全参数重设，防跨调用残值串场
// ============================================================================

sampler uScreen : register(s0);   //滤镜通道=拷屏；覆盖通道=白像素画布（不采样）
sampler uDensity : register(s1);  //密度窗口纹理，LinearClamp
sampler uNoise : register(s2);    //Masking/PerlinNoise 512²，LinearWrap；G 通道实测域 0.22~0.776

float2 uScreenSize;   //目标像素尺寸
float2 uWorldScale;   //目标px→世界px 仿射（每通道各自矩阵求逆后上载）
float2 uWorldOffset;
float2 uFogOrigin;    //密度窗口原点（世界px，整雾元对齐）
float2 uFogUvMul;     //1/(容量雾元数×64px)
float4 uFogUvClamp;   //xy=min uv, zw=max uv（半 texel 内缩到实际窗口子矩形）
float2 uPhase;        //层间噪声去相相位（前后层错开防同相贴纸感）
float uTime;
float uLayerMul;      //本层不透明度系数（背景 0.80 / 前景 0.40）
float uPresence;      //全局淡入淡出

float uFogLineY;      //雾线基准（世界px，潮汐驱动）
float uLakeRightPx;   //血湖右缘（世界px）：倾斜与衰减的原点
float uTiltPx;        //湖侧雾面抬升量（px）
float uTiltSpanPx;    //抬升衰减跨度（px）
float uSurfaceGlow;   //雾面亮边强度
float uEatLight;      //吃光强度（只有滤镜通道非零）
float uEatSpread;     //吃光晕开半径（屏幕px）

float uLakeWaterY;     //血湖真水面（世界px，固定不随潮汐）
float uRimFadeStartPx; //雾面亮边渐入起点（=水面右缘）：以西 rim 归零，湖上不许有悬空液面线
float uRimFadeSpanPx;  //渐入跨度（px）：岸线以东这么远内 rim 长回全强
float uWaterGlow;      //水面烬光反射带强度

//雾面亮边色：比雾本体亮一档的血色，让"这是一层液面"读得出来
static const float3 SURFACE_TINT = float3(0.62, 0.20, 0.16);
//水面反射色与水下深渊色：烬光落在血水上 / 深不见底的湖体
static const float3 WATER_TINT = float3(0.88, 0.30, 0.12);
static const float3 DEEP_TINT = float3(0.055, 0.008, 0.012);

//雾面世界 Y：湖侧抬高 + 三道不同波长的行波
//短波是面上的碎浪，中波是起伏，长波是整片雾海在缓缓呼吸
float FogSurfaceY(float wx) {
    float t = saturate(1.0 - (wx - uLakeRightPx) / max(uTiltSpanPx, 1.0));
    float lean = t * t * (3.0 - 2.0 * t);
    float y = uFogLineY - uTiltPx * lean;
    y += sin(wx * 0.00210 - uTime * 0.55) * 7.0;
    y += sin(wx * 0.00073 + uTime * 0.31) * 15.0;
    y += (tex2D(uNoise, float2(wx * 0.000075, uTime * 0.008)).g - 0.5) * 34.0;
    return y;
}

//共用求值：密度采样 + 3 倍频翻涌 + 雾面亮边
float4 FogEval(float2 tpx) {
    float2 world = uWorldOffset + tpx * uWorldScale;
    float2 fuv = clamp((world - uFogOrigin) * uFogUvMul, uFogUvClamp.xy, uFogUvClamp.zw);
    float4 cell = tex2D(uDensity, fuv);

    //世界锚定滚动噪声：y 正向偏移=纹样上飘，x 交替横向翻涌
    float2 wuv = world * 0.00074 + uPhase;
    float n1 = tex2D(uNoise, wuv + float2(uTime * 0.006, uTime * 0.014)).g;
    float n2 = tex2D(uNoise, wuv * 2.17 + float2(-uTime * 0.011, uTime * 0.026)).g;
    float n3 = tex2D(uNoise, wuv * 4.63 + float2(uTime * 0.019, uTime * 0.043)).g;
    float turb = n1 * 0.50 + n2 * 0.32 + n3 * 0.18;
    //域校准：turb 理论域≈0.22~0.78（PerlinNoise G 实测），映到 0..1，禁高分位死阈值
    float tn = saturate((turb - 0.30) * 2.6);

    float a = saturate(cell.a * (0.45 + 1.15 * tn)) * uLayerMul * uPresence;
    float3 col = cell.rgb * (0.88 + 0.24 * tn);

    //====== 雾面：这层雾有水位，面看得见 ======
    //湖区闸门：水面右缘以西 rim 归零（湖上没有第二条液面线），岸线以东渐入全强
    float rimGate = saturate((world.x - uRimFadeStartPx) / max(uRimFadeSpanPx, 1.0));
    float dSurf = world.y - FogSurfaceY(world.x);
    //贴面亮带：上下对称，站在雾里抬头是天花板，站在山上低头是海面
    float rim = exp2(-abs(dSurf) * 0.055) * (0.55 + 0.45 * tn);
    //面下近表层：约百来像素的厚度感，免得亮边读成一条贴上去的线
    float shelf = saturate(dSurf * 0.010) * exp2(-max(dSurf, 0.0) * 0.0075);
    //没雾的地方不许发光：全部乘密度
    float lit = uSurfaceGlow * cell.a * rimGate;
    col += SURFACE_TINT * (rim * 0.85 + shelf * 0.35) * lit;
    a = saturate(a + rim * 0.22 * lit * uPresence * saturate(cell.a * 3.0));

    //====== 血湖水面：近景唯一的锐利水平线，与雾海面在岸线处互补交接 ======
    float waterGate = 1.0 - rimGate;
    //小振幅行波：真水面比雾面稳得多，只轻轻晃
    float waterWave = sin(world.x * 0.0110 - uTime * 0.90) * 2.2
        + sin(world.x * 0.0037 + uTime * 0.50) * 3.6;
    float dw = world.y - (uLakeWaterY + waterWave);
    //锐利面线：几像素宽的亮心，雾越浓越被罩住
    float waterRim = exp2(-abs(dw) * 0.22);
    //面下波光带：高频噪声闪点，只活在近表 ~130px（阈值按 PerlinNoise G 实测域 0.22~0.776 取 75 分位）
    float glintBand = saturate(dw * 0.05) * exp2(-max(dw, 0.0) * 0.008);
    float glint = saturate(tex2D(uNoise, float2(world.x * 0.0040 - uTime * 0.030,
        world.y * 0.0040 + uTime * 0.011)).g - 0.55) * 2.8;
    float waterLit = uWaterGlow * uPresence * waterGate * (1.0 - cell.a * 0.40);
    col += WATER_TINT * (waterRim * 0.90 + glint * glintBand * 0.50) * waterLit;
    a = saturate(a + (waterRim * 0.30 + glint * glintBand * 0.12) * waterLit);

    //水下深渊：面线以下渐入深血色，游得越深世界越沉——这就是西界的劝返
    float underwater = saturate(dw * 0.004) * waterGate;
    col = lerp(col, DEEP_TINT, underwater * 0.72);
    a = saturate(a + underwater * 0.42 * uPresence);

    return float4(col, a);
}

//双环十六向采样：内环拾光斑本体、外环拾漫开的余晖——亮点在雾里晕成一团而不是穿透过来
float3 ScreenHalo(float2 uv, float2 r) {
    float2 ri = r * 0.45;
    float3 s = tex2D(uScreen, uv + float2(ri.x, 0.0)).rgb;
    s += tex2D(uScreen, uv - float2(ri.x, 0.0)).rgb;
    s += tex2D(uScreen, uv + float2(0.0, ri.y)).rgb;
    s += tex2D(uScreen, uv - float2(0.0, ri.y)).rgb;
    s += tex2D(uScreen, uv + ri * 0.7).rgb;
    s += tex2D(uScreen, uv - ri * 0.7).rgb;
    s += tex2D(uScreen, uv + float2(ri.x, -ri.y) * 0.7).rgb;
    s += tex2D(uScreen, uv - float2(ri.x, -ri.y) * 0.7).rgb;
    s += tex2D(uScreen, uv + float2(r.x, 0.0)).rgb;
    s += tex2D(uScreen, uv - float2(r.x, 0.0)).rgb;
    s += tex2D(uScreen, uv + float2(0.0, r.y)).rgb;
    s += tex2D(uScreen, uv - float2(0.0, r.y)).rgb;
    s += tex2D(uScreen, uv + r * 0.7).rgb;
    s += tex2D(uScreen, uv - r * 0.7).rgb;
    s += tex2D(uScreen, uv + float2(r.x, -r.y) * 0.7).rgb;
    s += tex2D(uScreen, uv - float2(r.x, -r.y) * 0.7).rgb;
    return s * 0.0625;
}

//前景瘴气：拷屏合成。忽略 COLOR0（FilterManager 中间级把 ColorOfTheSkies 当顶点色传入，
//消费它会引入夜色二次压暗）
float4 PSFogFilter(float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uScreen, uv);
    float4 fog = FogEval(uv * uScreenSize);
    float3 outc = lerp(src.rgb, fog.rgb, fog.a);

    //雾吃光：光穿不过记忆，只把雾自己烘亮一圈。溢出量按雾浓度回加，并向烬色偏——雾里的光是暖的
    float2 r = uEatSpread / max(uScreenSize, 1.0);
    float3 halo = max(ScreenHalo(uv, r) - src.rgb, 0.0);
    float haloLum = dot(halo, float3(0.30, 0.50, 0.20));
    halo = lerp(halo, haloLum * float3(1.00, 0.42, 0.22), 0.55);
    outc += halo * fog.a * uEatLight;

    return float4(outc, src.a);
}

//背景雾：预乘输出进 AlphaBlend 批（暗雾必须能压暗——加色批物理上画不出暗，全线预乘）
float4 PSFogOverlay(float2 uv : TEXCOORD0) : COLOR0 {
    float4 fog = FogEval(uv * uScreenSize);
    return float4(fog.rgb * fog.a, fog.a);
}

//注意：Filters.Scene 的 ScreenShaderData 按 pass 名（"FogFilter"）查表，不是 technique 名；
//传错名会在 ShaderData.Apply 空引用并连锁 FilterManager 半开批崩溃
technique TechFog {
    pass FogFilter {
        PixelShader = compile ps_3_0 PSFogFilter();
    }
    pass FogOverlay {
        PixelShader = compile ps_3_0 PSFogOverlay();
    }
}
