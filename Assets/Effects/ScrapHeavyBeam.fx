// ============================================================================
//ScrapHeavyBeam.fx 废钢统帅的重型切割光柱（扫削热扫段专用）
//材质：过载的工业焊炬，不是干净的能量死光。
//签名行为：①电弧芯低频游走（枪口钉死，越远越晃）②团块热流沿管推进
//③熔渣崩边（两缘异相咬边，颗粒团化不是连续毛边）④锈烟护鞘密度不均
//⑤体外迸溅星点贴着崩边奔跑。虚线预扫仍走 ScrapBeamLine，本文件只管热光柱。
//画法：沿线拉伸 quad（uv.x=枪口0→末端1，uv.y=横截），加色批内绘制：
//rgb 不预乘、a 携带全部包络（加色批源因子是 SourceAlpha）。
//噪声全走绑定采样（s1=PerlinNoise），门控 step/smoothstep，无动态分支
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例相位
float uAspect;      //quad 长/宽，噪声防拉伸
float uExpand;      //0..1 展开进度：枪口收口长度与崩边量
float uOpacity;     //整体透明度（塌缩期衰减）
float uHeat;        //0..1 亮度主控（=当前宽度/满宽）
float3 uCoreColor;  //焊橙内层
float3 uEdgeColor;  //锈红外缘

float pnoise(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float4 PSHeavyBeam(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float along = uv.x;
    float y = (uv.y - 0.5) * 2.0;

    //==== ①电弧芯游走：两频叠加弯折中轴，枪口钉死越远越晃 ====
    float wander = (pnoise(float2(along * uAspect * 0.10 - uTime * 0.6, uSeed)) - 0.5) * 0.30
        + (pnoise(float2(along * uAspect * 0.26 - uTime * 2.1, uSeed + 0.37)) - 0.5) * 0.12;
    wander *= smoothstep(0.0, 0.22, along);
    float d = abs(y - wander);

    //==== ②团块热流：熔料一团一团被推向末端，不是均匀脉冲 ====
    float flow = pnoise(float2(along * uAspect * 0.18 - uTime * 2.6, uSeed + 0.71));
    float chunk = smoothstep(0.48, 0.82, pnoise(float2(along * uAspect * 0.42 - uTime * 3.4, uSeed + 1.13)));

    //==== ③熔渣崩边：两缘异相异速咬边，颗粒团化 ====
    float sideSel = step(0.0, y - wander);
    float biteA = pnoise(float2(along * uAspect * 0.55 - uTime * 1.3, uSeed + 2.3));
    float biteB = pnoise(float2(along * uAspect * 0.62 - uTime * 2.0, uSeed + 4.7));
    float tear = smoothstep(0.38, 0.78, lerp(biteA, biteB, sideSel)) * (0.4 + 0.6 * uExpand);

    //==== 分层轮廓 ====
    float coreW = 0.15 + 0.07 * chunk;
    float core = pow(smoothstep(coreW + 0.10, 0.0, d), 1.4);
    float bodyR = 0.5 - tear * 0.22;
    float body = smoothstep(bodyR, bodyR - 0.32, d);
    //④锈烟护鞘：指数弥散 × 密度不均
    float sheath = exp(-d * 2.7) * (0.5 + 0.5 * pnoise(float2(along * uAspect * 0.14 + uTime * 0.5, uSeed + 3.1)));

    //==== ⑤体外迸溅星点：贴着崩边外侧一条窄带里奔跑 ====
    float sparkBand = smoothstep(bodyR + 0.42, bodyR + 0.05, d) * smoothstep(bodyR - 0.06, bodyR + 0.05, d);
    float spark = step(0.915, pnoise(float2(along * uAspect * 1.7 - uTime * 5.5, uSeed + y * 1.9)));

    //==== 端点：枪口收口（未展满收得更长）+ 末端噪声撕散 ====
    float muzzle = smoothstep(0.0, 0.04 + 0.08 * (1.0 - uExpand), along);
    float muzzleBoost = (1.0 - smoothstep(0.0, 0.10, along)) * 0.9;
    float tipRag = (pnoise(float2(y * 1.4 + uTime * 0.8, uSeed + 5.3)) - 0.5) * 0.07;
    float tip = 1.0 - smoothstep(0.84, 0.995, along + tipRag);

    //==== 合成：暖白芯不打纯白（焊橙材质定律）====
    float3 col = float3(1.0, 0.95, 0.82) * core * (0.95 + 0.75 * chunk)
        + uCoreColor * body * (0.55 + 0.45 * flow)
        + uEdgeColor * sheath * 0.55
        + float3(1.0, 0.82, 0.5) * spark * sparkBand * 1.5;
    col += uCoreColor * muzzleBoost * core;
    col *= 0.6 + 0.6 * uHeat;

    float a = saturate(core * 1.15 + body * 0.6 + sheath * 0.32 + spark * sparkBand * 0.9)
        * muzzle * tip * uOpacity * vc.a;
    return float4(col, a);
}

technique TechHeavyBeam {
    pass P0 {
        PixelShader = compile ps_3_0 PSHeavyBeam();
    }
}
