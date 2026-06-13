// ============================================================================
// VoidTimeShift.fx 虚空聚落时空叠加
// 采样 uImage0 屏幕
// ============================================================================

sampler uImage0 : register(s0);

float filterIntensity;    //过去滤镜 0~1
float transitionStrength; //切换演出钟形 0~1
float glitchProximity;    //鬼乱码临近失真 0~1
float uTime;
float2 texelSize;         //1/宽 1/高
//未传入时使用保底常量，保证边缘提取仍能工作
float2 pixelSize;

//廉价伪随机哈希，用于低幅度胶片颗粒
float hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 MainPS(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;

    //鬼乱码临近的采样位移：扫描块式水平抖动+高频抖动噪声，越近幅度越大
    if (glitchProximity > 0.01)
    {
        float gp = saturate(glitchProximity);
        //大块水平位移：按屏幕竖向分段，每段整体左右抖
        float blockRow = floor(uv.y * 24.0);
        float blockShift = (hash12(float2(blockRow, floor(uTime * 9.0))) - 0.5) * 0.035 * gp;
        //稀疏强位移块：少数竖条被剧烈错位
        float strongPick = hash12(float2(blockRow + 37.0, floor(uTime * 7.0)));
        if (strongPick > 1.0 - 0.35 * gp)
        {
            blockShift += (hash12(float2(blockRow + 91.0, floor(uTime * 11.0))) - 0.5) * 0.09 * gp;
        }
        //细密微抖
        float microShift = (hash12(float2(floor(uv.y * 200.0), floor(uTime * 40.0))) - 0.5) * 0.008 * gp;
        uv.x += blockShift + microShift;
    }

    //切换演出的采样位移：高速水平信号错位带，仅在transitionStrength显著时出现
    if (transitionStrength > 0.01)
    {
        //条带遮罩，取高带顶部的窄区
        float bandY = frac(uv.y * 4.0 + uTime * 0.8);
        float bandMask = smoothstep(0.85, 1.0, bandY) * transitionStrength;
        //随时间步进的整行偏移，幅度受钟形曲线控制
        float shift = (hash12(float2(floor(uv.y * 60.0), floor(uTime * 30.0))) - 0.5) * 0.03;
        uv.x += shift * bandMask;
    }

    float4 original = tex2D(uImage0, uv);
    float3 color = original.rgb;

    //========================================
    //1.过去滤镜：分离色调胶片+S曲线对比+可读性边缘增强
    //核心策略：不整体压暗，通过暗部冷蓝与亮部琥珀的色相分离提供"过去感"
    //========================================
    if (filterIntensity > 0.001)
    {
        float fi = filterIntensity;

        //亮度
        float lum = dot(color, float3(0.299, 0.587, 0.114));

        //采样四邻域亮度差，构造廉价边缘增强让地形轮廓在褪色画面中依然清晰可读
        float2 px = (pixelSize.x > 0) ? pixelSize : float2(1.0 / 1920.0, 1.0 / 1080.0);
        float lumL = dot(tex2D(uImage0, coords - float2(px.x, 0)).rgb, float3(0.299, 0.587, 0.114));
        float lumR = dot(tex2D(uImage0, coords + float2(px.x, 0)).rgb, float3(0.299, 0.587, 0.114));
        float lumU = dot(tex2D(uImage0, coords - float2(0, px.y)).rgb, float3(0.299, 0.587, 0.114));
        float lumD = dot(tex2D(uImage0, coords + float2(0, px.y)).rgb, float3(0.299, 0.587, 0.114));
        float edge = saturate((abs(lum - lumL) + abs(lum - lumR) + abs(lum - lumU) + abs(lum - lumD)) * 1.2);

        //去饱和仅到40%，仍需要足够色彩信息承载后续的分离色调
        float3 gray = float3(lum, lum, lum);
        color = lerp(color, gray, 0.40 * fi);

        //== 分离色调Split Toning ==
        //暗部色调加法：按 smoothstep(lum) 选择性推进阴影区RGB
        //亮部色调加法：对亮区推进暖琥珀
        //避免了"色调乘亮度"导致暗区色相被吃掉的问题
        float shadowMask = 1.0 - smoothstep(0.0, 0.55, lum);
        float highlightMask = smoothstep(0.35, 0.9, lum);

        //暗部推冷蓝：负R负G正B
        float3 shadowPush = float3(-0.08, -0.03, 0.10);
        //亮部推琥珀：正R正G负B
        float3 highlightPush = float3(0.15, 0.06, -0.12);

        color += shadowPush * shadowMask * fi;
        color += highlightPush * highlightMask * fi;

        //== 中间调整体轻微偏青灰 ==
        //仅在中段亮度做小幅横推，让画面不至于红绿对比过强
        float midMask = (1.0 - abs(lum - 0.5) * 2.0);
        midMask = saturate(midMask);
        color += float3(-0.02, 0.00, 0.03) * midMask * fi * 0.6;

        //== S曲线对比度 ==
        //用平滑step以中点0.5拉伸，暗处更暗亮处更亮，形成胶片感对比
        //力度系数0.35适中，避免过曝
        float3 contrast = smoothstep(0.0, 1.0, color);
        color = lerp(color, contrast, 0.35 * fi);

        //== 柔和vignette，幅度更低 ==
        float2 vc = (coords - 0.5) * 2.0;
        float vd = dot(vc, vc);
        float vignette = 1.0 - vd * 0.22 * fi;
        color *= saturate(vignette);

        //== 边缘轮廓增强，让地形在褪色里依然清晰可读 ==
        //系数从0.6×0.1提升到1.0×0.15，差距可见但不到描边程度
        color += float3(0.10, 0.13, 0.16) * edge * fi;

        //== 胶片颗粒 ==
        float grainTime = floor(uTime * 18.0);
        float grain = hash12(coords * 640.0 + grainTime) - 0.5;
        color += grain * 0.018 * fi;

        //== 角落漏光bloom暗示 ==
        //四角随时间缓慢呼吸的琥珀色柔光，面积小幅度低，补足"老胶片曝光不均"的质感
        float cornerX = min(coords.x, 1.0 - coords.x);
        float cornerY = min(coords.y, 1.0 - coords.y);
        float cornerDist = length(float2(cornerX, cornerY));
        float cornerGlow = smoothstep(0.25, 0.0, cornerDist);
        float breathe = 0.75 + 0.25 * sin(uTime * 0.6);
        color += float3(0.06, 0.04, 0.02) * cornerGlow * fi * breathe * 0.6;

        //== 角落胶片破损磨砂层 ==
        //仅在外沿区域叠加，不干扰玩家操作视野中央
        //分三种层次：大颗磨砂斑块褪色、稀疏静态白色划痕、稀疏静态黑色尘点
        float damageMask = smoothstep(0.20, 0.03, cornerDist);
        if (damageMask > 0.001)
        {
            //低频斑块：大颗砂粒云，缓慢随时间偏移，营造"发霉胶片"质感
            float2 patchCoord = floor(coords * 55.0) + floor(uTime * 0.5);
            float patchNoise = hash12(patchCoord);
            float patch = smoothstep(0.58, 0.96, patchNoise);
            float3 frostTone = float3(0.46, 0.40, 0.33);
            color = lerp(color, frostTone, damageMask * patch * fi * 0.55);

            //静态细碎划痕：不随时间变化，稀疏白色高光点
            float scratch = step(0.988, hash12(coords * 1900.0 + 17.3));
            color += float3(0.35, 0.30, 0.24) * scratch * damageMask * fi;

            //静态尘点：稀疏黑色斑点，模拟乳剂剥落
            float speck = step(0.991, hash12(coords * 2300.0 + 91.7));
            color -= float3(0.18, 0.18, 0.18) * speck * damageMask * fi;

            //极角落轻度去饱和，让最外围褪色最重
            float extremeCorner = smoothstep(0.09, 0.0, cornerDist);
            float cornerLum = dot(color, float3(0.299, 0.587, 0.114));
            color = lerp(color, float3(cornerLum, cornerLum, cornerLum), extremeCorner * fi * 0.35);
        }

        //== 整体亮度补偿 ==
        //所有叠加后如果变暗则轻微补回，确保总亮度约等于原图的92%
        //不再有独立的压暗系数
        color *= lerp(1.0, 0.95, fi);
    }

    //========================================
    //2.切换演出：中线RGB色散+亮度爆闪+中间裂缝
    //========================================
    if (transitionStrength > 0.01)
    {
        float ts = transitionStrength;

        //水平RGB色散，越靠近中线越强
        float bandCenterDist = abs(coords.y - 0.5);
        float bandFall = smoothstep(0.5, 0.0, bandCenterDist);
        float caOffset = 0.012 * ts * bandFall;
        float r = tex2D(uImage0, uv + float2(caOffset, 0)).r;
        float b = tex2D(uImage0, uv - float2(caOffset, 0)).b;
        color.r = lerp(color.r, r, ts * bandFall * 0.7);
        color.b = lerp(color.b, b, ts * bandFall * 0.7);

        //亮度短促一闪
        color += float3(0.08, 0.09, 0.10) * ts;

        //中间水平裂缝亮带
        float seam = smoothstep(0.008, 0.0, bandCenterDist) * ts;
        color += float3(0.25, 0.22, 0.18) * seam;
    }

    //========================================
    //3.鬼乱码临近扰动：色散+扫描闪带+冷色相偏移
    //即使不在过去视角下也以弱强度呈现作为心理提示
    //========================================
    if (glitchProximity > 0.01)
    {
        float gp = saturate(glitchProximity);

        //水平RGB色散，随时间抖动
        float caJitter = (hash12(float2(floor(uv.y * 80.0), floor(uTime * 25.0))) - 0.5) * 0.012 * gp;
        float caAmt = 0.008 * gp + abs(caJitter);
        float r2 = tex2D(uImage0, uv + float2(caAmt, 0)).r;
        float b2 = tex2D(uImage0, uv - float2(caAmt, 0)).b;
        color.r = lerp(color.r, r2, 0.85 * gp);
        color.b = lerp(color.b, b2, 0.85 * gp);

        //高频扫描闪带，每帧选几条竖向扫描行暗化或亮化
        float scanBand = hash12(float2(floor(uv.y * 300.0), floor(uTime * 55.0)));
        float scanFlicker = step(1.0 - 0.12 * gp, scanBand);
        color = lerp(color, float3(0.0, 0.0, 0.0), scanFlicker * 0.55 * gp);
        float scanGlow = step(1.0 - 0.04 * gp, scanBand);
        color += float3(0.35, 0.12, 0.42) * scanGlow * gp;

        //整体向荧光紫偏，距离越近色相越失真
        color += float3(-0.03, -0.05, 0.08) * gp * 0.6;

        //噪声颗粒叠加
        float gnoise = hash12(coords * 900.0 + floor(uTime * 30.0)) - 0.5;
        color += gnoise * 0.04 * gp;
    }

    color = saturate(color);
    return float4(color, original.a);
}

technique Technique1
{
    pass VoidTimeShiftPass
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}
