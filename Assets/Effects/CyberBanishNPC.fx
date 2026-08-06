// ============================================================================
//CyberBanishNPC.fx 赛博放逐 NPC 滤镜
//采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;        //放逐进度 0→1
float intensity;       //赛博空间效果强度
float seed;            //每个NPC独立的随机种子
float2 texelSize;      //1/texWidth, 1/texHeight

//工具函数

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    //=
    //故障扭曲偏移计算
    //=
    float timeHash = floor(uTime * 12.0 + seed * 100.0);
    float glitchStrength = lerp(0.3, 1.0, progress);

    //水平行撕裂：按行随机偏移
    float rowIdx = floor(coords.y / (texelSize.y * lerp(8.0, 3.0, progress)));
    float rowRand = hash21(float2(rowIdx, timeHash));
    bool rowActive = rowRand > lerp(0.85, 0.4, progress);
    float rowShift = (rowRand - 0.5) * 2.0 * texelSize.x * lerp(15.0, 60.0, progress * progress);
    if (!rowActive) rowShift = 0.0;

    //块状故障：随机矩形区域整体偏移
    float2 blockIdx = floor(coords / (texelSize * lerp(40.0, 12.0, progress)));
    float blockRand = hash21(blockIdx + timeHash);
    bool blockActive = blockRand > lerp(0.92, 0.55, progress);
    float2 blockShift = float2(0, 0);
    if (blockActive)
    {
        blockShift.x = (hash11(blockRand * 7.13) - 0.5) * texelSize.x * 30.0 * glitchStrength;
        blockShift.y = (hash11(blockRand * 3.77) - 0.5) * texelSize.y * 15.0 * glitchStrength;
    }

    //合成扭曲坐标
    float2 distorted = coords;
    distorted.x += rowShift * glitchStrength;
    distorted += blockShift;

    //=
    //RGB通道分离采样
    //=
    float channelSplit = lerp(2.0, 12.0, progress * progress) * texelSize.x;
    //各通道沿不同方向偏移
    float splitAngle = uTime * 3.0 + seed * 6.28;
    float2 rOff = float2(cos(splitAngle), sin(splitAngle)) * channelSplit;
    float2 bOff = float2(cos(splitAngle + 2.09), sin(splitAngle + 2.09)) * channelSplit;

    float4 colR = tex2D(uImage0, distorted + rOff);
    float4 colG = tex2D(uImage0, distorted);
    float4 colB = tex2D(uImage0, distorted + bOff);

    float4 color;
    color.r = colR.r;
    color.g = colG.g;
    color.b = colB.b;
    color.a = (colR.a + colG.a + colB.a) / 3.0;

    //无像素区域不处理
    if (color.a < 0.01)
        return float4(0, 0, 0, 0);

    //=
    //深红色滤镜（去饱和 + 红色偏移）
    //=
    float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));
    float redFilterStr = lerp(0.3, 0.85, progress) * intensity;

    float3 filtered = color.rgb;
    //去饱和
    filtered = lerp(filtered, float3(lum, lum, lum), redFilterStr * 0.6);
    //红色偏移
    float3 redTint = float3(lum * 1.4, lum * 0.3, lum * 0.25);
    filtered = lerp(filtered, redTint, redFilterStr);

    //=
    //扫描线干扰
    //=
    float scanFreq = lerp(80.0, 200.0, progress);
    float scanLine = sin(coords.y * scanFreq + uTime * 15.0) * 0.5 + 0.5;
    scanLine = pow(scanLine, lerp(8.0, 3.0, progress));
    filtered -= scanLine * lerp(0.08, 0.2, progress);

    //=
    //随机像素闪烁（数字噪点）
    //=
    float pixelNoise = hash21(coords * 500.0 + uTime * 10.0 + seed);
    float noiseStr = lerp(0.05, 0.25, progress);
    filtered += (pixelNoise - 0.5) * noiseStr;

    //=
    //阶段三 (>0.85): 高光闪白 → 透明消失
    //=
    float fadePhase = smoothstep(0.85, 1.0, progress);
    if (fadePhase > 0.0)
    {
        //白色闪光
        float flashPulse = sin(uTime * 40.0 + seed * 20.0) * 0.5 + 0.5;
        float flash = fadePhase * flashPulse;
        filtered = lerp(filtered, float3(1.0, 0.6, 0.5), flash * 0.7);

        //透明度急速衰减
        color.a *= 1.0 - pow(fadePhase, 1.5);
    }

    //=
    //边缘故障溢出光（NPC轮廓外围的红色数字残影）
    //=
    float edgeGlow = 0.0;
    float4 sampleUp    = tex2D(uImage0, coords + float2(0, -texelSize.y * 2.0));
    float4 sampleDown  = tex2D(uImage0, coords + float2(0,  texelSize.y * 2.0));
    float4 sampleLeft  = tex2D(uImage0, coords + float2(-texelSize.x * 2.0, 0));
    float4 sampleRight = tex2D(uImage0, coords + float2( texelSize.x * 2.0, 0));
    float neighborAlpha = (sampleUp.a + sampleDown.a + sampleLeft.a + sampleRight.a) * 0.25;
    //在透明与不透明交界处产生光晕
    edgeGlow = saturate(neighborAlpha - color.a * 0.5) * 2.0;
    float edgePulse = 0.5 + 0.5 * sin(uTime * 6.0 + coords.y * 30.0 + seed * 10.0);
    edgeGlow *= edgePulse * glitchStrength * 0.6;

    filtered += float3(0.8, 0.1, 0.05) * edgeGlow;

    //=
    //全局透明度闪烁（故障性质的整体明灭）
    //=
    //intensity 只调制上面的特效层，不参与最终颜色：
    //乘进 alpha 会让领域强度为 0 的视角(队友、领域收起后)整个实体消失
    float globalFlicker = 0.7 + 0.3 * sin(uTime * 8.0 + seed * 30.0);
    float blockFlicker = step(0.15, hash11(timeHash * 0.31 + seed)) > 0.0 ? 1.0 : 0.4;
    color.a *= lerp(1.0, globalFlicker * lerp(1.0, blockFlicker, progress), intensity);

    color.rgb = saturate(filtered);
    return color;
}

technique Technique1
{
    pass BanishPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
