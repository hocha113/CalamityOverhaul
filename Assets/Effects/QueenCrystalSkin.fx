// ============================================================================
// QueenCrystalSkin.fx 皇后晶面皮肤
// 叠加在本体精灵上的水晶质感加色层：分面镶嵌明暗 + 行进折射闪点 + 轮廓色散缘光
// ps-only(SpriteBatch Immediate 消费)，输出进 Additive 批：float4(rgb, 1) 直接加光
// 帧表渗色防线：邻域采样全部钳进 uUvRect 帧界(原版帧表零间距)
// 噪声实测值域 0.227~0.776，阈值一律过 nrm() 归一(VFX.md Noise-threshold rule)
// ============================================================================

float uTime;
float uIntensity;   //总强度 0~1
float uHueSeed;     //色相种子
float4 uUvRect;     //当前帧在整张贴图上的 uv 矩形(xy=偏移 zw=尺寸)
float2 uTexelSize;  //整张贴图的单像素 uv 尺寸

sampler bodySamp : register(s0);  //本体精灵(SpriteBatch 主纹理，刻意落 s0)
sampler noiseSamp : register(s1); //PerlinNoise，消费端显式绑定

//实测值域归一
float nrm(float x)
{
    return saturate((x - 0.227) / 0.549);
}

float3 PrismHue(float t)
{
    return 0.72 + 0.28 * cos(6.28318 * (t + float3(0.0, 0.35, 0.68)));
}

//邻域采样钳进帧界(留半像素余量)，防跨帧渗色
float2 ClampToFrame(float2 uv)
{
    float2 lo = uUvRect.xy + uTexelSize * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexelSize * 0.5;
    return clamp(uv, lo, hi);
}

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float4 body = tex2D(bodySamp, uv);
    float mask = body.a;

    //帧内局部坐标 0~1
    float2 lc = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);

    //=========================================================
    //分面镶嵌：三向量化条带交叉出直边平行四边形晶面，
    //噪声只做轻微扰曲(保凝胶感)，板块明暗走哈希+慢呼吸换亮
    //=========================================================
    float n1 = nrm(tex2D(noiseSamp, lc * 1.1 + float2(uHueSeed * 3.0, 0.17)).g);
    float2 wc = lc + (n1 - 0.5) * 0.07;
    float s1 = dot(wc, float2(0.944, 0.330)) * 4.6;
    float s2 = dot(wc, float2(-0.387, 0.922)) * 3.8;
    float s3 = dot(wc, float2(0.730, -0.684)) * 3.2;
    float id1 = floor(s1);
    float id2 = floor(s2);
    float id3 = floor(s3);
    //板块哈希明暗
    float plateHash = frac(sin(id1 * 127.1 + id2 * 311.7 + id3 * 74.7) * 43758.5453);
    //板块换亮：各板块相位错开的慢呼吸(晶体转光)
    float relight = 0.5 + 0.5 * sin(uTime * 1.6 + plateHash * 6.28318);
    float facet = (0.3 + plateHash * 0.7) * (0.55 + 0.45 * relight);

    //晶面裂线：条带边界细亮缝(直边碎面结构)
    float f1 = frac(s1);
    float f2 = frac(s2);
    float f3 = frac(s3);
    float seam = smoothstep(0.075, 0.0, min(f1, 1.0 - f1))
               + smoothstep(0.065, 0.0, min(f2, 1.0 - f2)) * 0.8
               + smoothstep(0.06, 0.0, min(f3, 1.0 - f3)) * 0.6;
    seam = saturate(seam);

    //=========================================================
    //行进折射闪点：旋转坐标高阈值噪声，沿体表游走
    //=========================================================
    float ca = cos(uTime * 0.35);
    float sa = sin(uTime * 0.35);
    float2 pr = float2(lc.x * ca - lc.y * sa, lc.x * sa + lc.y * ca);
    float sparkN = nrm(tex2D(noiseSamp, pr * 1.6 + float2(uTime * 0.06, uHueSeed * 7.0)).g);
    float sparkle = smoothstep(0.82, 0.95, sparkN);

    //扫掠亮带：一条斜向亮线周期性掠过身体(晶体转光)
    float sweep = frac(lc.x * 0.6 + lc.y * 0.4 - uTime * 0.22);
    float sweepBand = smoothstep(0.12, 0.0, abs(sweep - 0.5) - 0.04);

    //=========================================================
    //轮廓色散缘光：四邻域 alpha 边检(帧界钳制)，缘带三色错相
    //=========================================================
    float2 off = uTexelSize * 2.0;
    float aL = tex2D(bodySamp, ClampToFrame(uv - float2(off.x, 0.0))).a;
    float aR = tex2D(bodySamp, ClampToFrame(uv + float2(off.x, 0.0))).a;
    float aU = tex2D(bodySamp, ClampToFrame(uv - float2(0.0, off.y))).a;
    float aD = tex2D(bodySamp, ClampToFrame(uv + float2(0.0, off.y))).a;
    float edge = saturate(mask * 4.0 - aL - aR - aU - aD);
    //缘上色相随位置滚动(色散感)
    float3 rimHue = PrismHue(uHueSeed + lc.y * 0.6 + lc.x * 0.25 + uTime * 0.12);

    //=========================================================
    //合成(乘体表掩码，加色输出)
    //=========================================================
    float3 hueA = PrismHue(uHueSeed + uTime * 0.05);
    float3 hueB = PrismHue(uHueSeed + 0.4 + uTime * 0.05);
    float bodyLuma = dot(body.rgb, float3(0.3, 0.5, 0.2));

    float3 color = hueA * facet * (0.26 + bodyLuma * 0.34);
    color += hueB * seam * 0.34 * (0.45 + relight * 0.55);
    color += hueB * sweepBand * 0.24 * (0.4 + bodyLuma * 0.6);
    color += float3(1.0, 0.98, 0.95) * sparkle * 0.85;
    color += rimHue * edge * 0.85;

    color *= mask * uIntensity;
    //真加色批(SourceAlpha, One)：alpha=包络，rgb 直加
    return float4(color * vColor.rgb, vColor.a);
}

technique Technique1
{
    pass QueenCrystalSkinPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
