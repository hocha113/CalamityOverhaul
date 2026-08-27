// ============================================================================
//QueenBeeTelegraph.fx 女王蜂俯冲预警线
//uv.x 沿线 0起点→1末端，uv.y 横向；Additive 白色四边形
//身份区别于毁灭者的连续能量流：琥珀"蜂舞信号"，量化蜂房节流着向打击方向行进，
//锁定期节流并拢白热
//全程笛卡尔，floor量化无极角
//ps_3_0
// ============================================================================

float uTime;
float uIntensity;     //整体亮度(含淡入)
float uLockProgress;  //0追踪 0~1锁定推进
float uAspect;        //长宽比
float3 uColor;        //主色(琥珀金)

//哈希
float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float4 BeeTelegraphPS(float2 uv : TEXCOORD0) : COLOR0
{
    float lat = (uv.y - 0.5) * 2.0;   //-1..1 横向
    float x = uv.x * uAspect;          //等比沿线

    //端点羽化
    float endFade = smoothstep(0.0, 0.05, uv.x) * smoothstep(1.0, 0.985, uv.x);

    //蜂房节流：沿线量化成房格，向打击方向行进；锁定时格距压缩(并拢成实线感)
    //格距按细线宽折算(缩宽后 cellFreq 同步减半保持约170px格长)
    float cellFreq = lerp(0.3, 0.8, uLockProgress);
    float march = x * cellFreq - uTime * (5.0 + uLockProgress * 9.0);
    float cellId = floor(march);
    float cellT = frac(march);
    //每格菱形亮斑：中间亮两端灭，格与格错相
    float cellPulse = smoothstep(0.0, 0.35, cellT) * smoothstep(1.0, 0.62, cellT);
    float cellSeed = hash11(cellId * 13.7);
    cellPulse *= 0.55 + 0.45 * sin(uTime * 4.0 + cellSeed * 6.2831);

    //横向核心+光晕，锁定加粗白热；光晕收窄压暗，预警读作细导引线而非能量束
    float coreSharp = lerp(52.0, 18.0, uLockProgress);
    float core = exp(-lat * lat * coreSharp);
    float glow = exp(-lat * lat * 9.0) * 0.22;

    //锁定白闪振荡
    float flash = 1.0 + uLockProgress * 0.4 * sin(uTime * 42.0);

    //亮度合成：追踪期以行进房格为主体，锁定期核心接管
    float lum = core * (0.22 + cellPulse * 0.6 + uLockProgress * 1.15)
              + glow * (0.4 + cellPulse * 0.5);
    lum *= endFade * flash;

    float3 col = uColor * lum;
    //锁定期核心暖白热
    col += float3(1.0, 0.9, 0.7) * core * uLockProgress * lum * 0.8;

    return float4(col * uIntensity, 1.0);
}

technique BeeTelegraph
{
    pass P0
    {
        PixelShader = compile ps_3_0 BeeTelegraphPS();
    }
}
