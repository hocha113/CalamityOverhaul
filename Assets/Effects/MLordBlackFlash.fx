// ============================================================================
//MLordBlackFlash.fx 黑闪黑洞量体（月总大招）
//quad 出体（白像素画布 uv 0~1）：吞光暗核（真 alpha 遮挡）+ 光子环
//+ 斜吸积盘 + 红黑电弧缘。s1 = PerlinNoise（旋转笛卡尔采样，无 atan2 无极缝）
//输出预乘 alpha：AlphaBlend 下暗核真正咬掉背景
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
//压缩度 0~1：半径收紧、电弧变躁
float uCollapse;
//电弧强度 0~1
float uArc;
//整体可见度 0~1
float uAlpha;
//实例种子
float uSeed;

float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

//PerlinNoise 实测灰度域 ~0.227..0.776，归一化后再用
float Nrm(float n)
{
    return saturate((n - 0.227) / 0.549);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);

    //压缩：整体半径收紧（越搓越小、越密）
    float shrink = 1.0 - uCollapse * 0.22;
    float coreR = 0.30 * shrink;

    //―――― 吞光暗核：内部纯暗，边缘一段陡峭衰减 ――――
    float core = 1.0 - smoothstep(coreR, coreR + 0.045, r);

    //―――― 光子环：暗核外缘一圈被弯折的光 ――――
    float ring = exp(-pow((r - (coreR + 0.04)) / 0.016, 2.0));

    //―――― 斜吸积盘：各向异性压扁 + 旋转 + 噪声碎块 ――――
    float2 diskP = Rot(p, uTime * 1.2 + uSeed);
    diskP.y *= 3.1;
    float dr = length(diskP);
    float diskBand = exp(-pow((dr - (coreR + 0.22)) / 0.15, 2.0));
    float diskNoise = Nrm(tex2D(uNoise, Rot(p, uTime * 0.55) * 0.32 + uSeed).r);
    diskBand *= 0.4 + 0.6 * diskNoise;

    //―――― 红黑电弧：两层反向旋转噪声场的等值差，脊化取细丝 ――――
    float arcBand = exp(-pow((r - (coreR + 0.15)) / 0.2, 2.0));
    float n1 = Nrm(tex2D(uNoise, Rot(p, uTime * 2.0) * 0.5 + uSeed).r);
    float n2 = Nrm(tex2D(uNoise, Rot(p, -uTime * 1.6) * 0.47 + uSeed * 1.7).r);
    float arcs = pow(saturate(1.0 - abs(n1 - n2) * 4.5), 7.0) * arcBand * uArc;
    //压缩期电弧闪烁加剧（能量憋不住）
    arcs *= 0.75 + 0.25 * sin(uTime * (14.0 + uCollapse * 10.0) + uSeed * 40.0);

    //―――― 预乘合成：暗核压底，光件叠亮 ――――
    float3 col = float3(0.028, 0.01, 0.05) * core;
    col += float3(1.0, 0.2, 0.22) * ring * 0.85;
    col += float3(0.5, 0.05, 0.09) * diskBand * 0.5;
    col += float3(1.0, 0.16, 0.2) * arcs;

    float alpha = saturate(core + ring * 0.75 + diskBand * 0.42 + arcs * 0.7);
    return float4(col * uAlpha, alpha * uAlpha);
}

technique Technique1
{
    pass MLordBlackFlashPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
