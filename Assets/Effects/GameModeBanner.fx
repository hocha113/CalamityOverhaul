// ============================================================================
//GameModeBanner.fx 模式切换演出的大字背景横幅
//横贯屏宽的演出带，逐脸材质：残酷=撕裂血雾带+余烬拉丝 / 修罗=墨浪+描金流线 / 毁灭=苍银冷雾+星灰坠
//带芯压暗保大字可读；uReveal 自中心横向展开，前沿带一道亮线；两端软收口
//s1 = PerlinNoise（LinearWrap）；AlphaBlend 预乘；ps_3_0
//注意：uniform 上禁 if 分支（MojoShader 常量布局教训），三脸全算完按 uMode 链式 lerp
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
float uAlpha;    //CPU 总包络（入出场淡入淡出）
float uReveal;   //0..1 自中心横向展开
float uMode;     //0 残酷 / 1 修罗 / 2 毁灭
float uEnabled;  //1 开启向 / 0 关闭向（关闭压暗脱饱和）
float3 uAccent;  //表现脸主色
float3 uEmber;   //表现脸余烬色

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 p = uv - 0.5;

    //两端软收口 + 自中心的横向展开（收口在先，展开前沿另给亮线）
    float endFade = smoothstep(0.0, 0.06, uv.x) * smoothstep(1.0, 0.94, uv.x);
    float halfSpan = uReveal * 0.52;
    float dx = abs(p.x);
    float sweep = smoothstep(halfSpan, halfSpan - 0.05, dx);
    float front = smoothstep(0.035, 0.0, abs(dx - halfSpan));

    //共用噪声两层：慢雾 + 快纹
    float n1 = tex2D(uNoise, float2(uv.x * 2.2 + uTime * 0.045, uv.y * 0.9 + uTime * 0.012)).r;
    float n2 = tex2D(uNoise, float2(uv.x * 4.6 - uTime * 0.07, uv.y * 1.7 + 0.35)).r;

    //——残酷：撕裂血雾带（高频撕口缘）+ 余烬拉丝——
    //拉丝阈值按 Perlin 中值域（~0.5 居中）取窗，别用高次 pow（中值下整层归零）
    float edgeB = 0.34 + (n2 - 0.5) * 0.26;
    float bandB = smoothstep(edgeB, edgeB - 0.20, abs(p.y));
    float mistB = bandB * (0.45 + 0.55 * n1);
    float streakB = smoothstep(0.52, 0.86, tex2D(uNoise, float2(uv.x * 1.4 - uTime * 0.16, uv.y * 6.0)).r) * bandB;
    float3 colB = uAccent * mistB * 0.95;
    float aB = mistB * 0.75;
    float3 brightB = uEmber * streakB * 1.15;
    float brightAB = streakB * 0.55;

    //——修罗：墨浪缘 + 三条描金流线——
    float wave = sin(uv.x * 9.0 + uTime * 0.8) * 0.05 + sin(uv.x * 17.0 - uTime * 1.3) * 0.03;
    float edgeS = 0.36 + wave;
    float bandS = smoothstep(edgeS, edgeS - 0.24, abs(p.y));
    float mistS = bandS * (0.40 + 0.60 * n1);
    float fil = smoothstep(0.014, 0.0, abs(p.y - sin(uv.x * 6.0 + uTime * 0.9) * 0.10));
    fil += smoothstep(0.010, 0.0, abs(p.y - 0.16 - sin(uv.x * 8.5 - uTime * 1.2) * 0.07)) * 0.8;
    fil += smoothstep(0.010, 0.0, abs(p.y + 0.17 - sin(uv.x * 7.2 + uTime * 1.05 + 2.1) * 0.07)) * 0.8;
    fil *= bandS;
    float3 colS = uAccent * mistS * 0.95;
    float aS = mistS * 0.78;
    float3 brightS = uEmber * fil * 1.5;
    float brightAS = fil * 0.6;

    //——毁灭：苍银冷雾 + 星灰缓坠——
    float edgeN = 0.38 + (n1 - 0.5) * 0.10;
    float bandN = smoothstep(edgeN, edgeN - 0.26, abs(p.y));
    float mistN = bandN * (0.38 + 0.50 * n1);
    float ashN = smoothstep(0.56, 0.72, tex2D(uNoise, float2(uv.x * 5.0, uv.y * 2.2 + uTime * 0.10)).r) * bandN;
    float3 colN = uAccent * mistN * 1.35;
    float aN = mistN * 0.82;
    float3 brightN = uEmber * ashN * 1.6;
    float brightAN = ashN * 0.8;

    //三脸链式混合
    float wS = saturate(uMode);
    float wN = saturate(uMode - 1.0);
    float3 col = lerp(colB, colS, wS);
    col = lerp(col, colN, wN);
    float band = lerp(bandB, bandS, wS);
    band = lerp(band, bandN, wN);
    float a = lerp(aB, aS, wS);
    a = lerp(a, aN, wN);
    float3 bright = lerp(brightB, brightS, wS);
    bright = lerp(bright, brightN, wN);
    float brightA = lerp(brightAB, brightAS, wS);
    brightA = lerp(brightA, brightAN, wN);

    //带芯压暗：大字落座区往夜色收，抬底 alpha 保可读
    float core = smoothstep(0.26, 0.10, abs(p.y)) * band;
    col = lerp(col, float3(0.016, 0.010, 0.014), core * 0.62);
    a = max(a, core * 0.66);

    //签名亮件压在暗芯之上（带芯里减半，不糊大字）
    float coreMute = 1.0 - core * 0.5;
    col += bright * coreMute;
    a += brightA * coreMute;

    //展开前沿亮线
    col += lerp(uAccent, uEmber, 0.5) * front * band * 1.2;
    a += front * band * 0.5;

    //关闭向压暗 + 脱饱和
    col *= lerp(0.55, 1.0, uEnabled);
    float lum = dot(col, float3(0.33, 0.45, 0.22));
    col = lerp(float3(lum, lum, lum) * 0.9, col, lerp(0.65, 1.0, uEnabled));

    float alpha = saturate(a) * sweep * endFade * uAlpha;
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass GameModeBannerPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
