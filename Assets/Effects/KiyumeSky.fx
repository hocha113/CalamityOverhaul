//KiyumeSky.fx 鬼梦天幕：红黑穹顶 + 缓涌暗云带 + 双层远山脊 + 地平线上另一座湖畔村的影像
//与 KikasaDreamSky 同源同色板，两处关键改动：
//  1) 近排村落删掉，玩家脚下的村子现在是实体 tile，天空再画一排会和地面打架
//  2) 地平线不再自己猜，由 C# 按真实相机与村落基准行折算成 uHorizon 喂进来
//远山脊是这个世界的东界剪影；远村是"同一个村子在更远处又出现一次"，梦的逻辑
//W2 追加（KIY-P5-D）：E4 云后月轮（uMoonReveal 包络）+ E5 远山火把队列（uTorchLine），
//事件状态机在 KiyumeSkyEvents.cs，这里只消费参数；两层归零时数学全灭，无泄漏
//直线算术+平 tex2D，无分支；s0=白图 s1=PerlinNoise。全覆盖预乘输出

float uTime;        //秒
float uSkyAlpha;    //0-1 天空在场
float2 uScreenSize; //视口真实像素
float uCamX;        //真实相机世界 X（像素）
float uCamY;        //真实相机世界 Y（像素）
float uHorizon;     //地平线屏幕纵向比例 0-1，由 C# 按相机与村落基准折算
float uMoonReveal;  //E4 月轮包络 0-1（=0 时月层各项全乘零）
float2 uMoonPos;    //E4 月心 uv（C# 定值 (0.62, uHorizon-0.36)：月不动，月只会被看见）
float4 uTorchLine;  //E5 火把队列：x=队头（近脊噪声域）y=点数(0=层关) z=方向±1 w=相位种子

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

//====== 鬼梦色板：红与黑，别无其他（数值与 KikasaDreamSky.fx 一致） ======
static const float3 SKY_TOP    = float3(0.052, 0.008, 0.013); //穹顶近黑红
static const float3 SKY_MID    = float3(0.270, 0.042, 0.040); //中天深红
static const float3 HORIZON    = float3(0.560, 0.118, 0.055); //地平烬光
static const float3 CLOUD_DK   = float3(0.085, 0.012, 0.018); //暗云
static const float3 CLOUD_RIM  = float3(0.640, 0.160, 0.070); //云底烬缘
static const float3 RIDGE_FAR  = float3(0.118, 0.030, 0.032); //远山脊（雾里，亮一档拉纵深）
static const float3 RIDGE_NEAR = float3(0.062, 0.014, 0.019); //近山脊
static const float3 SIL_VILL   = float3(0.086, 0.020, 0.024); //远村剪影
static const float3 EMBER      = float3(0.950, 0.340, 0.140); //窗火
static const float3 GROUND_FOG = float3(0.150, 0.026, 0.026); //地平雾
static const float3 MOON_BODY  = float3(0.620, 0.600, 0.550); //月骨白（裁决20 A案：被血雾滤过的月）
static const float  MOON_R     = 0.045;                       //月盘半径（uv）

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//平采样散列：每户一签
float hash1(float cell, float seed) {
    return tex2D(uImage1, float2(cell * 0.0371 + seed * 0.1130, seed * 0.0713 + 0.317)).r;
}

//山脊线：三倍频叠加的低频起伏，返回该 x 处脊顶的 uv.y
float ridgeTop(float x, float baseY, float amp, float seed) {
    float n1 = noiseTex(float2(x * 0.089 + seed, 0.41));
    float n2 = noiseTex(float2(x * 0.243 + seed * 1.7, 0.73));
    float n3 = noiseTex(float2(x * 0.611 + seed * 2.3, 0.19));
    float h = n1 * 0.60 + n2 * 0.27 + n3 * 0.13;
    return baseY - (h - 0.34) * amp;
}

