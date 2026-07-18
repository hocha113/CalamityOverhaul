// ============================================================================
//SHPCModSniperLock.fx 狙击瞄具「锁定贯空」
//mode=0 导引线：细线随锁定渐显+呼吸脉动+甩枪晃动+锁满定格闪烁
//mode=1 贯空射线：开幕白炽贯穿+激波环缘+真空气流+残丝溶解
//coords.x 沿线 0枪口 1终点，coords.y 横截；s0+s1；无极坐标输入，无接缝风险
//ps_3_0
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float mode;          //0=导引线 1=贯空射线
float progress;      //mode0:锁定进度0~1  mode1:生命进度0~1
float fadeAlpha;     //整体透明度
float rayLength;     //射线像素长度(刻度密度)
float readyFlash;    //锁满定格闪烁 0~1
float jitter;        //甩枪晃动 0~1
float3 coreColor;    //线芯色
float3 edgeColor;    //辉光色

float4 GuideLine(float2 coords)
{
    float along = coords.x;

    //甩枪晃动：横向噪声位移，稳定瞄准时线是笔直的
    float wob = tex2D(noiseSamp, float2(along * 2.4 - uTime * 1.7, 0.37)).r - 0.5;
    float y = coords.y + wob * jitter * 0.22;
    float crossDist = abs(y - 0.5) * 2.0;

    //渐显前沿：锁定进度推着线头从枪口伸向远端
    float reach = saturate(progress * 1.06);
    float reveal = 1.0 - smoothstep(reach - 0.06, reach, along);
    //前沿亮头
    float head = smoothstep(reach - 0.10, reach - 0.02, along) * (1.0 - smoothstep(reach - 0.02, reach, along));

    //呼吸脉动：锁越满呼吸越急促
    float breathe = 0.85 + 0.15 * sin(uTime * (5.0 + progress * 5.0));

    //细线核心
    float coreW = (0.030 + 0.035 * progress) * breathe + readyFlash * 0.05;
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.4);

    //辉光
    float glowW = 0.16 + 0.20 * progress + readyFlash * 0.25;
    float glow = 1.0 - smoothstep(coreW * 0.5, glowW, crossDist);

    //蓄势能量包：短亮段自枪口流向远端，锁越满越亮
    float dashFreq = rayLength / 220.0;
    float dash = frac(along * dashFreq - uTime * 1.6);
    dash = smoothstep(0.80, 0.97, dash) * (1.0 - smoothstep(0.97, 1.0, dash));
    dash *= (1.0 - smoothstep(0.0, glowW * 0.8, crossDist)) * progress;

    //枪口根部渐亮
    float root = pow(saturate(1.0 - along / 0.05), 1.5) * 0.8;

    float bright = 0.35 + 0.65 * progress;
    float3 color = float3(0.0, 0.0, 0.0);
    color += coreColor * core * (0.9 + readyFlash * 1.6) * bright;
    color += edgeColor * glow * 0.5 * bright;
    color += coreColor * dash * 0.9;
    color += coreColor * head * 1.2;
    color += edgeColor * root;
    //锁满定格：整线泛白闪烁
    color += float3(1.0, 1.0, 1.0) * readyFlash * (core + glow * 0.4) * 0.8;

    float alpha = saturate(core + glow * 0.45 + dash * 0.6 + head + root * 0.5);
    alpha *= fadeAlpha * reveal;
    return float4(color * alpha, alpha);
}

float4 PierceRay(float2 coords)
{
    float along = coords.x;
    float crossDist = abs(coords.y - 0.5) * 2.0;
    float t = progress;

    //瞬时表现：首帧即全长点亮，随后坍缩
    float burst = 1.0 - smoothstep(0.0, 0.22, t);
    float shrink = 1.0 - smoothstep(0.15, 1.0, t);

    //白热核心
    float coreW = 0.16 * shrink + 0.02 * burst + 0.008;
    float core = 1.0 - smoothstep(0.0, coreW, crossDist);
    core = pow(saturate(core), 1.3);

    //冲击辉光
    float glowW = 0.55 * shrink + 0.06;
    float glow = 1.0 - smoothstep(coreW * 0.4, glowW, crossDist);

    //真空带气流：噪声顺线高速冲刷
    float flow = tex2D(noiseSamp, float2(along * 5.0 - uTime * 6.0, coords.y * 0.9)).r;
    float streak = glow * flow * 0.5 * shrink;

    //激波环缘：坍缩期边缘的窄亮圈
    float rim = smoothstep(glowW * 0.72, glowW, crossDist) * (1.0 - smoothstep(glowW, glowW * 1.25, crossDist));
    rim *= smoothstep(0.1, 0.35, t) * shrink * 1.4;

    //枪口炽闪与终点耀斑
    float muzzle = pow(saturate(1.0 - along / 0.07), 2.0) * (1.0 - crossDist) * burst * 1.3;
    float impact = pow(saturate((along - 0.965) / 0.035), 1.6) * (1.0 - crossDist * 0.7) * shrink;

    //末段残丝溶解：噪声抽签决定各段消隐先后
    float seg = tex2D(noiseSamp, float2(along * rayLength * 0.0004, 0.71)).r;
    float dissolve = 1.0 - smoothstep(seg * 0.55 + 0.45, 1.0, t);

    float3 color = float3(0.0, 0.0, 0.0);
    color += float3(1.0, 1.0, 1.0) * core * burst * 1.4;
    color += coreColor * core * 1.2;
    color += edgeColor * glow * 0.6;
    color += edgeColor * rim;
    color += coreColor * streak;
    color += coreColor * (muzzle + impact);

    float alpha = saturate(core + glow * 0.5 + rim * 0.6 + streak * 0.4 + muzzle + impact);
    alpha *= fadeAlpha * dissolve;
    return float4(color * alpha, alpha);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 result = (mode < 0.5) ? GuideLine(coords) : PierceRay(coords);
    return result * vertexColor;
}

technique Technique1
{
    pass SHPCModSniperLockPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
