//KikasaDreamSky.fx 鬼梦天空：红黑穹顶 + 缓涌暗云带 + 远处村落剪影两层视差
//（屋脊/烟囱程序化行列，零星窗火忽明忽暗，轮廓带记忆般的微颤，是影像不是实景）
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
static const float3 SIL_FAR   = float3(0.092, 0.022, 0.026);  //远村剪影（比近排亮一档，大气拉纵深）
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

//一排村落：连续起伏的地面剪影垫底（屋子长在地上，不是悬在线上的方块），
//每格抽签，空地/枯树/望楼/民居；民居出檐坡脊（脊线 pow 曲线下垂、檐口外挑），
//望楼窄高脊陡，枯树团冠被噪声啃出毛边；三成民居亮窗火、两成升炊烟。
//返回 x=剪影 y=窗火 z=炊烟
float3 villageRow(float x, float y, float baseY, float rollAmp, float seedRow, float lightGate) {
    float cell = floor(x);
    float fx = frac(x) - 0.5;

    //地面：连续起伏；房基取格心地面，屋子平放不跟着坡歪
    float gCont = baseY + (noiseTex(float2(x * 0.047 + seedRow * 0.31, 0.23)) - 0.5) * rollAmp;
    float gBase = baseY + (noiseTex(float2((cell + 0.5) * 0.047 + seedRow * 0.31, 0.23)) - 0.5) * rollAmp;

    float h1 = hash1(cell, seedRow);
    float h2 = hash1(cell, seedRow + 7.0);
    float h3 = hash1(cell, seedRow + 13.0);
    float h4 = hash1(cell, seedRow + 23.0);

    //地形剪影垫底：村子脚下是实地
    float sil = step(gCont, y);

    //抽签：14% 空地 / 16% 枯树 / 10% 望楼 / 60% 民居
    float isTree = step(0.14, h4) * step(h4, 0.30);
    float isTower = step(0.30, h4) * step(h4, 0.40);
    float isHut = step(0.40, h4);

    //== 民居：身比檐窄，脊线下垂、檐口外挑 ==
    float hutH = 0.028 + h1 * 0.034;
    float hutW = 0.125 + h2 * 0.135;
    float eave = 0.045 + h2 * 0.045;
    float roofH = 0.016 + h1 * 0.016;
    float roofSpan = hutW + eave;
    float top = gBase - hutH;
    float rr = saturate(abs(fx) / roofSpan);
    float roofLine = top - roofH * (1.0 - pow(rr, 1.45));
    float hutSil = saturate(
        step(abs(fx), roofSpan) * step(roofLine, y) * step(y, top + 0.006)
        + step(abs(fx), hutW) * step(top, y) * step(y, gBase + 0.02));
    sil = saturate(sil + hutSil * isHut);

    //== 望楼：窄高一柱，脊更陡 ==
    float twH = 0.065 + h1 * 0.045;
    float twW = 0.042 + h2 * 0.028;
    float twTop = gBase - twH;
    float twRr = saturate(abs(fx) / (twW + 0.028));
    float twRoofLine = twTop - 0.030 * (1.0 - pow(twRr, 1.3));
    float twSil = saturate(
        step(abs(fx), twW + 0.028) * step(twRoofLine, y) * step(y, twTop + 0.005)
        + step(abs(fx), twW) * step(twTop, y) * step(y, gBase + 0.02));
    sil = saturate(sil + twSil * isTower);

    //== 枯村之树：双团冠 + 细干，冠缘噪声啃蚀 ==
    float trH = 0.030 + h1 * 0.026;
    float2 c1 = float2(fx, y - (gBase - trH)) * float2(1.0, 1.6);
    float2 c2 = float2(fx - 0.10 + h2 * 0.20, y - (gBase - trH - 0.012)) * float2(1.0, 1.6);
    float blob = smoothstep(0.085, 0.055, length(c1))
        + smoothstep(0.062, 0.040, length(c2));
    float eaten = step(0.34, noiseTex(float2(cell * 0.171 + fx * 1.3, y * 9.0 + seedRow)));
    float trunk = step(abs(fx), 0.008) * step(gBase - trH, y) * step(y, gBase);
    sil = saturate(sil + (saturate(blob) * eaten + trunk) * isTree);

    //== 窗火：民居三成一格小窗，望楼顶窗常明 ==
    float wx = fx - (h2 - 0.5) * hutW;
    float wy = y - (top + hutH * 0.55);
    float win = step(abs(wx), 0.020) * step(abs(wy), 0.009) * step(0.72, h1) * isHut;
    float twWin = step(abs(fx), 0.014) * step(abs(y - (twTop + 0.014)), 0.008) * isTower;
    float flicker = 0.30 + 0.70 * noiseTex(float2(cell * 0.131, uTime * 0.067 + seedRow));
    float light = (win + twWin) * flicker * lightGate;

    //== 炊烟：两成人家一缕，越升越散、随风游摆 ==
    float smokeGate = step(0.80, h3) * isHut;
    float rise = saturate((top - y) * 6.5);
    float sway = (noiseTex(float2(cell * 0.37, y * 2.2 - uTime * 0.05)) - 0.5) * 0.10 * rise;
    float sx = fx - (h3 - 0.5) * hutW * 0.8 - sway;
    float smoke = exp2(-abs(sx) * 130.0 * (1.35 - rise * 0.95)) * smokeGate
        * rise * saturate(1.0 - rise * 0.80)
        * noiseTex(float2(cell * 0.53, y * 3.0 - uTime * 0.11));

    return float3(saturate(sil), light, smoke);
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

    //====== 远村：影像的两层。地形起伏垫底、屋树塔错落、炊烟窗火 ======
    //记忆微颤：轮廓横向极小幅漂移 + 整排明度慢呼吸，那只是影像
    float shiver = (noiseTex(float2(uTime * 0.05, 0.71)) - 0.5) * 0.006;
    float breathe = 0.82 + 0.18 * noiseTex(float2(uTime * 0.021, 0.29));

    //远排：小而密，雾里只剩个大概
    float xFar = (ux + shiver) * 5.2 + uCamX * 0.000085;
    float3 far = villageRow(xFar, uv.y, horizon + 0.012, 0.030, 3.0, 0.65);
    //近排：大而疏，黑得实
    float xNear = (ux - shiver * 1.6) * 3.1 + uCamX * 0.000200;
    float3 near = villageRow(xNear, uv.y, horizon + 0.065, 0.052, 11.0, 1.0);

    //远排先画，雾重；层间垫一道薄雾拉开纵深再落近排
    col = lerp(col, SIL_FAR, far.x * 0.80 * breathe);
    col += GROUND_FOG * far.z * 0.55;
    float midFog = smoothstep(horizon + 0.005, horizon + 0.10, uv.y);
    col = lerp(col, GROUND_FOG, midFog * 0.30);
    col = lerp(col, SIL_NEAR, near.x * 0.95);
    col += float3(0.30, 0.07, 0.05) * near.z * 0.85;
    //窗火：近排亮些，远排只是雾里一点暖
    col += EMBER * (far.y * 0.24 + near.y * 0.50);

    //====== 地平烬雾：把村脚与屏底揉进同一层暗红里 ======
    float fogBand = smoothstep(horizon - 0.01, horizon + 0.14, uv.y);
    col = lerp(col, GROUND_FOG, fogBand * 0.34);
    //屏底沉暗收边，接缝藏进暗部
    col = lerp(col, SKY_TOP, smoothstep(horizon + 0.24, 1.0, uv.y) * 0.62);

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
