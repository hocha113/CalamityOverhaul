//OniRainDescent.fx 深潜演出的湿墨冲刷合成（雨世界内再撑一层）
//TechDescent: 世界是画在纸上的，雨把它冲掉
//             湿墨冲刷（逐列噪声驱动 UV 向下位移 + 去饱和压向冷灰青）
//             → 雨帘合拢（竖直雨线随 uCover 增厚，=1 时饱和成整幅自含水幕，
//               结算跳变全被幕体盖住）
//             → 排墨（幕顶随 uDrain 向下排走，撕口自伞顶先裂，边缘噪声溶解）；
//             包络全零时输出恒等输入，交接零跳变。
//直线算术+平 tex2D，无分支；s0=屏幕帧 s1=PerlinNoise
float uTime;    //秒
float uInkRun;  //0-1 湿墨冲刷强度，结算帧归零（切断藏在满幕后）
float uCover;   //0-1 雨帘遮蔽，1=全遮蔽
float uDrain;   //0-1 排墨进度，幕顶向下排走
float uFlash;   //0-1 结算雷闪，隔幕透光
float uOriginU; //伞的 uv.x，排墨撕口圆心
float uAspect;  //宽/高

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float4 PSDescent(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;

    //====== 湿墨冲刷：颜色被雨水冲得向下流淌 ======
    //列速差（静态大列）+ 细列纹，各列淌速不一才像湿画
    float colA = tex2D(uImage1, float2(uv.x * 3.1, 0.13)).r;
    float colB = tex2D(uImage1, float2(uv.x * 9.7, 0.57)).r;
    float run = uInkRun * uInkRun;
    float drop = run * (0.06 + 0.30 * colA + 0.10 * colB);
    float wob = (colB - 0.5) * 0.012 * run;

    //三段拖影：向上取源=内容向下淌，顶行被拉成竖丝
    float3 c0 = tex2D(uImage0, uv).rgb;
    float3 c1 = tex2D(uImage0, float2(uv.x + wob, max(uv.y - drop * 0.5, 0.002))).rgb;
    float3 c2 = tex2D(uImage0, float2(uv.x - wob, max(uv.y - drop, 0.002))).rgb;
    float3 washed = c0 * (1.0 - run * 0.72) + (c1 + c2) * (run * 0.36);

    //冲掉的颜色汇成墨：去饱和→冷灰青压色
    float grey = dot(washed, float3(0.30, 0.55, 0.15));
    float3 inkWorld = lerp(washed, grey.xxx, run * 0.55);
    inkWorld *= lerp(float3(1.0, 1.0, 1.0), float3(0.62, 0.72, 0.78), run);

    //====== 雨帘：竖直雨线增厚成整幅水幕 ======
    //双层快速下刷雨线，排墨段附加下滑量（uDrain 连续，相位不跳）
    float slide = uDrain * 1.2;
    float s1v = tex2D(uImage1, float2(uv.x * 7.5, uv.y * 0.35 - uTime * 1.15 - slide)).r;
    float s2v = tex2D(uImage1, float2(uv.x * 13.0 + 0.31, uv.y * 0.5 - uTime * 0.85 - slide)).r;
    float streaks = s1v * 0.6 + s2v * 0.4;

    //雨线密度随合幕加密；uCover=1 时无论噪声取值 occ 恒饱和为 1（全遮蔽契约）
    float sd = saturate((streaks - 0.48 + uCover * 0.30) * 3.2);
    float occ = saturate(sd * uCover * 1.25 + uCover * uCover * 1.1 - 0.08);

    //====== 排墨：幕顶向下排走，撕口自伞顶先裂、边缘噪声溶解 ======
    float tearN = tex2D(uImage1, float2(uv.x * 2.2 + 0.71, uTime * 0.05)).r;
    float nearUmb = exp2(-abs((uv.x - uOriginU) * uAspect) * 2.0);
    float front = uDrain * (1.30 + 0.25 * nearUmb) - 0.05;
    float drainMask = saturate((uv.y - front + (tearN - 0.5) * 0.10) * 10.0);
    occ *= drainMask;

    //====== 幕体材质：合拢期透出被冲刷的世界，近满幕收为自含水幕 ======
    float2 ruv = clamp(uv + float2((s2v - 0.5) * 0.02, (s1v - 0.5) * 0.012), 0.002, 0.998);
    float3 refracted = tex2D(uImage0, ruv).rgb;
    float rgrey = dot(refracted, float3(0.30, 0.55, 0.15));
    refracted = lerp(refracted, rgrey.xxx, 0.5) * float3(0.62, 0.72, 0.78);

    //自含幕面：沉青底 + 灰白雨线 + 碎亮水光
    float glint = saturate((s2v - 0.72) * 6.0);
    float3 sheetFlat = float3(0.085, 0.108, 0.126)
        + float3(0.38, 0.45, 0.47) * (sd * 0.42 + streaks * 0.18)
        + float3(0.50, 0.57, 0.59) * glint * 0.35;

    //满幕前折射让位：结算前后幕体不再透出世界，切层不穿帮
    float selfGate = saturate(uCover * 1.6 - 0.35);
    float3 sheetCol = lerp(refracted * 0.55 + sheetFlat * 0.45, sheetFlat, selfGate);

    //====== 合成：冲刷世界之上盖水幕，雷闪从幕后透光 ======
    float3 col = lerp(inkWorld, sheetCol, occ);
    float flashQ = uFlash * uFlash;
    col += float3(0.55, 0.62, 0.64) * flashQ * (0.10 + occ * 0.30);

    return float4(col, 1.0);
}

technique TechDescent
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSDescent();
    }
}
