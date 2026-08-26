// ============================================================================
//TurnkeyBody.fx 沉波狱吏浸水尸身重绘（NPC 帧后处理，材质=浸水锈甲+污水）
//TechBody 单技法，三态由 uniform 权重乘混合（禁动态分支）：
//  水下态 uWet：焦散光带自水面扫下（上半身加权）+ 下半身沼水吸光沉暗
//  出水态 uDrip：下淌水线（高 x 频细列向下滚）+ 轮廓湿缘（邻域 alpha 四采样，下缘加权）
//  常湿层：窄反射带顺体缓慢下滑（湿滑质感的签名是移动的窄亮带，圆形高光=塑料）
//  搁浅态 uBeach：锈斑自脚部向上蔓延（承原图明暗）+ 后半段干涸去饱和；
//           uSeep 甲缝渗水线（行噪声挑水平缝，缝口湿暗+缝下渗水光），晾干归零
//采样全数钳进 uUvRect 帧界（帧表渗色双通道防线之一，另一半在 C# 源矩形/uUvRect 半像素内缩）
//坐标全笛卡尔（无 atan2）；直线算术+普通 tex2D，FNA3D 安全
//输入输出均预乘（tML 贴图加载即预乘），进 AlphaBlend 批；所有叠加项 ×base.a，轮廓外零溢出
//绑定噪声实测值域 0.227~0.776（三通道同灰度），阈值一律过 nrm() 归一
//消费入口 Scenarios/Dungeonworld/NPCs/Elites/DrownedTurnkey.cs（PreDraw 切 Immediate 套壳）
// ============================================================================

sampler uImage0 : register(s0);   //批主纹理：NPC 帧图
sampler uNoiseTex : register(s1); //PerlinNoise，LinearWrap，消费端上 s1

float uTime;        //秒
float2 uTexelSize;  //1/贴图尺寸
float4 uUvRect;     //帧界（xy=min zw=max，半像素内缩）
float uSeed;        //个体相位
float uWet;         //0~1 水下浸没
float uDrip;        //0~1 出水淌水包络（离水衰减）
float uBeach;       //0~1 搁浅锈化进度
float uSeep;        //0~1 甲缝渗水强度（CPU 包络：搁浅中期峰值、晾干归零）

//====== 浸水甲色板 ======
static const float3 WATER_GLINT = float3(0.580, 0.740, 0.720);  //湿光（沼绿偏白）
static const float3 CAUSTIC_COL = float3(0.360, 0.520, 0.500);  //焦散冷绿
static const float3 MURK_COL    = float3(0.055, 0.105, 0.095);  //沼水吸光
static const float3 RUST_COL    = float3(0.340, 0.160, 0.070);  //锈橙
static const float3 SEEP_DARK   = float3(0.050, 0.090, 0.080);  //缝口湿暗

//绑定噪声实测值域归一（0.227~0.776）
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

