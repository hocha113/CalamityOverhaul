// ============================================================================
//CultistPhaseSky.fx 教徒五阶段沉浸天幕(第二版:干净的仪式夜穹)
//一版五套全屏噪声互叠读作"乱",二版收敛:共享深空底+双层视差星野(全相统一秩序),
//每相只保留一个主宰元素:星旋=顶部风暴云盖 星云=大尺度柔雾 星尘=坠落光痕
//日耀=灼烧地平辉光 月明=死寂冷穹;uPhase 相邻线性交叉渐变;uStorm=星旋涌激
//uCam=相机视差锚(screenPosition/屏高),星野 3%/6.5%、云雾 5%~9% 层间差速给纵深
//预乘 AlphaBlend;s1=平铺 Perlin(消费端 Textures[1]+LinearWrap,实测值域 0.227~0.776 过 nrm)
//极角审计:全笛卡尔无 atan2;无动态分支,门控全走 step/smoothstep
// ============================================================================

sampler uImage0 : register(s0);   //全屏白像素(不采样)
sampler uNoise : register(s1);

float uTime;
float uIntensity;    //在场强度 0~1,满值近乎盖住原版背景
float uPhase;        //当前阶段(可带小数做换相渐变)
float uStorm;        //风暴涌激 0~1
float uAspect;       //屏宽/屏高
float2 uCam;         //相机位置/屏高,层间视差用

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

//绑定 Perlin 实测值域 0.227~0.776 归一(阈值必须过这里,否则高分位层是死代码)
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

