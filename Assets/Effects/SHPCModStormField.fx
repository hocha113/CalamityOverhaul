// ============================================================================
//SHPCModStormField.fx 风暴枪托领域场
//单 quad 全领域；顺时针气旋+雨丝风纹+电光隐现，强度驱动浓烈度
//极坐标接缝纪律：sin(angle*k) 仅整数 k；normAngle 仅乘整数后经 frac 进 tex2D(wrap)；
//手写噪声输入一律用旋转笛卡尔坐标或 dist
// ============================================================================

sampler uImage0 : register(s0);   //批画布白像素,不采样
sampler noiseSamp : register(s1); //Perlin噪声,消费端绑Textures[1]+LinearWrap

float uTime;            //C#侧自管理视觉时间，强度越高推进越快
float fadeAlpha;        //整体淡入淡出 0~1
float intensity;        //风暴强度 0~1，浓烈度总闸
float boltGauge;        //落雷计量 0~1，临界时边缘预兆
float3 deepColor;       //暗雨蓝底
float3 stormColor;      //风暴主蓝
float3 arcColor;        //电光青白

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//旋转笛卡尔坐标（刚性仿射，天然无缝），旋转方向为屏幕顺时针，与C#吹偏方向一致
float2 rot(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);
    float normAngle = (angle + 3.14159) / 6.28318;

    //边界：0.86 为领域实际半径，向外留辉光带；直线算术，尾端乘 edgeFade 归零（禁动态分支）
    float ringR = 0.86;
    float edgeFade = 1.0 - smoothstep(ringR, 1.0, dist);

    //风眼：场心相对平静，风暴集中在环带
    float eyeMask = smoothstep(0.10, 0.34, dist);

    //======== A. 云雾底色（双层旋转噪声，无缝） ========
    //屏幕坐标 y 向下：采样域转 -a 时图案视觉转 +a（顺时针），与 C# 吹偏方向一致
    float2 p1 = rot(centered, -uTime * 1.6);
    float2 p2 = rot(centered, -uTime * 0.9 + 2.1);
    float cloud1 = tex2D(noiseSamp, frac(p1 * 0.55 + float2(0.13, 0.37))).r;
    float cloud2 = tex2D(noiseSamp, frac(p2 * 0.95 + float2(0.61, 0.09))).g;
    float cloud = cloud1 * 0.6 + cloud2 * 0.4;
    float3 baseStorm = lerp(deepColor, stormColor, cloud * (0.35 + intensity * 0.45));

    //======== B. 气旋雨带（切向流动条纹，整数角频率+采样器wrap） ========
    //切向低频、径向高频 → 沿环向拉长的雨带；两层反差速度叠加
    //uv.x 用减号：图案沿 normAngle 增大方向行进 = 屏幕顺时针
    float2 bandUV1 = float2(normAngle * 3.0 - uTime * 2.2, dist * 2.6 - uTime * 0.7);
    float band1 = tex2D(noiseSamp, frac(bandUV1)).r;
    float2 bandUV2 = float2(normAngle * 5.0 - uTime * 3.4, dist * 4.0 - uTime * 1.1);
    float band2 = tex2D(noiseSamp, frac(bandUV2)).g;
    float band = smoothstep(0.42, 0.78, band1 * 0.62 + band2 * 0.38);
    band *= eyeMask;

    //======== C. 雨丝风纹（更细密的高频切向丝缕） ========
    float2 streakUV = float2(normAngle * 8.0 - uTime * 4.6, dist * 7.0 - uTime * 0.5);
    float streak = tex2D(noiseSamp, frac(streakUV)).b;
    streak = smoothstep(0.62, 0.92, streak) * eyeMask;

    //======== D. 电光隐现（12 分区随机弧段，强度/计量越高越频繁） ========
    float sectorF = normAngle * 12.0;
    float sector = floor(sectorF);
    float inSector = frac(sectorF);
    float strobe = floor(uTime * 9.0);
    float arcRoll = hash21(float2(sector, strobe));
    float arcThreshold = 0.94 - intensity * 0.18 - smoothstep(0.7, 1.0, boltGauge) * 0.12;
    float arcOn = step(arcThreshold, arcRoll);
    //弧段径向位置随分区抖动，限制在风暴环带内
    float arcR = lerp(0.45, 0.80, hash21(float2(sector * 7.0 + strobe, 3.7)));
    float arcJitter = (hash21(float2(sector + strobe * 13.0, floor(inSector * 6.0))) - 0.5) * 0.06;
    float arcLine = 1.0 - smoothstep(0.0, 0.018, abs(dist - (arcR + arcJitter)));
    //分区两端渐隐，避免相邻分区间硬切
    arcLine *= smoothstep(0.0, 0.18, inSector) * smoothstep(1.0, 0.82, inSector);
    float arc = arcLine * arcOn * eyeMask;

    //======== E. 边界气旋环（三频波浪，整数角频率，主波顺时针行进） ========
    float wave = sin(angle * 3.0 - uTime * 5.0) * 0.014
               + sin(angle * 7.0 + uTime * 3.2) * 0.007
               + sin(angle * 11.0 - uTime * 7.5) * 0.004;
    float ringDist = abs(dist + wave - ringR);
    float ring = 1.0 - smoothstep(0.0, 0.012, ringDist);
    float ringGlow = exp(-ringDist * ringDist * 900.0);
    //落雷临界：边界环高频脉动预警
    float gaugeHot = smoothstep(0.7, 1.0, boltGauge);
    float ringPulse = 1.0 + gaugeHot * 0.35 * sin(uTime * 24.0);
    ring *= ringPulse;
    ringGlow *= ringPulse;

    //======== 合成 ========
    float density = 0.25 + intensity * 0.75;
    float3 color = float3(0.0, 0.0, 0.0);
    color += baseStorm * 0.40 * eyeMask * density;
    color += stormColor * band * 0.42 * density;
    color += arcColor * streak * 0.30 * density;
    color += arcColor * arc * (0.8 + gaugeHot * 0.4);
    color += lerp(stormColor, arcColor, 0.5 + gaugeHot * 0.5) * (ring * 0.85 + ringGlow * 0.5) * (0.55 + intensity * 0.45);

    float alpha = cloud * 0.10 * eyeMask * density
        + band * 0.16 * density
        + streak * 0.10 * density
        + arc * 0.55
        + (ring * 0.5 + ringGlow * 0.3) * (0.5 + intensity * 0.5);
    alpha = saturate(alpha) * edgeFade * fadeAlpha;

    return float4(color * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModStormFieldPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
