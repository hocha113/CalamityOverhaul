// ============================================================================
// FishronTornado.fx 鲨鱼龙卷柱体
// 世界 quad：uv.x 横向 0~1，uv.y 纵向 0(顶)~1(底)
// 假体积：前后两层反向横滚的水带 + 边缘泡沫撕裂 + 顶冠散逸
// 直线算术无分支，噪声全走绑定贴图，无极角
// ============================================================================

float uTime;
float uIntensity;   // 0~1 起身/消散包络
float uGrade;       // 0/1 升格层级
float uSeed;        // 实例种子
float3 uDeepColor;
float3 uSeaColor;
float3 uFoamColor;

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

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // =========================================================
    // A. 柱形轮廓：上宽下窄 + 随高摇摆的中轴
    // =========================================================
    float sway = sin(uTime * 1.9 + uSeed * 7.0 + (1.0 - uv.y) * 2.6) * 0.085 * (1.0 - uv.y);
    float axis = 0.5 + sway;
    float dx = uv.x - axis;

    // 半宽：底 0.16 → 顶 0.44，升格更粗
    float halfW = lerp(0.44, 0.16, uv.y) * (1.0 + uGrade * 0.18);
    // 轮廓噪声撕边：随时间沿高度爬升
    float edgeN = tex2D(noiseSamp, float2(uv.x * 1.7 + uSeed, uv.y * 2.6 - uTime * 0.55)).r;
    halfW *= 0.82 + edgeN * 0.36;

    float rad = abs(dx) / max(halfW, 1e-4);      // 0 中轴 → 1 边缘
    float body = saturate(1.0 - rad);
    body = smoothstep(0.0, 0.35, body);

    // =========================================================
    // B. 假体积：前层快滚 + 后层反向慢滚，横向拉伸的水带
    // =========================================================
    float2 band1UV = float2(uv.x * 0.9 - uTime * 0.85 + uSeed, uv.y * 3.4);
    float band1 = tex2D(noiseSamp, band1UV).g;
    float2 band2UV = float2(uv.x * 0.6 + uTime * 0.52 + uSeed * 2.0, uv.y * 2.1 + 0.41);
    float band2 = tex2D(noiseSamp, band2UV).b;
    // 两层按横位混合：左缘前层主导、右缘后层主导 → 读作旋转
    float sideMix = saturate(uv.x);
    float bands = lerp(band1, band2, sideMix * 0.65 + 0.15);

    // 竖向上升气流条纹
    float2 upUV = float2(uv.x * 4.2 + uSeed, uv.y * 1.3 - uTime * 1.4);
    float updraft = tex2D(noiseSamp, upUV).r;

    float field = bands * 0.68 + updraft * 0.32;

    // =========================================================
    // C. 色阶：深水底色 → 海青水带 → 泡沫亮痕
    // =========================================================
    float3 col = lerp(uDeepColor, uSeaColor, saturate(field * 1.25)) * (0.75 + 0.25 * (1.0 - uv.y));
    // 泡沫亮痕：水带亮部窄阈值
    float foam = smoothstep(0.62, 0.86, field);
    col += uFoamColor * foam * (0.35 + uGrade * 0.2);

    // 边缘泡沫撕裂带
    float rim = smoothstep(0.55, 0.95, rad) * smoothstep(1.05, 0.85, rad);
    float rimN = tex2D(noiseSamp, float2(uv.y * 3.0 - uTime * 1.1, uv.x * 2.0 + uSeed)).g;
    float rimFoam = rim * smoothstep(0.38, 0.72, rimN);
    col += uFoamColor * rimFoam * 0.55;

    // 基座浪涌：底缘一圈翻涌的白
    float baseChurn = smoothstep(0.88, 1.0, uv.y) * (0.4 + 0.6 * bands);
    col += uFoamColor * baseChurn * 0.4;

    // 顶冠散逸：顶部被风撕开，密度衰减
    float crown = smoothstep(0.30, 0.02, uv.y);
    float crownN = tex2D(noiseSamp, float2(uv.x * 2.4 - uTime * 0.7, uv.y * 5.0 + uSeed)).b;
    float crownFade = 1.0 - crown * smoothstep(0.30, 0.62, crownN) * 0.85;

    // =========================================================
    // 合成（预乘）：体密度 × 包络 × 顶冠散逸
    // =========================================================
    float density = body * (0.52 + field * 0.48) * crownFade;
    // 画布护栏：uv 边缘强制归零防裁切
    float guard = smoothstep(0.0, 0.03, uv.x) * smoothstep(1.0, 0.97, uv.x)
        * smoothstep(0.0, 0.02, uv.y) * smoothstep(1.0, 0.985, uv.y);
    float alpha = saturate(density * uIntensity * guard) * 0.9;

    return float4(col * alpha, alpha) * vColor.a;
}

technique Technique1
{
    pass TornadoPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
