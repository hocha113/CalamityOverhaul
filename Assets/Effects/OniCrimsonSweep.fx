// ============================================================================
//OniCrimsonSweep.fx 鬼切连段刀光(刀身扫过的体积,TriangleStrip)
//几何契约(CrimsonSweepRenderer):外顶点=刀尖轨迹外扩 uEdgePadPx(剃刀线+外晕住这里),内顶点=刀身内侧点,C# 按 u 收成月牙
//uv.x=s 笔画弧长归一坐标(0 起笔..1 满程,锚定笔画起点,收缩推进时纹理不滑)
//uv.y=v 横向 0=内缘 1=外缘(刀尖侧)
//顶点色 R=z 归一(0.5 屏面,+近) G=本切片带宽/uBandPx(px 恒宽量用) A=不透明度
//时间轴全在 uniform:uHead 刃头 / uTail 体收缩前沿 / uErode 刃痕蚀退 / uFlash 落位闪 / uLayer 0 本体 1 滞后衬层
//
//材质=硬边不透明速度涂抹,层次自外向内:
//  外晕(3px 紧贴,不圆) → 2.4px 暖白剃刀线(px 恒宽,沿程串珠明暗) → 第二道细刃文
//  → 热核(白热纤维) → 灼烧带(橙热纤维,核与体的交界在烧) → 体(绯红,三股错位笔道 + 亮/暗纤维 + 脊状裂纹)
//  → 内缘被纤维撕成锯齿,贴一道近黑硬沿(不是模糊)
//沿程:刃头 ≤4px 硬前缘 + 12px 热前晕;尾端老化(更暗、更碎、裂纹更多、密度更低)
//收缩前沿/蚀退边都带橙热烧边(能量在烧掉,不是在淡出);体退后剃刀线冷却成墨线按干笔断丝(飞白)撕
//全部阈值用窄 smoothstep/近 step:要的是硬、碎、锐,不要圆滑渐变
//无 uTime 滚动(涂抹是冻结的运动记录),无分支,plain tex2D,预乘 alpha → BlendState.AlphaBlend
//采样器显式寄存器:s1=PerlinNoise s2=NoiseSoft01 s3=SlashBrush01(真 alpha,r*a),C# 侧先绑再 Apply
// ============================================================================

float4x4 transformMatrix;
float uHead;          //0..1 刃头(揭开前沿)
float uTail;          //0..1 体收缩前沿(>=uHead 时体全收,只剩刃痕)
float uErode;         //0..1 刃痕蚀退(尾先死)
float uFlash;         //0..1 落位满形闪(≤2 帧)
float uLayer;         //0 本体 1 滞后衬层(上一帧轮廓,压暗无剃刀线)
float uStrokeLen;     //px 满程弧长
float uBandPx;        //px 刃头处带宽(几何最大值,含外扩余量)
float uEdgePadPx;     //px 几何外缘超出刀尖轨迹的余量
float uSeed;
float uFade;
float uFarSel;        //0 整体 +1 近半 -1 远半
float uFarDim;        //远半压暗地板(0=不分层)

float3 uColHot;       //暖白热
float3 uColBright;    //亮绯红
float3 uColDeep;      //深红
float3 uColDark;      //近黑暗酒红
float3 uColEmber;     //灼烧橙

sampler noiseSamp : register(s1);
sampler softSamp : register(s2);
sampler brushSamp : register(s3);

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
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

//窄阶跃:硬材质的通用刀口
float Hard(float edge, float width, float x)
{
    return smoothstep(edge - width, edge + width, x);
}

