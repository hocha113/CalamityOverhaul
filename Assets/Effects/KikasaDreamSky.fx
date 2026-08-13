//KikasaDreamSky.fx 鬼梦天空：红黑穹顶 + 缓涌暗云带 + 远处村落剪影两层视差
//（屋脊/烟囱程序化行列，零星窗火忽明忽暗，轮廓带记忆般的微颤——是影像不是实景）
//+ 地平烬雾。全覆盖预乘输出，跨 0 深度切片画一次盖过原版远景。
//视差按还原后的真实相机值（uCamX/uCamY）驱动。
//直线算术+平 tex2D，无分支；s0=白图 s1=PerlinNoise

float uTime;        //秒
float uSkyAlpha;    //0-1 天空在场（DreamBlend 驱动交叉渐变）
float2 uScreenSize; //视口真实像素
float uCamX;        //真实相机世界 X（像素）
float uCamY;        //真实相机世界 Y（像素）

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

//====== 鬼梦色板：红与黑，别无其他 ======
static const float3 SKY_TOP   = float3(0.052, 0.008, 0.013);  //穹顶近黑红
static const float3 SKY_MID   = float3(0.270, 0.042, 0.040);  //中天深红
static const float3 HORIZON   = float3(0.560, 0.118, 0.055);  //地平烬光
static const float3 CLOUD_DK  = float3(0.085, 0.012, 0.018);  //暗云
static const float3 CLOUD_RIM = float3(0.640, 0.160, 0.070);  //云底烬缘
static const float3 SIL_FAR   = float3(0.066, 0.014, 0.018);  //远村剪影
static const float3 SIL_NEAR  = float3(0.030, 0.007, 0.011);  //近村剪影
static const float3 EMBER     = float3(0.950, 0.340, 0.140);  //窗火
static const float3 GROUND_FOG = float3(0.150, 0.026, 0.026); //地平雾

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//平采样散列：每户一签
float hash1(float cell, float seed) {
    return tex2D(uImage1, float2(cell * 0.0371 + seed * 0.1130, seed * 0.0713 + 0.317)).r;
}

//一排村落的剪影：x 已含视差与格数，返回 sil（剪影）与窗火强度
//户型三签：房高/半宽/烟囱有无；窗火按户稀疏点亮，噪声呼吸忽明忽暗
float2 villageRow(float x, float y, float baseY, float seedRow, float lightGate) {
    float cell = floor(x);
    float fx = frac(x) - 0.5;

    float h1 = hash1(cell, seedRow);
    float h2 = hash1(cell, seedRow + 7.0);
    float h3 = hash1(cell, seedRow + 13.0);

    float hgt = 0.034 + h1 * 0.040;                 //房高（uv）
    float wid = 0.150 + h2 * 0.175;                 //半宽（格内）
    float roofH = 0.016 + h1 * 0.014;

    float inBox = step(abs(fx), wid);
    float top = baseY - hgt;
    //屋脊三角：越靠山墙越低
    float roofLine = top - roofH * saturate(1.0 - abs(fx) / max(wid, 0.001));
    float sil = step(roofLine, y) * inBox * step(y, baseY + 0.06);

    //烟囱：六成人家有，偏一侧的细柱
    float cx = fx - (h3 - 0.5) * wid * 1.1;
    float chim = step(abs(cx), 0.018)
        * step(top - roofH - 0.020, y) * step(y, top)
        * step(0.42, h3);
    sil = saturate(sil + chim);

    //窗火：三成人家亮着一格窗，呼吸忽明忽暗
    float wx = fx - (h2 - 0.5) * wid * 0.9;
    float wy = y - (top + hgt * 0.55);
    float win = step(abs(wx), 0.030) * step(abs(wy), 0.011) * step(0.68, h1);
    float flicker = 0.35 + 0.65 * noiseTex(float2(cell * 0.131, uTime * 0.083 + seedRow));
    float light = win * flicker * lightGate;

    return float2(sil, light);
}

float4 PSDreamSky(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float aspect = uScreenSize.x / max(uScreenSize.y, 1.0);
    float ux = uv.x * aspect;

    //====== 穹顶：红黑竖向层次，地平一线烬光 ======
    float3 col = lerp(SKY_TOP, SKY_MID, smoothstep(0.05, 0.62, uv.y));
    //地平随相机竖移轻轻抬落
    float horizon = 0.600 + clamp(uCamY * -0.0000045, -0.05, 0.05);
    float hGlow = exp2(-abs(uv.y - horizon) * 9.5);
    col = lerp(col, HORIZON, hGlow * 0.62);

    //====== 暗云带：两层反向缓涌，云底压着一线烬缘 ======
    float c0 = noiseTex(float2(ux * 0.33 + uTime * 0.0080, uv.y * 1.35 + 0.13));
    float c1 = noiseTex(float2(ux * 0.71 - uTime * 0.0121, uv.y * 2.10 + 0.57));
    float cloud = saturate((c0 * 0.62 + c1 * 0.38 - 0.42) * 2.6)
        * smoothstep(horizon + 0.02, horizon - 0.34, uv.y);
    col = lerp(col, CLOUD_DK, cloud * 0.72);
    //云底烬缘：贴近地平的云被村火烘出一线暖边
    float rim = saturate((c0 - 0.52) * 5.0) * exp2(-abs(uv.y - horizon + 0.10) * 12.0);
    col += CLOUD_RIM * rim * 0.16;

    //====== 远村：两层视差剪影，轮廓微颤——那只是影像 ======
    //记忆微颤：轮廓横向极小幅漂移 + 整排明度慢呼吸
    float shiver = (noiseTex(float2(uTime * 0.05, 0.71)) - 0.5) * 0.006;
    float breathe = 0.82 + 0.18 * noiseTex(float2(uTime * 0.021, 0.29));

    //远排：小而密，贴着地平
    float xFar = (ux + shiver) * 4.6 + uCamX * 0.000085;
    float2 far = villageRow(xFar, uv.y, horizon + 0.006, 3.0, 0.7);
    //近排：大而疏，压得更低更黑
    float xNear = (ux - shiver * 1.6) * 2.9 + uCamX * 0.000200;
    float2 near = villageRow(xNear, uv.y, horizon + 0.052, 11.0, 1.0);

    col = lerp(col, SIL_FAR, far.x * 0.88 * breathe);
    col = lerp(col, SIL_NEAR, near.x * 0.94);
    //窗火与其在雾里的光晕
    col += EMBER * far.y * 0.30;
    col += EMBER * near.y * 0.52;

    //====== 地平烬雾：村脚下沉进雾里，屏底交给地形 ======
    float fogBand = smoothstep(horizon - 0.01, horizon + 0.16, uv.y);
    col = lerp(col, GROUND_FOG, fogBand * 0.55);
    //屏底再压黑，把接缝藏进暗部
    col = lerp(col, SKY_TOP, smoothstep(horizon + 0.20, 1.0, uv.y) * 0.75);

    //====== 远场烬点：稀疏红星缓缓上浮 ======
    float mote = noiseTex(float2(ux * 2.7 + uTime * 0.006, uv.y * 3.1 + uTime * 0.030));
    float spark = saturate((mote - 0.80) * 12.0)
        * smoothstep(horizon + 0.10, horizon - 0.30, uv.y);
    col += EMBER * spark * 0.10;

    //预乘输出，全覆盖
    return float4(col * uSkyAlpha, uSkyAlpha);
}

technique TechSky {
    pass P0 {
        PixelShader = compile ps_3_0 PSDreamSky();
    }
}
