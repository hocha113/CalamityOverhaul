// ============================================================================
//ScrapBeamLine.fx 废钢统帅的统一"线"材质：
//热芯（白热窄条）+ 衍射护鞘（色缘指数衰减）+ 沿线噪声呼吸（热流窜动）
//+ 端点软融。uDash>0 时变成滚动虚线的预警线形态，uHot 驱动整体亮度与芯宽。
//消费者：镭射脉冲弹体、扫削射线、突刺/工具预警线、探照灯锥、指挥红线。
//画法：沿线拉伸的四边形（uv.x=沿线 0..1，uv.y=横截 0..1），加色批内绘制。
//噪声 2 次采样，门控走 step/lerp，无动态分支。s0=白像素 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例相位
float uHot;         //0..1 亮度与芯宽主控
float uDash;        //0=实线 1=滚动虚线预警
float uAspect;      //线长/线宽，噪声防拉伸
float uFadeHead;    //头端软融长度（uv.x 比例）
float uFadeTail;    //尾端软融长度
float3 uCoreColor;  //芯色（近白热）
float3 uEdgeColor;  //鞘色（锈红/焊橙）

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float4 PSBeamLine(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    //横截距离：0=中线 1=边缘
    float d = abs(uv.y - 0.5) * 2.0;

    //沿线热流：两频噪声窜动，读出"电在跑"
    float n0 = noiseTex(float2(uv.x * uAspect * 0.09 - uTime * 1.7, uSeed));
    float n1 = noiseTex(float2(uv.x * uAspect * 0.23 + uTime * 0.9, uSeed * 1.7 + 0.31));
    float flow = 0.72 + n0 * 0.34 + n1 * 0.18;

    //热芯：uHot 越高芯越宽越亮
    float coreW = 0.16 + uHot * 0.22;
    float core = smoothstep(coreW + 0.10, 0.0, d);
    //护鞘：指数衰减的色缘
    float sheath = exp(-d * 3.4) * flow;

    //端点软融
    float env = smoothstep(0.0, max(uFadeHead, 0.001), uv.x)
        * smoothstep(1.0, 1.0 - max(uFadeTail, 0.001), uv.x);

    //预警虚线：滚动斑马纹 + 呼吸，芯保留微弱常亮读出指向
    float dash = step(0.42, frac(uv.x * uAspect * 0.06 - uTime * 2.6 + uSeed));
    float dashMul = lerp(1.0, 0.22 + dash * 0.78, uDash);

    float3 color = uCoreColor * core * (0.85 + uHot * 0.9)
        + uEdgeColor * sheath * (0.5 + uHot * 0.5);
    //只在 BlendState.Additive 批内绘制：加进画面的光 = rgb × a，
    //故 rgb 不预乘、a 携带全部包络（加色批源因子是 SourceAlpha，a=0 会整张消失）
    float a = saturate((core + sheath * 0.6) * env * dashMul) * vc.a;
    return float4(color, a);
}

technique TechBeamLine {
    pass P0 {
        PixelShader = compile ps_3_0 PSBeamLine();
    }
}