float4 PSSweep(PSInput input) : COLOR0
{
    float s = input.TexCoords.x;
    float v = input.TexCoords.y;
    float w = 1.0 - v;                                      //0 外缘 → 1 内缘
    float bandPx = max(uBandPx * max(input.Color.g, 0.05), 8.0);
    float dOut = w * bandPx;                                //距几何外缘 px
    float L = max(uStrokeLen, 60.0);
    float sPx = s * L;
    float pad = uEdgePadPx;
    float wIn = saturate((dOut - pad) / max(bandPx - pad, 1.0));   //0 刀尖轨迹 → 1 内缘
    float isBody = 1.0 - saturate(uLayer);

    //---- 远近半侧 ----
    float zN = input.Color.r * 2.0 - 1.0;
    float farW = smoothstep(0.10, -0.10, zN);
    float passMul = lerp(1.0, lerp(farW, 1.0 - farW, step(0.5, uFarSel)), step(0.5, abs(uFarSel)));
    float dimFloor = lerp(1.0, uFarDim, step(0.01, uFarDim));
    float depthDim = lerp(1.0, dimFloor, saturate(-zN));

    //---- 三股错位笔道:跨带分三条 lane,各自沿 s 错位,纤维/撕口在 lane 间不连续 ----
    float lane = min(floor(wIn * 3.0), 2.0);
    float laneN = tex2D(noiseSamp, float2(lane * 0.37 + uSeed * 11.3, 0.53 + uSeed * 0.2)).r;
    float laneShift = (laneN - 0.5) * 52.0;                 //px
    float sL = sPx + laneShift;
    float laneFrac = frac(wIn * 3.0);
    float groove = 1.0 - Hard(0.06, 0.03, min(laneFrac, 1.0 - laneFrac));   //lane 交界细缝

    //---- 纤维场:笔刷长丝(归一空间,贴图本身平滑) + 两级噪声纤维(像素空间采样,512 贴图 ≤3 texel/px,
    //     沿 s 拉长 4~6 倍、跨带压短 → 长纤维;不许按归一坐标平铺十几遍,那是逐像素椒盐) ----
    float4 b1 = tex2D(brushSamp, float2(sL / 260.0 + uSeed * 3.1, wIn * 0.92 + 0.04));
    float4 b2 = tex2D(brushSamp, float2(sL / 120.0 - uSeed * 7.7 + 0.31, wIn * 0.55 + 0.22));
    float streak = saturate(b1.r * b1.a * 0.65 + b2.r * b2.a * 0.35);   //笔刷长丝,只做明度乘子
    //噪声纤维:沿 s 拉长 10 倍以上,跨带 ~10px 一根;均值 0.5 的噪声直接阈,别混进均值不明的笔刷
    float f2 = tex2D(noiseSamp, float2(sL / 2200.0 - uSeed * 5.1, dOut / 200.0 + uSeed)).r;
    float f3 = tex2D(noiseSamp, float2(sL / 900.0 + uSeed * 7.7, dOut / 160.0 + 0.37)).r;
    float fiberN = f2 * 0.62 + f3 * 0.38;
    float fibHot = saturate((fiberN - 0.52) * 6.5);         //亮纤维,硬阈
    float fibDark = saturate((0.43 - fiberN) * 6.0) * 0.8;  //暗纤维

    //---- 裂纹:沿 s 拉长的低频噪声零交叉 → 顺着运动方向的几根发丝暗线,不是龟裂;主要住在老化的尾段 ----
    float vn = tex2D(noiseSamp, float2(sPx / 1600.0 + uSeed * 2.3, dOut / 150.0 + uSeed * 0.7)).r;
    float vn2 = tex2D(noiseSamp, float2(sPx / 2600.0 - uSeed * 4.1, dOut / 260.0 + 0.61)).r;
    float vein = (1.0 - Hard(0.010, 0.006, abs(vn - 0.5))) * 0.8
               + (1.0 - Hard(0.008, 0.005, abs(vn2 - 0.5))) * 0.4;
    vein = saturate(vein);

    //---- 密度块:低频,有的地方吃得更实 ----
    float dens = tex2D(softSamp, float2(sPx / 430.0 + uSeed, wIn * 0.8 + uSeed * 2.0)).r;

    //---- 撕裂前沿(收缩)与蚀退:只随带内像素位置起伏 + lane 错位,同一条前沿不同高度不同时刻断;低频=大碎片不是梳齿 ----
    float tearA = tex2D(noiseSamp, float2(dOut / 60.0 + uSeed * 7.3, 0.41 + uSeed * 0.5)).r;
    float tearB = tex2D(noiseSamp, float2(dOut / 45.0 - uSeed * 2.2, 0.77)).r;
    float tearPx = (tearA * 0.62 + tearB * 0.38 - 0.5) * 46.0 + laneShift * 0.6;

    //---- 门控 ----
    float headPx = (uHead - s) * L;                         //>0 在刃头后方
    float reveal = smoothstep(-1.5, 2.5, headPx);           //前缘 ≤4px:全画面最锐的边
    float tailPx = (s - uTail) * L + tearPx;
    float bodyLive = smoothstep(-3.0, 5.0, tailPx);         //收缩前沿几乎一刀切
    float live = saturate((s - uTail) / max(uHead - uTail, 0.02));   //0 尾 → 1 刃头
    float age = 1.0 - live;

    //---- 蚀退(刃痕期):体按团块噪声撕,线按沿 s 拉长的干笔断丝(飞白)撕,尾先死、线最后死 ----
    float eN = tex2D(noiseSamp, float2(sPx / 300.0 + uSeed * 9.1, dOut / 120.0 + uSeed * 1.7)).r;
    float fN = tex2D(noiseSamp, float2(sPx / 260.0 + uSeed * 4.3, 0.5 + uSeed * 0.21)).r;
    //uErode=1 时刃头端阈值须压过噪声上界(0.78)+线的滞后(0.34),否则头端一截永远死不掉
    float eTh = uErode * 2.10 - s * 0.95;
    float surviveBody = Hard(eTh + 0.02, 0.03, eN);
    float surviveRazor = Hard(eTh - 0.27, 0.06, eN * 0.45 + fN * 0.55);

    //---- 横截面 ----
    //剃刀线:居中 pad,2.4px 芯 1.4px 羽化,px 恒宽;沿程串珠明暗
    float razor = 1.0 - smoothstep(0.0, 1.4, abs(dOut - pad) - 0.7);
    float bead = tex2D(noiseSamp, float2(sPx / 220.0 + uSeed * 9.0, 0.5 + uSeed * 0.13)).r;
    float razorI = 0.78 + 0.6 * smoothstep(0.42, 0.62, bead);
    //刀尖轨迹外只留 3px 紧晕
    float padCut = smoothstep(pad - 0.8, pad + 0.6, dOut);
    float halo = exp(-pow(max(pad - dOut, 0.0) / 3.2, 2.0)) * (1.0 - padCut) * (0.55 + 0.45 * fibHot);
    //第二道细刃文,贴热核里侧一条半亮线
    float hamon = 1.0 - Hard(0.0, 0.9, abs(dOut - pad - 7.0) - 0.6);
    hamon *= 0.55 + 0.45 * smoothstep(0.3, 0.7, f2);
    //区带(纤维把区带边界也撕毛:同一 wIn 上亮纤维处热核伸得更深)
    float zoneJit = (fibHot - fibDark) * 0.05;
    float core = 1.0 - Hard(0.15 + zoneJit, 0.05, wIn);             //热核 外侧 ~0.10..0.20,窄而白
    float burnZone = Hard(0.11 + zoneJit, 0.04, wIn) * (1.0 - Hard(0.32 + zoneJit, 0.07, wIn));   //灼烧带:核与体交界一圈在烧
    //内缘:纤维撕成锯齿的硬切口 + 紧贴的近黑硬沿(rim 在切口内侧 4% 带宽)
    float rimEdge = 0.985 - 0.12 * fibHot - 0.06 * (1.0 - dens) - 0.05 * age + 0.04 * fibDark;
    float innerCut = 1.0 - Hard(rimEdge, 0.010, wIn);
    float rim = Hard(rimEdge - 0.045, 0.012, wIn) * innerCut;
    float body = 1.0 - smoothstep(0.62, rimEdge, wIn) * 0.22;   //体向内只轻微变薄,不做软渐隐

    //---- 体色:层次自外向内 白热→橙烧→绯红纤维→深红→近黑沿;笔刷长丝做整体明度乘子 ----
    float3 col = lerp(uColDeep, uColBright, saturate(0.40 + fibHot * 0.55 - fibDark * 0.45 - wIn * 0.34 + core * 0.20));
    col *= 0.82 + 0.36 * streak;
    col = lerp(col, uColEmber, burnZone * saturate(0.40 + 0.60 * fibHot) * (0.75 + 0.25 * live));
    col = lerp(col, uColHot, core * saturate(0.78 + 0.22 * fibHot) * (0.78 + 0.22 * live));
    col += uColHot * hamon * 0.55 * core * live;
    //裂纹/暗纤维/lane 缝/硬沿 压暗;尾端老化加重
    float darkMask = saturate(vein * (0.30 + 0.55 * age) + fibDark * (0.35 + 0.35 * age) * (1.0 - core)
        + groove * 0.30 + rim * 1.0);
    col = lerp(col, uColDark, darkMask * 0.95);
    col *= lerp(0.62, 1.0, live);
    col *= depthDim;

    //---- 体 alpha:不透明为本;暗纤维处透一点,尾端密度掉 ----
    float aBody = body * (0.86 + 0.12 * dens) * (1.0 - fibDark * 0.22 * (0.5 + 0.5 * age));
    float aCore = core * 0.98;
    float aRim = rim * 0.92;
    float alphaBody = saturate(max(max(aBody, aCore), aRim)) * innerCut * lerp(0.74, 1.0, live)
        * padCut * bodyLive * surviveBody * reveal;

    //---- 烧边:收缩前沿与蚀退边一圈橙热(能量在烧掉);刃头 12px 热前晕 ----
    float burnFront = smoothstep(-6.0, 2.0, tailPx) * (1.0 - smoothstep(2.0, 16.0, tailPx)) * step(0.001, uTail);
    float burnErode = Hard(eTh, 0.02, eN) * (1.0 - Hard(eTh + 0.07, 0.04, eN)) * step(0.001, uErode);
    float burn = saturate(burnFront + burnErode) * (0.55 + 0.45 * fibHot);
    float front = exp(-pow(headPx / 12.0, 2.0)) * (0.45 + 0.55 * fibHot) * step(0.0, headPx);
    col += uColEmber * burn * 2.2 * alphaBody;
    col += uColHot * front * 1.3 * alphaBody;

    //---- 落位闪 ----
    col += uColHot * uFlash * (0.45 + 0.55 * core) * alphaBody;

    //---- 刃痕暗衬:体退去后剃刀线内侧 1.5px 暗线,亮天空上立形;随蚀退加重成墨线 ----
    float scarUnder = smoothstep(pad + 0.9, pad + 1.8, dOut) * (1.0 - smoothstep(pad + 2.6, pad + 3.8, dOut));
    float scarPhase = saturate(1.0 - bodyLive);
    float alphaUnder = scarUnder * (0.50 + 0.30 * uErode) * scarPhase * surviveRazor * reveal * padCut;
    col = lerp(col, uColDark, alphaUnder);
    alphaBody = max(alphaBody, alphaUnder);

    //---- 剃刀线:体活着时暖白串珠;体退后随蚀退冷却成墨(余韵留墨) ----
    float cool = smoothstep(0.10, 0.80, uErode) * scarPhase;
    float3 razorCol = lerp(uColHot * razorI * lerp(0.92, 1.12, live), uColDeep * 0.9, cool) * depthDim;
    razorCol += uColHot * uFlash * 0.5;
    float alphaRazor = razor * padCut * surviveRazor * reveal * lerp(1.0, 0.85, cool) * isBody;

    //---- 外晕:剃刀线外 3px 暖白紧晕,半加法(alpha 低、色不压) ----
    float alphaHalo = halo * 0.55 * surviveRazor * reveal * (1.0 - cool) * isBody;
    float3 haloCol = uColHot * depthDim;

    //---- 滞后衬层:上一帧轮廓压暗做体的下层,给爆发期时间厚度 ----
    alphaBody *= lerp(1.0, 0.42, uLayer);
    col = lerp(col, col * 0.55, uLayer);

    float aOut = alphaRazor + alphaBody * (1.0 - alphaRazor);
    float3 rgb = razorCol * alphaRazor + col * alphaBody * (1.0 - alphaRazor);
    rgb += haloCol * alphaHalo;
    aOut = saturate(aOut + alphaHalo * 0.6);
    float k = uFade * input.Color.a * passMul;
    return float4(rgb * k, aOut * k);
}

technique TechSweep
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSSweep();
    }
}
