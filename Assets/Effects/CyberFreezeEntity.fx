// ============================================================================
//CyberFreezeEntity.fx 赛博冻结实体覆盖
//故障滤镜+六角网格；采样实体贴图 s0
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;        //冻结进度 0(刚冻结)→1(即将解冻)
float intensity;       //赛博空间效果强度
float seed;            //每个实体独立的随机种子
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

//六角网格边缘距离
float hexDist(float2 p)
{
    p = abs(p);
    float c = dot(p, normalize(float2(1.0, 1.7320508)));
    c = max(c, p.x);
    return c;
}

//六角网格UV映射
float4 hexGrid(float2 uv, float scale)
{
    float sqrt3 = 1.7320508;
    float2 r = float2(1.0, sqrt3);
    float2 h = r * 0.5;

    float2 a = fmod(uv, r) - h;
    float2 b = fmod(uv - h, r) - h;

    float2 gv;
    if (length(a) < length(b))
        gv = a;
    else
        gv = b;

    float edgeDist = hexDist(gv);
    float2 id = uv - gv;

    //x = edge distance, y = cell id hash, zw = cell center
    return float4(edgeDist, hash21(id), id.x, id.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    //=
    //故障扭曲偏移计算
    //=
    float timeHash = floor(uTime * 8.0 + seed * 50.0);

    //冻结状态下故障强度明显且持续存在
    float glitchBase = 0.45 + 0.25 * sin(uTime * 2.5 + seed * 10.0);
    //周期性故障尖峰 (每隔几秒一次剧烈抽搐)
    float spikeCycle = sin(uTime * 0.8 + seed * 3.0);
    float spike = smoothstep(0.85, 1.0, spikeCycle) * 0.4;
    //解冻前故障加剧
    float thawGlitch = smoothstep(0.75, 1.0, progress) * 0.5;
    float glitchStrength = glitchBase + spike + thawGlitch;

    //水平行撕裂
    float rowIdx = floor(coords.y / (texelSize.y * 10.0));
    float rowRand = hash21(float2(rowIdx, timeHash));
    bool rowActive = rowRand > lerp(0.72, 0.35, glitchStrength);
    float rowShift = (rowRand - 0.5) * 2.0 * texelSize.x * 40.0 * glitchStrength;
    if (!rowActive) rowShift = 0.0;

    //块状故障（仅水平偏移，避免序列图垂直采样越界）
    float2 blockIdx = floor(coords / (texelSize * 25.0));
    float blockRand = hash21(blockIdx + timeHash);
    bool blockActive = blockRand > lerp(0.80, 0.45, glitchStrength);
    float blockShiftX = 0.0;
    if (blockActive)
    {
        blockShiftX = (hash11(blockRand * 5.17) - 0.5) * texelSize.x * 35.0 * glitchStrength;
    }

    float2 distorted = coords;
    distorted.x += rowShift + blockShiftX;

    //=
    //RGB通道分离
    //=
    //RGB通道分离（仅水平方向，避免序列图垂直采样越界）
    float channelSplit = lerp(3.5, 10.0, glitchStrength) * texelSize.x;
    float2 rOff = float2(channelSplit, 0.0);
    float2 bOff = float2(-channelSplit, 0.0);

    float4 colR = tex2D(uImage0, distorted + rOff);
    float4 colG = tex2D(uImage0, distorted);
    float4 colB = tex2D(uImage0, distorted + bOff);

    float4 color;
    color.r = colR.r;
    color.g = colG.g;
    color.b = colB.b;
    color.a = (colR.a + colG.a + colB.a) / 3.0;

    if (color.a < 0.01)
        return float4(0, 0, 0, 0);

    //=
    //冻结色调：去饱和 + 暗红晶偏移（黑墙风格）
    //=
    float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));
    float freezeStr = lerp(0.7, 0.3, progress) * intensity;

    float3 filtered = color.rgb;
    //去饱和
    filtered = lerp(filtered, float3(lum, lum, lum), freezeStr * 0.7);
    //暗红/品红偏移（偏冷红，与领域暖橙红区分）
    float3 crimsonTint = float3(lum * 1.2, lum * 0.15, lum * 0.35);
    filtered = lerp(filtered, crimsonTint, freezeStr * 0.6);

    //=
    //扫描线干扰
    //=
    float scanFreq = 80.0;
    float scanLine = sin(coords.y * scanFreq + uTime * 5.0) * 0.5 + 0.5;
    scanLine = pow(scanLine, 4.0);
    filtered -= scanLine * 0.22 * freezeStr;
    //粗扫描带 (间隔较大的明暗条纹)
    float thickScan = sin(coords.y * 20.0 + uTime * 2.0) * 0.5 + 0.5;
    thickScan = smoothstep(0.4, 0.6, thickScan);
    filtered *= 1.0 - thickScan * 0.15 * freezeStr;

    //=
    //六角能量网格覆盖
    //=
    float hexScale = 12.0; //控制六角网格大小
    float2 hexUV = coords * float2(1.0 / texelSize.x, 1.0 / texelSize.y) / hexScale;

    float4 hex = hexGrid(hexUV, 1.0);
    float hexEdge = hex.x;
    float hexId = hex.y;

    //六角边缘线
    float edgeWidth = 0.08;
    float hexLine = 1.0 - smoothstep(edgeWidth - 0.02, edgeWidth, hexEdge);

    //六角网格颜色（暗红晶风格）
    float3 hexCrimson = float3(0.9, 0.06, 0.2);
    float3 hexDark = float3(0.45, 0.02, 0.12);

    //网格呼吸脉冲
    float pulse = 0.6 + 0.4 * sin(uTime * 3.0 + hexId * 6.28 + seed * 10.0);

    //部分格子随机闪烁
    float cellFlicker = step(0.7, hash11(hexId * 100.0 + floor(uTime * 4.0 + seed * 20.0)));
    float cellGlow = pulse * (0.3 + cellFlicker * 0.5);

    //六角覆盖强度：刚冻结时最强，解冻前减弱
    float hexOverlayStr = lerp(0.5, 0.15, progress) * intensity;

    //叠加六角边缘光
    float3 hexColor = lerp(hexDark, hexCrimson, pulse) * hexLine * hexOverlayStr;
    //格子内部微弱发光
    float3 cellFillColor = hexDark * (1.0 - hexLine) * cellGlow * hexOverlayStr * 0.2;

    filtered += hexColor + cellFillColor;

    //=
    //数字噪点
    //=
    float pixelNoise = hash21(coords * 400.0 + uTime * 8.0 + seed);
    filtered += (pixelNoise - 0.5) * 0.08 * freezeStr;

    //=
    //解冻阶段 (>0.85): 故障加剧 + 闪烁
    //=
    float thawPhase = smoothstep(0.85, 1.0, progress);
    if (thawPhase > 0.0)
    {
        float thawFlicker = sin(uTime * 25.0 + seed * 15.0) * 0.5 + 0.5;
        //闪烁恢复原色
        filtered = lerp(filtered, color.rgb, thawPhase * thawFlicker * 0.5);
        //偶尔全屏白闪
        float whiteFlash = step(0.9, hash11(floor(uTime * 15.0) + seed)) * thawPhase;
        filtered = lerp(filtered, float3(1.0, 0.5, 0.5), whiteFlash * 0.3);
    }

    //=
    //边缘发光
    //=
    float4 sampleUp    = tex2D(uImage0, coords + float2(0, -texelSize.y * 2.0));
    float4 sampleDown  = tex2D(uImage0, coords + float2(0,  texelSize.y * 2.0));
    float4 sampleLeft  = tex2D(uImage0, coords + float2(-texelSize.x * 2.0, 0));
    float4 sampleRight = tex2D(uImage0, coords + float2( texelSize.x * 2.0, 0));
    float neighborAlpha = (sampleUp.a + sampleDown.a + sampleLeft.a + sampleRight.a) * 0.25;
    float edgeGlow = saturate(neighborAlpha - color.a * 0.5) * 2.0;
    float edgePulse = 0.5 + 0.5 * sin(uTime * 4.0 + coords.y * 20.0 + seed * 8.0);
    edgeGlow *= edgePulse * 0.5 * freezeStr;

    filtered += float3(0.85, 0.06, 0.2) * edgeGlow;

    //=
    //全局透明度控制
    //=
    //intensity 只调制上面的特效层(freezeStr/hexOverlayStr)，不参与最终颜色：
    //乘进 alpha 会让领域强度为 0 的视角(队友、领域收起后)整个实体消失
    float globalFlicker = 0.85 + 0.15 * sin(uTime * 5.0 + seed * 20.0);
    color.a *= lerp(1.0, globalFlicker, intensity);

    color.rgb = saturate(filtered);
    return color;
}

technique Technique1
{
    pass FreezePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