//星野层:哈希格稀疏星点,各自明灭;gate 越高星越稀
float starLayer(float2 base, float cells, float seedOfs, float gate) {
    float2 g = base * cells;
    float2 id = floor(g);
    float2 f = g - id;
    float h1 = nrm(noise(id * 0.0293 + seedOfs));
    float h2 = nrm(noise(id * 0.0517 + seedOfs + 3.71));
    float sel = step(gate, nrm(noise(id * 0.0731 + seedOfs + 8.13)));
    float2 d = f - (float2(h1, h2) * 0.56 + 0.22);
    float dot_ = exp(-dot(d, d) * 150.0);
    float tw = 0.62 + 0.38 * sin(uTime * (0.8 + h1 * 2.6) + h2 * 21.0);
    return dot_ * sel * (0.35 + 0.65 * h2) * tw;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 c = uv - 0.5;
    c.x *= uAspect;
    float rc = length(c);
    //等比坐标:星点保持圆形,云雾不被屏幕拉扁
    float2 sq = float2(uv.x * uAspect, uv.y);

    //相邻阶段线性权重
    float w0 = saturate(1.0 - abs(uPhase - 0.0));
    float w1 = saturate(1.0 - abs(uPhase - 1.0));
    float w2 = saturate(1.0 - abs(uPhase - 2.0));
    float w3 = saturate(1.0 - abs(uPhase - 3.0));
    float w4 = saturate(1.0 - abs(uPhase - 4.0));

    //---- 共享深空底:相色纵向渐变+边缘沉暗(把视线交还给场心的星球与弹幕) ----
    float3 tint = float3(0.014, 0.038, 0.075) * w0
                + float3(0.045, 0.012, 0.055) * w1
                + float3(0.010, 0.028, 0.050) * w2
                + float3(0.070, 0.022, 0.008) * w3
                + float3(0.008, 0.016, 0.014) * w4;
    float horizon = pow(saturate(uv.y), 2.0);
    float vig = 1.0 - smoothstep(0.55, 1.05, rc) * 0.45;
    float3 col = tint * (0.7 + horizon * 0.9) * vig;

    //---- 星野双层:远层慢近层快,月明黯淡日耀被昼光洗淡 ----
    float star1 = starLayer(sq * 0.9 + uCam * 0.030 + float2(uTime * 0.0016, 0.0), 22.0, 0.0, 0.55);
    float star2 = starLayer(sq * 1.0 + uCam * 0.065 + float2(uTime * 0.0031, 0.0), 36.0, 4.43, 0.66);
    float starMute = 1.0 - w4 * 0.55 - w3 * 0.45 - w0 * uStorm * 0.5;
    float3 starCol = lerp(float3(0.72, 0.82, 0.95), float3(1.0, 0.95, 0.88), horizon);
    col += starCol * (star1 * 0.80 + star2 * 0.45) * starMute * vig;

    //---- 星旋:风暴云盖只住上方,顶光浮雕,下半天留干净深空 ----
    float spd = 1.0 + uStorm * 1.5;
    float2 cuv = float2(sq.x * 0.62, uv.y * 1.55) + float2(uTime * 0.022 * spd, 0.0) + uCam * 0.09;
    float cl = noise(cuv) * 0.62 + noise(cuv * 2.3 + 4.1) * 0.38;
    float clift = cl - noise(cuv + float2(0.0, 0.05));
    float capMask = 1.0 - smoothstep(0.12, 0.55, uv.y);
    float3 vx = float3(0.055, 0.080, 0.105) * smoothstep(0.30, 0.80, nrm(cl)) * 1.15;
    vx += float3(0.24, 0.34, 0.42) * saturate(clift * 3.6) * 0.38;
    vx *= capMask;

    //---- 星云:两频大尺度柔雾,雾芯微光,无环无圈 ----
    float2 nuv = sq * 0.55 + uCam * 0.05 + float2(uTime * 0.006, -uTime * 0.004);
    float fog1 = nrm(noise(nuv));
    float fog2 = nrm(noise(nuv * 1.9 + 7.3));
    float neb = smoothstep(0.42, 0.95, fog1 * 0.65 + fog2 * 0.35);
    float3 nb = lerp(float3(0.10, 0.020, 0.115), float3(0.28, 0.065, 0.28), fog2) * neb;
    nb += float3(0.85, 0.45, 0.80) * pow(neb, 3.0) * 0.16;

    //---- 星尘:哈希列坠落光痕(头亮尾淡),垫一层薄冷雾 ----
    float2 duv = sq + uCam * 0.055;
    float dcol = floor(duv.x * 34.0);
    float dh = nrm(noise(float2(dcol * 0.0371, 0.517)));
    float dgate = step(0.60, nrm(noise(float2(dcol * 0.0593, 5.113))));
    float dphase = frac(duv.y * (1.1 + dh * 0.8) - uTime * (0.10 + dh * 0.08) + dh * 9.0);
    float streak = pow(dphase, 7.0) * dgate;
    float3 sd = float3(0.55, 0.85, 0.95) * streak * 0.42;
    sd += float3(0.030, 0.075, 0.110) * fog2 * 0.8;

    //---- 日耀:地平被地面烈焰烤亮+升腾热斑,天顶仍是暗空 ----
    float2 huv = float2(sq.x * 0.8, uv.y * 1.4) + float2(uTime * 0.018, 0.0) + uCam * 0.075;
    float heat = nrm(noise(huv) * 0.6 + noise(huv * 2.1 + 3.3) * 0.4);
    float3 so = float3(0.55, 0.18, 0.03) * pow(saturate(uv.y), 2.6) * (0.7 + 0.6 * heat);
    so += float3(0.95, 0.55, 0.20) * pow(saturate(uv.y), 6.0) * 0.5;
    float rise = pow(nrm(noise(huv * 1.6 + float2(0.0, uTime * 0.05))), 4.0) * (1.0 - uv.y * 0.6);
    so += float3(0.85, 0.40, 0.10) * rise * 0.32;

    //---- 月明:死寂雾场缓漂,缘上蚀青 ----
    float mist = nrm(noise(sq * 0.7 + uCam * 0.04 + uTime * float2(0.003, 0.005)));
    float3 mo = float3(0.05, 0.10, 0.09) * smoothstep(0.45, 0.95, mist) * 0.5;
    mo += float3(0.30, 0.62, 0.52) * pow(saturate(rc * 1.1), 3.5) * 0.10;

    col += vx * w0 + nb * w1 + sd * w2 + so * w3 + mo * w4;
    //涌激压暗与月明沉场作用在总量:星也跟着沉
    col *= 1.0 - w0 * uStorm * 0.22 - w4 * 0.22;

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
