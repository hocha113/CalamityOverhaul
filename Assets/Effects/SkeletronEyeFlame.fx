// ============================================================================
//SkeletronEyeFlame.fx 眼窝怨火瞳（每眼一张竖长quad，SpriteBatch Immediate + Additive，ps-only）
//UV：x 0~1 横截面，y 0焰尖→1焰根（quad origin 在底边中点，焰根锚眼窝）
//材质：眼窝里的怨火，不是普通火苗——它在看你。签名行为：
//①怨火焰体：根致密尖撕舌，双层反向上舔流动，高处甩尾侧摆
//②怨瞳：根部炽亮眼球 + 竖长暗缝瞳孔（加色批里靠压亮度成暗缝），随 uLook 追视目标
//③三温层：骨白芯/幽青体/深青缘；狂怒(uCurse)时焰体向诅咒紫过渡、焰高抬升
//④烬屑：焰体上方稀疏亮点剥落
//加色批输出 (SourceAlpha, One)：rgb 不预乘、a 携带包络；无极角运算，噪声全走焰面uv
// ============================================================================

float uTime;
float uSeed;        //每眼相位 0~1
float uIntensity;   //眼火强度 0~1.6（>1 = 过曝怒相：焰更高更亮）
float uCurse;       //诅咒紫混比 0~1（狂怒/大招期）
float2 uLook;       //瞳孔追视偏移（眼局部空间 -1~1）
float3 uCoreColor;  //骨白芯
float3 uBodyColor;  //幽青体
float3 uEdgeColor;  //深青缘
float3 uCurseColor; //诅咒紫

// 噪声固定 s1：本 shader 不采样 s0（画布是白像素 quad），
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

//PerlinNoise.png 实测值域 0.22~0.776，阈值前归一（VFX.md Noise-threshold rule）
float nrm(float v)
{
    return saturate((v - 0.22) / 0.556);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float x = (coords.x - 0.5) * 2.0;   //-1~1 横
    float h = 1.0 - coords.y;           //0 根 → 1 尖
    float inten = uIntensity;

    //焰高随强度抬升：hN 是对"当前焰高"归一后的高度
    float reach = 0.55 + 0.45 * saturate(inten);
    float hN = h / reach;

    //---- 高处甩尾侧摆（根部钉死）----
    float sway = sin(uTime * 5.2 + uSeed * 17.0) * 0.55 + sin(uTime * 9.7 + uSeed * 31.0) * 0.25;
    float xb = x - sway * hN * hN * 0.55;

    //---- 双层反向上舔流动 ----
    float n1 = nrm(tex2D(noiseSamp, float2(xb * 0.35 + uSeed, h * 0.9 - uTime * 1.15 + uSeed * 3.1)).r);
    float n2 = nrm(tex2D(noiseSamp, float2(xb * 1.1 - uSeed * 1.7 + uTime * 0.1, h * 1.9 - uTime * 2.1 + uSeed)).r);
    float flow = n1 * 0.62 + n2 * 0.38;

    //---- 焰体轮廓：根宽尖窄 + 噪声咬边 ----
    float width = (0.62 - hN * 0.50) * (0.52 + 0.58 * flow);
    float body = saturate((width - abs(xb)) * 4.2);

    //---- 焰舌撕裂：尖部断离成舌屑（根部少穿孔）----
    float tear = saturate((n2 * 1.05 - hN * 0.75) * 2.6 + 0.72 - 0.5 * hN);
    body *= tear;

    //顶端必归零 + 根部软起 + 画布护栏
    body *= smoothstep(1.05, 0.80, hN);
    body *= smoothstep(0.0, 0.05, h);
    body *= smoothstep(1.0, 0.90, abs(x));

    //---- 三温层（芯是小内核，幽青才是体色主体）----
    float rim = smoothstep(0.35, 1.0, abs(xb) / max(width, 0.03));
    float core = saturate((width * 0.24 - abs(xb)) * 7.0) * saturate(1.0 - hN * 1.7);

    //---- 怨瞳：炽亮眼球 + 竖长暗缝，随 uLook 追视 ----
    float2 eyeP = float2(x - uLook.x * 0.18, (h - 0.16 - uLook.y * 0.06) * 1.35);
    float ball = exp2(-dot(eyeP, eyeP) * 44.0);
    float2 slitP = float2(eyeP.x * 5.2, eyeP.y * 1.4);
    float slit = exp2(-dot(slitP, slitP) * 36.0);

    //---- 组色 ----
    float3 bodyCol = lerp(uBodyColor, uCurseColor, saturate(uCurse * (0.35 + 0.45 * hN)));
    float3 col = uEdgeColor * rim * body * 0.8
        + bodyCol * body * (0.52 + flow * 0.55)
        + uCoreColor * core * 0.5
        + lerp(uCoreColor, uBodyColor, 0.22) * ball * (0.85 + inten * 0.4);

    //---- 瞳缝从全部亮层里雕出来（加色批不能变暗，只能少加）----
    float slitShadow = 1.0 - slit * 0.8 * saturate(ball * 2.2);
    col *= slitShadow;

    //---- 烬屑剥落 ----
    float speck = step(0.90, n2) * smoothstep(0.25, 0.80, hN) * tear;
    col += lerp(uCoreColor, uCurseColor, uCurse * 0.5) * speck * 0.8;

    //根致密尖稀薄；强度进亮度也进包络
    float dens = lerp(0.85, 0.32, hN);
    float bright = 0.72 + 0.28 * min(inten, 1.6);
    float alpha = saturate(body * dens * 0.72 + ball * 0.85 * slitShadow + speck * 0.30)
        * vertexColor.a * saturate(inten * 2.0);
    return float4(col * bright, alpha);
}

technique Technique1
{
    pass SkeletronEyeFlamePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