//一排村落：连续起伏的地面剪影垫底（屋子长在地上，不是悬在线上的方块），
//每格抽签，空地/枯树/望楼/民居；民居出檐坡脊，望楼窄高脊陡，枯树团冠被噪声啃出毛边；
//三成民居亮窗火、两成升炊烟。返回 x=剪影 y=窗火 z=炊烟
float3 villageRow(float x, float y, float baseY, float rollAmp, float seedRow, float sizeMul) {
    float cell = floor(x);
    float fx = frac(x) - 0.5;

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
    float hutH = (0.020 + h1 * 0.024) * sizeMul;
    float hutW = (0.090 + h2 * 0.098) * sizeMul;
    float eave = (0.032 + h2 * 0.032) * sizeMul;
    float roofH = (0.011 + h1 * 0.012) * sizeMul;
    float roofSpan = hutW + eave;
    float top = gBase - hutH;
    float rr = saturate(abs(fx) / roofSpan);
    float roofLine = top - roofH * (1.0 - pow(rr, 1.45));
    float hutSil = saturate(
        step(abs(fx), roofSpan) * step(roofLine, y) * step(y, top + 0.005)
        + step(abs(fx), hutW) * step(top, y) * step(y, gBase + 0.016));
    sil = saturate(sil + hutSil * isHut);

    //== 望楼：窄高一柱，脊更陡 ==
    float twH = (0.046 + h1 * 0.032) * sizeMul;
    float twW = (0.030 + h2 * 0.020) * sizeMul;
    float twTop = gBase - twH;
    float twRr = saturate(abs(fx) / (twW + 0.020 * sizeMul));
    float twRoofLine = twTop - 0.021 * sizeMul * (1.0 - pow(twRr, 1.3));
    float twSil = saturate(
        step(abs(fx), twW + 0.020 * sizeMul) * step(twRoofLine, y) * step(y, twTop + 0.004)
        + step(abs(fx), twW) * step(twTop, y) * step(y, gBase + 0.016));
    sil = saturate(sil + twSil * isTower);

    //== 枯村之树：双团冠 + 细干，冠缘噪声啃蚀 ==
    float trH = (0.022 + h1 * 0.019) * sizeMul;
    float2 c1 = float2(fx, y - (gBase - trH)) * float2(1.0, 1.6);
    float2 c2 = float2(fx - (0.07 - h2 * 0.14) * sizeMul, y - (gBase - trH - 0.009)) * float2(1.0, 1.6);
    float blob = smoothstep(0.062 * sizeMul, 0.040 * sizeMul, length(c1))
        + smoothstep(0.045 * sizeMul, 0.029 * sizeMul, length(c2));
    float eaten = step(0.34, noiseTex(float2(cell * 0.171 + fx * 1.3, y * 9.0 + seedRow)));
    float trunk = step(abs(fx), 0.006) * step(gBase - trH, y) * step(y, gBase);
    sil = saturate(sil + (saturate(blob) * eaten + trunk) * isTree);

    //== 窗火：民居三成一格小窗，望楼顶窗常明 ==
    float wx = fx - (h2 - 0.5) * hutW;
    float wy = y - (top + hutH * 0.55);
    float win = step(abs(wx), 0.015 * sizeMul) * step(abs(wy), 0.007 * sizeMul) * step(0.72, h1) * isHut;
    float twWin = step(abs(fx), 0.011 * sizeMul) * step(abs(y - (twTop + 0.010 * sizeMul)), 0.006 * sizeMul) * isTower;
    float flicker = 0.30 + 0.70 * noiseTex(float2(cell * 0.131, uTime * 0.067 + seedRow));
    float light = (win + twWin) * flicker;

    //== 炊烟：两成人家一缕，越升越散、随风游摆 ==
    float smokeGate = step(0.80, h3) * isHut;
    float rise = saturate((top - y) * 7.5);
    float sway = (noiseTex(float2(cell * 0.37, y * 2.2 - uTime * 0.05)) - 0.5) * 0.09 * rise;
    float sx = fx - (h3 - 0.5) * hutW * 0.8 - sway;
    float smoke = exp2(-abs(sx) * 150.0 * (1.35 - rise * 0.95)) * smokeGate
        * rise * saturate(1.0 - rise * 0.80)
        * noiseTex(float2(cell * 0.53, y * 3.0 - uTime * 0.11));

    return float3(saturate(sil), light, smoke);
}

