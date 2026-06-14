// ============================================================================
//HackTurretCircuitFault.fx 骇客炮台电路故障
//采样 uImage0；mode 0短路 1过载
// ============================================================================

sampler uImage0 : register(s0);

float2 texelSize;  //1/宽 1/高
float intensity;   //0~1
float mode;        //0短路 1过载，可插值
float uTime;

//哈希随机
float hash1(float n)
{
    return frac(sin(n) * 43758.5453);
}
float hash2(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float4 TurretFaultPass(float2 coords : TEXCOORD0, float4 smpColor : COLOR0) : COLOR0
{
    float str = saturate(intensity);
    float2 uv = coords;

    //========================================
    //1.水平撕裂带：按行随机挑若干条做横向错位
    //========================================
    float rowBucket = floor(uv.y / (texelSize.y * 4.0));
    float rowSeed = hash1(rowBucket + floor(uTime * 14.0) * 37.0);
    float tearGate = step(0.80, rowSeed);
    float tearDir = (rowSeed - 0.5);
    //过载状态下撕裂幅度更大
    float tearAmp = lerp(0.035, 0.065, mode);
    uv.x += tearDir * tearAmp * tearGate * str;

    //========================================
    //2.RGB 色散：短路细、过载粗
    //========================================
    float abBase = lerp(0.0022, 0.0055, mode);
    float abJitter = 0.6 + 0.4 * sin(uTime * 26.0 + rowBucket);
    float ab = abBase * str * abJitter;
    float4 rCh = tex2D(uImage0, uv + float2(ab, 0));
    float4 gCh = tex2D(uImage0, uv);
    float4 bCh = tex2D(uImage0, uv - float2(ab, 0));
    float4 texColor = float4(rCh.r, gCh.g, bCh.b, gCh.a);

    if (texColor.a < 0.01)
        return float4(0, 0, 0, 0);

    float4 color = texColor * smpColor;
    float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));

    //========================================
    //3.去饱和+压暗，塑造"死电路"底色
    //========================================
    float3 dead = float3(lum, lum, lum) * 0.42;
    color.rgb = lerp(color.rgb, dead, str * 0.85);

    //模式色调：冷蓝/热红双极，允许插值
    float3 coldTint = float3(0.28, 0.55, 1.10);
    float3 hotTint  = float3(1.20, 0.22, 0.70);
    float3 tint = lerp(coldTint, hotTint, mode);
    color.rgb *= lerp(float3(1.0, 1.0, 1.0), tint, str * 0.72);

    //========================================
    //4.周期性电弧横线（飞扫）
    //========================================
    float arcSpeed = lerp(1.6, 1.1, mode);
    float arcFrac = frac(uTime * arcSpeed);
    float arcY = frac(arcFrac + hash1(floor(uTime * arcSpeed)) * 0.2);
    //电弧窗口：窗口内才亮
    float arcWin = smoothstep(0.0, 0.02, arcFrac) * smoothstep(0.28, 0.18, arcFrac);
    //Y轴上的细线遮罩
    float arcLineMask = smoothstep(texelSize.y * 2.5, 0.0, abs(uv.y - arcY));
    //轻微水平闪烁
    float arcJitter = 0.6 + 0.4 * hash1(floor(uv.x / (texelSize.x * 3.0)) + floor(uTime * 36.0));
    float3 arcColor = lerp(float3(0.85, 1.05, 1.35), float3(1.35, 0.55, 1.05), mode);
    color.rgb += arcColor * arcWin * arcLineMask * arcJitter * str * 1.8;

    //========================================
    //5.模式特有粒子：短路=竖向电火花柱；过载=品红斑块灼伤
    //========================================
    //短路分量
    float colBucket = floor(uv.x / (texelSize.x * 7.0));
    float sparkSeed = hash2(float2(colBucket, floor(uTime * 22.0)));
    float sparkGate = step(0.92, sparkSeed);
    float sparkAtten = 1.0 - saturate(abs(uv.y - 0.5) * 1.4);
    float3 sparkColor = float3(0.65, 0.90, 1.15);
    float3 shortAdd = sparkColor * sparkGate * sparkAtten * str * 0.85;

    //过载分量
    float blobN = hash2(float2(floor(uv.x / (texelSize.x * 4.0)),
                               floor(uv.y / (texelSize.y * 4.0) + uTime * 2.4)));
    float blobMask = smoothstep(0.78, 1.0, blobN);
    float3 blobColor = float3(1.15, 0.30, 0.70);
    float3 overAdd = blobColor * blobMask * str * 0.80;

    color.rgb += lerp(shortAdd, overAdd, mode);

    //========================================
    //6.CRT 扫描线压暗
    //========================================
    float scanLine = frac(uv.y / (texelSize.y * 2.0));
    float scanDark = smoothstep(0.45, 0.5, scanLine) * smoothstep(0.55, 0.5, scanLine);
    color.rgb *= 1.0 - scanDark * 0.28 * str;

    //========================================
    //7.随机全屏过曝/断电闪烁
    //========================================
    //断电黑闪：极短瞬间整体变暗，表示电力中断
    float blackout = step(0.97, hash1(floor(uTime * 9.0) + 13.0));
    color.rgb *= 1.0 - blackout * str * 0.55;
    //过曝白闪：短暂高亮，模拟电弧爆闪
    float overexp = step(0.96, hash1(floor(uTime * 11.0) + 71.0));
    color.rgb += tint * overexp * str * 0.6;

    //========================================
    //8.轻度对比度增强，避免整体扁平
    //========================================
    float3 mid = float3(0.32, 0.32, 0.32);
    color.rgb = mid + (color.rgb - mid) * lerp(1.0, 1.18, str * 0.5);

    color.rgb = saturate(color.rgb);
    return color;
}

technique Technique1
{
    pass HackTurretCircuitFaultPass
    {
        PixelShader = compile ps_3_0 TurretFaultPass();
    }
}