float4 PSBody(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0 {
    //帧内归一坐标：多帧精灵表上图案频率按单帧算
    float2 fl = (coords - uUvRect.xy) / max(uUvRect.zw - uUvRect.xy, 0.00001);
    float4 base = tex2D(uImage0, clamp(coords, uUvRect.xy, uUvRect.zw)) * vc;

    //水下焦散光带：双频噪声亮纹缓慢扫过，上半身加权（光从水面折下来）
    float ca = nrm(tex2D(uNoiseTex, float2(fl.x * 0.9 + uTime * 0.055 + uSeed, fl.y * 0.5 - uTime * 0.023)).r);
    float cb = nrm(tex2D(uNoiseTex, float2(fl.x * 2.1 - uTime * 0.041, fl.y * 1.1 + uSeed)).r);
    float caustic = saturate((ca * 0.65 + cb * 0.35 - 0.50) * 3.0) * saturate(1.0 - fl.y * 0.72);
    base.rgb += CAUSTIC_COL * caustic * base.a * (0.95 * uWet);

    //水下沉暗：下半身吃沼水吸光（水下的它只剩一具轮廓，细节让给焦散）
    base.rgb = lerp(base.rgb, MURK_COL * base.a, saturate(fl.y * 1.15 - 0.10) * 0.55 * uWet);

    //湿滑窄反射带：一线水光顺体下滑带一条软随影，列噪声错相防整条直线
    float wetAmt = max(uWet, uDrip);
    float colJit = (nrm(tex2D(uNoiseTex, float2(fl.x * 1.4 + uSeed, 0.37)).r) - 0.5) * 0.22;
    float bandPos = frac(uTime * 0.16 + uSeed * 0.31) + colJit;
    float band = exp2(-abs(fl.y - bandPos) * 26.0) + 0.45 * exp2(-abs(fl.y - bandPos + 0.13) * 40.0);
    base.rgb += WATER_GLINT * band * base.a * (0.45 * wetAmt);

    //出水下淌水线：高 x 频细列向下滚动，随 uDrip 收干
    float run = nrm(tex2D(uNoiseTex, float2(fl.x * 5.2 + uSeed * 1.7, fl.y * 0.8 - uTime * 0.9)).r);
    base.rgb += WATER_GLINT * saturate((run - 0.66) * 6.0) * base.a * (0.62 * uDrip);

    //轮廓湿缘：邻域 alpha 四采样找轮廓，下缘加权（水膜挂在轮廓下侧滴坠前的堆积）
    float2 t2 = uTexelSize * 2.0;
    float aL = tex2D(uImage0, clamp(coords - float2(t2.x, 0.0), uUvRect.xy, uUvRect.zw)).a;
    float aR = tex2D(uImage0, clamp(coords + float2(t2.x, 0.0), uUvRect.xy, uUvRect.zw)).a;
    float aU = tex2D(uImage0, clamp(coords - float2(0.0, t2.y), uUvRect.xy, uUvRect.zw)).a;
    float aD = tex2D(uImage0, clamp(coords + float2(0.0, t2.y), uUvRect.xy, uUvRect.zw)).a;
    float edge = saturate(base.a * 1.2 - min(min(aL, aR), min(aU, aD)));
    float downBias = 0.40 + saturate(base.a - aD) * 1.10;
    base.rgb += WATER_GLINT * edge * downBias * base.a * (0.26 * wetAmt);

    //搁浅锈斑：双频噪声阈值锈橙暗斑自脚部向上蔓延，承原图明暗（锈长在甲上不是盖在甲上）
    //反向边 smoothstep(a, a-w, x) 在本管线会越界外推（lerp 反向外插爆白，2026-08 实测），
    //一律用显式 saturate 斜坡代替
    float r0 = nrm(tex2D(uNoiseTex, fl * float2(2.4, 2.0) + uSeed).r);
    float r1 = nrm(tex2D(uNoiseTex, fl * float2(6.4, 5.2) - uSeed * 1.3).r);
    //阈值自 1.02（高于噪声值域上限，uBeach=0 全身零锈）随搁浅进度下降，脚部降得更快
    float rustThr = 1.02 - uBeach * (0.42 + 0.28 * fl.y);
    float rustT = saturate(((r0 * 0.6 + r1 * 0.4) - rustThr) / 0.18);
    float rustM = rustT * rustT * (3.0 - 2.0 * rustT);
    float srcLuma = dot(base.rgb, float3(0.333, 0.333, 0.333));
    base.rgb = lerp(base.rgb, RUST_COL * (0.40 + 0.90 * srcLuma) * base.a, rustM * 0.75);

    //甲缝渗水线：行噪声（只依赖 y）挑出少数水平缝，缝口一线湿暗、缝下渗水光缓慢流动
    float seam = saturate((nrm(tex2D(uNoiseTex, float2(uSeed, fl.y * 3.4)).r) - 0.72) * 9.0);
    float seepFlow = nrm(tex2D(uNoiseTex, float2(fl.x * 3.0 + uSeed, fl.y * 2.0 - uTime * 0.5)).r);
    base.rgb = lerp(base.rgb, SEEP_DARK * base.a, seam * uSeep * 0.50);
    base.rgb += WATER_GLINT * seam * saturate((seepFlow - 0.45) * 2.2) * base.a * (0.55 * uSeep);

    //干涸去饱和：搁浅后半段向尘灰（uBeach>0.65 起效，湿光此时已被 CPU 包络收走）
    float dry = saturate(uBeach * 2.86 - 1.86);
    base.rgb = lerp(base.rgb, srcLuma * float3(0.92, 0.90, 0.84) * base.a, dry * 0.30);

    return base;
}

technique TechBody {
    pass P0 {
        PixelShader = compile ps_3_0 PSBody();
    }
}
