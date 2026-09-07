// ============================================================================
//EmpressScreenPrism.fx 光之女皇·全屏后效
//脉冲=径向RGB分光+扩张白环+短暂白闪；环境档=屏缘轻色散描边
//命中链=整屏压黑+沿击向运动模糊；竞技场=圈外压暗+越界晕影；终章停顿=预光/闪
//采样uImage0屏幕；直线算术+plain tex2D，无分支无tex2Dlod（FNA约束）
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图（全屏拷贝）

float uTime;
float uProgress;      //脉冲进度 0起爆→1结束
float uIntensity;     //脉冲强度 0~1
float uAmbient;       //环境棱彩档 0~1
float2 uCenter;       //脉冲中心UV
float uAspect;        //宽高比
float uHitDark;       //命中压黑 0~0.8
float2 uFlashDir;     //定向闪光：方向×强度（0~1.2）
float2 uArenaCenter;  //竞技场圆心UV
float uArenaRadius;   //竞技场半径（宽高比修正后UV单位），0=关
float uArenaSoft;     //圈外过渡带宽（同单位）
float uArenaPull;     //本人越界深度 0~1
float uArenaFlash;    //被捕闪光 0~0.8
float uPhaseGlow;     //终章停顿预光 0~1
float uPhaseFlash;    //终章停顿闪 0~1

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //几何：以脉冲中心为原点的径向量（修正宽高比）
    float2 rel = coords - uCenter;
    rel.x *= uAspect;
    float dist = length(rel) + 1e-5;
    float2 dir = rel / dist;
    dir.x /= uAspect;

    //脉冲：色散强度随进度退潮；分光位移沿径向
    float pulseEnv = uIntensity * pow(saturate(1.0 - uProgress), 1.35);
    float ringR = lerp(0.05, 1.35, pow(saturate(uProgress), 0.62));
    float ring = exp(-pow((dist - ringR) * 9.0, 2.0)) * pulseEnv;
    float echo = exp(-pow((dist - ringR * 0.55) * 13.0, 2.0)) * pulseEnv * 0.6;
    float split = (0.016 + 0.024 * exp(-dist * 1.8) + 0.018 * (ring + echo)) * pulseEnv;

    //环境档：屏缘轻色散
    float2 cent = coords - float2(0.5, 0.5);
    cent.x *= uAspect;
    float rimDist = length(cent);
    float2 rimDir = cent / (rimDist + 1e-5);
    rimDir.x /= uAspect;
    float rimSplit = uAmbient * 0.0075 * smoothstep(0.25, 0.85, rimDist);

    float2 offR = dir * split + rimDir * rimSplit;
    float2 offB = -dir * split - rimDir * rimSplit;

    float4 src = tex2D(uImage0, coords);
    float rCh = tex2D(uImage0, coords + offR).r;
    float bCh = tex2D(uImage0, coords + offB).b;
    float3 prismatic = float3(rCh, src.g, bCh);

    //定向闪光：沿击向四抽头运动模糊，强度随向量长度
    float fl = length(uFlashDir);
    float2 fd = uFlashDir * 0.05;
    fd.x /= uAspect;
    float3 blur = tex2D(uImage0, coords + fd * 0.5).rgb
                + tex2D(uImage0, coords - fd * 0.5).rgb
                + tex2D(uImage0, coords + fd).rgb
                + tex2D(uImage0, coords - fd).rgb;
    blur *= 0.25;
    prismatic = lerp(prismatic, blur, saturate(fl * 1.4));

    //白闪与环带珠光
    float flash = uIntensity * pow(saturate(1.0 - uProgress), 3.0) * 0.5;
    float3 ringGlow = float3(1.0, 0.97, 0.92) * ring * 0.85;
    float3 echoGlow = (float3(0.75, 0.9, 1.0) + 0.35 * float3(sin(uTime * 3.0), sin(uTime * 3.0 + 2.1), sin(uTime * 3.0 + 4.2))) * echo * 0.5;

    float lum = dot(prismatic, float3(0.299, 0.587, 0.114));
    float3 color = lerp(prismatic, prismatic + (prismatic - float3(lum, lum, lum)) * 0.3, uAmbient);
    color += ringGlow + echoGlow + float3(flash, flash, flash);

    //竞技场：圈外压暗成暖黑，圈缘留一线暖光；本人越界时屏缘泛暖红晕影
    float2 arel = coords - uArenaCenter;
    arel.x *= uAspect;
    float ad = length(arel);
    float arenaOn = step(0.0001, uArenaRadius);
    float outside = smoothstep(uArenaRadius, uArenaRadius + max(uArenaSoft, 0.001), ad) * arenaOn;
    float edge = exp(-pow((ad - uArenaRadius) * 60.0, 2.0)) * arenaOn;
    float3 warmDark = float3(0.09, 0.05, 0.025);
    color = lerp(color, color * 0.22 + warmDark * 0.25, outside * 0.9);
    color += float3(1.0, 0.82, 0.5) * edge * 0.18;
    color += float3(0.55, 0.2, 0.05) * smoothstep(0.3, 0.9, rimDist) * uArenaPull * 0.7;

    //命中链：先压黑，再沿击向提亮
    color *= 1.0 - uHitDark;
    color += float3(1.0, 0.95, 0.85) * fl * 0.32;

    //终章停顿：预光（暖色呼吸提亮）与闪
    color += float3(1.0, 0.9, 0.7) * uPhaseGlow * 0.12 * (0.55 + 0.45 * sin(uTime * 6.0));
    color += float3(1.0, 1.0, 1.0) * uPhaseFlash * 0.6;
    color += float3(1.0, 0.85, 0.6) * uArenaFlash * 0.5;

    return float4(color, src.a) * vertexColor;
}

technique Technique1
{
    pass EmpressScreenPrismPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
