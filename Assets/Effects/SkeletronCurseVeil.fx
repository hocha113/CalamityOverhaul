// ============================================================================
//SkeletronCurseVeil.fx 骷髅王诅咒之幕
//全屏后效：黑暗领域视界压缩 + 冲击环折射 + 骨白冲击帧
//直线算术 + 平铺 tex2D，无动态分支；噪声全走笛卡尔坐标（无极角，无接缝）
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAspect;              //屏宽/屏高
float uDomain;              //黑暗领域强度 0~1
float2 uCenter;             //视界中心（本地玩家屏幕uv）
float uFlash;               //骨白冲击帧强度
float uFlashProgress;       //冲击帧进度 0~1
float4 ringData[2];         //xy=中心uv z=半径(屏高归一) w=强度

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //宽高比修正坐标（圆读作圆）
    float2 pc = float2(coords.x * uAspect, coords.y);

    //---- 冲击环位移场（先算合位移再采样，单次取屏）----
    float2 push = float2(0.0, 0.0);
    float ringGlow = 0.0;
    for (int i = 0; i < 2; i++)
    {
        float2 rc = float2(ringData[i].x * uAspect, ringData[i].y);
        float2 toPix = pc - rc;
        float rd = length(toPix);
        float band = exp(-abs(rd - ringData[i].z) * 34.0) * ringData[i].w;
        float2 dir = toPix / max(rd, 0.0012);
        push += dir * band * 0.011;
        ringGlow += band;
    }

    float4 src = tex2D(uImage0, coords + float2(push.x / uAspect, push.y));
    float3 color = src.rgb;
    float lum = dot(color, float3(0.299, 0.587, 0.114));

    //---- 黑暗领域 ----
    //边界幽火舔舐：笛卡尔噪声扰动边界半径
    float lick = tex2D(noiseSamp, coords * 2.2 + float2(uTime * 0.05, -uTime * 0.08)).r - 0.5;
    float d = distance(pc, float2(uCenter.x * uAspect, uCenter.y));
    float rClear = lerp(1.45, 0.335, uDomain) + lick * 0.14 * uDomain;
    float dark = smoothstep(rClear, rClear + 0.46, d);

    //高亮穿透：眼火/弹幕在黑暗中仍可读
    float punch = smoothstep(0.52, 0.95, lum) * 0.78;
    dark *= (1.0 - punch);
    dark *= uDomain;

    //暗部冷紫压色 + 去饱和
    float3 curseDark = float3(0.055, 0.03, 0.10);
    float3 graded = lerp(color, float3(lum, lum, lum) * float3(0.72, 0.86, 1.0), 0.4 * uDomain);
    color = lerp(graded, curseDark, dark * 0.92);

    //整体压暗呼吸
    float breathe = 0.96 + 0.04 * sin(uTime * 1.7);
    color *= 1.0 - uDomain * 0.16 * breathe;

    //边界带幽青辉（在暗部边缘挂一圈将熄的火）
    float edgeBand = exp(-abs(d - rClear - 0.20) * 9.0) * uDomain;
    float edgeFlick = 0.75 + 0.25 * tex2D(noiseSamp, coords * 3.1 + float2(-uTime * 0.11, uTime * 0.07)).r;
    color += float3(0.10, 0.30, 0.30) * edgeBand * edgeFlick * 0.6;

    //---- 冲击环辉光 ----
    color += float3(0.28, 0.62, 0.58) * ringGlow * 0.5;

    //---- 骨白冲击帧（死亡终爆一次）----
    float flashCurve = uFlash * pow(saturate(1.0 - uFlashProgress), 1.5);
    float bw = smoothstep(0.30, 0.64, lum);
    //前22%负相
    float invertPhase = 1.0 - smoothstep(0.08, 0.22, uFlashProgress);
    float tone = lerp(bw, 1.0 - bw, invertPhase);
    float3 boneMono = float3(tone, tone, tone) * float3(0.98, 0.95, 0.86);
    //暗角收束
    float2 cc = coords * 2.0 - 1.0;
    boneMono *= 1.0 - dot(cc, cc) * 0.28;
    color = lerp(color, boneMono, saturate(flashCurve));

    return float4(color, src.a);
}

technique Technique1
{
    pass SkeletronCurseVeilPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