float4 PSKiyumeSky(float2 coords : TEXCOORD0) : COLOR0 {
    float2 uv = coords;
    float aspect = uScreenSize.x / max(uScreenSize.y, 1.0);
    float ux = uv.x * aspect;
    float horizon = uHorizon;

    //====== 穹顶：红黑竖向层次，地平一线烬光 ======
    float3 col = lerp(SKY_TOP, SKY_MID, smoothstep(0.05, 0.62, uv.y));
    float hGlow = exp2(-abs(uv.y - horizon) * 9.5);
    col = lerp(col, HORIZON, hGlow * 0.62);

    //====== 云噪声场：云带与月缘啃蚀共用同一次采样 ======
    float c0 = noiseTex(float2(ux * 0.33 + uTime * 0.0080, uv.y * 1.35 + 0.13));
    float c1 = noiseTex(float2(ux * 0.71 - uTime * 0.0121, uv.y * 2.10 + 0.57));
    float cloudField = c0 * 0.62 + c1 * 0.38;

    //====== E4 云后月轮：红黑世界唯一一次冷色（画在云带前=月在云后）======
    float2 mpos = float2(uMoonPos.x * aspect, uMoonPos.y);
    float md = length(float2(ux, uv.y) - mpos);
    //月缘用云絮场啃蚀：缘口随云涌缓慢变化，像被云咬着
    float eat = (cloudField - 0.50) * 0.020;
    float disc = 1.0 - smoothstep(MOON_R - 0.007 + eat, MOON_R + 0.003 + eat, md);
    float mSurf = noiseTex(float2(ux * 2.9 + 4.7, uv.y * 2.9 + 8.3));  //月面低频斑驳
    float limb = smoothstep(0.0, MOON_R, md);                          //缘口压暗
    //──裁决20 A案体色。备案B"黑月"切换点：把下两行换成
    //  float3 moonBody = SKY_TOP * 0.55;
    //  col = lerp(col, moonBody, disc * uMoonReveal);
    //  月盘即变比云更暗的负空间"月蚀"（纯红黑），血晕与云隙全保留──
    float3 moonBody = MOON_BODY * (0.78 - limb * 0.18) * (0.90 + 0.10 * mSurf);  //峰亮 0.62*0.78≈0.48≤0.5
    col = lerp(col, moonBody, disc * uMoonReveal);
    //外圈血晕：EMBER×0.12 包边，"隔着一层脏玻璃看它"
    float mHalo = exp2(-abs(md - MOON_R) * 30.0) * (1.0 - disc * 0.55);
    col += EMBER * mHalo * 0.12 * uMoonReveal;

    //====== 暗云带：两层反向缓涌，云底压着一线烬缘 ======
    float cloud = saturate((cloudField - 0.42) * 2.6)
        * smoothstep(horizon + 0.02, horizon - 0.34, uv.y);
    //月位半径 0.12 内云遮蔽减弱：云隙感=云自己让开
    cloud *= 1.0 - uMoonReveal * smoothstep(0.12, 0.035, md) * 0.70;
    col = lerp(col, CLOUD_DK, cloud * 0.72);
    float rim = saturate((c0 - 0.52) * 5.0) * exp2(-abs(uv.y - horizon + 0.10) * 12.0);
    col += CLOUD_RIM * rim * 0.16;

    //====== 远山脊：两层不同视差的低频起伏，是这个世界看得见的东界 ======
    float xRidgeFar = ux * 0.62 + uCamX * 0.000045;
    float ridgeFarY = ridgeTop(xRidgeFar, horizon - 0.028, 0.140, 5.0);
    float silRidgeFar = step(ridgeFarY, uv.y);
    col = lerp(col, RIDGE_FAR, silRidgeFar * 0.86);
    //层间垫薄雾拉纵深
    col = lerp(col, GROUND_FOG, silRidgeFar * smoothstep(ridgeFarY, ridgeFarY + 0.10, uv.y) * 0.22);

    float xRidgeNear = ux * 1.05 + uCamX * 0.000110;
    float ridgeNearY = ridgeTop(xRidgeNear, horizon + 0.006, 0.088, 19.0);
    float silRidgeNear = step(ridgeNearY, uv.y);
    col = lerp(col, RIDGE_NEAR, silRidgeNear * 0.92);

    //====== E5 远山火把队列：近脊线上一串烬点缓慢横移，不回头 ======
    //落点与近脊同一噪声域：队伍钉在山脊地物上，不随镜头横移打滑；
    //逐像素只算最近一个槽位（半径 0.004 < 半点距 0.006，辉点互不重叠，免循环）；
    //点距 0.012(uv)×1.05 入域——与 KiyumeScore.TorchSpacingUv 同步改
    float tSpacing = 0.012 * 1.05;
    //队体拖在队头行进方向的后侧（槽 0=队头，槽序向后），与 KiyumeSkyEvents 的出入画折算一致；
    //槽号钳制而非截断：队两端外的像素量到端点火把，晕光自然衰减无接缝
    float tRel = (uTorchLine.x - xRidgeNear) * uTorchLine.z;
    float tSlot = clamp(floor(tRel / tSpacing + 0.5), 0.0, uTorchLine.y - 1.0);
    float tIn = step(0.5, uTorchLine.y);
    float tx = uTorchLine.x - uTorchLine.z * tSlot * tSpacing;
    //脊 y 三点平均防跳变（±半点距再采两次），抬 0.006 让火点坐在脊顶（均值削峰的补偿）
    float ty = (ridgeTop(tx - 0.006, horizon + 0.006, 0.088, 19.0)
        + ridgeTop(tx, horizon + 0.006, 0.088, 19.0)
        + ridgeTop(tx + 0.006, horizon + 0.006, 0.088, 19.0)) * (1.0 / 3.0) - 0.006;
    float td = length(float2((xRidgeNear - tx) / 1.05, uv.y - ty));
    //辉点半径 0.004：硬芯+微晕，各点异相慢闪烁；EMBER 零新色
    float tFlick = 0.45 + 0.55 * noiseTex(float2(tSlot * 0.317 + uTorchLine.w, uTime * 0.041 + tSlot * 0.113));
    float tGlow = exp2(-td * 760.0) + exp2(-td * 210.0) * 0.28;
    col += EMBER * tGlow * tFlick * tIn;

    //====== 远村：地平线上另一座村子的影像。轮廓带记忆般的微颤，整排明度慢呼吸 ======
    float shiver = (noiseTex(float2(uTime * 0.05, 0.71)) - 0.5) * 0.005;
    float breathe = 0.80 + 0.20 * noiseTex(float2(uTime * 0.021, 0.29));
    float xVill = (ux + shiver) * 4.4 + uCamX * 0.000078;
    float3 vill = villageRow(xVill, uv.y, horizon + 0.020, 0.024, 3.0, 0.78);
    //村子长在近山脊上：只有脊线以下才画，免得整排飘在天上
    float onRidge = step(ridgeNearY - 0.030, uv.y);
    col = lerp(col, SIL_VILL, vill.x * 0.82 * breathe * onRidge);
    col += GROUND_FOG * vill.z * 0.50 * onRidge;
    col += EMBER * vill.y * 0.34 * onRidge;

    //====== 地平烬雾：把村脚、山脚与屏底揉进同一层暗红 ======
    float fogBand = smoothstep(horizon - 0.01, horizon + 0.16, uv.y);
    col = lerp(col, GROUND_FOG, fogBand * 0.36);
    //屏底沉暗收边，接缝藏进暗部；实体地形要从这层黑里剥出来
    col = lerp(col, SKY_TOP, smoothstep(horizon + 0.22, 1.0, uv.y) * 0.70);

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
        PixelShader = compile ps_3_0 PSKiyumeSky();
    }
}
