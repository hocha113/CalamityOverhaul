// ============================================================================
//FishSparklingBeam.fx 闪光皇后鱼相干激光束
//UV.x 1=鱼嘴(头)→0=远端(尾)；UV.y 横截面；Additive quad
//三层亮度结构：暗外晕/饱和单色中层/热芯，边缘干净笔直
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间
float uWidthMul;    //0~1束径倍率，蓄束/衰减时收窄
float uCharge;      //1=蓄束导引线(无热芯+高频闪烁) 0=正式击发
float uOvershoot;   //击发帧过冲0~1，热芯推向纯白并整体增益
float uHalfWidthPx; //quad当前半宽像素，换算热芯像素宽
float seed;         //本束随机种子，错开多束相位
float3 uColor;      //饱和中层单色
float3 uDarkColor;  //暗色外晕

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

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;                  //1鱼嘴(近) → 0远端
    float cross_ = (uv.y - 0.5) * 2.0;   //-1~1横截面
    float ax = abs(cross_);

    //远端耗散：束径沿末段收窄至尖(收窄离场，非alpha渐隐)，微噪声蚀边
    float tipN = tex2D(noiseSamp, float2(along * 4.0 + seed * 7.0, seed + uTime * 1.6)).r;
    float w = smoothstep(-0.02, 0.24, along + (tipN - 0.5) * 0.09);
    if (w < 0.003)
        return float4(0, 0, 0, 0);
    float axw = ax / w;

    //中层：干净笔直的锐利边缘(相干性)，横截面微干涉条纹
    float mid = 1.0 - smoothstep(0.42, 0.52, axw);
    float fringe = 1.0 + 0.08 * sin(cross_ * 26.0 + seed * 40.0);

    //暗外晕：束体压底
    float halo = exp(-axw * axw * 2.6);

    //热芯≤2px：按当前像素宽换算归一化半宽
    float corePx = 1.1 / max(uHalfWidthPx * w, 1.0);
    float core = 1.0 - smoothstep(corePx * 0.7, corePx * 1.6, axw);
    core *= 1.0 - uCharge;

    //蓄束导引线：高频明灭，沿束相位滚动
    float flick = lerp(1.0, 0.5 + 0.5 * sin(uTime * 70.0 + seed * 31.0 + along * 9.0), uCharge);
    //沿束闪烁微光：相干散斑，向远端滚动(能量自鱼嘴涌向末端)
    float scint = 0.90 + 0.20 * tex2D(noiseSamp, float2(along * 6.0 + uTime * 5.0, seed + 0.41)).r;

    //过冲期热芯推白，常驻期为淡色调
    float3 coreCol = lerp(saturate(uColor * 0.55 + 0.45), float3(1.0, 1.0, 1.0), uOvershoot);

    float3 color = uDarkColor * halo * 0.55;
    color += uColor * mid * fringe * scint;
    color += coreCol * core * (0.9 + 1.3 * uOvershoot);
    color *= flick * (1.0 + uOvershoot * 0.35);

    float alpha = saturate(halo * 0.30 + mid * 0.72 + core * 0.9) * flick;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass FishSparklingBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
