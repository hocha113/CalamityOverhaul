// ============================================================================
//EocBloodFog.fx 克眼血雾全屏合成
//雾团体积遮蔽(最多10团) + 血幕收拢暗角 + 心跳脉动 + 血闪
//直线算术+纯tex2D，无动态分支；极角零使用，噪声全走笛卡尔滚动
//Opaque批回写：输出恒a=1
// ============================================================================

sampler uImage0 : register(s0);
texture uNoise;
sampler2D noiseTex = sampler_state
{
    texture = <uNoise>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;
float uAspect;
float4 blobData[10];   //xy=屏幕uv中心 z=半径(屏高归一) w=密度0~1
float blobCount;
float uVignette;       //血幕收拢 0~1
float uPulse;          //心跳脉动 0~1
float uFlash;          //血闪 0~1

//酒红雾色板
static const float3 MistShallow = float3(0.376, 0.055, 0.086);
static const float3 MistDeep    = float3(0.180, 0.020, 0.036);
static const float3 FlashBlood  = float3(0.640, 0.055, 0.090);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float3 scene = tex2D(uImage0, coords).rgb;

    //----------------------------------------------------------------
    //雾团累积：软核+噪声撕裂边缘，两层滚动湍流
    //----------------------------------------------------------------
    float fogSum = 0.0;
    float coreSum = 0.0;
    //低频涌动与高频撕边（笛卡尔滚动，无接缝）
    float2 aspectUV = coords * float2(uAspect, 1.0);
    float turbA = tex2D(noiseTex, aspectUV * 1.35 + float2(uTime * 0.045, -uTime * 0.03)).r;
    float turbB = tex2D(noiseTex, aspectUV * 3.1 + float2(-uTime * 0.07, uTime * 0.055)).r;
    float turb = turbA * 0.65 + turbB * 0.35;

    for (int i = 0; i < 10; i++)
    {
        float valid = step(float(i), blobCount - 0.5);
        float2 d = (coords - blobData[i].xy) * float2(uAspect, 1.0);
        float dist = length(d);
        float radius = max(blobData[i].z, 1e-4);
        //噪声抖动半径：边缘湿撕
        float edgeWobble = (turb - 0.5) * radius * 0.5;
        float shaped = dist + edgeWobble;
        //软外缘、近实心核
        float body = 1.0 - smoothstep(radius * 0.42, radius, shaped);
        float core = 1.0 - smoothstep(0.0, radius * 0.5, shaped);
        fogSum += body * blobData[i].w * valid;
        coreSum += core * blobData[i].w * valid;
    }

    float fogAlpha = saturate(fogSum) * 0.94;
    float coreness = saturate(coreSum);
    //雾色：外浅内深，湍流带出干湿变化
    float3 fogCol = lerp(MistShallow, MistDeep, coreness * 0.8 + turb * 0.2);
    fogCol *= 0.85 + turbA * 0.3;
    float3 col = lerp(scene, fogCol, fogAlpha);

    //----------------------------------------------------------------
    //血幕收拢：四缘向心压暗+湿脉络，心跳呼吸
    //----------------------------------------------------------------
    float2 c = (coords - 0.5) * float2(uAspect, 1.0) * 2.0;
    float edgeR = length(c);
    float breathe = 1.0 + uPulse * 0.22 * sin(uTime * 9.2);
    float vignMask = smoothstep(0.62, 1.35, edgeR * breathe);
    //脉络：径向拉丝噪声（沿指向中心的方向拉伸采样，笛卡尔实现）
    float2 towardCenter = coords + (float2(0.5, 0.5) - coords) * 0.35;
    float vein = tex2D(noiseTex, towardCenter * float2(2.2 * uAspect, 2.2) + float2(0.0, uTime * 0.02)).r;
    vignMask *= 0.75 + vein * 0.5;
    float vign = saturate(vignMask * uVignette);
    col = lerp(col, MistDeep * 0.55, vign);

    //----------------------------------------------------------------
    //血闪：全屏压向深血红，亮部染红暗部沉黑，≤16帧脉冲
    //----------------------------------------------------------------
    float lum = dot(col, float3(0.3, 0.5, 0.2));
    float3 flashCol = FlashBlood * (0.45 + lum * 0.9);
    col = lerp(col, flashCol, saturate(uFlash) * 0.82);

    return float4(col, 1.0);
}

technique Technique1
{
    pass EocBloodFogPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
