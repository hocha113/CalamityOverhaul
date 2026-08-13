// ============================================================================
// FishronGrabVeil.fx 涡底入水滤镜（投技被抓者本机专用）
// 全屏 ping-pong：折射晃动 + 深海压色 + 边缘暗角 + 焦散亮纹
// 直线算术无分支，噪声全走绑定贴图（FNA3D 安全），无极角
// ============================================================================

float uTime;
float uIntensity;   // 0~1 沉没浓度
float uAspect;      // 宽高比

// s0 = 拷屏画布（SpriteBatch.Draw 主纹理，有意占用 s0）
sampler screenSamp : register(s0);
// 噪声固定在 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图，
// sampler_state 块在 FNA 下会被分配到 s0 导致噪声读到画布；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // =========================================================
    // A. 水体折射：双尺度噪声推挤采样坐标，慢速对流
    // =========================================================
    float2 nuv1 = uv * float2(2.2, 1.6) + float2(uTime * 0.05, uTime * 0.031);
    float2 nuv2 = uv * float2(4.7, 3.4) - float2(uTime * 0.037, uTime * 0.052);
    float n1 = tex2D(noiseSamp, nuv1).r;
    float n2 = tex2D(noiseSamp, nuv2).g;
    float2 wobble = (float2(n1, n2) - 0.5) * 0.010 * uIntensity;
    // 边缘晃得比中心凶：涡壁近在咫尺
    float2 fromCenter = uv - 0.5;
    float edge = saturate(dot(fromCenter, fromCenter) * 4.0);
    wobble *= 0.55 + edge * 0.9;

    float3 scene = tex2D(screenSamp, uv + wobble).rgb;

    // =========================================================
    // B. 深海压色：整体压进青绿，亮部保得多、暗部沉得快
    // =========================================================
    float3 deepTint = scene * float3(0.42, 0.74, 0.84) + float3(0.010, 0.045, 0.062);
    float luma = dot(scene, float3(0.299, 0.587, 0.114));
    float tintAmt = uIntensity * (0.62 - luma * 0.22);
    float3 col = lerp(scene, deepTint, saturate(tintAmt));

    // =========================================================
    // C. 焦散亮纹：噪声脊线的窄带高光，水面透下来的碎光
    // =========================================================
    float caustic = tex2D(noiseSamp, uv * float2(3.4, 2.2) + float2(uTime * 0.09, -uTime * 0.06)).b;
    float ridge = smoothstep(0.48, 0.55, caustic) * smoothstep(0.62, 0.55, caustic);
    col += float3(0.22, 0.5, 0.52) * ridge * uIntensity * 0.16;

    // =========================================================
    // D. 暗角：涡底四壁收拢的窒息感
    // =========================================================
    float vign = saturate(edge * 1.15);
    col = lerp(col, col * float3(0.30, 0.46, 0.52), vign * uIntensity * 0.8);

    return float4(col, 1.0) * vColor;
}

technique Technique1
{
    pass GrabVeilPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
