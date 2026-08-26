// ============================================================================
//CultistPhaseSky.fx 教徒五阶段沉浸天幕:身处星球内部/风暴眼内部的混乱
//uPhase 0星旋 1星云 2星尘 3日耀 4月明,相邻线性交叉渐变;uStorm=风暴涌激(星旋出场拉满)
//星旋=对转涡流带(刚体旋转场,连续无缝) 星云=层积雾絮 星尘=坠落光痕 日耀=升腾热浪 月明=死寂雾场
//预乘 AlphaBlend;s1=平铺 Perlin(消费端 Textures[1]+LinearWrap)
// ============================================================================

sampler uImage0 : register(s0);   //全屏白像素(不采样)
sampler uNoise : register(s1);

float uTime;
float uIntensity;    //在场强度 0~1,满值近乎盖住原版背景
float uPhase;        //当前阶段(可带小数做换相渐变)
float uStorm;        //风暴涌激 0~1
float uAspect;       //屏宽/屏高,保持涡流圆形

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

float2 rot(float2 v, float a) {
    float c = cos(a);
    float s = sin(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 c = uv - 0.5;
    c.x *= uAspect;

    //相邻阶段线性权重
    float w0 = saturate(1.0 - abs(uPhase - 0.0));
    float w1 = saturate(1.0 - abs(uPhase - 1.0));
    float w2 = saturate(1.0 - abs(uPhase - 2.0));
    float w3 = saturate(1.0 - abs(uPhase - 3.0));
    float w4 = saturate(1.0 - abs(uPhase - 4.0));
    float rc = length(c);

    //---- 星旋:乌云密布,顶光滚云(uStorm 加速压暗) ----
    float spd = 1.0 + uStorm * 1.6;
    float2 cuv = c * float2(1.15, 1.9) + float2(uTime * 0.035 * spd, 0.0);
    float cl1 = noise(cuv);
    float cl2 = noise(cuv * 2.1 + float2(uTime * 0.022 * spd, 3.7));
    float cloudV = cl1 * 0.62 + cl2 * 0.38;
    //顶光浮雕:云顶亮缘,云底沉黑
    float cliftV = cloudV - noise(cuv + float2(0.0, 0.045));
    float3 vx = lerp(float3(0.006, 0.010, 0.016), float3(0.085, 0.115, 0.150), smoothstep(0.30, 0.85, cloudV));
    vx += float3(0.30, 0.42, 0.50) * saturate(cliftV * 4.0) * 0.35;
    vx *= 1.0 - uStorm * 0.35;

    //---- 星云:扭曲的光晕,噪声折射的同心环 ----
    float warpN = noise(c * 1.3 + uTime * float2(0.012, -0.009));
    float dWarp = rc + (warpN - 0.5) * 0.42;
    float ringPh = frac(dWarp * 2.6 - uTime * 0.07);
    float haloRing = smoothstep(0.08, 0.0, abs(ringPh - 0.5) - 0.16);
    float nebFog = noise(c * 0.8 + uTime * float2(-0.014, 0.010) + 6.3);
    float3 nb = lerp(float3(0.045, 0.010, 0.075), float3(0.30, 0.07, 0.32), smoothstep(0.3, 0.9, nebFog));
    nb += float3(0.95, 0.52, 0.85) * haloRing * (0.28 + 0.30 * nebFog);
    nb += float3(1.0, 0.86, 1.0) * pow(saturate(1.0 - dWarp), 3.0) * 0.22;

    //---- 星尘:雷闪交加的暗夜云天 ----
    float2 suv = c * float2(1.3, 2.0) + float2(uTime * 0.028, 0.0);
    float scl = noise(suv) * 0.6 + noise(suv * 2.3 + 8.1) * 0.4;
    //节拍化天闪:云层被从内部照亮
    float beat = uTime * 2.1;
    float fseed = floor(beat);
    float fph = frac(beat);
    float fgate = step(0.42, noise(float2(fseed * 0.0713 + 0.171, 0.353)));
    float fenv = smoothstep(0.00, 0.05, fph) * (1.0 - smoothstep(0.07, 0.30, fph)) * fgate;
    float fx = noise(float2(fseed * 0.0577 + 0.313, 0.611));
    float flashBlob = exp(-pow((uv.x - fx) * 3.2, 2.0)) * (1.0 - uv.y * 0.8);
    float3 sd = lerp(float3(0.008, 0.014, 0.032), float3(0.055, 0.085, 0.135), smoothstep(0.32, 0.88, scl));
    sd += float3(0.75, 0.90, 1.0) * fenv * flashBlob * (0.5 + 0.7 * scl);
    //远雷剪影线:闪光时一道竖折光痕
    float boltRidge = 1.0 - smoothstep(0.006, 0.020, abs(noise(float2(uv.y * 1.8 + fseed * 3.7, 0.27)) - 0.5) * 0.4 + abs(uv.x - fx) * 0.5);
    sd += float3(0.85, 0.95, 1.0) * boltRidge * fenv * 0.8;

    //---- 日耀:被炙烤的橙色云天空,云底吃火光 ----
    float2 huv = c * float2(1.2, 1.8) + float2(uTime * 0.030, 0.0);
    float hcl = noise(huv) * 0.6 + noise(huv * 2.2 + 5.5) * 0.4;
    //底光浮雕:云的下缘被地面烈焰烤亮
    float hlift = hcl - noise(huv - float2(0.0, 0.040));
    float grad = 1.0 - uv.y;
    float3 so = lerp(float3(0.14, 0.030, 0.008), float3(0.62, 0.20, 0.04), smoothstep(0.28, 0.85, hcl) * 0.7 + grad * 0.35);
    so += float3(1.0, 0.60, 0.18) * saturate(hlift * 4.5) * 0.5;
    so += float3(1.0, 0.80, 0.35) * pow(grad, 2.5) * 0.28;

    //---- 月明:死寂雾场,缘上蚀青 ----
    float mist = noise(c * 1.1 + c * (uTime * 0.008) + uTime * float2(0.004, 0.006));
    float3 mo = float3(0.012, 0.016, 0.020)
        + float3(0.14, 0.26, 0.22) * smoothstep(0.55, 0.95, mist) * 0.35;
    mo += float3(0.55, 1.0, 0.85) * pow(saturate(rc * 1.15), 3.0) * 0.06;

    float3 col = vx * w0 + nb * w1 + sd * w2 + so * w3 + mo * w4;
    //全覆盖:满强度时完全盖住原版背景
    float alpha = saturate(uIntensity * 0.97);
    return float4(col * alpha, alpha) * vertexColor;
}

technique TechPhaseSky
{
    pass PhaseSkyPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
