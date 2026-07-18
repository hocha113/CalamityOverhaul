// ============================================================================
//SHPCModBrace.fx 支架枪托地面锚定场
//画布横置于支架底部，UV.y=GROUND_Y 为地面线；Additive，全笛卡尔无极坐标
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;            //时间
float fadeAlpha;        //整体透明度 0~1
float deployProgress;   //0~1 锚栓钉入/弧带展开进度
float armProgress;      //0~1 站定酝酿进度，预备光环从外向锚点收拢
float recoilFlash;      //0~1 开火后坐闪光
float3 mainColor;       //锚定工程绿
float3 accentColor;     //亮白绿高光

static const float GROUND_Y = 0.6875;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float3 color = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    //---- 预备光环：宽椭圆环随酝酿向锚点收拢，展开开始后让位
    float armVis = armProgress * saturate(1.0 - deployProgress * 2.5);
    if (armVis > 0.005)
    {
        float2 d = (uv - float2(0.5, GROUND_Y)) * float2(1.0, 2.6);
        float dist = length(d);
        float r = lerp(0.46, 0.13, armProgress);
        float ring = smoothstep(0.045, 0.0, abs(dist - r));
        //虚线跳动，收拢中带扫描节奏
        float dash = 0.6 + 0.4 * sin(uv.x * 56.0 + uTime * 9.0);
        float ringGlow = ring * dash * armVis * (0.25 + 0.75 * armProgress);
        color += mainColor * ringGlow * 0.85;
        alpha += ringGlow * 0.6;
    }

    if (deployProgress > 0.003)
    {
        //---- 能量锚栓×3：中栓先钉入，两侧随后
        float boltSum = 0.0;
        float impactSum = 0.0;
        for (int i = 0; i < 3; i++)
        {
            float xi = (i == 0) ? 0.5 : ((i == 1) ? 0.36 : 0.64);
            float order = (i == 0) ? 0.0 : ((i == 1) ? 0.8 : 1.6);
            float p = saturate(deployProgress * 2.6 - order);
            if (p <= 0.001)
                continue;

            float dx = abs(uv.x - xi);
            //栓体：地上短柱打入地下，顶端渐隐
            float topY = GROUND_Y - 0.26 * p;
            float botY = GROUND_Y + 0.14 * p;
            float inSpan = step(topY, uv.y) * step(uv.y, botY);
            float body = smoothstep(0.016, 0.002, dx) * inSpan;
            float tipFade = saturate((uv.y - topY) / 0.10);
            boltSum += body * tipFade * p;

            //钉入点亮斑：地面处的能量积聚
            float2 dImp = (uv - float2(xi, GROUND_Y)) * float2(1.0, 1.8);
            impactSum += exp(-dot(dImp, dImp) * 260.0) * p;
        }
        float boltBoost = 1.0 + recoilFlash * 1.4;
        color += mainColor * boltSum * 0.9 * boltBoost;
        color += accentColor * boltSum * boltSum * 0.5 * boltBoost;
        color += accentColor * impactSum * 0.8 * boltBoost;
        alpha += (boltSum * 0.75 + impactSum * 0.6) * boltBoost;

        //---- 地面稳定弧带：从锚点向两端展开的水平能量层
        float spread = 0.44 * deployProgress;
        float bandY = exp(-pow((uv.y - GROUND_Y) * 22.0, 2.0));
        float bandX = smoothstep(spread, spread - 0.09, abs(uv.x - 0.5));
        float shimmer = tex2D(noiseSamp, float2(uv.x * 1.8 - uTime * 0.25, uv.y * 3.0)).r;
        float band = bandY * bandX * (0.45 + 0.55 * shimmer);
        color += mainColor * band * 0.7;
        alpha += band * 0.45;

        //---- 下压能量丝：噪声向地面流动，表现能量压入地下
        float wispMask = smoothstep(GROUND_Y - 0.30, GROUND_Y - 0.04, uv.y)
            * step(uv.y, GROUND_Y) * bandX;
        float wisp = tex2D(noiseSamp, float2(uv.x * 2.6, uv.y * 1.6 - uTime * 0.55)).r;
        wisp = pow(wisp, 2.2) * wispMask * deployProgress;
        color += mainColor * wisp * 0.55;
        alpha += wisp * 0.3;

        //---- 金属高光扫掠：斜向亮带周期掠过弧带区
        float sweepPhase = frac(uv.x * 0.9 + uv.y * 0.25 - uTime * 0.16);
        float sweep = smoothstep(0.09, 0.0, abs(sweepPhase - 0.5)) * bandY * bandX;
        color += accentColor * sweep * 0.35;
        alpha += sweep * 0.2;

        //---- 后坐全域闪光
        color += accentColor * recoilFlash * bandY * bandX * 0.4;
        alpha += recoilFlash * band * 0.3;
    }

    alpha = saturate(alpha) * fadeAlpha;
    return float4(color * fadeAlpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModBracePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
